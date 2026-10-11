using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Orders;

public sealed record OrderRecord(
    string OrderId,
    string SiteId,
    string CustomerId,
    int OrderYear,
    int OrderNumber,
    string VisibleCode,
    string ReceivedAt,
    string Status,
    int RowVersion);

public sealed record OrderFinancials(int TotalCents, int PaidCents, int BalanceCents, int UndefinedPriceCount);

public sealed record OrderSummary(
    string OrderId,
    string VisibleCode,
    string CustomerName,
    string Status,
    string ReceivedAt,
    int TotalCents,
    int PaidCents,
    int BalanceCents,
    int ItemCount);

public sealed class ConcurrencyConflictException(string entity)
    : InvalidOperationException($"{entity}: il dato è stato modificato da un'altra operazione. Ricaricare e riprovare.");

/// <summary>
/// Rende leggibile un blocco imposto dal database (un trigger di regola, o un vincolo di
/// unicità) invece di lasciar risalire il messaggio tecnico di SQLite.
/// </summary>
public sealed class OrderRuleViolationException : InvalidOperationException
{
    public OrderRuleViolationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Blocco Commesse — gestione delle commesse (ordini). La numerazione progressiva e sicura
/// (regola 2 dello schema) è delegata a NumberingService, già collaudato nelle Fondazioni.
/// </summary>
public sealed class OrderService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    private readonly NumberingService _numbering = new(paths, maintenanceGate);

    public async Task<OrderRecord> CreateAsync(
        string siteId,
        string customerId,
        DateTimeOffset? receivedAt = null,
        CancellationToken cancellationToken = default)
    {
        var year = (receivedAt ?? DateTimeOffset.Now).Year;
        var reserved = await _numbering.ReserveNextOrderAsync(siteId, year, cancellationToken);
        var receivedAtIso = (receivedAt ?? DateTimeOffset.Now).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var orderId = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO orders(order_id, site_id, customer_id, order_year, order_number, visible_code, received_at)
                VALUES ($id, $siteId, $customerId, $year, $number, $visibleCode, $receivedAt);
                """;
            insert.Parameters.AddWithValue("$id", orderId);
            insert.Parameters.AddWithValue("$siteId", siteId);
            insert.Parameters.AddWithValue("$customerId", customerId);
            insert.Parameters.AddWithValue("$year", year);
            insert.Parameters.AddWithValue("$number", reserved.Number);
            insert.Parameters.AddWithValue("$visibleCode", reserved.VisibleCode);
            insert.Parameters.AddWithValue("$receivedAt", receivedAtIso);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            return new OrderRecord(orderId, siteId, customerId, year, reserved.Number, reserved.VisibleCode, receivedAtIso, "APERTA", 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public Task CloseAsync(string orderId, int expectedRowVersion, CancellationToken cancellationToken = default) =>
        SetStatusAsync(orderId, "CHIUSA", "closed_at", expectedRowVersion, cancellationToken);

    public async Task CancelAsync(string orderId, string reason, int expectedRowVersion, CancellationToken cancellationToken = default)
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
                UPDATE orders
                   SET status = 'ANNULLATA', canceled_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                       cancellation_reason = $reason,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE order_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", orderId);
            command.Parameters.AddWithValue("$reason", reason.Trim());
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Commessa");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task ReopenAsync(string orderId, string reason, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Il motivo della riapertura è obbligatorio.", nameof(reason));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE orders
                   SET status = 'APERTA', reopened_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), reopen_reason = $reason,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE order_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", orderId);
            command.Parameters.AddWithValue("$reason", reason.Trim());
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Commessa");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<OrderFinancials> GetFinancialsAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT total_cents, paid_cents, balance_cents, undefined_price_count FROM v_order_financials WHERE order_id = $id;";
        command.Parameters.AddWithValue("$id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new OrderFinancials(0, 0, 0, 0);
        }

        return new OrderFinancials(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    public async Task<OrderRecord?> FindAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT order_id, site_id, customer_id, order_year, order_number, visible_code, received_at, status, row_version
            FROM orders WHERE order_id = $id;
            """;
        command.Parameters.AddWithValue("$id", orderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadOrder(reader);
    }

    public async Task<IReadOnlyList<OrderRecord>> ListByCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT order_id, site_id, customer_id, order_year, order_number, visible_code, received_at, status, row_version
            FROM orders WHERE customer_id = $customerId ORDER BY received_at DESC;
            """;
        command.Parameters.AddWithValue("$customerId", customerId);

        var results = new List<OrderRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadOrder(reader));
        }

        return results;
    }

    /// <summary>
    /// Elenco delle commesse non chiuse (aperte o da risolvere), con nome cliente e totali già
    /// calcolati, per la schermata principale dell'interfaccia grafica: evita che l'interfaccia
    /// debba interrogare direttamente il database.
    /// </summary>
    public async Task<IReadOnlyList<OrderSummary>> ListOpenAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT o.order_id, o.visible_code,
                   trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name,
                   o.status, o.received_at,
                   COALESCE(f.total_cents, 0), COALESCE(f.paid_cents, 0), COALESCE(f.balance_cents, 0),
                   (SELECT COUNT(*) FROM items i WHERE i.order_id = o.order_id AND i.workflow_status <> 'ANNULLATO')
            FROM orders o
            JOIN customers c ON c.customer_id = o.customer_id
            LEFT JOIN v_order_financials f ON f.order_id = o.order_id
            WHERE o.status = 'APERTA'
            ORDER BY o.received_at DESC;
            """;

        var results = new List<OrderSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new OrderSummary(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8)));
        }

        return results;
    }

    private static OrderRecord ReadOrder(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2),
        reader.GetInt32(3), reader.GetInt32(4), reader.GetString(5),
        reader.GetString(6), reader.GetString(7), reader.GetInt32(8));

    private async Task SetStatusAsync(
        string orderId, string status, string timestampColumn, int expectedRowVersion, CancellationToken cancellationToken)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                UPDATE orders
                   SET status = $status, {timestampColumn} = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE order_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", orderId);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Commessa");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }
}
