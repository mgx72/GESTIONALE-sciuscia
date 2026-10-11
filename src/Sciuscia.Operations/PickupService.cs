using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Operations;

public sealed record PickupRecord(
    string PickupId,
    string OrderId,
    string OccurredAt,
    string? Notes,
    IReadOnlyList<string> ItemIds);

/// <summary>
/// Ritiro di uno o più articoli da parte del cliente. Il database garantisce da solo che un
/// articolo non possa essere ritirato due volte (vincolo di unicità su pickup_items.item_id) e
/// che un ritiro, una volta registrato, non si possa più modificare né eliminare (regola dello
/// schema: ritiri e messaggi sono eventi storici, si correggono con annotazioni, non riscrivendo
/// il passato). Registrare un ritiro porta automaticamente gli articoli coinvolti allo stato
/// "Consegnato".
/// </summary>
public sealed class PickupService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<PickupRecord> CreateAsync(
        string orderId,
        IReadOnlyList<string> itemIds,
        DateTimeOffset? occurredAt = null,
        string? notes = null,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        if (itemIds is null || itemIds.Count == 0)
        {
            throw new ArgumentException("Indicare almeno un articolo da ritirare.", nameof(itemIds));
        }

        var occurredAtIso = (occurredAt ?? DateTimeOffset.Now).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var checkItems = connection.CreateCommand())
            {
                checkItems.Transaction = (SqliteTransaction)transaction;
                checkItems.CommandText =
                    $"SELECT COUNT(*) FROM items WHERE order_id = $orderId AND item_id IN ({string.Join(",", itemIds.Select((_, index) => $"$item{index}"))});";
                checkItems.Parameters.AddWithValue("$orderId", orderId);
                for (var index = 0; index < itemIds.Count; index++)
                {
                    checkItems.Parameters.AddWithValue($"$item{index}", itemIds[index]);
                }

                var matching = Convert.ToInt32(await checkItems.ExecuteScalarAsync(cancellationToken));
                if (matching != itemIds.Count)
                {
                    throw new ArgumentException("Uno o più articoli indicati non appartengono a questa commessa.", nameof(itemIds));
                }
            }

            var pickupId = Guid.NewGuid().ToString();
            await using (var insertPickup = connection.CreateCommand())
            {
                insertPickup.Transaction = (SqliteTransaction)transaction;
                insertPickup.CommandText =
                    """
                    INSERT INTO pickups(pickup_id, order_id, occurred_at, notes, operator_id)
                    VALUES ($id, $orderId, $occurredAt, $notes, $operatorId);
                    """;
                insertPickup.Parameters.AddWithValue("$id", pickupId);
                insertPickup.Parameters.AddWithValue("$orderId", orderId);
                insertPickup.Parameters.AddWithValue("$occurredAt", occurredAtIso);
                insertPickup.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);
                insertPickup.Parameters.AddWithValue("$operatorId", (object?)operatorId ?? DBNull.Value);
                await insertPickup.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var itemId in itemIds)
            {
                await using var insertPickupItem = connection.CreateCommand();
                insertPickupItem.Transaction = (SqliteTransaction)transaction;
                insertPickupItem.CommandText = "INSERT INTO pickup_items(pickup_id, item_id) VALUES ($pickupId, $itemId);";
                insertPickupItem.Parameters.AddWithValue("$pickupId", pickupId);
                insertPickupItem.Parameters.AddWithValue("$itemId", itemId);

                try
                {
                    await insertPickupItem.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
                {
                    throw new InvalidOperationException("Uno di questi articoli risulta già ritirato in precedenza.", exception);
                }

                await using var markDelivered = connection.CreateCommand();
                markDelivered.Transaction = (SqliteTransaction)transaction;
                markDelivered.CommandText =
                    """
                    UPDATE items
                       SET workflow_status = 'CONSEGNATO', updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                     WHERE item_id = $itemId;
                    """;
                markDelivered.Parameters.AddWithValue("$itemId", itemId);

                try
                {
                    await markDelivered.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
                {
                    throw new InvalidOperationException("Un articolo non può essere consegnato: manca ancora il costo di una lavorazione esterna approvata.", exception);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new PickupRecord(pickupId, orderId, occurredAtIso, notes, itemIds);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<PickupRecord>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.pickup_id, p.order_id, p.occurred_at, p.notes,
                   (SELECT group_concat(pi.item_id) FROM pickup_items pi WHERE pi.pickup_id = p.pickup_id)
            FROM pickups p WHERE p.order_id = $orderId ORDER BY p.occurred_at;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);

        var results = new List<PickupRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemIds = reader.IsDBNull(4)
                ? Array.Empty<string>()
                : reader.GetString(4).Split(',', StringSplitOptions.RemoveEmptyEntries);
            results.Add(new PickupRecord(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), itemIds));
        }

        return results;
    }
}
