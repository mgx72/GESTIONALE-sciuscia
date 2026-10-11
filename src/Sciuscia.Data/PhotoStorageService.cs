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
            var destination = ReserveDestination(year, orderCode, itemCode, extension);

            await using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            return await DescribeStoredPhotoAsync(destination, Path.GetFileName(sourcePath), cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Come <see cref="ImportAsync"/>, ma partendo da byte già in memoria invece che da un file su
    /// disco: usato per le fotografie scattate dalla webcam direttamente dentro la schermata
    /// (catturate come immagine nella pagina e inviate al ponte C#), che non esistono come file
    /// prima di essere salvate qui.
    /// </summary>
    public async Task<StoredPhoto> ImportBytesAsync(
        byte[] imageBytes,
        string extension,
        int year,
        string orderCode,
        string itemCode,
        string? originalFilename,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            throw new InvalidDataException("Nessun dato fotografico ricevuto.");
        }

        extension = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidDataException("Formato fotografia non supportato.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            var destination = ReserveDestination(year, orderCode, itemCode, extension);
            await File.WriteAllBytesAsync(destination, imageBytes, cancellationToken);
            return await DescribeStoredPhotoAsync(destination, originalFilename, cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Sposta il file di una fotografia cancellata in un cestino interno (mai cancellazione
    /// definitiva da qui: resta recuperabile a mano se serve), mantenendo la stessa struttura di
    /// cartelle usata per l'archiviazione normale.
    /// </summary>
    public async Task<string> MoveToTrashAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            var source = Path.Combine(paths.PhotosRoot, relativePath);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException("Fotografia da cancellare non trovata sul disco.", source);
            }

            var trashRoot = Path.Combine(paths.PhotosRoot, "_Cestino");
            var trashDestination = Path.Combine(trashRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(trashDestination)!);

            if (File.Exists(trashDestination))
            {
                var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var directory = Path.GetDirectoryName(trashDestination)!;
                var stem = Path.GetFileNameWithoutExtension(trashDestination);
                var extension = Path.GetExtension(trashDestination);
                trashDestination = Path.Combine(directory, $"{stem}_{stamp}{extension}");
            }

            await Task.Run(() => File.Move(source, trashDestination), cancellationToken);
            return Path.GetRelativePath(paths.PhotosRoot, trashDestination);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    private string ReserveDestination(int year, string orderCode, string itemCode, string extension)
    {
        var folder = EnsureItemFolder(year, orderCode, itemCode);
        var baseName = SafeSegment(itemCode);
        var sequence = NextSequence(folder, baseName, extension);
        var filename = $"{baseName}_{sequence:00}{extension}";
        return Path.Combine(folder, filename);
    }

    private async Task<StoredPhoto> DescribeStoredPhotoAsync(string destination, string? originalFilename, CancellationToken cancellationToken)
    {
        await using var hashStream = File.OpenRead(destination);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, cancellationToken)).ToLowerInvariant();
        var relative = Path.GetRelativePath(paths.PhotosRoot, destination);
        return new StoredPhoto(
            destination,
            relative,
            hash,
            new FileInfo(destination).Length,
            originalFilename ?? Path.GetFileName(destination));
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

