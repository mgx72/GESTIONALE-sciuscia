using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Orders;

public sealed record PhotoRecord(
    string PhotoId,
    string ItemId,
    string RelativePath,
    string? OriginalFilename,
    string Extension,
    long FileSizeBytes,
    string Sha256,
    int OrientationDegrees,
    int SortOrder,
    string AcquiredAt,
    string Source,
    int RowVersion);

/// <summary>
/// Fotografie collegate a un articolo. Qui si tiene la riga nel database (ordine, cestino
/// logico, concorrenza); il file sul disco è invece gestito da <see cref="PhotoStorageService"/>
/// (Fondazioni), già collaudato. Le due parti vengono chiamate in sequenza, mai annidate, perché
/// condividono lo stesso semaforo di scrittura dell'intero programma.
/// </summary>
public sealed class PhotoService(
    AppPaths paths,
    SemaphoreSlim maintenanceGate,
    PhotoStorageService storage,
    OrderService orders,
    ItemService items)
{
    private static readonly HashSet<string> ValidSources = new(StringComparer.Ordinal)
    {
        "WEBCAM", "IMPORTAZIONE", "TELEFONO", "MIGRAZIONE",
    };

    public async Task<PhotoRecord> AddAsync(
        string itemId,
        byte[] imageBytes,
        string extension,
        string source,
        string? originalFilename = null,
        CancellationToken cancellationToken = default)
    {
        if (!ValidSources.Contains(source))
        {
            throw new ArgumentException($"Origine fotografia non valida: {source}.", nameof(source));
        }

        var item = await items.FindAsync(itemId, cancellationToken)
            ?? throw new InvalidOperationException("Articolo non trovato.");
        var order = await orders.FindAsync(item.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("Commessa non trovata.");

        // Scrittura del file: PhotoStorageService gestisce da sola il semaforo per questo passo.
        var stored = await storage.ImportBytesAsync(
            imageBytes, extension, order.OrderYear, order.VisibleCode, item.VisibleCode, originalFilename, cancellationToken);

        var acquiredAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var normalizedExtension = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);

            int sortOrder;
            await using (var sequenceCommand = connection.CreateCommand())
            {
                sequenceCommand.CommandText = "SELECT COALESCE(MAX(sort_order), 0) + 1 FROM photos WHERE item_id = $itemId;";
                sequenceCommand.Parameters.AddWithValue("$itemId", itemId);
                sortOrder = Convert.ToInt32(await sequenceCommand.ExecuteScalarAsync(cancellationToken));
            }

            var photoId = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO photos(
                    photo_id, item_id, relative_path, original_filename, extension,
                    file_size_bytes, sha256, sort_order, acquired_at, source)
                VALUES (
                    $id, $itemId, $relativePath, $originalFilename, $extension,
                    $sizeBytes, $sha256, $sortOrder, $acquiredAt, $source);
                """;
            insert.Parameters.AddWithValue("$id", photoId);
            insert.Parameters.AddWithValue("$itemId", itemId);
            insert.Parameters.AddWithValue("$relativePath", stored.RelativePath);
            insert.Parameters.AddWithValue("$originalFilename", (object?)stored.OriginalFilename ?? DBNull.Value);
            insert.Parameters.AddWithValue("$extension", normalizedExtension);
            insert.Parameters.AddWithValue("$sizeBytes", stored.SizeBytes);
            insert.Parameters.AddWithValue("$sha256", stored.Sha256);
            insert.Parameters.AddWithValue("$sortOrder", sortOrder);
            insert.Parameters.AddWithValue("$acquiredAt", acquiredAt);
            insert.Parameters.AddWithValue("$source", source);

            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new OrderRuleViolationException(exception.Message, exception);
            }

            return new PhotoRecord(
                photoId, itemId, stored.RelativePath, stored.OriginalFilename, normalizedExtension,
                stored.SizeBytes, stored.Sha256, 0, sortOrder, acquiredAt, source, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<PhotoRecord>> ListByItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT photo_id, item_id, relative_path, original_filename, extension, file_size_bytes,
                   sha256, orientation_degrees, sort_order, acquired_at, source, row_version
            FROM photos WHERE item_id = $itemId AND deleted_at IS NULL ORDER BY sort_order;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);

        var results = new List<PhotoRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PhotoRecord(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
                reader.GetInt64(5), reader.GetString(6), reader.GetInt32(7),
                reader.GetInt32(8), reader.GetString(9), reader.GetString(10), reader.GetInt32(11)));
        }

        return results;
    }

    /// <summary>Cancellazione logica (regola dello schema: mai eliminare una riga fotografia). Il file viene spostato nel cestino interno, non cancellato dal disco.</summary>
    public async Task DeleteAsync(string photoId, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        string relativePath;
        await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken))
        {
            await using var lookup = connection.CreateCommand();
            lookup.CommandText = "SELECT relative_path FROM photos WHERE photo_id = $id AND deleted_at IS NULL;";
            lookup.Parameters.AddWithValue("$id", photoId);
            var result = await lookup.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException("Fotografia non trovata.");
            }

            relativePath = (string)result;
        }

        var trashRelativePath = await storage.MoveToTrashAsync(relativePath, cancellationToken);

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE photos
                   SET deleted_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), trash_relative_path = $trash,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE photo_id = $id AND row_version = $expected AND deleted_at IS NULL;
                """;
            command.Parameters.AddWithValue("$id", photoId);
            command.Parameters.AddWithValue("$trash", trashRelativePath);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Fotografia");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }
}
