using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Orders;

public sealed record ItemRecord(
    string ItemId,
    string OrderId,
    int ItemSequence,
    string VisibleCode,
    string CategoryId,
    string BrandId,
    string Description,
    string? DueDate,
    bool IsUrgent,
    bool IsPriority,
    string WorkflowStatus,
    int LabelRevision,
    int RowVersion);

/// <summary>
/// Articoli di una commessa. Il sottocodice (regola 3 dello schema, es. "LAB-126/2") è
/// assegnato qui in modo sicuro perché tutte le scritture sul database passano dallo stesso
/// semaforo di manutenzione: non serve un lucchetto SQL aggiuntivo per evitare doppioni.
/// </summary>
public sealed class ItemService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<ItemRecord> AddAsync(
        string orderId,
        string orderVisibleCode,
        string categoryId,
        string brandId,
        string description,
        string? dueDate = null,
        bool isUrgent = false,
        bool isPriority = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("La descrizione dell'articolo è obbligatoria.", nameof(description));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            int sequence;
            await using (var sequenceCommand = connection.CreateCommand())
            {
                sequenceCommand.Transaction = (SqliteTransaction)transaction;
                sequenceCommand.CommandText = "SELECT COALESCE(MAX(item_sequence), 0) + 1 FROM items WHERE order_id = $orderId;";
                sequenceCommand.Parameters.AddWithValue("$orderId", orderId);
                sequence = Convert.ToInt32(await sequenceCommand.ExecuteScalarAsync(cancellationToken));
            }

            var visibleCode = NumberingService.ItemVisibleCode(orderVisibleCode, sequence);
            var itemId = Guid.NewGuid().ToString();

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText =
                    """
                    INSERT INTO items(item_id, order_id, item_sequence, visible_code, category_id, brand_id, description, due_date, is_urgent, is_priority)
                    VALUES ($id, $orderId, $sequence, $visibleCode, $categoryId, $brandId, $description, $dueDate, $urgent, $priority);
                    """;
                insert.Parameters.AddWithValue("$id", itemId);
                insert.Parameters.AddWithValue("$orderId", orderId);
                insert.Parameters.AddWithValue("$sequence", sequence);
                insert.Parameters.AddWithValue("$visibleCode", visibleCode);
                insert.Parameters.AddWithValue("$categoryId", categoryId);
                insert.Parameters.AddWithValue("$brandId", brandId);
                insert.Parameters.AddWithValue("$description", description.Trim());
                insert.Parameters.AddWithValue("$dueDate", (object?)dueDate ?? DBNull.Value);
                insert.Parameters.AddWithValue("$urgent", isUrgent ? 1 : 0);
                insert.Parameters.AddWithValue("$priority", isPriority ? 1 : 0);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new ItemRecord(itemId, orderId, sequence, visibleCode, categoryId, brandId, description.Trim(), dueDate, isUrgent, isPriority, "DA_LAVORARE", 1, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Cambia lo stato di lavorazione dell'articolo. Il database rifiuta da solo il passaggio a
    /// Pronto/Avvisato/Consegnato se manca il costo di una lavorazione esterna approvata
    /// (regola 6 dello schema): l'errore tecnico di SQLite viene qui tradotto in un messaggio
    /// comprensibile.
    /// </summary>
    public async Task SetWorkflowStatusAsync(string itemId, string workflowStatus, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE items
                   SET workflow_status = $status, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE item_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", itemId);
            command.Parameters.AddWithValue("$status", workflowStatus);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);

            int updated;
            try
            {
                updated = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new OrderRuleViolationException(exception.Message, exception);
            }

            if (updated == 0)
            {
                throw new ConcurrencyConflictException("Articolo");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task SetDueDateAsync(string itemId, string? dueDate, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE items
                   SET due_date = $dueDate, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE item_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", itemId);
            command.Parameters.AddWithValue("$dueDate", (object?)dueDate ?? DBNull.Value);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Articolo");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task CancelAsync(string itemId, string reason, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Il motivo dell'annullamento è obbligatorio.", nameof(reason));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE items
                   SET workflow_status = 'ANNULLATO', canceled_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                       cancellation_reason = $reason,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE item_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", itemId);
            command.Parameters.AddWithValue("$reason", reason.Trim());
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Articolo");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Rilegge l'articolo dal database. Utile prima di un aggiornamento con controllo di
    /// concorrenza (row_version), perché ogni lavorazione aggiunta o modificata fa scattare un
    /// trigger che aggiorna da solo label_revision e row_version dell'articolo.
    /// </summary>
    public async Task<ItemRecord?> FindAsync(string itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT item_id, order_id, item_sequence, visible_code, category_id, brand_id, description,
                   due_date, is_urgent, is_priority, workflow_status, label_revision, row_version
            FROM items WHERE item_id = $itemId;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ItemRecord(
            reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetInt64(8) == 1, reader.GetInt64(9) == 1,
            reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12));
    }

    public async Task<IReadOnlyList<ItemRecord>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT item_id, order_id, item_sequence, visible_code, category_id, brand_id, description,
                   due_date, is_urgent, is_priority, workflow_status, label_revision, row_version
            FROM items WHERE order_id = $orderId ORDER BY item_sequence;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);

        var results = new List<ItemRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ItemRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt64(8) == 1, reader.GetInt64(9) == 1,
                reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12)));
        }

        return results;
    }
}
