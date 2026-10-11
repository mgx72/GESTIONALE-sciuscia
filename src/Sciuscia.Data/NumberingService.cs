using Microsoft.Data.Sqlite;

namespace Sciuscia.Foundations;

public sealed record ReservedOrderNumber(int Number, string VisibleCode);

public sealed class NumberingService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<ReservedOrderNumber> ReserveNextOrderAsync(
        string siteId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var create = connection.CreateCommand())
            {
                create.Transaction = (SqliteTransaction)transaction;
                create.CommandText =
                    """
                    INSERT INTO number_sequences(site_id, sequence_name, sequence_year, last_value)
                    VALUES ($siteId, 'COMMESSA', $year, 0)
                    ON CONFLICT(site_id, sequence_name, sequence_year) DO NOTHING;
                    """;
                create.Parameters.AddWithValue("$siteId", siteId);
                create.Parameters.AddWithValue("$year", year);
                await create.ExecuteNonQueryAsync(cancellationToken);
            }

            int number;
            await using (var increment = connection.CreateCommand())
            {
                increment.Transaction = (SqliteTransaction)transaction;
                increment.CommandText =
                    """
                    UPDATE number_sequences
                       SET last_value = last_value + 1,
                           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now')
                     WHERE site_id = $siteId
                       AND sequence_name = 'COMMESSA'
                       AND sequence_year = $year
                    RETURNING last_value;
                    """;
                increment.Parameters.AddWithValue("$siteId", siteId);
                increment.Parameters.AddWithValue("$year", year);
                number = Convert.ToInt32(await increment.ExecuteScalarAsync(cancellationToken));
            }

            string siteCode;
            await using (var readSite = connection.CreateCommand())
            {
                readSite.Transaction = (SqliteTransaction)transaction;
                readSite.CommandText = "SELECT code FROM sites WHERE site_id = $siteId;";
                readSite.Parameters.AddWithValue("$siteId", siteId);
                siteCode = Convert.ToString(await readSite.ExecuteScalarAsync(cancellationToken))
                    ?? throw new InvalidDataException("Sede non trovata.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new ReservedOrderNumber(number, $"{siteCode}-{number}");
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public static string ItemVisibleCode(string orderVisibleCode, int itemSequence)
    {
        if (string.IsNullOrWhiteSpace(orderVisibleCode))
        {
            throw new ArgumentException("Codice commessa obbligatorio.", nameof(orderVisibleCode));
        }

        if (itemSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemSequence));
        }

        return $"{orderVisibleCode}/{itemSequence}";
    }
}

