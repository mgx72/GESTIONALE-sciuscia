namespace Sciuscia.Foundations;

/// <summary>
/// Capacità di lavoro interna di una giornata (punto "distribuzione del carico" della roadmap,
/// successivo al Calendario di lavoro): un unico valore in minuti per tutto il laboratorio,
/// impostabile in Impostazioni > Calendario aperture, usato per confrontare il carico previsto
/// (somma dei tempi delle lavorazioni interne) con quanto è realisticamente gestibile in una
/// giornata. Le mezze giornate (vedi WorkCalendarService) dimezzano questo valore.
/// </summary>
public sealed class OperationalCapacityService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    public async Task<int> GetDailyCapacityMinutesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT daily_capacity_minutes FROM installation_settings WHERE settings_id = 1;";
        var result = await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Configurazione iniziale LAB non trovata.");
        return Convert.ToInt32(result);
    }

    public async Task SetDailyCapacityMinutesAsync(int dailyCapacityMinutes, CancellationToken cancellationToken = default)
    {
        if (dailyCapacityMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dailyCapacityMinutes), "La capacità giornaliera deve essere maggiore di zero.");
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE installation_settings
                   SET daily_capacity_minutes = $minutes,
                       updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'),
                       row_version = row_version + 1
                 WHERE settings_id = 1;
                """;
            command.Parameters.AddWithValue("$minutes", dailyCapacityMinutes);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }
}
