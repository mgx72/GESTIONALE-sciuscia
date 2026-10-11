using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace Sciuscia.Foundations;

public static class FoundationSelfTest
{
    public static async Task RunAsync()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"Sciuscia-SelfTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);

        try
        {
            var runtime = new FoundationRuntime(new FoundationOptions(testRoot));
            var installation = await runtime.Database.EnsureCreatedAsync();
            var first = await runtime.Numbering.ReserveNextOrderAsync(installation.SiteId, 2026);
            var second = await runtime.Numbering.ReserveNextOrderAsync(installation.SiteId, 2026);

            if (first.Number != 1 || second.Number != 2)
            {
                throw new InvalidDataException("Progressivi non consecutivi.");
            }

            var samplePhoto = Path.Combine(testRoot, "foto-prova.jpg");
            await File.WriteAllBytesAsync(samplePhoto, [0xFF, 0xD8, 0xFF, 0xD9]);
            var stored = await runtime.Photos.ImportAsync(
                samplePhoto,
                2026,
                first.VisibleCode,
                NumberingService.ItemVisibleCode(first.VisibleCode, 1));
            if (!File.Exists(stored.AbsolutePath) || stored.SizeBytes != 4)
            {
                throw new IOException("Archiviazione fotografia non riuscita.");
            }

            var backup = await runtime.Backups.CreateAsync();
            if (!File.Exists(Path.Combine(backup.DirectoryPath, "sciuscia-lab.db")) ||
                !File.Exists(Path.Combine(backup.DirectoryPath, "manifest.json")))
            {
                throw new IOException("Backup incompleto.");
            }

            var backupDatabase = Path.Combine(backup.DirectoryPath, "sciuscia-lab.db");
            await using (var exclusive = new FileStream(backupDatabase, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(exclusive)).ToLowerInvariant();
                if (hash != backup.DatabaseSha256)
                    throw new InvalidDataException("Impronta del backup non corrispondente.");
            }
            await using (var backupConnection = await DatabaseInitializer.OpenReadyConnectionAsync(backupDatabase, pooling: false))
            {
                await DatabaseInitializer.VerifyIntegrityAsync(backupConnection);
                await using var sequence = backupConnection.CreateCommand();
                sequence.CommandText = "SELECT last_value FROM number_sequences WHERE sequence_name = 'COMMESSA';";
                if (Convert.ToInt32(await sequence.ExecuteScalarAsync()) != 2)
                    throw new InvalidDataException("Progressivi non conservati nel backup.");
            }
            var copiedPhoto = Path.Combine(backup.DirectoryPath, "Fotografie", stored.RelativePath);
            if (!File.Exists(copiedPhoto) ||
                !File.ReadAllBytes(copiedPhoto).SequenceEqual(File.ReadAllBytes(stored.AbsolutePath)))
                throw new InvalidDataException("Fotografia non conservata nel backup.");

            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(runtime.Paths.DatabasePath);
            await DatabaseInitializer.VerifyIntegrityAsync(connection);

            Console.WriteLine("AUTOTEST SUPERATO");
            Console.WriteLine($"Database: {runtime.Paths.DatabasePath}");
            Console.WriteLine($"Progressivi: {first.VisibleCode}, {second.VisibleCode}");
            Console.WriteLine($"Fotografia: {stored.RelativePath}");
            Console.WriteLine($"Backup: {backup.DirectoryPath}");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
