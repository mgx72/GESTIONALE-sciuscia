using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;
using System.Globalization;

namespace Sciuscia.Orders;

public sealed record ExternalAssignmentRecord(
    string ExternalAssignmentId,
    string ItemId,
    string ArtisanId,
    string EntrustedAt,
    string? ExpectedReturnDate,
    string? ReturnedAt,
    int RowVersion);

public sealed record ActiveExternalAssignment(string ExternalAssignmentId, string ItemId, string ItemCode, string ArtisanId, string ArtisanName, string EntrustedAt);

public sealed record ExternalAssignmentDetail(
    string ExternalAssignmentId,
    string ItemId,
    string ArtisanId,
    string ArtisanName,
    string EntrustedAt,
    string? ExpectedReturnDate,
    string? ReturnedAt,
    int RowVersion);

/// <summary>
/// Affidamento e rientro dagli artigiani esterni. Il database garantisce da solo che un
/// articolo abbia al massimo un affidamento attivo alla volta (regola 4 dello schema): qui il
/// vincolo viene solo tradotto in un messaggio comprensibile invece del codice SQLite grezzo.
/// </summary>
public sealed class ExternalAssignmentService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    private static readonly TimeZoneInfo ItalianTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "W. Europe Standard Time" : "Europe/Rome");

    public async Task<ExternalAssignmentRecord> AssignAsync(
        string itemId,
        string artisanId,
        DateTimeOffset? entrustedAt = null,
        string? expectedReturnDate = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var entrustedAtIso = (entrustedAt ?? DateTimeOffset.Now).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var assignmentId = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO external_assignments(external_assignment_id, item_id, artisan_id, entrusted_at, expected_return_date, notes)
                VALUES ($id, $itemId, $artisanId, $entrustedAt, $expectedReturn, $notes);
                """;
            insert.Parameters.AddWithValue("$id", assignmentId);
            insert.Parameters.AddWithValue("$itemId", itemId);
            insert.Parameters.AddWithValue("$artisanId", artisanId);
            insert.Parameters.AddWithValue("$entrustedAt", entrustedAtIso);
            insert.Parameters.AddWithValue("$expectedReturn", (object?)expectedReturnDate ?? DBNull.Value);
            insert.Parameters.AddWithValue("$notes", (object?)notes ?? DBNull.Value);

            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException("Questo articolo ha già un affidamento esterno attivo: farlo rientrare prima di riaffidarlo.", exception);
            }

            return new ExternalAssignmentRecord(assignmentId, itemId, artisanId, entrustedAtIso, expectedReturnDate, null, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public Task MarkReturnedAsync(string assignmentId, DateTimeOffset? returnedAt, int expectedRowVersion, CancellationToken cancellationToken = default)
        => MarkReturnedCoreAsync(assignmentId, returnedAt, null, expectedRowVersion, cancellationToken);

    /// <summary>
    /// La data di calendario è italiana, indipendentemente dal fuso del computer.
    /// Poiché l'ora non è nota, si conserva il primo istante compatibile con la data e
    /// l'affidamento: inizio del giorno italiano, oppure affidamento se nello stesso giorno.
    /// Una data nulla mantiene il significato di rientro all'istante corrente.
    /// </summary>
    public Task MarkReturnedOnDateAsync(string assignmentId, DateOnly? returnedOn, int expectedRowVersion, CancellationToken cancellationToken = default)
        => MarkReturnedCoreAsync(assignmentId, null, returnedOn, expectedRowVersion, cancellationToken);

    private async Task MarkReturnedCoreAsync(string assignmentId, DateTimeOffset? returnedAt, DateOnly? returnedOn, int expectedRowVersion, CancellationToken cancellationToken)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            if (returnedOn.HasValue)
            {
                await using var read = connection.CreateCommand();
                read.CommandText = "SELECT entrusted_at FROM external_assignments WHERE external_assignment_id = $id AND row_version = $expected;";
                read.Parameters.AddWithValue("$id", assignmentId);
                read.Parameters.AddWithValue("$expected", expectedRowVersion);
                var entrustedAtIso = await read.ExecuteScalarAsync(cancellationToken) as string
                    ?? throw new ConcurrencyConflictException("Affidamento esterno");
                var entrustedAt = DateTimeOffset.Parse(entrustedAtIso, CultureInfo.InvariantCulture);
                var entrustedOn = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entrustedAt, ItalianTimeZone).DateTime);
                if (returnedOn.Value < entrustedOn)
                {
                    throw new InvalidOperationException("La data di rientro non può essere precedente a quella di affidamento.");
                }

                returnedAt = returnedOn.Value == entrustedOn
                    ? entrustedAt
                    : new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                        returnedOn.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), ItalianTimeZone));
            }

            var returnedAtIso = (returnedAt ?? DateTimeOffset.UtcNow).ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE external_assignments
                   SET returned_at = $returnedAt, row_version = row_version + 1
                 WHERE external_assignment_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", assignmentId);
            command.Parameters.AddWithValue("$returnedAt", returnedAtIso);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);

            int updated;
            try
            {
                updated = await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException("La data di rientro non può essere precedente a quella di affidamento.", exception);
            }

            if (updated == 0)
            {
                throw new ConcurrencyConflictException("Affidamento esterno");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task CancelAsync(string assignmentId, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE external_assignments
                   SET canceled_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE external_assignment_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", assignmentId);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Affidamento esterno");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Rilegge un singolo affidamento con il nome dell'artigiano già unito (per mostrarlo, per
    /// esempio, dentro il dettaglio di una commessa senza dover interrogare separatamente
    /// l'anagrafica artigiani).
    /// </summary>
    public async Task<ExternalAssignmentDetail?> FindAsync(string assignmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT ea.external_assignment_id, ea.item_id, ea.artisan_id, a.display_name,
                   ea.entrusted_at, ea.expected_return_date, ea.returned_at, ea.row_version
            FROM external_assignments ea
            JOIN artisans a ON a.artisan_id = ea.artisan_id
            WHERE ea.external_assignment_id = $id;
            """;
        command.Parameters.AddWithValue("$id", assignmentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ExternalAssignmentDetail(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetInt32(7));
    }

    public async Task<IReadOnlyList<ActiveExternalAssignment>> ListActiveByArtisanAsync(string artisanId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT external_assignment_id, item_id, item_code, artisan_id, artisan_name, entrusted_at
            FROM v_active_external_assignments WHERE artisan_id = $artisanId ORDER BY entrusted_at;
            """;
        command.Parameters.AddWithValue("$artisanId", artisanId);

        var results = new List<ActiveExternalAssignment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ActiveExternalAssignment(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        }

        return results;
    }
}

