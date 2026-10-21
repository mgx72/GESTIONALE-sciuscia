using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

public sealed record ArtisanRecord(
    string ArtisanId,
    string DisplayName,
    string? ContactName,
    string? Phone,
    string? Email,
    string? Address,
    int? SolicitAfterDays,
    bool Active,
    int RowVersion);

/// <summary>Anagrafica artigiani esterni a cui vengono affidate le lavorazioni esterne.</summary>
public sealed class ArtisanService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<ArtisanRecord> CreateAsync(
        string displayName,
        string? contactName,
        string? phone,
        string? email,
        string? address,
        int? solicitAfterDays = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Il nome dell'artigiano è obbligatorio.", nameof(displayName));
        }

        if (solicitAfterDays is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(solicitAfterDays), "I giorni di sollecito devono essere maggiori di zero.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var artisanId = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO artisans(artisan_id, display_name, contact_name, phone, email, address, solicit_after_days)
                VALUES ($id, $displayName, $contactName, $phone, $email, $address, $solicitAfterDays);
                """;
            insert.Parameters.AddWithValue("$id", artisanId);
            insert.Parameters.AddWithValue("$displayName", displayName.Trim());
            insert.Parameters.AddWithValue("$contactName", (object?)contactName ?? DBNull.Value);
            insert.Parameters.AddWithValue("$phone", (object?)phone ?? DBNull.Value);
            insert.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
            insert.Parameters.AddWithValue("$address", (object?)address ?? DBNull.Value);
            insert.Parameters.AddWithValue("$solicitAfterDays", (object?)solicitAfterDays ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            return new ArtisanRecord(artisanId, displayName.Trim(), contactName, phone, email, address, solicitAfterDays, true, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task UpdateAsync(
        string artisanId,
        string displayName,
        string? contactName,
        string? phone,
        string? email,
        string? address,
        int expectedRowVersion,
        int? solicitAfterDays = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Il nome dell'artigiano è obbligatorio.", nameof(displayName));
        }

        if (solicitAfterDays is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(solicitAfterDays), "I giorni di sollecito devono essere maggiori di zero.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var update = connection.CreateCommand();
            update.CommandText =
                """
                UPDATE artisans
                   SET display_name = $displayName, contact_name = $contactName, phone = $phone,
                       email = $email, address = $address, solicit_after_days = $solicitAfterDays,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE artisan_id = $id AND row_version = $expected;
                """;
            update.Parameters.AddWithValue("$id", artisanId);
            update.Parameters.AddWithValue("$displayName", displayName.Trim());
            update.Parameters.AddWithValue("$contactName", (object?)contactName ?? DBNull.Value);
            update.Parameters.AddWithValue("$phone", (object?)phone ?? DBNull.Value);
            update.Parameters.AddWithValue("$email", (object?)email ?? DBNull.Value);
            update.Parameters.AddWithValue("$address", (object?)address ?? DBNull.Value);
            update.Parameters.AddWithValue("$solicitAfterDays", (object?)solicitAfterDays ?? DBNull.Value);
            update.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Artigiano");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task SetActiveAsync(string artisanId, bool active, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE artisans
                   SET active = $active, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE artisan_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", artisanId);
            command.Parameters.AddWithValue("$active", active ? 1 : 0);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Artigiano");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<ArtisanRecord>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT artisan_id, display_name, contact_name, phone, email, address, solicit_after_days, active, row_version
            FROM artisans
            WHERE active = 1 OR $includeInactive = 1
            ORDER BY display_name;
            """;
        command.Parameters.AddWithValue("$includeInactive", includeInactive ? 1 : 0);

        var results = new List<ArtisanRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ArtisanRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.GetInt64(7) == 1,
                reader.GetInt32(8)));
        }

        return results;
    }
}
