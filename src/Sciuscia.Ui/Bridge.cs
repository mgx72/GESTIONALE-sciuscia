using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Sciuscia.Foundations;
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
                "articoli.cambiaStato" => await CambiaStatoArticoloAsync(request.Payload),
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

            itemDetails.Add(new ItemDetailDto(
                item,
                categoryNames.GetValueOrDefault(item.CategoryId, "—"),
                brandNames.GetValueOrDefault(item.BrandId, "—"),
                workDetails));
        }

        var customerName = customer is null
            ? "—"
            : $"{customer.Surname} {customer.FirstName}".Trim();

        return new OrderDetailDto(order, customerName, financials, itemDetails);
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
        return await runtime.Registry.Artisans.CreateAsync(nome, contatto, telefono, email, indirizzo);
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
        await runtime.Registry.Artisans.UpdateAsync(artigianoId, nome, contatto, telefono, email, indirizzo, rowVersion);
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
        return await runtime.Registry.WorkTypes.CreateAsync(nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents, costoStandardCents);
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
        var rowVersion = RequireInt(payload, "rowVersion");
        await runtime.Registry.WorkTypes.UpdateAsync(lavorazioneId, nome, testoEtichetta, esecuzione, modalitaPrezzo, prezzoStandardCents, costoStandardCents, rowVersion);
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

        return await runtime.Orders.ItemWorks.AddAsync(
            itemId, workTypeId, nome, testoEtichetta, fascia, prezzoCents, costoCents, esecuzione, assignmentId);
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
}

/// <summary>Tutto ciò che serve alla schermata di dettaglio di una commessa, già pronto per la pagina.</summary>
internal sealed record OrderDetailDto(OrderRecord Order, string CustomerName, OrderFinancials Financials, IReadOnlyList<ItemDetailDto> Items);

internal sealed record ItemDetailDto(ItemRecord Item, string CategoryName, string BrandName, IReadOnlyList<ItemWorkDetailDto> Works);

internal sealed record ItemWorkDetailDto(ItemWorkRecord Work, string? ArtisanName);

internal sealed record BridgeRequest(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("payload")] JsonElement Payload = default);

internal sealed record BridgeResponse(
    [property: JsonPropertyName("requestId")] string? RequestId,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] string? Error);
