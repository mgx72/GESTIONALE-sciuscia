using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Operations;

public sealed record PrintEventRecord(
    string PrintEventId,
    string? OrderId,
    string? ItemId,
    string DocumentType,
    string Outcome,
    string PrintedAt);

public sealed record ItemLabelToReprint(string ItemId, string VisibleCode, int LabelRevision, int? LastPrintedLabelRevision);

/// <summary>
/// Registro degli eventi di stampa (ricevute, fotografie, etichette). Quando una stampa etichetta
/// riesce, l'articolo viene aggiornato per ricordare quale versione dell'etichetta è stata
/// effettivamente stampata (regola dello schema: label_revision cambia da sola quando cambiano i
/// dati stampati, e last_printed_label_revision segue solo dopo una stampa riuscita). Il database
/// impedisce di modificare o eliminare un evento di stampa già registrato.
/// </summary>
public sealed class PrintService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<PrintEventRecord> RecordAsync(
        string? orderId,
        string? itemId,
        string documentType,
        string templateVersion,
        string outcome,
        DateTimeOffset? printedAt = null,
        string? printerName = null,
        int? dataRevision = null,
        int? labelRevision = null,
        string? errorMessage = null,
        string? operatorId = null,
        string? snapshotJson = null,
        CancellationToken cancellationToken = default)
    {
        if (orderId is null && itemId is null)
        {
            throw new ArgumentException("Specificare almeno la commessa o l'articolo a cui si riferisce la stampa.");
        }

        if (documentType == "ETICHETTA" && itemId is null)
        {
            throw new ArgumentException("La stampa di un'etichetta richiede l'articolo.", nameof(itemId));
        }

        var printedAtIso = (printedAt ?? DateTimeOffset.Now).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var printEventId = Guid.NewGuid().ToString();
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText =
                    """
                    INSERT INTO print_events(
                        print_event_id, order_id, item_id, document_type, template_version, printer_name,
                        printed_at, outcome, data_revision, label_revision, error_message, operator_id, snapshot_json)
                    VALUES (
                        $id, $orderId, $itemId, $documentType, $templateVersion, $printerName,
                        $printedAt, $outcome, $dataRevision, $labelRevision, $errorMessage, $operatorId, $snapshot);
                    """;
                insert.Parameters.AddWithValue("$id", printEventId);
                insert.Parameters.AddWithValue("$orderId", (object?)orderId ?? DBNull.Value);
                insert.Parameters.AddWithValue("$itemId", (object?)itemId ?? DBNull.Value);
                insert.Parameters.AddWithValue("$documentType", documentType);
                insert.Parameters.AddWithValue("$templateVersion", templateVersion);
                insert.Parameters.AddWithValue("$printerName", (object?)printerName ?? DBNull.Value);
                insert.Parameters.AddWithValue("$printedAt", printedAtIso);
                insert.Parameters.AddWithValue("$outcome", outcome);
                insert.Parameters.AddWithValue("$dataRevision", (object?)dataRevision ?? DBNull.Value);
                insert.Parameters.AddWithValue("$labelRevision", (object?)labelRevision ?? DBNull.Value);
                insert.Parameters.AddWithValue("$errorMessage", (object?)errorMessage ?? DBNull.Value);
                insert.Parameters.AddWithValue("$operatorId", (object?)operatorId ?? DBNull.Value);
                insert.Parameters.AddWithValue("$snapshot", (object?)snapshotJson ?? DBNull.Value);

                try
                {
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
                {
                    throw new InvalidOperationException("Dati di stampa non validi (tipo documento o esito non ammessi).", exception);
                }
            }

            if (documentType == "ETICHETTA" && outcome == "RIUSCITA" && itemId is not null)
            {
                await using var markPrinted = connection.CreateCommand();
                markPrinted.Transaction = (SqliteTransaction)transaction;
                markPrinted.CommandText =
                    """
                    UPDATE items
                       SET last_printed_label_revision = label_revision
                     WHERE item_id = $itemId;
                    """;
                markPrinted.Parameters.AddWithValue("$itemId", itemId);
                await markPrinted.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new PrintEventRecord(printEventId, orderId, itemId, documentType, outcome, printedAtIso);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Articoli la cui etichetta stampata non corrisponde più ai dati attuali (vista v_items_to_reprint_label).</summary>
    public async Task<IReadOnlyList<ItemLabelToReprint>> ListItemsToReprintLabelAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT item_id, visible_code, label_revision, last_printed_label_revision FROM v_items_to_reprint_label;";

        var results = new List<ItemLabelToReprint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ItemLabelToReprint(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));
        }

        return results;
    }

    public async Task<IReadOnlyList<PrintEventRecord>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT print_event_id, order_id, item_id, document_type, outcome, printed_at
            FROM print_events WHERE order_id = $orderId ORDER BY printed_at;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);

        var results = new List<PrintEventRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PrintEventRecord(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        }

        return results;
    }
}
