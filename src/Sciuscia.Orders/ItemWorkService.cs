using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Orders;

public sealed record ItemWorkRecord(
    string ItemWorkId,
    string ItemId,
    string? SourceWorkTypeId,
    string? ExternalAssignmentId,
    string AppliedName,
    string AppliedLabelText,
    string? SelectedBand,
    string PriceStatus,
    int? PriceCents,
    int? CostCents,
    string ExecutionType,
    string ApprovalStatus,
    int RowVersion);

/// <summary>
/// Lavorazioni applicate a un articolo. Nome, testo etichetta e prezzo vengono "congelati" qui
/// al momento dell'inserimento (regola dello schema: le modifiche successive al Listino non
/// devono cambiare quanto già applicato a una commessa) — per questo il chiamante passa i
/// valori già decisi, non un riferimento vivo al Listino.
/// </summary>
public sealed class ItemWorkService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<ItemWorkRecord> AddAsync(
        string itemId,
        string? sourceWorkTypeId,
        string appliedName,
        string appliedLabelText,
        string? selectedBand,
        int? priceCents,
        int? costCents,
        string executionType = "INTERNA",
        string? externalAssignmentId = null,
        string approvalStatus = "APPROVATA",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appliedName) || string.IsNullOrWhiteSpace(appliedLabelText))
        {
            throw new ArgumentException("Nome e testo etichetta della lavorazione sono obbligatori.");
        }

        if (executionType == "INTERNA" && externalAssignmentId is not null)
        {
            throw new ArgumentException("Una lavorazione interna non può avere un affidamento esterno collegato.", nameof(externalAssignmentId));
        }

        if (executionType == "ESTERNA" && externalAssignmentId is null)
        {
            throw new ArgumentException("Una lavorazione esterna richiede un affidamento.", nameof(externalAssignmentId));
        }

        var priceStatus = priceCents.HasValue ? "DEFINITO" : "DA_DEFINIRE";

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var itemWorkId = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO item_works(
                    item_work_id, item_id, source_work_type_id, external_assignment_id,
                    applied_name, applied_label_text, selected_band, price_status, price_cents, cost_cents,
                    execution_type, approval_status)
                VALUES (
                    $id, $itemId, $workTypeId, $assignmentId,
                    $name, $labelText, $band, $priceStatus, $price, $cost,
                    $executionType, $approvalStatus);
                """;
            insert.Parameters.AddWithValue("$id", itemWorkId);
            insert.Parameters.AddWithValue("$itemId", itemId);
            insert.Parameters.AddWithValue("$workTypeId", (object?)sourceWorkTypeId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$assignmentId", (object?)externalAssignmentId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$name", appliedName.Trim());
            insert.Parameters.AddWithValue("$labelText", appliedLabelText.Trim());
            insert.Parameters.AddWithValue("$band", (object?)selectedBand ?? DBNull.Value);
            insert.Parameters.AddWithValue("$priceStatus", priceStatus);
            insert.Parameters.AddWithValue("$price", (object?)priceCents ?? DBNull.Value);
            insert.Parameters.AddWithValue("$cost", (object?)costCents ?? DBNull.Value);
            insert.Parameters.AddWithValue("$executionType", executionType);
            insert.Parameters.AddWithValue("$approvalStatus", approvalStatus);

            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new OrderRuleViolationException(exception.Message, exception);
            }

            return new ItemWorkRecord(
                itemWorkId, itemId, sourceWorkTypeId, externalAssignmentId, appliedName.Trim(), appliedLabelText.Trim(),
                selectedBand, priceStatus, priceCents, costCents, executionType, approvalStatus, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public Task ApproveAsync(string itemWorkId, int expectedRowVersion, CancellationToken cancellationToken = default) =>
        SetApprovalAsync(itemWorkId, "APPROVATA", expectedRowVersion, cancellationToken);

    public Task RejectAsync(string itemWorkId, int expectedRowVersion, CancellationToken cancellationToken = default) =>
        SetApprovalAsync(itemWorkId, "RIFIUTATA", expectedRowVersion, cancellationToken);

    /// <summary>Necessario prima di poter segnare l'articolo come Pronto (regola 6 dello schema).</summary>
    public async Task SetCostAsync(string itemWorkId, int costCents, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        if (costCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(costCents), "Il costo non può essere negativo.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE item_works
                   SET cost_cents = $cost, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE item_work_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", itemWorkId);
            command.Parameters.AddWithValue("$cost", costCents);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Lavorazione");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Annulla la lavorazione (il database impedisce di eliminarla, regola 8 dello schema).</summary>
    public async Task CancelAsync(string itemWorkId, string reason, int expectedRowVersion, CancellationToken cancellationToken = default)
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
                UPDATE item_works
                   SET canceled_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), cancellation_reason = $reason,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE item_work_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", itemWorkId);
            command.Parameters.AddWithValue("$reason", reason.Trim());
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Lavorazione");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<ItemWorkRecord>> ListByItemAsync(string itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT item_work_id, item_id, source_work_type_id, external_assignment_id, applied_name, applied_label_text,
                   selected_band, price_status, price_cents, cost_cents, execution_type, approval_status, row_version
            FROM item_works WHERE item_id = $itemId ORDER BY sort_order;
            """;
        command.Parameters.AddWithValue("$itemId", itemId);

        var results = new List<ItemWorkRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ItemWorkRecord(
                reader.GetString(0), reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetInt32(9),
                reader.GetString(10), reader.GetString(11), reader.GetInt32(12)));
        }

        return results;
    }

    private async Task SetApprovalAsync(string itemWorkId, string approvalStatus, int expectedRowVersion, CancellationToken cancellationToken)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE item_works
                   SET approval_status = $status, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE item_work_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", itemWorkId);
            command.Parameters.AddWithValue("$status", approvalStatus);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Lavorazione");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }
}
