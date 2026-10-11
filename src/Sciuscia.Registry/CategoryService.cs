using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

public sealed record CategoryRecord(string CategoryId, string Name, int SortOrder, bool Active, int RowVersion);

/// <summary>Anagrafica categorie (tipologie di articolo: scarpe, borse, cinture, ...).</summary>
public sealed class CategoryService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<CategoryRecord> CreateAsync(string name, int sortOrder = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della categoria è obbligatorio.", nameof(name));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var categoryId = Guid.NewGuid().ToString();
            try
            {
                await using var insert = connection.CreateCommand();
                insert.CommandText =
                    "INSERT INTO categories(category_id, name, sort_order) VALUES ($id, $name, $sortOrder);";
                insert.Parameters.AddWithValue("$id", categoryId);
                insert.Parameters.AddWithValue("$name", name.Trim());
                insert.Parameters.AddWithValue("$sortOrder", sortOrder);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19) // SQLITE_CONSTRAINT
            {
                throw new InvalidOperationException($"Esiste già una categoria chiamata «{name.Trim()}».", exception);
            }

            return new CategoryRecord(categoryId, name.Trim(), sortOrder, true, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task UpdateAsync(string categoryId, string name, int sortOrder, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della categoria è obbligatorio.", nameof(name));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var update = connection.CreateCommand();
            update.CommandText =
                """
                UPDATE categories
                   SET name = $name, sort_order = $sortOrder,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                       row_version = row_version + 1
                 WHERE category_id = $id AND row_version = $expected;
                """;
            update.Parameters.AddWithValue("$id", categoryId);
            update.Parameters.AddWithValue("$name", name.Trim());
            update.Parameters.AddWithValue("$sortOrder", sortOrder);
            update.Parameters.AddWithValue("$expected", expectedRowVersion);
            var updated = await update.ExecuteNonQueryAsync(cancellationToken);
            if (updated == 0)
            {
                throw new ConcurrencyConflictException("Categoria");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task SetActiveAsync(string categoryId, bool active, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE categories
                   SET active = $active, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE category_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", categoryId);
            command.Parameters.AddWithValue("$active", active ? 1 : 0);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Categoria");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task<IReadOnlyList<CategoryRecord>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT category_id, name, sort_order, active, row_version
            FROM categories
            WHERE active = 1 OR $includeInactive = 1
            ORDER BY sort_order, name;
            """;
        command.Parameters.AddWithValue("$includeInactive", includeInactive ? 1 : 0);

        var results = new List<CategoryRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CategoryRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
                reader.GetInt64(3) == 1, reader.GetInt32(4)));
        }

        return results;
    }
}
