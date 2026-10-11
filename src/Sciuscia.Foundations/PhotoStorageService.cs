using System.Security.Cryptography;

namespace Sciuscia.Foundations;

public sealed record StoredPhoto(
    string AbsolutePath,
    string RelativePath,
    string Sha256,
    long SizeBytes,
    string OriginalFilename);

public sealed class PhotoStorageService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public string EnsureItemFolder(int year, string orderCode, string itemCode)
    {
        var folder = Path.Combine(
            paths.PhotosRoot,
            year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SafeSegment(orderCode),
            SafeSegment(itemCode));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public async Task<StoredPhoto> ImportAsync(
        string sourcePath,
        int year,
        string orderCode,
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Fotografia da importare non trovata.", sourcePath);
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidDataException("Formato fotografia non supportato.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            var folder = EnsureItemFolder(year, orderCode, itemCode);
            var baseName = SafeSegment(itemCode);
            var sequence = NextSequence(folder, baseName, extension);
            var filename = $"{baseName}_{sequence:00}{extension}";
            var destination = Path.Combine(folder, filename);

            await using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            await using var hashStream = File.OpenRead(destination);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();
            var relative = Path.GetRelativePath(paths.PhotosRoot, destination);
            return new StoredPhoto(
                destination,
                relative,
                hash,
                new FileInfo(destination).Length,
                Path.GetFileName(sourcePath));
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    private static int NextSequence(string folder, string baseName, string extension)
    {
        var highest = 0;
        foreach (var file in Directory.EnumerateFiles(folder, $"{baseName}_*{extension}"))
        {
            var stem = Path.GetFileNameWithoutExtension(file);
            var suffix = stem[(baseName.Length + 1)..];
            if (int.TryParse(suffix, out var parsed))
            {
                highest = Math.Max(highest, parsed);
            }
        }

        return checked(highest + 1);
    }

    private static string SafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Codice non valorizzato.", nameof(value));
        }

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Trim()
            .Select(character => character == '/' || character == '\\' || invalid.Contains(character) ? '-' : character)
            .ToArray());
        return cleaned.Trim('.', ' ');
    }
}

