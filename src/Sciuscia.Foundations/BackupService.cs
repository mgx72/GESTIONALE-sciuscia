using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Sciuscia.Foundations;

public sealed record BackupResult(string DirectoryPath, string DatabaseSha256, long TotalBytes);

public sealed class BackupService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<BackupResult> CreateAsync(CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            paths.EnsureDirectories();
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
            var destination = UniqueDirectory(Path.Combine(paths.BackupRoot, $"Backup_{stamp}"));
            Directory.CreateDirectory(destination);

            var databaseCopy = Path.Combine(destination, "sciuscia-lab.db");
            await using (var source = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken))
            await using (var target = await DatabaseInitializer.OpenReadyConnectionAsync(databaseCopy, pooling: false, cancellationToken: cancellationToken))
            {
                source.BackupDatabase(target);
                await DatabaseInitializer.VerifyIntegrityAsync(target, cancellationToken);
                // Il backup deve essere completo nel singolo file, senza dipendere dal WAL.
                await using var journal = target.CreateCommand();
                journal.CommandText = "PRAGMA journal_mode = DELETE;";
                var mode = Convert.ToString(await journal.ExecuteScalarAsync(cancellationToken));
                if (!string.Equals(mode, "delete", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Impossibile rendere autonomo il database di backup.");
                }
            }

            var backupPhotos = Path.Combine(destination, "Fotografie");
            await CopyDirectoryAsync(paths.PhotosRoot, backupPhotos, cancellationToken);

            await using var databaseStream = File.OpenRead(databaseCopy);
            var databaseHash = Convert.ToHexString(
                await SHA256.HashDataAsync(databaseStream, cancellationToken)).ToLowerInvariant();
            var totalBytes = Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);

            var manifest = new
            {
                createdAt = DateTimeOffset.UtcNow,
                databaseFile = "sciuscia-lab.db",
                databaseSha256 = databaseHash,
                includesPhotos = true,
                totalBytes
            };
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(destination, "manifest.json"), manifestJson, cancellationToken);

            return new BackupResult(destination, databaseHash, totalBytes);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    private static string UniqueDirectory(string desired)
    {
        if (!Directory.Exists(desired))
        {
            return desired;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{desired}_{suffix}";
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Impossibile determinare una cartella backup libera.");
    }

    private static async Task CopyDirectoryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
        }
    }
}
