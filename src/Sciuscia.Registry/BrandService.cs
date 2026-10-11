using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

public sealed record BrandRecord(string BrandId, string CanonicalName, bool Protected, bool Active, int RowVersion);

/// <summary>
/// Anagrafica marche. La marca "Senza marca" è protetta (creata dalle Fondazioni al primo
/// avvio): non può essere disattivata né rimossa con una fusione.
/// </summary>
public sealed class BrandService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<BrandRecord> CreateAsync(string canonicalName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            throw new ArgumentException("Il nome della marca è obbligatorio.", nameof(canonicalName));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var brandId = Guid.NewGuid().ToString();
            try
            {
                await using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO brands(brand_id, canonical_name) VALUES ($id, $name);";
                insert.Parameters.AddWithValue("$id", brandId);
                insert.Parameters.AddWithValue("$name", canonicalName.Trim());
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"Esiste già una marca chiamata «{canonicalName.Trim()}».", exception);
            }

            return new BrandRecord(brandId, canonicalName.Trim(), false, true, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task UpdateAsync(string brandId, string canonicalName, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(canonicalName))
        {
            throw new ArgumentException("Il nome della marca è obbligatorio.", nameof(canonicalName));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);

            await using (var protectedCheck = connection.CreateCommand())
            {
                protectedCheck.CommandText = "SELECT protected FROM brands WHERE brand_id = $id;";
                protectedCheck.Parameters.AddWithValue("$id", brandId);
                var result = await protectedCheck.ExecuteScalarAsync(cancellationToken);
                if (result is null)
                {
                    throw new InvalidOperationException("Marca non trovata.");
                }

                if (Convert.ToInt64(result) == 1)
                {
                    throw new InvalidOperationException("La marca «Senza marca» è protetta e non può essere rinominata.");
                }
            }

            try
            {
                await using var update = connection.CreateCommand();
                update.CommandText =
                    """
                    UPDATE brands
                       SET canonical_name = $name, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                     WHERE brand_id = $id AND row_version = $expected;
                    """;
                update.Parameters.AddWithValue("$id", brandId);
                update.Parameters.AddWithValue("$name", canonicalName.Trim());
                update.Parameters.AddWithValue("$expected", expectedRowVersion);
                if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    throw new ConcurrencyConflictException("Marca");
                }
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"Esiste già una marca chiamata «{canonicalName.Trim()}».", exception);
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task SetActiveAsync(string brandId, bool active, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);

            if (!active)
            {
                await using var protectedCheck = connection.CreateCommand();
                protectedCheck.CommandText = "SELECT protected FROM brands WHERE brand_id = $id;";
                protectedCheck.Parameters.AddWithValue("$id", brandId);
                var result = await protectedCheck.ExecuteScalarAsync(cancellationToken);
                if (result is null)
                {
                    throw new InvalidOperationException("Marca non trovata.");
                }

                if (Convert.ToInt64(result) == 1)
                {
                    throw new InvalidOperationException("La marca «Senza marca» è protetta e non può essere disattivata.");
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE brands
                   SET active = $active, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE brand_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", brandId);
            command.Parameters.AddWithValue("$active", active ? 1 : 0);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Marca");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Fonde due marche duplicate: sposta tutti gli articoli dalla marca rimossa a quella
    /// mantenuta, registra la fusione in brand_merges e disattiva la marca rimossa.
    /// Non tocca i nomi già congelati nelle lavorazioni delle commesse passate.
    /// </summary>
    public async Task MergeAsync(
        string removedBrandId,
        string retainedBrandId,
        string? operatorId,
        CancellationToken cancellationToken = default)
    {
        if (removedBrandId == retainedBrandId)
        {
            throw new ArgumentException("Non è possibile fondere una marca con sé stessa.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var protectedCheck = connection.CreateCommand())
            {
                protectedCheck.Transaction = (SqliteTransaction)transaction;
                protectedCheck.CommandText = "SELECT protected FROM brands WHERE brand_id = $id;";
                protectedCheck.Parameters.AddWithValue("$id", removedBrandId);
                var result = await protectedCheck.ExecuteScalarAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Marca da rimuovere non trovata.");
                if (Convert.ToInt64(result) == 1)
                {
                    throw new InvalidOperationException("La marca «Senza marca» è protetta e non può essere rimossa con una fusione.");
                }
            }

            await using (var moveItems = connection.CreateCommand())
            {
                moveItems.Transaction = (SqliteTransaction)transaction;
                moveItems.CommandText = "UPDATE items SET brand_id = $retained, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1 WHERE brand_id = $removed;";
                moveItems.Parameters.AddWithValue("$retained", retainedBrandId);
                moveItems.Parameters.AddWithValue("$removed", removedBrandId);
                await moveItems.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var recordMerge = connection.CreateCommand())
            {
                recordMerge.Transaction = (SqliteTransaction)transaction;
                recordMerge.CommandText =
                    """
                    INSERT INTO brand_merges(brand_merge_id, removed_brand_id, retained_brand_id, operator_id)
                    VALUES ($id, $removed, $retained, $operatorId);
                    """;
                recordMerge.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                recordMerge.Parameters.AddWithValue("$removed", removedBrandId);
                recordMerge.Parameters.AddWithValue("$retained", retainedBrandId);
                recordMerge.Parameters.AddWithValue("$operatorId", (object?)operatorId ?? DBNull.Value);
                await recordMerge.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var deactivate = connection.CreateCommand())
            {
                deactivate.Transaction = (SqliteTransaction)transaction;
                deactivate.CommandText = "UPDATE brands SET active = 0, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1 WHERE brand_id = $id;";
                deactivate.Parameters.AddWithValue("$id", removedBrandId);
                await deactivate.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<BrandRecord>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT brand_id, canonical_name, protected, active, row_version
            FROM brands
            WHERE active = 1 OR $includeInactive = 1
            ORDER BY canonical_name;
            """;
        command.Parameters.AddWithValue("$includeInactive", includeInactive ? 1 : 0);

        var results = new List<BrandRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new BrandRecord(
                reader.GetString(0), reader.GetString(1),
                reader.GetInt64(2) == 1, reader.GetInt64(3) == 1, reader.GetInt32(4)));
        }

        return results;
    }
}
