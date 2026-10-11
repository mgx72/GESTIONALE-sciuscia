using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Operations;

public sealed record PaymentEventRecord(
    string PaymentEventId,
    string OrderId,
    int AmountCents,
    string PaymentType,
    string OccurredAt,
    string? CorrectsPaymentEventId,
    string? Reason);

/// <summary>
/// Pagamenti registrati su una commessa. Il database impedisce di modificarli o eliminarli
/// (regola dello schema: i pagamenti si correggono solo con un nuovo evento di segno opposto,
/// mai riscrivendo la storia). Qui i vincoli tecnici di SQLite vengono tradotti in messaggi
/// comprensibili.
/// </summary>
public sealed class PaymentService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public Task<PaymentEventRecord> AddAsync(
        string orderId,
        int amountCents,
        string paymentType,
        DateTimeOffset? occurredAt = null,
        string? operatorId = null,
        string? transactionKey = null,
        CancellationToken cancellationToken = default) =>
        InsertAsync(orderId, amountCents, paymentType, occurredAt, operatorId, transactionKey, null, null, cancellationToken);

    /// <summary>
    /// Corregge un pagamento precedente senza modificarlo: registra un nuovo evento (tipicamente
    /// di segno opposto) collegato a quello originale, con un motivo obbligatorio.
    /// </summary>
    public Task<PaymentEventRecord> AddCorrectionAsync(
        string orderId,
        string correctsPaymentEventId,
        int amountCents,
        string reason,
        DateTimeOffset? occurredAt = null,
        string? operatorId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Il motivo della correzione è obbligatorio.", nameof(reason));
        }

        return InsertAsync(orderId, amountCents, "CORREZIONE", occurredAt, operatorId, null, correctsPaymentEventId, reason.Trim(), cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentEventRecord>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT payment_event_id, order_id, amount_cents, payment_type, occurred_at, corrects_payment_event_id, reason
            FROM payment_events WHERE order_id = $orderId ORDER BY occurred_at;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);

        var results = new List<PaymentEventRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PaymentEventRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return results;
    }

    private async Task<PaymentEventRecord> InsertAsync(
        string orderId,
        int amountCents,
        string paymentType,
        DateTimeOffset? occurredAt,
        string? operatorId,
        string? transactionKey,
        string? correctsPaymentEventId,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (amountCents == 0)
        {
            throw new ArgumentException("L'importo non può essere zero.", nameof(amountCents));
        }

        var occurredAtIso = (occurredAt ?? DateTimeOffset.Now).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var paymentEventId = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO payment_events(payment_event_id, order_id, amount_cents, payment_type, occurred_at, corrects_payment_event_id, reason, operator_id, transaction_key)
                VALUES ($id, $orderId, $amount, $type, $occurredAt, $corrects, $reason, $operatorId, $transactionKey);
                """;
            insert.Parameters.AddWithValue("$id", paymentEventId);
            insert.Parameters.AddWithValue("$orderId", orderId);
            insert.Parameters.AddWithValue("$amount", amountCents);
            insert.Parameters.AddWithValue("$type", paymentType);
            insert.Parameters.AddWithValue("$occurredAt", occurredAtIso);
            insert.Parameters.AddWithValue("$corrects", (object?)correctsPaymentEventId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
            insert.Parameters.AddWithValue("$operatorId", (object?)operatorId ?? DBNull.Value);
            insert.Parameters.AddWithValue("$transactionKey", (object?)transactionKey ?? DBNull.Value);

            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException("Tipo di pagamento non valido, oppure correzione senza motivo.", exception);
            }

            return new PaymentEventRecord(paymentEventId, orderId, amountCents, paymentType, occurredAtIso, correctsPaymentEventId, reason);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }
}
