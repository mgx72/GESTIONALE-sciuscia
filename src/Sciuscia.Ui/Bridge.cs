using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sciuscia.Foundations;
using Sciuscia.Operations;
using Sciuscia.Orders;
using Sciuscia.Registry;

namespace Sciuscia.Ui;

/// <summary>
/// Unico punto di contatto tra le pagine web mostrate nella finestra e i servizi già collaudati
/// (Fondazioni/Anagrafiche/Commesse/Operazioni). Ogni richiesta arriva come testo JSON
/// {requestId, command, payload} e la risposta torna come {requestId, result} oppure
/// {requestId, error}: un errore qui non è mai un crash dell'applicazione, ma un messaggio che
/// la pagina può mostrare comprensibilmente all'operatore.
/// </summary>
internal sealed class Bridge(FoundationRuntime runtime, InstallationState installation)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> HandleAsync(string? requestJson)
    {
        string? requestId = null;
        try
        {
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                throw new InvalidOperationException("Richiesta vuota ricevuta dalla schermata.");
            }

            var request = JsonSerializer.Deserialize<BridgeRequest>(requestJson, JsonOptions)
                ?? throw new InvalidOperationException("Richiesta non valida ricevuta dalla schermata.");
            requestId = request.RequestId;

            object? result = request.Command switch
            {
                "commesse.elenco" => await runtime.Orders.Orders.ListOpenAsync(),
                "commesse.dettaglio" => await DettaglioCommessaAsync(request.Payload),
                "clienti.cerca" => await CercaClientiAsync(request.Payload),
                "clienti.creaRapido" => await CreaClienteRapidoAsync(request.Payload),
                "commesse.crea" => await CreaCommessaAsync(request.Payload),
                "anagrafiche.categorie" => await runtime.Registry.Categories.ListAsync(OptionalBool(request.Payload, "includiInattive") ?? false),
                "anagrafiche.creaCategoria" => await CreaCategoriaAsync(request.Payload),
                "anagrafiche.modificaCategoria" => await ModificaCategoriaAsync(request.Payload),
                "anagrafiche.attivaCategoria" => await ImpostaAttivaCategoriaAsync(request.Payload, true),
                "anagrafiche.disattivaCategoria" => await ImpostaAttivaCategoriaAsync(request.Payload, false),
                "anagrafiche.marche" => await runtime.Registry.Brands.ListAsync(OptionalBool(request.Payload, "includiInattive") ?? false),
                "anagrafiche.creaMarca" => await CreaMarcaAsync(request.Payload),
                "anagrafiche.modificaMarca" => await ModificaMarcaAsync(request.Payload),
                "anagrafiche.attivaMarca" => await ImpostaAttivaMarcaAsync(request.Payload, true),
                "anagrafiche.disattivaMarca" => await ImpostaAttivaMarcaAsync(request.Payload, false),
                "anagrafiche.listino" => await runtime.Registry.WorkTypes.ListAsync(OptionalBool(request.Payload, "includiInattive") ?? false),
                "anagrafiche.fasceLavorazione" => await FasceLavorazioneAsync(request.Payload),
                "anagrafiche.creaLavorazioneListino" => await CreaLavorazioneListinoAsync(request.Payload),
                "anagrafiche.modificaLavorazioneListino" => await ModificaLavorazioneListinoAsync(request.Payload),
                "anagrafiche.attivaLavorazione" => await ImpostaAttivaLavorazioneAsync(request.Payload, true),
                "anagrafiche.disattivaLavorazione" => await ImpostaAttivaLavorazioneAsync(request.Payload, false),
                "anagrafiche.impostaFasciaLavorazione" => await ImpostaFasciaLavorazioneAsync(request.Payload),
                "anagrafiche.importaListino" => await ImportaListinoAsync(request.Payload),
                "impostazioni.calendario" => await CalendarioAsync(request.Payload),
                "impostazioni.creaChiusuraCalendario" => await CreaChiusuraCalendarioAsync(request.Payload),
                "impostazioni.creaAperturaCalendario" => await CreaAperturaCalendarioAsync(request.Payload),
                "impostazioni.eliminaEccezioneCalendario" => await EliminaEccezioneCalendarioAsync(request.Payload),
                "impostazioni.leggiCapacitaGiornaliera" => await LeggiCapacitaGiornalieraAsync(),
                "impostazioni.impostaCapacitaGiornaliera" => await ImpostaCapacitaGiornalieraAsync(request.Payload),
                "agenda.elenco" => await AgendaElencoAsync(),
                "home.riepilogo" => await runtime.Orders.Home.GetRiepilogoAsync(),
                "ritiri.prontiDaRitirare" => await runtime.Orders.ReadyPickups.ListAsync(),
                "artigiani.pressoArtigiani" => await runtime.Orders.ArtisanWork.ListAsync(),
                "artigiani.segnaRientrato" => await SegnaRientratoAsync(request.Payload),
                "anagrafiche.artigiani" => await runtime.Registry.Artisans.ListAsync(OptionalBool(request.Payload, "includiInattive") ?? false),
                "anagrafiche.creaArtigiano" => await CreaArtigianoAsync(request.Payload),
                "anagrafiche.modificaArtigiano" => await ModificaArtigianoAsync(request.Payload),
                "anagrafiche.attivaArtigiano" => await ImpostaAttivaArtigianoAsync(request.Payload, true),
                "anagrafiche.disattivaArtigiano" => await ImpostaAttivaArtigianoAsync(request.Payload, false),
                "anagrafiche.clienti" => await ElencoClientiAsync(request.Payload),
                "anagrafiche.creaCliente" => await CreaClienteAnagraficaAsync(request.Payload),
                "anagrafiche.modificaCliente" => await ModificaClienteAsync(request.Payload),
                "anagrafiche.attivaCliente" => await ImpostaAttivoClienteAsync(request.Payload, true),
                "anagrafiche.disattivaCliente" => await ImpostaAttivoClienteAsync(request.Payload, false),
                "articoli.crea" => await CreaArticoloAsync(request.Payload),
                "articoli.aggiungiLavorazione" => await AggiungiLavorazioneAsync(request.Payload),
                "articoli.impostaPrezzoLavorazione" => await ImpostaPrezzoLavorazioneAsync(request.Payload),
                "articoli.impostaCostoLavorazione" => await ImpostaCostoLavorazioneAsync(request.Payload),
                "articoli.annullaLavorazione" => await AnnullaLavorazioneAsync(request.Payload),
                "articoli.rifiutaLavorazione" => await RifiutaLavorazioneAsync(request.Payload),
                "articoli.cambiaStato" => await CambiaStatoArticoloAsync(request.Payload),
                "articoli.impostaScadenza" => await ImpostaScadenzaArticoloAsync(request.Payload),
                "commesse.impostaScadenzaTutti" => await ImpostaScadenzaTuttiAsync(request.Payload),
                "articoli.aggiungiFoto" => await AggiungiFotoAsync(request.Payload),
                "articoli.eliminaFoto" => await EliminaFotoAsync(request.Payload),
                "operazioni.registraPagamento" => await RegistraPagamentoAsync(request.Payload),
                "operazioni.correggiPagamento" => await CorreggiPagamentoAsync(request.Payload),
                "operazioni.registraRitiro" => await RegistraRitiroAsync(request.Payload),
                "operazioni.inviaMessaggio" => await InviaMessaggioAsync(request.Payload),
                "operazioni.registraStampa" => await RegistraStampaAsync(request.Payload),
                "sistema.apriLink" => ApriLink(request.Payload),
                _ => throw new InvalidOperationException($"Comando non riconosciuto: {request.Command}"),
            };

            return JsonSerializer.Serialize(new BridgeResponse(requestId, result, null), JsonOptions);
        }
        catch (Exception exception)
        {
            return JsonSerializer.Serialize(new BridgeResponse(requestId, null, exception.Message), JsonOptions);
        }
    }

    private async Task<IReadOnlyList<CustomerRecord>> CercaClientiAsync(JsonElement payload)
    {
        var query = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("query", out var queryProp)
            ? queryProp.GetString() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return await runtime.Registry.Customers.SearchAsync(query);
    }

    private async Task<CustomerRecord> CreaClienteRapidoAsync(JsonElement payload)
    {
        var surname = RequireString(payload, "cognome");
        var firstName = OptionalString(payload, "nome");
        var mobile = OptionalString(payload, "cellulare");
        return await runtime.Registry.Customers.CreateAsync(surname, firstName, mobile, null);
    }

    private async Task<OrderRecord> CreaCommessaAsync(JsonElement payload)
    {
        var customerId = RequireString(payload, "clienteId");
        return await runtime.Orders.Orders.CreateAsync(installation.SiteId, customerId);
    }

    /// <summary>
    /// Prepara tutto ciò che serve alla schermata di dettaglio di una commessa in un'unica
    /// risposta: dati della commessa, nome del cliente, totali, e per ogni articolo la sua
    /// categoria/marca in chiaro e le lavorazioni applicate (con il nome dell'artigiano quando
    /// la lavorazione è affidata all'esterno).
    /// </summary>
    private async Task<OrderDetailDto> DettaglioCommessaAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var order = await runtime.Orders.Orders.FindAsync(orderId)
            ?? throw new InvalidOperationException("Commessa non trovata.");
        var customer = await runtime.Registry.Customers.FindAsync(order.CustomerId);
        var financials = await runtime.Orders.Orders.GetFinancialsAsync(orderId);
        var items = await runtime.Orders.Items.ListByOrderAsync(orderId);

        var categories = await runtime.Registry.Categories.ListAsync(includeInactive: true);
        var brands = await runtime.Registry.Brands.ListAsync(includeInactive: true);
        var categoryNames = categories.ToDictionary(c => c.CategoryId, c => c.Name);
        var brandNames = brands.ToDictionary(b => b.BrandId, b => b.CanonicalName);

        var itemDetails = new List<ItemDetailDto>();
        foreach (var item in items)
        {
            var works = await runtime.Orders.ItemWorks.ListByItemAsync(item.ItemId);
            var workDetails = new List<ItemWorkDetailDto>();
            foreach (var work in works)
            {
                string? artisanName = null;
                if (work.ExternalAssignmentId is not null)
                {
                    var assignment = await runtime.Orders.ExternalAssignments.FindAsync(work.ExternalAssignmentId);
                    artisanName = assignment?.ArtisanName;
                }

                workDetails.Add(new ItemWorkDetailDto(work, artisanName));
            }

            var photos = await runtime.Orders.Photos.ListByItemAsync(item.ItemId);
            var photoDetails = new List<PhotoDto>();
            foreach (var photo in photos)
            {
                photoDetails.Add(await ToPhotoDtoAsync(photo));
            }

            itemDetails.Add(new ItemDetailDto(
                item,
                categoryNames.GetValueOrDefault(item.CategoryId, "—"),
                brandNames.GetValueOrDefault(item.BrandId, "—"),
                workDetails,
                photoDetails));
        }

        var customerName = customer is null
            ? "—"
            : $"{customer.Surname} {customer.FirstName}".Trim();

        var payments = await runtime.Operations.Payments.ListByOrderAsync(orderId);
        var pickups = await runtime.Operations.Pickups.ListByOrderAsync(orderId);
        var messages = await runtime.Operations.Messages.ListByOrderAsync(orderId);
        var prints = await runtime.Operations.Prints.ListByOrderAsync(orderId);

        return new OrderDetailDto(
            order, customerName, customer?.MobileOriginal, customer?.Email, financials, itemDetails,
            payments, pickups, messages, prints);
    }

    /// <summary>
    /// Elenco degli articoli "Da lavorare" per l'Agenda lavori (specifica, sezione 5). L'ordine dei
    /// gruppi di lavorazione segue lo stesso ordine del Listino (Anagrafiche → Listino lavorazioni,
    /// "Sposta su/giù"): qui si usa semplicemente la posizione di ciascuna lavorazione nell'elenco
    /// già ordinato, senza bisogno di leggere la colonna sort_order a parte. Le lavorazioni a testo
    /// libero (non collegate al Listino) non hanno una posizione e vanno sempre in fondo.
    /// </summary>
    private async Task<IReadOnlyList<AgendaItemDto>> AgendaElencoAsync()
    {
        var righe = await runtime.Orders.Agenda.ListDaLavorareAsync();
        var listino = await runtime.Registry.WorkTypes.ListAsync(includeInactive: true);
        var ordineLavorazione = new Dictionary<string, int>();
        var durataLavorazione = new Dictionary<string, int?>();
        for (var indice = 0; indice < listino.Count; indice++)
        {
            ordineLavorazione[listino[indice].WorkTypeId] = indice;
            durataLavorazione[listino[indice].WorkTypeId] = listino[indice].DefaultDurationMinutes;
        }

        var risultati = new List<AgendaItemDto>();
        foreach (var riga in righe)
        {
            var lavorazioni = await runtime.Orders.ItemWorks.ListByItemAsync(riga.Item.ItemId);
            var lavorazioniInterne = lavorazioni
                .Where(w => w.CanceledAt is null && w.ExecutionType == "INTERNA")
                .Select(w => new AgendaWorkDto(
                    w.AppliedLabelText,
                    w.SourceWorkTypeId is not null && ordineLavorazione.TryGetValue(w.SourceWorkTypeId, out var posizione)
                        ? posizione
                        : int.MaxValue,
                    w.SourceWorkTypeId is not null && durataLavorazione.TryGetValue(w.SourceWorkTypeId, out var minuti)
                        ? minuti
                        : null))
                .OrderBy(w => w.Ordine)
                .ThenBy(w => w.Etichetta)
                .ToList();

            risultati.Add(new AgendaItemDto(
                riga.Item, riga.OrderId, riga.OrderVisibleCode, riga.CustomerName, riga.CategoryName, riga.BrandName,
                lavorazioniInterne));
        }

        return risultati;
    }

    /// <summary>
    /// Crea una categoria al volo dal modulo "Nuovo articolo", per quando l'anagrafica categorie
    /// è ancora vuota (nessuna categoria viene creata automaticamente all'installazione, a
    /// differenza della marca "Senza marca") e l'operatore non può ancora aprire la schermata
    /// Anagrafiche dedicata.
    /// </summary>
    private async Task<CategoryRecord> CreaCategoriaAsync(JsonElement payload)
    {
        var nome = RequireString(payload, "nome");
        return await runtime.Registry.Categories.CreateAsync(nome);
    }

    private async Task<CategoryRecord> ModificaCategoriaAsync(JsonElement payload)
    {
        var categoriaId = RequireString(payload, "categoriaId");
        var nome = RequireString(payload, "nome");
        var ordine = OptionalInt(payload, "ordine") ?? 0;
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Categories.UpdateAsync(categoriaId, nome, ordine, rowVersion);
        var categorie = await runtime.Registry.Categories.ListAsync(includeInactive: true);
        return categorie.First(c => c.CategoryId == categoriaId);
    }

    private async Task<CategoryRecord> ImpostaAttivaCategoriaAsync(JsonElement payload, bool attiva)
    {
        var categoriaId = RequireString(payload, "categoriaId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Categories.SetActiveAsync(categoriaId, attiva, rowVersion);
        var categorie = await runtime.Registry.Categories.ListAsync(includeInactive: true);
        return categorie.First(c => c.CategoryId == categoriaId);
    }

    private async Task<BrandRecord> CreaMarcaAsync(JsonElement payload)
    {
        var nome = RequireString(payload, "nome");
        return await runtime.Registry.Brands.CreateAsync(nome);
    }

    private async Task<BrandRecord> ModificaMarcaAsync(JsonElement payload)
    {
        var marcaId = RequireString(payload, "marcaId");
        var nome = RequireString(payload, "nome");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Brands.UpdateAsync(marcaId, nome, rowVersion);
        var marche = await runtime.Registry.Brands.ListAsync(includeInactive: true);
        return marche.First(b => b.BrandId == marcaId);
    }

    private async Task<BrandRecord> ImpostaAttivaMarcaAsync(JsonElement payload, bool attiva)
    {
        var marcaId = RequireString(payload, "marcaId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Brands.SetActiveAsync(marcaId, attiva, rowVersion);
        var marche = await runtime.Registry.Brands.ListAsync(includeInactive: true);
        return marche.First(b => b.BrandId == marcaId);
    }

    /// <summary>
    /// Crea un artigiano al volo dal modulo "Aggiungi lavorazione", per quando si vuole segnare
    /// una lavorazione come esterna ma l'anagrafica artigiani è ancora vuota (nessun artigiano
    /// viene creato automaticamente all'installazione).
    /// </summary>
    private async Task<ArtisanRecord> CreaArtigianoAsync(JsonElement payload)
    {
        var nome = RequireString(payload, "nome");
        var contatto = OptionalString(payload, "contatto");
        var telefono = OptionalString(payload, "telefono");
        var email = OptionalString(payload, "email");
        var indirizzo = OptionalString(payload, "indirizzo");
        var giorniSollecito = OptionalInt(payload, "giorniSollecito");
        return await runtime.Registry.Artisans.CreateAsync(nome, contatto, telefono, email, indirizzo, giorniSollecito);
    }

    private async Task<ArtisanRecord> ModificaArtigianoAsync(JsonElement payload)
    {
        var artigianoId = RequireString(payload, "artigianoId");
        var nome = RequireString(payload, "nome");
        var contatto = OptionalString(payload, "contatto");
        var telefono = OptionalString(payload, "telefono");
        var email = OptionalString(payload, "email");
        var indirizzo = OptionalString(payload, "indirizzo");
        var rowVersion = RequireInt(payload, "rowVersion");
        var giorniSollecito = OptionalInt(payload, "giorniSollecito");
        await runtime.Registry.Artisans.UpdateAsync(artigianoId, nome, contatto, telefono, email, indirizzo, rowVersion, giorniSollecito);
        var artigiani = await runtime.Registry.Artisans.ListAsync(includeInactive: true);
        return artigiani.First(a => a.ArtisanId == artigianoId);
    }

    private async Task<ArtisanRecord> ImpostaAttivaArtigianoAsync(JsonElement payload, bool attiva)
    {
        var artigianoId = RequireString(payload, "artigianoId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Artisans.SetActiveAsync(artigianoId, attiva, rowVersion);
        var artigiani = await runtime.Registry.Artisans.ListAsync(includeInactive: true);
        return artigiani.First(a => a.ArtisanId == artigianoId);
    }

    private async Task<IReadOnlyList<CustomerRecord>> ElencoClientiAsync(JsonElement payload)
    {
        var query = OptionalString(payload, "query") ?? string.Empty;
        return await runtime.Registry.Customers.SearchAsync(query, includeInactive: true);
    }

    /// <summary>Crea un cliente completo dalla schermata Anagrafiche (a differenza della creazione al volo dentro "Nuova commessa", qui si può inserire anche l'email).</summary>
    private async Task<CustomerRecord> CreaClienteAnagraficaAsync(JsonElement payload)
    {
        var cognome = RequireString(payload, "cognome");
        var nome = OptionalString(payload, "nome");
        var cellulare = OptionalString(payload, "cellulare");
        var email = OptionalString(payload, "email");
        return await runtime.Registry.Customers.CreateAsync(cognome, nome, cellulare, email);
    }

    private async Task<CustomerRecord> ModificaClienteAsync(JsonElement payload)
    {
        var clienteId = RequireString(payload, "clienteId");
        var cognome = RequireString(payload, "cognome");
        var nome = OptionalString(payload, "nome");
        var cellulare = OptionalString(payload, "cellulare");
        var email = OptionalString(payload, "email");
        var statoPrivacy = OptionalString(payload, "statoPrivacy") ?? "NON_REGISTRATA";
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Customers.UpdateAsync(clienteId, cognome, nome, cellulare, email, statoPrivacy, rowVersion);
        return await runtime.Registry.Customers.FindAsync(clienteId)
            ?? throw new InvalidOperationException("Cliente non trovato dopo l'aggiornamento.");
    }

    private async Task<CustomerRecord> ImpostaAttivoClienteAsync(JsonElement payload, bool attivo)
    {
        var clienteId = RequireString(payload, "clienteId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.Customers.SetActiveAsync(clienteId, attivo, rowVersion);
        return await runtime.Registry.Customers.FindAsync(clienteId)
            ?? throw new InvalidOperationException("Cliente non trovato dopo l'aggiornamento.");
    }

    private async Task<IReadOnlyList<WorkTypePriceBand>> FasceLavorazioneAsync(JsonElement payload)
    {
        var workTypeId = RequireString(payload, "workTypeId");
        return await runtime.Registry.WorkTypes.GetPriceBandsAsync(workTypeId);
    }

    /// <summary>Crea una nuova voce del listino dalla schermata Anagrafiche.</summary>
    private async Task<WorkTypeRecord> CreaLavorazioneListinoAsync(JsonElement payload)
    {
        var nome = RequireString(payload, "nome");
        var testoEtichetta = RequireString(payload, "testoEtichetta");
        var esecuzione = OptionalString(payload, "esecuzione") ?? "INTERNA";
        var modalitaPrezzo = OptionalString(payload, "modalitaPrezzo") ?? "DA_DEFINIRE";
        var prezzoStandardCents = OptionalInt(payload, "prezzoStandardCents");
        var costoStandardCents = OptionalInt(payload, "costoStandardCents");
        var tempoRealizzoMinuti = OptionalInt(payload, "tempoRealizzoMinuti");
        return await runtime.Registry.WorkTypes.CreateAsync(nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents, costoStandardCents, tempoRealizzoMinuti);
    }

    private async Task<WorkTypeRecord> ModificaLavorazioneListinoAsync(JsonElement payload)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var nome = RequireString(payload, "nome");
        var testoEtichetta = RequireString(payload, "testoEtichetta");
        var esecuzione = OptionalString(payload, "esecuzione") ?? "INTERNA";
        var modalitaPrezzo = OptionalString(payload, "modalitaPrezzo") ?? "DA_DEFINIRE";
        var prezzoStandardCents = OptionalInt(payload, "prezzoStandardCents");
        var costoStandardCents = OptionalInt(payload, "costoStandardCents");
        var tempoRealizzoMinuti = OptionalInt(payload, "tempoRealizzoMinuti");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.WorkTypes.UpdateAsync(lavorazioneId, nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents, costoStandardCents, tempoRealizzoMinuti, rowVersion);
        var listino = await runtime.Registry.WorkTypes.ListAsync(includeInactive: true);
        return listino.First(w => w.WorkTypeId == lavorazioneId);
    }

    private async Task<WorkTypeRecord> ImpostaAttivaLavorazioneAsync(JsonElement payload, bool attiva)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.WorkTypes.SetActiveAsync(lavorazioneId, attiva, rowVersion);
        var listino = await runtime.Registry.WorkTypes.ListAsync(includeInactive: true);
        return listino.First(w => w.WorkTypeId == lavorazioneId);
    }

    private async Task<IReadOnlyList<WorkTypePriceBand>> ImpostaFasciaLavorazioneAsync(JsonElement payload)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var fascia = RequireString(payload, "fascia");
        var prezzoCents = RequireInt(payload, "prezzoCents");
        var costoCents = OptionalInt(payload, "costoCents");
        await runtime.Registry.WorkTypes.SetPriceBandAsync(lavorazioneId, fascia, prezzoCents, costoCents);
        return await runtime.Registry.WorkTypes.GetPriceBandsAsync(lavorazioneId);
    }

    private async Task<ItemRecord> CreaArticoloAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var categoryId = RequireString(payload, "categoriaId");
        var brandId = RequireString(payload, "marcaId");
        var descrizione = RequireString(payload, "descrizione");
        var scadenza = OptionalString(payload, "scadenza");
        var urgente = OptionalBool(payload, "urgente") ?? false;
        var priorita = OptionalBool(payload, "priorita") ?? false;

        var order = await runtime.Orders.Orders.FindAsync(orderId)
            ?? throw new InvalidOperationException("Commessa non trovata.");

        return await runtime.Orders.Items.AddAsync(orderId, order.VisibleCode, categoryId, brandId, descrizione, scadenza, urgente, priorita);
    }

    /// <summary>
    /// Applica una lavorazione a un articolo. Se è esterna, crea prima l'affidamento
    /// all'artigiano scelto e lo collega alla lavorazione, così come richiesto dallo schema dati
    /// (una lavorazione esterna deve sempre avere un affidamento).
    /// </summary>
    private async Task<ItemWorkRecord> AggiungiLavorazioneAsync(JsonElement payload)
    {
        var itemId = RequireString(payload, "articoloId");
        var workTypeId = OptionalString(payload, "workTypeId");
        var nome = RequireString(payload, "nome");
        var testoEtichetta = RequireString(payload, "testoEtichetta");
        var fascia = OptionalString(payload, "fascia");
        var prezzoCents = OptionalInt(payload, "prezzoCents");
        var costoCents = OptionalInt(payload, "costoCents");
        var esecuzione = OptionalString(payload, "esecuzione") ?? "INTERNA";
        var artigianoId = OptionalString(payload, "artigianoId");

        string? assignmentId = null;
        if (esecuzione == "ESTERNA")
        {
            if (string.IsNullOrWhiteSpace(artigianoId))
            {
                throw new ArgumentException("Selezionare l'artigiano per una lavorazione esterna.");
            }

            var assignment = await runtime.Orders.ExternalAssignments.AssignAsync(itemId, artigianoId);
            assignmentId = assignment.ExternalAssignmentId;
        }

        // Una lavorazione senza prezzo resta in attesa di risposta del cliente al preventivo
        // (approval_status PENDENTE): la conferma arriva impostando il prezzo (ImpostaPrezzoLavorazioneAsync),
        // il rifiuto con RifiutaLavorazioneAsync. Una lavorazione già prezzata nasce invece approvata, come prima.
        var approvazioneIniziale = prezzoCents.HasValue ? "APPROVATA" : "PENDENTE";

        return await runtime.Orders.ItemWorks.AddAsync(
            itemId, workTypeId, nome, testoEtichetta, fascia, prezzoCents, costoCents, esecuzione, assignmentId, approvazioneIniziale);
    }

    /// <summary>Decide (o corregge) il prezzo di una lavorazione già applicata, es. dopo che il cliente accetta un preventivo.</summary>
    private async Task<object> ImpostaPrezzoLavorazioneAsync(JsonElement payload)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var prezzoCents = RequireInt(payload, "prezzoCents");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.ItemWorks.SetPriceAsync(lavorazioneId, prezzoCents, rowVersion);
        return new { ok = true };
    }

    /// <summary>Corregge il costo dell'artigiano di una lavorazione esterna già applicata alla commessa.</summary>
    private async Task<object> ImpostaCostoLavorazioneAsync(JsonElement payload)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var costoCents = RequireInt(payload, "costoCents");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.ItemWorks.SetCostAsync(lavorazioneId, costoCents, rowVersion);
        return new { ok = true };
    }

    /// <summary>Annulla una lavorazione applicata (es. il cliente non vuole più farla fare): resta nello storico, non si elimina.</summary>
    private async Task<object> AnnullaLavorazioneAsync(JsonElement payload)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var motivo = RequireString(payload, "motivo");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.ItemWorks.CancelAsync(lavorazioneId, motivo, rowVersion);
        return new { ok = true };
    }

    /// <summary>
    /// Segna come rifiutata dal cliente una lavorazione ancora senza prezzo (risposta a un preventivo
    /// inviato): resta senza prezzo, ma esce dall'elenco di quelle ancora in attesa di risposta e non
    /// entra più nel totale della commessa (vedi v_order_financials).
    /// </summary>
    private async Task<object> RifiutaLavorazioneAsync(JsonElement payload)
    {
        var lavorazioneId = RequireString(payload, "lavorazioneId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.ItemWorks.RejectAsync(lavorazioneId, rowVersion);
        return new { ok = true };
    }

    private async Task<ItemRecord> CambiaStatoArticoloAsync(JsonElement payload)
    {
        var itemId = RequireString(payload, "articoloId");
        var nuovoStato = RequireString(payload, "nuovoStato");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.Items.SetWorkflowStatusAsync(itemId, nuovoStato, rowVersion);
        return await runtime.Orders.Items.FindAsync(itemId)
            ?? throw new InvalidOperationException("Articolo non trovato dopo l'aggiornamento.");
    }

    /// <summary>Riporta sull'articolo la data scelta nella finestra "Consulta carico delle prossime settimane" (specifica, sezione 3).</summary>
    private async Task<ItemRecord> ImpostaScadenzaArticoloAsync(JsonElement payload)
    {
        var itemId = RequireString(payload, "articoloId");
        var scadenza = OptionalString(payload, "scadenza");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.Items.SetDueDateAsync(itemId, scadenza, rowVersion);
        return await runtime.Orders.Items.FindAsync(itemId)
            ?? throw new InvalidOperationException("Articolo non trovato dopo l'aggiornamento.");
    }

    /// <summary>
    /// Applica la stessa data a tutti gli articoli non annullati della commessa in un colpo
    /// (specifica: "la data può essere applicata a tutti gli articoli e poi corretta sulle
    /// eccezioni"). Ogni articolo resta comunque correggibile singolarmente dopo.
    /// </summary>
    private async Task<object> ImpostaScadenzaTuttiAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var scadenza = OptionalString(payload, "scadenza");
        var articoli = await runtime.Orders.Items.ListByOrderAsync(orderId);
        var aggiornati = 0;
        foreach (var articolo in articoli)
        {
            if (articolo.WorkflowStatus == "ANNULLATO")
            {
                continue;
            }

            await runtime.Orders.Items.SetDueDateAsync(articolo.ItemId, scadenza, articolo.RowVersion);
            aggiornati++;
        }

        return new { ok = true, aggiornati };
    }

    /// <summary>
    /// Riceve una fotografia già pronta come immagine (scattata con la webcam direttamente nella
    /// pagina, o scelta da file e letta lì): la pagina manda i byte come testo base64 (eventualmente
    /// con il prefisso "data:...;base64," che qui viene tolto) insieme all'estensione da usare.
    /// </summary>
    private async Task<PhotoDto> AggiungiFotoAsync(JsonElement payload)
    {
        var itemId = RequireString(payload, "articoloId");
        var immagineBase64 = RequireString(payload, "immagineBase64");
        var estensione = RequireString(payload, "estensione");
        var sorgente = OptionalString(payload, "sorgente") ?? "WEBCAM";
        var nomeFile = OptionalString(payload, "nomeFile");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(EstraiBase64(immagineBase64));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Dati fotografia non validi.", exception);
        }

        var photo = await runtime.Orders.Photos.AddAsync(itemId, bytes, estensione, sorgente, nomeFile);
        return await ToPhotoDtoAsync(photo);
    }

    /// <summary>
    /// Importa in blocco marche, categorie e lavorazioni/prezzi/costi del Listino da un file Excel
    /// preparato secondo il modello. Usato sia per il primo caricamento sia per gli aggiornamenti
    /// successivi (l'utente modifica il file e lo importa di nuovo).
    /// </summary>
    private async Task<ListinoImportResult> ImportaListinoAsync(JsonElement payload)
    {
        var fileBase64 = RequireString(payload, "fileBase64");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(EstraiBase64(fileBase64));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("File non valido: non è stato possibile leggerlo.", exception);
        }

        try
        {
            return await runtime.Registry.ListinoImport.ImportAsync(bytes);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ArgumentException("Il file non sembra un Excel (.xlsx) valido, oppure non segue il modello previsto.", exception);
        }
    }

    /// <summary>
    /// Il calendario di lavoro (Impostazioni → Calendario aperture): un esito per ogni giorno
    /// dell'intervallo richiesto (lavorativo, chiuso o a mezza giornata, e perché).
    /// </summary>
    private async Task<IReadOnlyList<WorkDayInfo>> CalendarioAsync(JsonElement payload)
    {
        var daData = RequireDate(payload, "daData");
        var aData = RequireDate(payload, "aData");
        return await runtime.Calendar.GetCalendarAsync(daData, aData);
    }

    private async Task<CalendarExceptionRecord> CreaChiusuraCalendarioAsync(JsonElement payload)
    {
        var daData = RequireDate(payload, "daData");
        var aData = RequireDate(payload, "aData");
        var mezzaGiornata = OptionalBool(payload, "mezzaGiornata") ?? false;
        var descrizione = RequireString(payload, "descrizione");
        return await runtime.Calendar.AddClosureAsync(daData, aData, mezzaGiornata, descrizione);
    }

    private async Task<CalendarExceptionRecord> CreaAperturaCalendarioAsync(JsonElement payload)
    {
        var data = RequireDate(payload, "data");
        var descrizione = RequireString(payload, "descrizione");
        return await runtime.Calendar.AddExtraordinaryOpeningAsync(data, descrizione);
    }

    private async Task<object> EliminaEccezioneCalendarioAsync(JsonElement payload)
    {
        var eccezioneId = RequireString(payload, "eccezioneId");
        await runtime.Calendar.DeleteExceptionAsync(eccezioneId);
        return new { ok = true };
    }

    private async Task<object> LeggiCapacitaGiornalieraAsync()
    {
        var minuti = await runtime.Capacity.GetDailyCapacityMinutesAsync();
        return new { capacitaGiornalieraMinuti = minuti };
    }

    private async Task<object> ImpostaCapacitaGiornalieraAsync(JsonElement payload)
    {
        var minuti = RequireInt(payload, "capacitaGiornalieraMinuti");
        await runtime.Capacity.SetDailyCapacityMinutesAsync(minuti);
        return new { capacitaGiornalieraMinuti = minuti };
    }

    private async Task<object> EliminaFotoAsync(JsonElement payload)
    {
        var fotoId = RequireString(payload, "fotoId");
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Orders.Photos.DeleteAsync(fotoId, rowVersion);
        return new { ok = true };
    }

    /// <summary>Registra un pagamento (parziale o saldo) su una commessa. Il database non permette importi a zero.</summary>
    private async Task<PaymentEventRecord> RegistraPagamentoAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var importoCents = RequireInt(payload, "importoCents");
        var tipo = RequireString(payload, "tipo");
        return await runtime.Operations.Payments.AddAsync(orderId, importoCents, tipo);
    }

    /// <summary>
    /// Corregge un pagamento già registrato con un nuovo evento (tipicamente di segno opposto),
    /// senza mai modificare o eliminare quello originale: il motivo è obbligatorio.
    /// </summary>
    private async Task<PaymentEventRecord> CorreggiPagamentoAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var correggePagamentoId = RequireString(payload, "correggePagamentoId");
        var importoCents = RequireInt(payload, "importoCents");
        var motivo = RequireString(payload, "motivo");
        return await runtime.Operations.Payments.AddCorrectionAsync(orderId, correggePagamentoId, importoCents, motivo);
    }

    /// <summary>Registra il ritiro di uno o più articoli: il database li porta automaticamente allo stato "Consegnato".</summary>
    private async Task<PickupRecord> RegistraRitiroAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var articoli = RequireStringArray(payload, "articoliId");
        var note = OptionalString(payload, "note");
        return await runtime.Operations.Pickups.CreateAsync(orderId, articoli, notes: note);
    }

    /// <summary>
    /// Registra il rientro di un articolo affidato a un artigiano (specifica, sezione 7, comando
    /// "Segna Rientrato"). La data propone oggi ma resta modificabile, per registrare un rientro
    /// avvenuto prima; questo comando non cambia da solo lo stato di lavorazione dell'articolo,
    /// che resta una scelta dell'operatore dal dettaglio della commessa, come già per tutti gli
    /// altri cambi di stato.
    /// </summary>
    private async Task<object> SegnaRientratoAsync(JsonElement payload)
    {
        var affidamentoId = RequireString(payload, "affidamentoId");
        var rowVersion = RequireInt(payload, "rowVersion");
        var dataRientro = OptionalDate(payload, "dataRientro");
        await runtime.Orders.ExternalAssignments.MarkReturnedOnDateAsync(affidamentoId, dataRientro, rowVersion);
        return new { ok = true };
    }

    /// <summary>Registra un messaggio inviato al cliente, con il testo effettivo congelato al momento dell'invio.</summary>
    private async Task<MessageRecord> InviaMessaggioAsync(JsonElement payload)
    {
        var orderId = RequireString(payload, "commessaId");
        var tipo = RequireString(payload, "tipo");
        var scopo = RequireString(payload, "scopo");
        var testo = RequireString(payload, "testo");
        var destinatario = OptionalString(payload, "destinatario");
        var articoli = OptionalStringArray(payload, "articoliId");
        return await runtime.Operations.Messages.SendAsync(orderId, tipo, scopo, testo, articoli, destinatario);
    }

    /// <summary>
    /// Registra un evento di stampa (ricevuta, fotografie o etichetta). Per l'etichetta serve
    /// anche la versione corrente dell'etichetta dell'articolo, così il database sa riconoscere
    /// quando l'etichetta stampata non corrisponde più ai dati attuali.
    /// </summary>
    private async Task<PrintEventRecord> RegistraStampaAsync(JsonElement payload)
    {
        var orderId = OptionalString(payload, "commessaId");
        var articoloId = OptionalString(payload, "articoloId");
        var tipoDocumento = RequireString(payload, "tipoDocumento");
        var esito = RequireString(payload, "esito");

        int? labelRevision = null;
        if (tipoDocumento == "ETICHETTA" && articoloId is not null)
        {
            var articolo = await runtime.Orders.Items.FindAsync(articoloId);
            labelRevision = articolo?.LabelRevision;
        }

        return await runtime.Operations.Prints.RecordAsync(orderId, articoloId, tipoDocumento, "v1", esito, labelRevision: labelRevision);
    }

    /// <summary>
    /// Apre un collegamento con il programma predefinito del PC (il browser per WhatsApp Web,
    /// il programma di posta per un "mailto:"): serve per preparare un messaggio al cliente senza
    /// dover copiare/incollare il testo a mano. Ammessi solo link http/https/mailto, mai percorsi
    /// di file o altri schemi, per sicurezza.
    /// </summary>
    private static object ApriLink(JsonElement payload)
    {
        var url = RequireString(payload, "url");
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Collegamento non consentito.");
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return new { ok = true };
    }

    private static string EstraiBase64(string valore)
    {
        var virgola = valore.IndexOf(',');
        return valore.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && virgola >= 0
            ? valore[(virgola + 1)..]
            : valore;
    }

    private async Task<PhotoDto> ToPhotoDtoAsync(PhotoRecord photo)
    {
        var absolutePath = Path.Combine(runtime.Paths.PhotosRoot, photo.RelativePath);
        var bytes = await File.ReadAllBytesAsync(absolutePath);
        var mime = photo.Extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        return new PhotoDto(photo.PhotoId, photo.ItemId, dataUrl, photo.OriginalFilename, photo.AcquiredAt, photo.Source, photo.SortOrder, photo.RowVersion);
    }

    private static string RequireString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString()))
        {
            return property.GetString()!;
        }

        throw new ArgumentException($"Campo obbligatorio mancante: {propertyName}.");
    }

    private static string? OptionalString(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static int RequireInt(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value))
        {
            return value;
        }

        throw new ArgumentException($"Campo obbligatorio mancante: {propertyName}.");
    }

    private static int? OptionalInt(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    private static IReadOnlyList<string> RequireStringArray(JsonElement payload, string propertyName)
    {
        var list = ReadStringArray(payload, propertyName);
        if (list is { Count: > 0 })
        {
            return list;
        }

        throw new ArgumentException($"Campo obbligatorio mancante: {propertyName}.");
    }

    private static IReadOnlyList<string>? OptionalStringArray(JsonElement payload, string propertyName)
    {
        var list = ReadStringArray(payload, propertyName);
        return list is { Count: > 0 } ? list : null;
    }

    private static List<string>? ReadStringArray(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Array)
        {
            return property.EnumerateArray()
                .Select(element => element.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }

        return null;
    }

    private static bool? OptionalBool(JsonElement payload, string propertyName)
    {
        if (payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(propertyName, out var property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            return property.GetBoolean();
        }

        return null;
    }

    /// <summary>Legge una data nel formato "AAAA-MM-GG" (lo stesso usato dai campi &lt;input type="date"&gt;).</summary>
    private static DateOnly RequireDate(JsonElement payload, string propertyName)
    {
        var testo = RequireString(payload, propertyName);
        if (DateOnly.TryParse(testo, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var data))
        {
            return data;
        }

        throw new ArgumentException($"Data non valida: {propertyName}.");
    }

    /// <summary>Legge una data facoltativa nel formato "AAAA-MM-GG"; assente o vuota restituisce null.</summary>
    private static DateOnly? OptionalDate(JsonElement payload, string propertyName)
    {
        var testo = OptionalString(payload, propertyName);
        if (string.IsNullOrWhiteSpace(testo))
        {
            return null;
        }

        if (DateOnly.TryParse(testo, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var data))
        {
            return data;
        }

        throw new ArgumentException($"Data non valida: {propertyName}.");
    }
}

/// <summary>Tutto ciò che serve alla schermata di dettaglio di una commessa, già pronto per la pagina.</summary>
internal sealed record OrderDetailDto(
    OrderRecord Order,
    string CustomerName,
    string? CustomerMobile,
    string? CustomerEmail,
    OrderFinancials Financials,
    IReadOnlyList<ItemDetailDto> Items,
    IReadOnlyList<PaymentEventRecord> Payments,
    IReadOnlyList<PickupRecord> Pickups,
    IReadOnlyList<MessageRecord> Messages,
    IReadOnlyList<PrintEventRecord> Prints);

internal sealed record ItemDetailDto(ItemRecord Item, string CategoryName, string BrandName, IReadOnlyList<ItemWorkDetailDto> Works, IReadOnlyList<PhotoDto> Photos);

internal sealed record ItemWorkDetailDto(ItemWorkRecord Work, string? ArtisanName);

/// <summary>Un articolo "Da lavorare" per l'Agenda, con solo le lavorazioni interne non annullate.</summary>
internal sealed record AgendaItemDto(
    ItemRecord Item,
    string OrderId,
    string OrderVisibleCode,
    string CustomerName,
    string CategoryName,
    string BrandName,
    IReadOnlyList<AgendaWorkDto> Works);

/// <summary>
/// Una lavorazione interna mostrata nell'Agenda, con la sua posizione nell'ordine del Listino
/// (per il raggruppamento) e il tempo di realizzo stimato (per il carico a ore in "Consulta
/// carico"/Agenda) — assente se la lavorazione del Listino non ha un tempo impostato.
/// </summary>
internal sealed record AgendaWorkDto(string Etichetta, int Ordine, int? DurationMinutes);

/// <summary>Fotografia pronta per la pagina: l'immagine è già inclusa come data URL, così la pagina la mostra senza un'altra richiesta.</summary>
internal sealed record PhotoDto(
    string PhotoId,
    string ItemId,
    string DataUrl,
    string? OriginalFilename,
    string AcquiredAt,
    string Source,
    int SortOrder,
    int RowVersion);

internal sealed record BridgeRequest(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("payload")] JsonElement Payload = default);

internal sealed record BridgeResponse(
    [property: JsonPropertyName("requestId")] string? RequestId,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] string? Error);

