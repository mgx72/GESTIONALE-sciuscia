using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

public sealed record CustomerRecord(
    string CustomerId,
    string Surname,
    string? FirstName,
    string? MobileOriginal,
    string? MobileNormalized,
    string? Email,
    string PrivacyStatus,
    bool Active,
    int RowVersion);

public sealed class DuplicateMobileException(string mobileNormalized)
    : InvalidOperationException($"Il cellulare è già registrato per un altro cliente: {mobileNormalized}");

public sealed class ConcurrencyConflictException(string entity)
    : InvalidOperationException($"{entity}: il dato è stato modificato da un'altra operazione. Ricaricare e riprovare.");

/// <summary>
/// Anagrafica clienti. Applica la regola 1 dello schema (cellulare normalizzato univoco)
/// controllando prima l'unicità e restituendo un errore chiaro invece di lasciar fallire
/// il vincolo SQL con un messaggio tecnico.
/// </summary>
public sealed class CustomerService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<CustomerRecord> CreateAsync(
        string surname,
        string? firstName,
        string? mobileOriginal,
        string? email,
        string privacyStatus = "NON_REGISTRATA",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(surname))
        {
            throw new ArgumentException("Il cognome è obbligatorio.", nameof(surname));
        }

        var mobileNormalized = MobileNumberNormalizer.Normalize(mobileOriginal);

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await EnsureMobileAvailableAsync(connection, transaction, mobileNormalized, null, cancellationToken);

            var customerId = Guid.NewGuid().ToString();
            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText =
                    """
                    INSERT INTO customers(customer_id, surname, first_name, mobile_original, mobile_normalized, email, privacy_status, privacy_recorded_at)
                    VALUES ($id, $surname, $firstName, $mobileOriginal, $mobileNormalized, $email, $privacyStatus,
                        CASE WHEN $privacyStatus = 'NON_REGISTRATA' THEN NULL ELSE strftime('%Y-%m-%dT%H:%M:%fZ','now') END);
                    """;
                insert.Parameters.AddWithValue("$id", customerId);
                insert.Parameters.AddWithValue("$surname", surname.Trim());
                insert.Parameters.AddWithValue("$firstName", (object?)firstName ?? DBNull.Value);
                insert.Parameters.AddWithValue("$mobileOriginal", (object?)mobileOriginal ?? DBNull.Value);
                insert.Parameters.AddWithValue("$mobileNormalized", (object?)mobileNormalized ?? DBNull.Value);
                insert.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
                insert.Parameters.AddWithValue("$privacyStatus", privacyStatus);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new CustomerRecord(customerId, surname.Trim(), firstName, mobileOriginal, mobileNormalized, email, privacyStatus, true, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task UpdateAsync(
        string customerId,
        string surname,
        string? firstName,
        string? mobileOriginal,
        string? email,
        string privacyStatus,
        int expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(surname))
        {
            throw new ArgumentException("Il cognome è obbligatorio.", nameof(surname));
        }

        var mobileNormalized = MobileNumberNormalizer.Normalize(mobileOriginal);

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await EnsureMobileAvailableAsync(connection, transaction, mobileNormalized, customerId, cancellationToken);

            int updated;
            await using (var update = connection.CreateCommand())
            {
                update.Transaction = (SqliteTransaction)transaction;
                update.CommandText =
                    """
                    UPDATE customers
                       SET surname = $surname,
                           first_name = $firstName,
                           mobile_original = $mobileOriginal,
                           mobile_normalized = $mobileNormalized,
                           email = $email,
                           privacy_status = $privacyStatus,
                           privacy_recorded_at = CASE
                               WHEN privacy_status <> $privacyStatus THEN strftime('%Y-%m-%dT%H:%M:%fZ','now')
                               ELSE privacy_recorded_at
                           END,
                           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                           row_version = row_version + 1
                     WHERE customer_id = $id AND row_version = $expected;
                    """;
                update.Parameters.AddWithValue("$id", customerId);
                update.Parameters.AddWithValue("$surname", surname.Trim());
                update.Parameters.AddWithValue("$firstName", (object?)firstName ?? DBNull.Value);
                update.Parameters.AddWithValue("$mobileOriginal", (object?)mobileOriginal ?? DBNull.Value);
                update.Parameters.AddWithValue("$mobileNormalized", (object?)mobileNormalized ?? DBNull.Value);
                update.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
                update.Parameters.AddWithValue("$privacyStatus", privacyStatus);
                update.Parameters.AddWithValue("$expected", expectedRowVersion);
                updated = await update.ExecuteNonQueryAsync(cancellationToken);
            }

            if (updated == 0)
            {
                throw new ConcurrencyConflictException("Cliente");
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task SetActiveAsync(string customerId, bool active, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE customers
                   SET active = $active,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                       row_version = row_version + 1
                 WHERE customer_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", customerId);
            command.Parameters.AddWithValue("$active", active ? 1 : 0);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            var updated = await command.ExecuteNonQueryAsync(cancellationToken);
            if (updated == 0)
            {
                throw new ConcurrencyConflictException("Cliente");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Ricerca per cognome/nome (contiene, senza distinzione di maiuscole) o per cellulare normalizzato.</summary>
    /// <summary>
    /// Rilegge un cliente dal database dato il suo identificativo. Utile per mostrare il nome
    /// del cliente in altre schermate (per esempio il dettaglio di una commessa) senza dover
    /// rifare una ricerca testuale.
    /// </summary>
    public async Task<CustomerRecord?> FindAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT customer_id, surname, first_name, mobile_original, mobile_normalized, email, privacy_status, active, row_version
            FROM customers WHERE customer_id = $id;
            """;
        command.Parameters.AddWithValue("$id", customerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CustomerRecord(
            reader.GetString(0), reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6), reader.GetInt64(7) == 1, reader.GetInt32(8));
    }

    public async Task<IReadOnlyList<CustomerRecord>> SearchAsync(string query, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();

        var normalizedQuery = MobileNumberNormalizer.Normalize(query);
        command.CommandText =
            """
            SELECT customer_id, surname, first_name, mobile_original, mobile_normalized, email, privacy_status, active, row_version
            FROM customers
            WHERE (active = 1 OR $includeInactive = 1)
              AND (
                    (surname || ' ' || COALESCE(first_name, '')) LIKE '%' || $query || '%' COLLATE NOCASE
                    OR (mobile_normalized IS NOT NULL AND $normalizedQuery IS NOT NULL AND mobile_normalized = $normalizedQuery)
                  )
            ORDER BY surname, first_name;
            """;
        command.Parameters.AddWithValue("$includeInactive", includeInactive ? 1 : 0);
        command.Parameters.AddWithValue("$query", query.Trim());
        command.Parameters.AddWithValue("$normalizedQuery", (object?)normalizedQuery ?? DBNull.Value);

        return await ReadCustomersAsync(command, cancellationToken);
    }

    private static async Task EnsureMobileAvailableAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? mobileNormalized,
        string? excludingCustomerId,
        CancellationToken cancellationToken)
    {
        if (mobileNormalized is null)
        {
            return;
        }

        await using var check = connection.CreateCommand();
        check.Transaction = (SqliteTransaction)transaction;
        check.CommandText =
            """
            SELECT 1 FROM customers
             WHERE mobile_normalized = $mobile
               AND (customer_id <> $excludingId OR $excludingId IS NULL)
             LIMIT 1;
            """;
        check.Parameters.AddWithValue("$mobile", mobileNormalized);
        check.Parameters.AddWithValue("$excludingId", (object?)excludingCustomerId ?? DBNull.Value);
        var result = await check.ExecuteScalarAsync(cancellationToken);
        if (result is not null)
        {
            throw new DuplicateMobileException(mobileNormalized);
        }
    }

    private static async Task<IReadOnlyList<CustomerRecord>> ReadCustomersAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var results = new List<CustomerRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CustomerRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(6),
                reader.GetInt64(7) == 1,
                reader.GetInt32(8)));
        }

        return results;
    }
}
