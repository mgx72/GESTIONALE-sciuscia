using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

public sealed record WorkTypeRecord(
    string WorkTypeId,
    string Name,
    string LabelText,
    string ExecutionDefault,
    string PriceMode,
    int? StandardPriceCents,
    int? StandardCostCents,
    bool Active,
    int RowVersion);

public sealed record WorkTypePriceBand(string BandCode, int PriceCents, int? CostCents);

/// <summary>Listino lavorazioni (il "Listino" citato nello schema dati).</summary>
public sealed class WorkTypeService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    private static readonly HashSet<string> ValidExecutionModes = new(StringComparer.Ordinal) { "INTERNA", "ESTERNA" };
    private static readonly HashSet<string> ValidPriceModes = new(StringComparer.Ordinal) { "FISSO", "FASCE", "DA_DEFINIRE" };
    private static readonly HashSet<string> ValidBands = new(StringComparer.Ordinal) { "A", "B", "C" };

    public async Task<WorkTypeRecord> CreateAsync(
        string name,
        string labelText,
        string executionDefault,
        string priceMode,
        int? standardPriceCents,
        int? standardCostCents,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della lavorazione è obbligatorio.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(labelText))
        {
            throw new ArgumentException("Il testo per l'etichetta è obbligatorio.", nameof(labelText));
        }

        if (!ValidExecutionModes.Contains(executionDefault))
        {
            throw new ArgumentOutOfRangeException(nameof(executionDefault), "Valori ammessi: INTERNA, ESTERNA.");
        }

        if (!ValidPriceModes.Contains(priceMode))
        {
            throw new ArgumentOutOfRangeException(nameof(priceMode), "Valori ammessi: FISSO, FASCE, DA_DEFINIRE.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var workTypeId = Guid.NewGuid().ToString();
            try
            {
                await using var insert = connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO work_types(work_type_id, name, label_text, execution_default, price_mode, standard_price_cents, standard_cost_cents)
                    VALUES ($id, $name, $labelText, $executionDefault, $priceMode, $standardPrice, $standardCost);
                    """;
                insert.Parameters.AddWithValue("$id", workTypeId);
                insert.Parameters.AddWithValue("$name", name.Trim());
                insert.Parameters.AddWithValue("$labelText", labelText.Trim());
                insert.Parameters.AddWithValue("$executionDefault", executionDefault);
                insert.Parameters.AddWithValue("$priceMode", priceMode);
                insert.Parameters.AddWithValue("$standardPrice", (object?)standardPriceCents ?? DBNull.Value);
                insert.Parameters.AddWithValue("$standardCost", (object?)standardCostCents ?? DBNull.Value);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"Esiste già una lavorazione chiamata «{name.Trim()}».", exception);
            }

            return new WorkTypeRecord(workTypeId, name.Trim(), labelText.Trim(), executionDefault, priceMode, standardPriceCents, standardCostCents, true, 1);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task UpdateAsync(
        string workTypeId,
        string name,
        string labelText,
        string executionDefault,
        string priceMode,
        int? standardPriceCents,
        int? standardCostCents,
        int expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Il nome della lavorazione è obbligatorio.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(labelText))
        {
            throw new ArgumentException("Il testo per l'etichetta è obbligatorio.", nameof(labelText));
        }

        if (!ValidExecutionModes.Contains(executionDefault))
        {
            throw new ArgumentOutOfRangeException(nameof(executionDefault), "Valori ammessi: INTERNA, ESTERNA.");
        }

        if (!ValidPriceModes.Contains(priceMode))
        {
            throw new ArgumentOutOfRangeException(nameof(priceMode), "Valori ammessi: FISSO, FASCE, DA_DEFINIRE.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            try
            {
                await using var update = connection.CreateCommand();
                update.CommandText =
                    """
                    UPDATE work_types
                       SET name = $name, label_text = $labelText, execution_default = $executionDefault,
                           price_mode = $priceMode, standard_price_cents = $standardPrice, standard_cost_cents = $standardCost,
                           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                     WHERE work_type_id = $id AND row_version = $expected;
                    """;
                update.Parameters.AddWithValue("$id", workTypeId);
                update.Parameters.AddWithValue("$name", name.Trim());
                update.Parameters.AddWithValue("$labelText", labelText.Trim());
                update.Parameters.AddWithValue("$executionDefault", executionDefault);
                update.Parameters.AddWithValue("$priceMode", priceMode);
                update.Parameters.AddWithValue("$standardPrice", (object?)standardPriceCents ?? DBNull.Value);
                update.Parameters.AddWithValue("$standardCost", (object?)standardCostCents ?? DBNull.Value);
                update.Parameters.AddWithValue("$expected", expectedRowVersion);
                if (await update.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    throw new ConcurrencyConflictException("Lavorazione");
                }
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"Esiste già una lavorazione chiamata «{name.Trim()}».", exception);
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public async Task SetActiveAsync(string workTypeId, bool active, int expectedRowVersion, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE work_types
                   SET active = $active, updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                 WHERE work_type_id = $id AND row_version = $expected;
                """;
            command.Parameters.AddWithValue("$id", workTypeId);
            command.Parameters.AddWithValue("$active", active ? 1 : 0);
            command.Parameters.AddWithValue("$expected", expectedRowVersion);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ConcurrencyConflictException("Lavorazione");
            }
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Imposta il prezzo/costo di una fascia (A/B/C) per una lavorazione a prezzo a fasce.</summary>
    public async Task SetPriceBandAsync(string workTypeId, string bandCode, int priceCents, int? costCents, CancellationToken cancellationToken = default)
    {
        if (!ValidBands.Contains(bandCode))
        {
            throw new ArgumentOutOfRangeException(nameof(bandCode), "Fasce ammesse: A, B, C.");
        }

        if (priceCents < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceCents), "Il prezzo non può essere negativo.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO work_type_price_bands(work_type_id, band_code, price_cents, cost_cents)
                VALUES ($workTypeId, $band, $price, $cost)
                ON CONFLICT(work_type_id, band_code) DO UPDATE SET price_cents = excluded.price_cents, cost_cents = excluded.cost_cents;
                """;
            command.Parameters.AddWithValue("$workTypeId", workTypeId);
            command.Parameters.AddWithValue("$band", bandCode);
            command.Parameters.AddWithValue("$price", priceCents);
            command.Parameters.AddWithValue("$cost", (object?)costCents ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Segna se una lavorazione è compatibile con una categoria, con eventuale avviso da mostrare all'operatore.</summary>
    public async Task SetCategoryCompatibilityAsync(string categoryId, string workTypeId, bool allowed, string? warningText, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO category_work_compatibility(category_id, work_type_id, allowed, warning_text)
                VALUES ($categoryId, $workTypeId, $allowed, $warning)
                ON CONFLICT(category_id, work_type_id) DO UPDATE SET allowed = excluded.allowed, warning_text = excluded.warning_text;
                """;
            command.Parameters.AddWithValue("$categoryId", categoryId);
            command.Parameters.AddWithValue("$workTypeId", workTypeId);
            command.Parameters.AddWithValue("$allowed", allowed ? 1 : 0);
            command.Parameters.AddWithValue("$warning", (object?)warningText ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>
    /// Legge le fasce di prezzo (A/B/C) già impostate per una lavorazione a prezzo a fasce, in
    /// modo che l'interfaccia grafica possa far scegliere la fascia giusta quando si applica la
    /// lavorazione a un articolo.
    /// </summary>
    public async Task<IReadOnlyList<WorkTypePriceBand>> GetPriceBandsAsync(string workTypeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT band_code, price_cents, cost_cents FROM work_type_price_bands WHERE work_type_id = $workTypeId ORDER BY band_code;";
        command.Parameters.AddWithValue("$workTypeId", workTypeId);

        var results = new List<WorkTypePriceBand>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new WorkTypePriceBand(
                reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2)));
        }

        return results;
    }

    public async Task<IReadOnlyList<WorkTypeRecord>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT work_type_id, name, label_text, execution_default, price_mode, standard_price_cents, standard_cost_cents, active, row_version
            FROM work_types
            WHERE active = 1 OR $includeInactive = 1
            ORDER BY sort_order, name;
            """;
        command.Parameters.AddWithValue("$includeInactive", includeInactive ? 1 : 0);

        var results = new List<WorkTypeRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new WorkTypeRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetInt32(6),
                reader.GetInt64(7) == 1,
                reader.GetInt32(8)));
        }

        return results;
    }
}
