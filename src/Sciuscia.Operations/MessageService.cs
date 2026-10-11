using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Operations;

public sealed record MessageRecord(
    string MessageId,
    string OrderId,
    string MessageType,
    string Purpose,
    string FinalText,
    string? Recipient,
    string SentAt,
    string DeliveryStatus,
    IReadOnlyList<string> ItemIds);

/// <summary>
/// Messaggi inviati al cliente (avviso pronto, preventivo, sollecito...), con il testo
/// effettivamente inviato "congelato" al momento della registrazione. Il database impedisce di
/// modificare o eliminare un messaggio già registrato (è uno storico, non un dato di lavoro).
/// </summary>
public sealed class MessageService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<MessageRecord> SendAsync(
        string orderId,
        string messageType,
        string purpose,
        string finalText,
        IReadOnlyList<string>? itemIds = null,
        string? recipient = null,
        DateTimeOffset? sentAt = null,
        string deliveryStatus = "REGISTRATO",
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(finalText))
        {
            throw new ArgumentException("Il testo del messaggio è obbligatorio.", nameof(finalText));
        }

        var sentAtIso = (sentAt ?? DateTimeOffset.Now).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var items = itemIds ?? Array.Empty<string>();

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var messageId = Guid.NewGuid().ToString();
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText =
                    """
                    INSERT INTO messages(message_id, order_id, message_type, purpose, final_text, recipient, sent_at, delivery_status, operator_id)
                    VALUES ($id, $orderId, $type, $purpose, $text, $recipient, $sentAt, $status, $operatorId);
                    """;
                insert.Parameters.AddWithValue("$id", messageId);
                insert.Parameters.AddWithValue("$orderId", orderId);
                insert.Parameters.AddWithValue("$type", messageType);
                insert.Parameters.AddWithValue("$purpose", purpose);
                insert.Parameters.AddWithValue("$text", finalText.Trim());
                insert.Parameters.AddWithValue("$recipient", (object?)recipient ?? DBNull.Value);
                insert.Parameters.AddWithValue("$sentAt", sentAtIso);
                insert.Parameters.AddWithValue("$status", deliveryStatus);
                insert.Parameters.AddWithValue("$operatorId", (object?)operatorId ?? DBNull.Value);

                try
                {
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
                {
                    throw new InvalidOperationException("Tipo o scopo del messaggio non validi.", exception);
                }
            }

            foreach (var itemId in items)
            {
                await using var insertItem = connection.CreateCommand();
                insertItem.Transaction = (SqliteTransaction)transaction;
                insertItem.CommandText = "INSERT INTO message_items(message_id, item_id) VALUES ($messageId, $itemId);";
                insertItem.Parameters.AddWithValue("$messageId", messageId);
                insertItem.Parameters.AddWithValue("$itemId", itemId);
                await insertItem.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new MessageRecord(messageId, orderId, messageType, purpose, finalText.Trim(), recipient, sentAtIso, deliveryStatus, items);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<MessageRecord>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT m.message_id, m.order_id, m.message_type, m.purpose, m.final_text, m.recipient, m.sent_at, m.delivery_status,
                   (SELECT group_concat(mi.item_id) FROM message_items mi WHERE mi.message_id = m.message_id)
            FROM messages m WHERE m.order_id = $orderId ORDER BY m.sent_at;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);

        var results = new List<MessageRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemIds = reader.IsDBNull(8)
                ? Array.Empty<string>()
                : reader.GetString(8).Split(',', StringSplitOptions.RemoveEmptyEntries);
            results.Add(new MessageRecord(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6), reader.GetString(7), itemIds));
        }

        return results;
    }
}
