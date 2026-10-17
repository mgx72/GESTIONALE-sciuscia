namespace Sciuscia.Foundations;

public sealed record CalendarExceptionRecord(
    string CalendarExceptionId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string ExceptionType,
    double CapacityFactor,
    string Description);

/// <summary>
/// Un singolo giorno del calendario con il suo esito: lavorativo, chiuso o a mezza giornata, e
/// perché. CalendarExceptionId è presente solo quando il motivo è un'eccezione manuale (così la
/// schermata può offrire "Elimina" su quella riga) — è assente per chiusure ordinarie, festività
/// e patronale, che non sono righe salvate e non si possono eliminare.
/// </summary>
public sealed record WorkDayInfo(DateOnly Date, double CapacityFactor, string Reason, string? CalendarExceptionId);

/// <summary>
/// Calendario di lavoro (punto 12 della specifica funzionale): calcola se un giorno è lavorativo
/// tenendo conto delle chiusure ordinarie (domenica e lunedì), delle festività italiane e della
/// festa patronale (automatiche, calcolate — non salvate nel database), e delle eccezioni manuali
/// inserite dall'utente (chiusure, anche a mezza giornata, e aperture straordinarie).
///
/// "Capacità" di un giorno: 1.0 = giornata lavorativa intera, 0.5 = mezza giornata, 0.0 = chiuso.
/// Un'apertura straordinaria porta sempre la capacità a 1.0 anche su un giorno altrimenti chiuso;
/// una chiusura manuale (intera o a mezza giornata) riduce la capacità di un giorno altrimenti
/// lavorativo. Le eccezioni manuali vivono in Impostazioni → Calendario aperture; l'Agenda lavori
/// ne mostrerà solo gli effetti, senza poterle modificare direttamente (come richiesto dalla specifica).
/// </summary>
public sealed class WorkCalendarService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    /// <summary>
    /// Festa patronale: Sant'Antonio da Padova, 13 giugno, ricorrenza annuale fissa (così come
    /// indicato dalla specifica — non è configurabile dall'utente).
    /// </summary>
    private static readonly (int Mese, int Giorno) FestaPatronale = (6, 13);

    public async Task<CalendarExceptionRecord> AddClosureAsync(
        DateOnly startsOn, DateOnly endsOn, bool mezzaGiornata, string description, CancellationToken cancellationToken = default)
    {
        if (endsOn < startsOn)
        {
            throw new ArgumentException("La data finale non può essere prima della data iniziale.", nameof(endsOn));
        }

        if (mezzaGiornata && startsOn != endsOn)
        {
            throw new ArgumentException("La mezza giornata è consentita solo per una chiusura di un unico giorno.", nameof(mezzaGiornata));
        }

        return await InserisciEccezioneAsync(startsOn, endsOn, "CHIUSURA", mezzaGiornata ? 0.5 : 0.0, description, cancellationToken);
    }

    public async Task<CalendarExceptionRecord> AddExtraordinaryOpeningAsync(
        DateOnly date, string description, CancellationToken cancellationToken = default)
    {
        return await InserisciEccezioneAsync(date, date, "APERTURA_STRAORDINARIA", 1.0, description, cancellationToken);
    }

    private async Task<CalendarExceptionRecord> InserisciEccezioneAsync(
        DateOnly startsOn, DateOnly endsOn, string tipo, double capacita, string description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("La descrizione è obbligatoria.", nameof(description));
        }

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            var id = Guid.NewGuid().ToString();
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO calendar_exceptions(calendar_exception_id, starts_on, ends_on, exception_type, capacity_factor, description)
                VALUES ($id, $startsOn, $endsOn, $type, $capacity, $description);
                """;
            insert.Parameters.AddWithValue("$id", id);
            insert.Parameters.AddWithValue("$startsOn", startsOn.ToString("yyyy-MM-dd"));
            insert.Parameters.AddWithValue("$endsOn", endsOn.ToString("yyyy-MM-dd"));
            insert.Parameters.AddWithValue("$type", tipo);
            insert.Parameters.AddWithValue("$capacity", capacita);
            insert.Parameters.AddWithValue("$description", description.Trim());
            await insert.ExecuteNonQueryAsync(cancellationToken);

            return new CalendarExceptionRecord(id, startsOn, endsOn, tipo, capacita, description.Trim());
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Elimina un'eccezione manuale (chiusura o apertura straordinaria). Non elimina mai festività/patronale, che non sono righe salvate.</summary>
    public async Task DeleteExceptionAsync(string calendarExceptionId, CancellationToken cancellationToken = default)
    {
        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM calendar_exceptions WHERE calendar_exception_id = $id;";
            command.Parameters.AddWithValue("$id", calendarExceptionId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    /// <summary>Elenca le eccezioni manuali (chiusure e aperture straordinarie) che intersecano l'intervallo richiesto, ordinate per data.</summary>
    public async Task<IReadOnlyList<CalendarExceptionRecord>> ListExceptionsAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT calendar_exception_id, starts_on, ends_on, exception_type, capacity_factor, description
            FROM calendar_exceptions
            WHERE starts_on <= $toDate AND ends_on >= $fromDate
            ORDER BY starts_on;
            """;
        command.Parameters.AddWithValue("$fromDate", fromDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$toDate", toDate.ToString("yyyy-MM-dd"));

        var risultati = new List<CalendarExceptionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            risultati.Add(new CalendarExceptionRecord(
                reader.GetString(0),
                DateOnly.Parse(reader.GetString(1)),
                DateOnly.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.GetDouble(4),
                reader.GetString(5)));
        }

        return risultati;
    }

    /// <summary>
    /// Calcola l'esito di ogni giorno nell'intervallo richiesto (lavorativo/chiuso/mezza giornata
    /// e perché), tenendo conto di chiusure ordinarie, festività automatiche, patronale ed
    /// eccezioni manuali. Pensato per essere usato sia da Impostazioni → Calendario aperture (per
    /// mostrare l'effetto delle eccezioni) sia in futuro dall'Agenda lavori.
    /// </summary>
    public async Task<IReadOnlyList<WorkDayInfo>> GetCalendarAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentException("La data finale non può essere prima della data iniziale.", nameof(toDate));
        }

        var eccezioni = await ListExceptionsAsync(fromDate, toDate, cancellationToken);
        var risultati = new List<WorkDayInfo>();

        for (var data = fromDate; data <= toDate; data = data.AddDays(1))
        {
            risultati.Add(ValutaGiorno(data, eccezioni));
        }

        return risultati;
    }

    /// <summary>Capacità (0.0/0.5/1.0) di un singolo giorno, con lo stesso calcolo usato da <see cref="GetCalendarAsync"/>.</summary>
    public async Task<WorkDayInfo> GetDayAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var eccezioni = await ListExceptionsAsync(date, date, cancellationToken);
        return ValutaGiorno(date, eccezioni);
    }

    private static WorkDayInfo ValutaGiorno(DateOnly data, IReadOnlyList<CalendarExceptionRecord> eccezioni)
    {
        // Le eccezioni manuali hanno sempre la precedenza su chiusure ordinarie/festività/patronale:
        // un'apertura straordinaria riapre qualsiasi giorno, una chiusura manuale chiude (del tutto
        // o a mezza giornata) qualsiasi giorno, anche uno che sarebbe altrimenti lavorativo.
        var eccezioneDelGiorno = eccezioni.FirstOrDefault(e => data >= e.StartsOn && data <= e.EndsOn);
        if (eccezioneDelGiorno is not null)
        {
            var motivo = eccezioneDelGiorno.ExceptionType == "APERTURA_STRAORDINARIA"
                ? $"Apertura straordinaria: {eccezioneDelGiorno.Description}"
                : eccezioneDelGiorno.CapacityFactor == 0.5
                    ? $"Chiusura (mezza giornata): {eccezioneDelGiorno.Description}"
                    : $"Chiusura: {eccezioneDelGiorno.Description}";
            return new WorkDayInfo(data, eccezioneDelGiorno.CapacityFactor, motivo, eccezioneDelGiorno.CalendarExceptionId);
        }

        if (data.DayOfWeek is DayOfWeek.Sunday or DayOfWeek.Monday)
        {
            return new WorkDayInfo(data, 0.0, data.DayOfWeek == DayOfWeek.Sunday ? "Chiusura ordinaria (domenica)" : "Chiusura ordinaria (lunedì)", null);
        }

        var festivita = NomeFestivitaAutomatica(data);
        if (festivita is not null)
        {
            return new WorkDayInfo(data, 0.0, festivita, null);
        }

        if (data.Month == FestaPatronale.Mese && data.Day == FestaPatronale.Giorno)
        {
            return new WorkDayInfo(data, 0.0, "Festa patronale (Sant'Antonio, 13 giugno)", null);
        }

        return new WorkDayInfo(data, 1.0, "Giornata lavorativa", null);
    }

    /// <summary>Festività italiane a data fissa più Pasquetta (lunedì dell'Angelo); torna null se il giorno non è una festività.</summary>
    private static string? NomeFestivitaAutomatica(DateOnly data)
    {
        if (data.Month == 1 && data.Day == 1) return "Capodanno";
        if (data.Month == 1 && data.Day == 6) return "Epifania";
        if (data.Month == 4 && data.Day == 25) return "Festa della Liberazione";
        if (data.Month == 5 && data.Day == 1) return "Festa dei Lavoratori";
        if (data.Month == 6 && data.Day == 2) return "Festa della Repubblica";
        if (data.Month == 8 && data.Day == 15) return "Ferragosto";
        if (data.Month == 11 && data.Day == 1) return "Tutti i Santi";
        if (data.Month == 12 && data.Day == 8) return "Immacolata Concezione";
        if (data.Month == 12 && data.Day == 25) return "Natale";
        if (data.Month == 12 && data.Day == 26) return "Santo Stefano";

        var pasqua = CalcolaDomenicaDiPasqua(data.Year);
        if (data == pasqua) return "Pasqua";
        if (data == pasqua.AddDays(1)) return "Pasquetta (lunedì dell'Angelo)";

        return null;
    }

    /// <summary>Algoritmo di Meeus/Jones/Butcher per calcolare la data della domenica di Pasqua (calendario gregoriano).</summary>
    private static DateOnly CalcolaDomenicaDiPasqua(int anno)
    {
        var a = anno % 19;
        var b = anno / 100;
        var c = anno % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mese = (h + l - 7 * m + 114) / 31;
        var giorno = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(anno, mese, giorno);
    }
}
