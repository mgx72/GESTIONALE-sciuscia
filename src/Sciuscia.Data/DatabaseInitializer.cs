using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Sciuscia.Foundations;

public sealed record InstallationState(string SiteId, string OperatorId, string DatabasePath);

public sealed class DatabaseInitializer(
    AppPaths paths,
    FoundationOptions options,
    SemaphoreSlim maintenanceGate)
{
    public async Task<InstallationState> EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        await maintenanceGate.WaitAsync(cancellationToken);

        try
        {
            var isNew = !File.Exists(paths.DatabasePath) || new FileInfo(paths.DatabasePath).Length == 0;
            await using var connection = await OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);

            if (isNew)
            {
                var schema = await ReadEmbeddedSchemaAsync(cancellationToken);
                await using var schemaCommand = connection.CreateCommand();
                schemaCommand.CommandText = schema;
                await schemaCommand.ExecuteNonQueryAsync(cancellationToken);
                return await SeedInstallationAsync(connection, cancellationToken);
            }

            await VerifyIntegrityAsync(connection, cancellationToken);
            return await ReadInstallationAsync(connection, cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    public static SqliteConnection OpenConnection(string databasePath, bool pooling = true)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = pooling
        };

        return new SqliteConnection(builder.ToString());
    }

    /// <summary>
    /// Apre la connessione e applica le impostazioni obbligatorie per-connessione di SQLite
    /// (foreign_keys e busy_timeout non vengono salvate nel file del database: vanno reimpostate
    /// ogni volta che si apre una nuova connessione, altrimenti i controlli sui collegamenti tra
    /// tabelle definiti nello schema non verrebbero applicati). Usare pooling:false per le
    /// connessioni che devono poter essere chiuse in modo garantito e immediato (per esempio la
    /// copia di backup, che subito dopo va riletta con File.OpenRead su Windows).
    /// </summary>
    public static async Task<SqliteConnection> OpenReadyConnectionAsync(
        string databasePath,
        bool pooling = true,
        CancellationToken cancellationToken = default)
    {
        var connection = OpenConnection(databasePath, pooling);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using (var pragmas = connection.CreateCommand())
            {
                pragmas.CommandText =
                    """
                    PRAGMA foreign_keys = ON;
                    PRAGMA busy_timeout = 5000;
                    """;
                await pragmas.ExecuteNonQueryAsync(cancellationToken);
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public static async Task VerifyIntegrityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
        await foreignKeys.ExecuteNonQueryAsync(cancellationToken);

        await using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(await integrity.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Controllo database non superato: {result}");
        }

        await using var foreignKeyCheck = connection.CreateCommand();
        foreignKeyCheck.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await foreignKeyCheck.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("Il database contiene collegamenti non validi.");
        }
    }

    private async Task<InstallationState> SeedInstallationAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var siteId = Guid.NewGuid().ToString();
        var operatorId = Guid.NewGuid().ToString();
        var brandId = Guid.NewGuid().ToString();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteAsync(connection, transaction,
            "INSERT INTO sites(site_id, code, name) VALUES ($id, $code, $name);",
            cancellationToken,
            ("$id", siteId), ("$code", options.SiteCode), ("$name", options.SiteName));

        await ExecuteAsync(connection, transaction,
            "INSERT INTO operators(operator_id, display_name) VALUES ($id, 'Operatore LAB');",
            cancellationToken, ("$id", operatorId));

        await ExecuteAsync(connection, transaction,
            """
            INSERT INTO brands(brand_id, canonical_name, protected)
            VALUES ($id, 'Senza marca', 1);
            """,
            cancellationToken, ("$id", brandId));

        await ExecuteAsync(connection, transaction,
            """
            INSERT INTO installation_settings(
                settings_id, site_id, store_name, store_address,
                store_phone_1, store_phone_2, opening_hours,
                receipt_warning, photos_root, backup_root, automatic_backup_at)
            VALUES (
                1, $siteId, $storeName, $address,
                $phone1, $phone2, $hours,
                $warning, $photosRoot, $backupRoot, '14:00');
            """,
            cancellationToken,
            ("$siteId", siteId),
            ("$storeName", options.StoreName),
            ("$address", options.StoreAddress),
            ("$phone1", options.StorePhone1),
            ("$phone2", options.StorePhone2),
            ("$hours", options.OpeningHours),
            ("$warning", FoundationOptions.ApprovedReceiptWarning),
            ("$photosRoot", paths.PhotosRoot),
            ("$backupRoot", paths.BackupRoot));

        await transaction.CommitAsync(cancellationToken);
        return new InstallationState(siteId, operatorId, paths.DatabasePath);
    }

    private static async Task<InstallationState> ReadInstallationAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT s.site_id,
                   COALESCE((SELECT operator_id FROM operators WHERE active = 1 ORDER BY created_at LIMIT 1), ''),
                   s.code
            FROM installation_settings cfg
            JOIN sites s ON s.site_id = cfg.site_id
            WHERE cfg.settings_id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidDataException("Configurazione iniziale LAB non trovata.");
        }

        return new InstallationState(reader.GetString(0), reader.GetString(1), connection.DataSource);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string> ReadEmbeddedSchemaAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("001_schema_solo_lab.sql", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Schema SQLite incorporato non trovato.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
