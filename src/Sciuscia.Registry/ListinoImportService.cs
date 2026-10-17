using System.Globalization;
using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

public sealed record ListinoImportResult(
    int MarcheNuove, int MarcheEsistenti,
    int CategorieNuove, int CategorieEsistenti,
    int LavorazioniNuove, int LavorazioniAggiornate,
    IReadOnlyList<string> Avvisi);

/// <summary>
/// Importazione in blocco di marche, categorie e lavorazioni/prezzi/costi del Listino da un file
/// Excel preparato secondo il modello (foglio "Marche", foglio "Categorie", foglio "Listino").
/// Pensato per il caricamento iniziale e per gli aggiornamenti successivi in blocco: l'utente
/// modifica il file e lo importa di nuovo quando vuole — le marche/categorie già esistenti non
/// vengono duplicate, le lavorazioni già esistenti (stesso nome) vengono aggiornate, quelle nuove
/// vengono create. Le righe con dati non validi vengono segnalate e saltate, senza bloccare le
/// altre (un errore di distrazione in una riga non deve impedire di aggiornare tutto il resto).
/// </summary>
public sealed class ListinoImportService(AppPaths paths, SemaphoreSlim maintenanceGate)
{
    private static readonly HashSet<string> EsecuzioniValide = new(StringComparer.OrdinalIgnoreCase) { "INTERNA", "ESTERNA" };
    private static readonly HashSet<string> ModalitaValide = new(StringComparer.OrdinalIgnoreCase) { "FISSO", "FASCE", "DA_DEFINIRE" };

    public async Task<ListinoImportResult> ImportAsync(byte[] fileBytes, CancellationToken cancellationToken = default)
    {
        var reader = XlsxReader.Open(fileBytes);
        var avvisi = new List<string>();

        await maintenanceGate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);

            var (marcheNuove, marcheEsistenti) = reader.HasSheet("Marche")
                ? await ImportaNomiAsync(connection, reader, "Marche", "Marca", "brands", "canonical_name", cancellationToken)
                : (0, 0);

            var (categorieNuove, categorieEsistenti) = reader.HasSheet("Categorie")
                ? await ImportaNomiAsync(connection, reader, "Categorie", "Categoria", "categories", "name", cancellationToken)
                : (0, 0);

            var (lavorazioniNuove, lavorazioniAggiornate) = reader.HasSheet("Listino")
                ? await ImportaListinoAsync(connection, reader, avvisi, cancellationToken)
                : (0, 0);

            if (!reader.HasSheet("Marche") && !reader.HasSheet("Categorie") && !reader.HasSheet("Listino"))
            {
                avvisi.Add("Il file non contiene nessuno dei fogli attesi (Marche, Categorie, Listino): non è stato importato nulla.");
            }

            return new ListinoImportResult(marcheNuove, marcheEsistenti, categorieNuove, categorieEsistenti, lavorazioniNuove, lavorazioniAggiornate, avvisi);
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    private static async Task<(int Nuove, int Esistenti)> ImportaNomiAsync(
        SqliteConnection connection, XlsxReader reader, string foglio, string colonna, string tabella, string colonnaNome, CancellationToken cancellationToken)
    {
        var righe = reader.ReadSheetAsRows(foglio);
        var nuove = 0;
        var esistenti = 0;

        foreach (var riga in righe)
        {
            var nome = riga.GetValueOrDefault(colonna, "").Trim();
            if (nome.Length == 0)
            {
                continue;
            }

            // Le due tabelle (brands/categories) hanno una chiave primaria testuale diversa (brand_id / category_id), generata qui.
            await using var comando = connection.CreateCommand();
            var id = Guid.NewGuid().ToString();
            comando.CommandText = tabella == "brands"
                ? $"INSERT INTO brands(brand_id, canonical_name) VALUES ($id, $nome) ON CONFLICT({colonnaNome}) DO NOTHING;"
                : $"INSERT INTO categories(category_id, name) VALUES ($id, $nome) ON CONFLICT({colonnaNome}) DO NOTHING;";
            comando.Parameters.AddWithValue("$id", id);
            comando.Parameters.AddWithValue("$nome", nome);
            var righeInserite = await comando.ExecuteNonQueryAsync(cancellationToken);
            if (righeInserite > 0)
            {
                nuove++;
            }
            else
            {
                esistenti++;
            }
        }

        return (nuove, esistenti);
    }

    private static async Task<(int Nuove, int Aggiornate)> ImportaListinoAsync(
        SqliteConnection connection, XlsxReader reader, List<string> avvisi, CancellationToken cancellationToken)
    {
        var righe = reader.ReadSheetAsRows("Listino");
        var nuove = 0;
        var aggiornate = 0;
        var numeroRiga = 1; // la riga 1 del foglio è l'intestazione, i dati iniziano dalla riga 2

        foreach (var riga in righe)
        {
            numeroRiga++;

            var nome = riga.GetValueOrDefault("Lavorazione", "").Trim();
            if (nome.Length == 0)
            {
                continue; // riga vuota o senza nome: saltata senza avviso
            }

            var esecuzione = riga.GetValueOrDefault("Esecuzione", "").Trim();
            if (esecuzione.Length == 0)
            {
                esecuzione = "INTERNA";
            }
            else if (!EsecuzioniValide.Contains(esecuzione))
            {
                avvisi.Add($"Riga {numeroRiga} («{nome}»): esecuzione «{esecuzione}» non valida (deve essere INTERNA o ESTERNA) — riga saltata.");
                continue;
            }
            else
            {
                esecuzione = esecuzione.ToUpperInvariant();
            }

            var modalita = riga.GetValueOrDefault("ModalitaPrezzo", "").Trim();
            if (modalita.Length == 0)
            {
                modalita = "FISSO";
            }
            else if (!ModalitaValide.Contains(modalita))
            {
                avvisi.Add($"Riga {numeroRiga} («{nome}»): modalità prezzo «{modalita}» non valida (deve essere FISSO, FASCE o DA_DEFINIRE) — riga saltata.");
                continue;
            }
            else
            {
                modalita = modalita.ToUpperInvariant();
            }

            int? prezzoCents, costoCents;
            try
            {
                prezzoCents = EuroInCentesimi(riga.GetValueOrDefault("Prezzo", ""));
                costoCents = EuroInCentesimi(riga.GetValueOrDefault("Costo", ""));
            }
            catch (FormatException)
            {
                avvisi.Add($"Riga {numeroRiga} («{nome}»): prezzo o costo non è un numero valido — riga saltata.");
                continue;
            }

            if (modalita == "FISSO" && prezzoCents is null)
            {
                avvisi.Add($"Riga {numeroRiga} («{nome}»): modalità FISSO senza un Prezzo — riga saltata.");
                continue;
            }

            var etichetta = riga.GetValueOrDefault("Etichetta", "").Trim();
            if (etichetta.Length == 0)
            {
                etichetta = nome;
            }

            string? workTypeId;
            int? rowVersionEsistente;
            await using (var ricerca = connection.CreateCommand())
            {
                ricerca.CommandText = "SELECT work_type_id, row_version FROM work_types WHERE name = $nome COLLATE NOCASE;";
                ricerca.Parameters.AddWithValue("$nome", nome);
                await using var lettore = await ricerca.ExecuteReaderAsync(cancellationToken);
                if (await lettore.ReadAsync(cancellationToken))
                {
                    workTypeId = lettore.GetString(0);
                    rowVersionEsistente = lettore.GetInt32(1);
                }
                else
                {
                    workTypeId = null;
                    rowVersionEsistente = null;
                }
            }

            var prezzoStandard = modalita == "FISSO" ? prezzoCents : null;
            var costoStandard = modalita == "FISSO" ? costoCents : null;

            if (workTypeId is null)
            {
                workTypeId = Guid.NewGuid().ToString();
                await using var inserimento = connection.CreateCommand();
                inserimento.CommandText =
                    """
                    INSERT INTO work_types(work_type_id, name, label_text, execution_default, price_mode, standard_price_cents, standard_cost_cents)
                    VALUES ($id, $nome, $etichetta, $esecuzione, $modalita, $prezzo, $costo);
                    """;
                inserimento.Parameters.AddWithValue("$id", workTypeId);
                inserimento.Parameters.AddWithValue("$nome", nome);
                inserimento.Parameters.AddWithValue("$etichetta", etichetta);
                inserimento.Parameters.AddWithValue("$esecuzione", esecuzione);
                inserimento.Parameters.AddWithValue("$modalita", modalita);
                inserimento.Parameters.AddWithValue("$prezzo", (object?)prezzoStandard ?? DBNull.Value);
                inserimento.Parameters.AddWithValue("$costo", (object?)costoStandard ?? DBNull.Value);
                await inserimento.ExecuteNonQueryAsync(cancellationToken);
                nuove++;
            }
            else
            {
                await using var aggiornamento = connection.CreateCommand();
                aggiornamento.CommandText =
                    """
                    UPDATE work_types
                       SET label_text = $etichetta, execution_default = $esecuzione, price_mode = $modalita,
                           standard_price_cents = $prezzo, standard_cost_cents = $costo,
                           updated_at = strftime('%Y-%m-%dT%H:%M:%fZ','now'), row_version = row_version + 1
                     WHERE work_type_id = $id AND row_version = $expected;
                    """;
                aggiornamento.Parameters.AddWithValue("$id", workTypeId);
                aggiornamento.Parameters.AddWithValue("$etichetta", etichetta);
                aggiornamento.Parameters.AddWithValue("$esecuzione", esecuzione);
                aggiornamento.Parameters.AddWithValue("$modalita", modalita);
                aggiornamento.Parameters.AddWithValue("$prezzo", (object?)prezzoStandard ?? DBNull.Value);
                aggiornamento.Parameters.AddWithValue("$costo", (object?)costoStandard ?? DBNull.Value);
                aggiornamento.Parameters.AddWithValue("$expected", rowVersionEsistente!.Value);
                var righeAggiornate = await aggiornamento.ExecuteNonQueryAsync(cancellationToken);
                if (righeAggiornate == 0)
                {
                    avvisi.Add($"Riga {numeroRiga} («{nome}»): la lavorazione è stata modificata da qualcun altro nel frattempo — riga saltata, riprova l'importazione.");
                    continue;
                }

                aggiornate++;
            }

            if (modalita == "FASCE")
            {
                foreach (var bandCode in new[] { "A", "B", "C" })
                {
                    int? prezzoFascia, costoFascia;
                    try
                    {
                        prezzoFascia = EuroInCentesimi(riga.GetValueOrDefault($"Prezzo{bandCode}", ""));
                        costoFascia = EuroInCentesimi(riga.GetValueOrDefault($"Costo{bandCode}", ""));
                    }
                    catch (FormatException)
                    {
                        avvisi.Add($"Riga {numeroRiga} («{nome}»): prezzo o costo della fascia {bandCode} non è un numero valido — fascia saltata.");
                        continue;
                    }

                    if (prezzoFascia is null)
                    {
                        continue; // fascia non usata da questa lavorazione (es. solo A e B, non C)
                    }

                    await using var fascia = connection.CreateCommand();
                    fascia.CommandText =
                        """
                        INSERT INTO work_type_price_bands(work_type_id, band_code, price_cents, cost_cents)
                        VALUES ($workTypeId, $band, $price, $cost)
                        ON CONFLICT(work_type_id, band_code) DO UPDATE SET price_cents = excluded.price_cents, cost_cents = excluded.cost_cents;
                        """;
                    fascia.Parameters.AddWithValue("$workTypeId", workTypeId);
                    fascia.Parameters.AddWithValue("$band", bandCode);
                    fascia.Parameters.AddWithValue("$price", prezzoFascia.Value);
                    fascia.Parameters.AddWithValue("$cost", (object?)costoFascia ?? DBNull.Value);
                    await fascia.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        return (nuove, aggiornate);
    }

    /// <summary>Converte un importo in euro (testo, con '.' o ',' come separatore decimale) in centesimi.</summary>
    private static int? EuroInCentesimi(string testo)
    {
        testo = testo.Trim();
        if (testo.Length == 0)
        {
            return null;
        }

        testo = testo.Replace(',', '.');
        if (!double.TryParse(testo, NumberStyles.Float, CultureInfo.InvariantCulture, out var valore))
        {
            throw new FormatException($"Valore numerico non valido: «{testo}».");
        }

        if (valore < 0)
        {
            throw new FormatException("Un importo non può essere negativo.");
        }

        return (int)Math.Round(valore * 100, MidpointRounding.AwayFromZero);
    }
}
