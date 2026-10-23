using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Orders;

/// <summary>
/// Autotest del blocco Commesse: crea un database Fondazioni isolato e temporaneo e verifica
/// numerazione commessa/articolo, lavorazioni interne ed esterne, il blocco "Pronto senza
/// costo artigiano" imposto dal database, e l'affidamento esterno singolo attivo.
/// </summary>
public static class OrdersSelfTest
{
    public static async Task RunAsync()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"Sciuscia-OrdersSelfTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var maintenanceGate = new SemaphoreSlim(1, 1);

        try
        {
            var paths = new AppPaths(testRoot);
            var options = new FoundationOptions(testRoot);
            var database = new DatabaseInitializer(paths, options, maintenanceGate);
            var installation = await database.EnsureCreatedAsync();
            var photoStorage = new PhotoStorageService(paths, maintenanceGate);
            var orders = new OrdersRuntime(paths, maintenanceGate, photoStorage);

            var (customerId, categoryId, brandId, artisanId) = await SeedMinimalRegistryAsync(paths);

            // --- Commessa e numerazione (regole 2 e 3 dello schema) ---
            var order = await orders.Orders.CreateAsync(installation.SiteId, customerId);
            if (order.OrderNumber != 1 || order.VisibleCode != "LAB-1")
            {
                throw new InvalidDataException($"Numerazione commessa non corretta: {order.VisibleCode}.");
            }

            var item1 = await orders.Items.AddAsync(order.OrderId, order.VisibleCode, categoryId, brandId, "Scarpa di prova");
            var item2 = await orders.Items.AddAsync(order.OrderId, order.VisibleCode, categoryId, brandId, "Borsa di prova");
            if (item1.ItemSequence != 1 || item2.ItemSequence != 2 ||
                item1.VisibleCode != $"{order.VisibleCode}/1" || item2.VisibleCode != $"{order.VisibleCode}/2")
            {
                throw new InvalidDataException("Numerazione sottocodice articolo non corretta.");
            }

            // --- Fotografie (aggiunta, elenco, cancellazione logica col file spostato nel cestino) ---
            var fotoFinta = System.Text.Encoding.ASCII.GetBytes("finta-immagine-per-autotest");
            var foto = await orders.Photos.AddAsync(item1.ItemId, fotoFinta, ".jpg", "WEBCAM");
            var fotoElenco = await orders.Photos.ListByItemAsync(item1.ItemId);
            if (fotoElenco.Count != 1 || fotoElenco[0].PhotoId != foto.PhotoId)
            {
                throw new InvalidDataException("La fotografia aggiunta non risulta nell'elenco dell'articolo.");
            }

            await orders.Photos.DeleteAsync(foto.PhotoId, foto.RowVersion);
            var fotoElencoDopoCancellazione = await orders.Photos.ListByItemAsync(item1.ItemId);
            if (fotoElencoDopoCancellazione.Count != 0)
            {
                throw new InvalidDataException("La fotografia cancellata non avrebbe dovuto comparire ancora nell'elenco.");
            }

            // --- Lavorazione interna a prezzo definito ---
            // Nota: aggiungere una lavorazione fa scattare un trigger che aggiorna da solo
            // label_revision e row_version dell'articolo (regola 7 dello schema), quindi va
            // riletto l'articolo prima di qualsiasi aggiornamento con controllo di concorrenza.
            await orders.ItemWorks.AddAsync(item1.ItemId, null, "Risuolatura", "Risuolatura", null, 2500, 1000);
            item1 = await orders.Items.FindAsync(item1.ItemId) ?? throw new InvalidDataException("Articolo 1 non trovato dopo l'inserimento della lavorazione.");
            await orders.Items.SetWorkflowStatusAsync(item1.ItemId, "PRONTO_DA_AVVISARE", item1.RowVersion);

            // --- Lavorazione esterna: il database blocca "Pronto" finché manca il costo (regola 6) ---
            var assignment = await orders.ExternalAssignments.AssignAsync(item2.ItemId, artisanId);
            try
            {
                await orders.ExternalAssignments.AssignAsync(item2.ItemId, artisanId);
                throw new InvalidDataException("Un secondo affidamento attivo sullo stesso articolo avrebbe dovuto essere rifiutato.");
            }
            catch (InvalidOperationException)
            {
                // atteso: affidamento esterno singolo attivo per articolo.
            }

            var externalWork = await orders.ItemWorks.AddAsync(
                item2.ItemId, null, "Tintura", "Tintura", "B", 2000, null,
                "ESTERNA", assignment.ExternalAssignmentId);
            item2 = await orders.Items.FindAsync(item2.ItemId) ?? throw new InvalidDataException("Articolo 2 non trovato dopo l'inserimento della lavorazione.");

            try
            {
                await orders.Items.SetWorkflowStatusAsync(item2.ItemId, "PRONTO_DA_AVVISARE", item2.RowVersion);
                throw new InvalidDataException("L'articolo non avrebbe dovuto poter diventare Pronto senza il costo dell'artigiano.");
            }
            catch (OrderRuleViolationException)
            {
                // atteso: il fallimento sopra non modifica l'articolo, quindi item2.RowVersion resta valido.
            }

            await orders.ItemWorks.SetCostAsync(externalWork.ItemWorkId, 700, externalWork.RowVersion);
            await orders.Items.SetWorkflowStatusAsync(item2.ItemId, "PRONTO_DA_AVVISARE", item2.RowVersion);

            await orders.ExternalAssignments.MarkReturnedAsync(assignment.ExternalAssignmentId, null, assignment.RowVersion);
            await VerifyReturnDatesAsync(orders.ExternalAssignments, item2.ItemId, artisanId);

            // --- Totali della commessa (vista v_order_financials) ---
            var financials = await orders.Orders.GetFinancialsAsync(order.OrderId);
            if (financials.TotalCents != 2500 + 2000)
            {
                throw new InvalidDataException($"Totale commessa inatteso: {financials.TotalCents} centesimi invece di 4500.");
            }

            if (financials.UndefinedPriceCount != 0)
            {
                throw new InvalidDataException("Conteggio lavorazioni senza prezzo inatteso.");
            }

            // --- Annullamento commessa (motivo obbligatorio, storico conservato) ---
            await orders.Orders.CancelAsync(order.OrderId, "Prova autotest", order.RowVersion);
            var reloaded = await orders.Orders.FindAsync(order.OrderId)
                ?? throw new InvalidDataException("Commessa annullata non più trovata.");
            if (reloaded.Status != "ANNULLATA")
            {
                throw new InvalidDataException("L'annullamento della commessa non è stato applicato.");
            }

            await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath))
            {
                await DatabaseInitializer.VerifyIntegrityAsync(connection);
            }

            Console.WriteLine("AUTOTEST COMMESSE SUPERATO");
            Console.WriteLine($"Commessa: {order.VisibleCode} (annullata di prova)");
            Console.WriteLine($"Articoli: {item1.VisibleCode}, {item2.VisibleCode}");
            Console.WriteLine($"Totale lavorazioni: {financials.TotalCents} centesimi");
            Console.WriteLine("Fotografia: aggiunta, elencata e cancellata (logicamente) con successo");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static async Task VerifyReturnDatesAsync(ExternalAssignmentService service, string itemId, string artisanId)
    {
        // Il risultato atteso è scritto esplicitamente: nessuna dipendenza dal fuso del runner.
        (string Entrusted, string ReturnedOn, string Expected)[] cases =
        [
            ("2026-10-21T13:45:00.123Z", "2026-10-21", "2026-10-21T13:45:00.123Z"),
            ("2026-01-21T23:30:00.000Z", "2026-01-22", "2026-01-21T23:30:00.000Z"),
            ("2026-07-21T22:30:00.000Z", "2026-07-22", "2026-07-21T22:30:00.000Z"),
            ("2026-01-21T13:45:00.000Z", "2026-01-22", "2026-01-21T23:00:00.000Z"),
            ("2026-07-21T13:45:00.000Z", "2026-07-22", "2026-07-21T22:00:00.000Z"),
            // Il cambio d'ora avviene dopo mezzanotte: conta l'offset all'inizio del giorno.
            ("2026-03-28T12:00:00.000Z", "2026-03-29", "2026-03-28T23:00:00.000Z"),
            ("2026-03-28T12:00:00.000Z", "2026-03-30", "2026-03-29T22:00:00.000Z"),
            ("2026-10-24T12:00:00.000Z", "2026-10-25", "2026-10-24T22:00:00.000Z"),
            ("2026-10-24T12:00:00.000Z", "2026-10-26", "2026-10-25T23:00:00.000Z"),
        ];
        foreach (var (entrusted, returnedOn, expected) in cases)
        {
            var assignment = await service.AssignAsync(itemId, artisanId,
                DateTimeOffset.Parse(entrusted, System.Globalization.CultureInfo.InvariantCulture));
            await service.MarkReturnedOnDateAsync(assignment.ExternalAssignmentId,
                DateOnly.ParseExact(returnedOn, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), assignment.RowVersion);
            var saved = await service.FindAsync(assignment.ExternalAssignmentId)
                ?? throw new InvalidDataException("Affidamento di prova non trovato.");
            if (saved.ReturnedAt != expected || saved.RowVersion != assignment.RowVersion + 1 ||
                (await service.ListActiveByArtisanAsync(artisanId)).Any(a => a.ExternalAssignmentId == assignment.ExternalAssignmentId))
                throw new InvalidDataException($"Rientro {returnedOn}: atteso {expected}, ottenuto {saved.ReturnedAt}.");
        }

        // Il 21 UTC è già il 22 in Italia: il 21 va rifiutato anche se coincide con la data UTC.
        var invalid = await service.AssignAsync(itemId, artisanId,
            new DateTimeOffset(2026, 10, 21, 22, 30, 0, TimeSpan.Zero));
        foreach (var day in new[] { new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21) })
        {
            var rejected = false;
            try
            {
                await service.MarkReturnedOnDateAsync(invalid.ExternalAssignmentId, day, invalid.RowVersion);
            }
            catch (InvalidOperationException) { rejected = true; }
            var saved = await service.FindAsync(invalid.ExternalAssignmentId);
            if (!rejected || saved is null || saved.ReturnedAt != null || saved.RowVersion != invalid.RowVersion)
                throw new InvalidDataException("Una data precedente deve essere rifiutata senza modifiche.");
        }

        // L'API per istanti precisi conserva il vincolo cronologico, anche nello stesso giorno.
        var instantRejected = false;
        try
        {
            await service.MarkReturnedAsync(invalid.ExternalAssignmentId,
                new DateTimeOffset(2026, 10, 21, 22, 29, 0, TimeSpan.Zero), invalid.RowVersion);
        }
        catch (InvalidOperationException) { instantRejected = true; }
        if (!instantRejected) throw new InvalidDataException("Istante precedente all'affidamento accettato.");

        await service.MarkReturnedOnDateAsync(invalid.ExternalAssignmentId, new DateOnly(2026, 10, 22), invalid.RowVersion);
        var conflict = false;
        try
        {
            await service.MarkReturnedOnDateAsync(invalid.ExternalAssignmentId, new DateOnly(2026, 10, 23), invalid.RowVersion);
        }
        catch (ConcurrencyConflictException) { conflict = true; }
        if (!conflict) throw new InvalidDataException("Versione obsoleta accettata per il rientro.");
    }

    /// <summary>
    /// Inserisce con SQL diretto un cliente, una categoria, una marca e un artigiano minimi:
    /// il blocco Commesse non referenzia Anagrafiche (per non creare un ciclo tra i progetti),
    /// quindi per l'autotest bastano righe minime create qui, non i servizi veri di Registry.
    /// </summary>
    private static async Task<(string CustomerId, string CategoryId, string BrandId, string ArtisanId)> SeedMinimalRegistryAsync(AppPaths paths)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath);

        var customerId = Guid.NewGuid().ToString();
        var categoryId = Guid.NewGuid().ToString();
        var artisanId = Guid.NewGuid().ToString();
        string brandId;

        await using (var insertCustomer = connection.CreateCommand())
        {
            insertCustomer.CommandText = "INSERT INTO customers(customer_id, surname) VALUES ($id, 'Cliente di prova');";
            insertCustomer.Parameters.AddWithValue("$id", customerId);
            await insertCustomer.ExecuteNonQueryAsync();
        }

        await using (var insertCategory = connection.CreateCommand())
        {
            insertCategory.CommandText = "INSERT INTO categories(category_id, name) VALUES ($id, 'Categoria di prova');";
            insertCategory.Parameters.AddWithValue("$id", categoryId);
            await insertCategory.ExecuteNonQueryAsync();
        }

        await using (var insertArtisan = connection.CreateCommand())
        {
            insertArtisan.CommandText = "INSERT INTO artisans(artisan_id, display_name) VALUES ($id, 'Artigiano di prova');";
            insertArtisan.Parameters.AddWithValue("$id", artisanId);
            await insertArtisan.ExecuteNonQueryAsync();
        }

        await using (var readBrand = connection.CreateCommand())
        {
            readBrand.CommandText = "SELECT brand_id FROM brands WHERE canonical_name = 'Senza marca';";
            brandId = Convert.ToString(await readBrand.ExecuteScalarAsync())
                ?? throw new InvalidDataException("Marca «Senza marca» seminata dalle Fondazioni non trovata.");
        }

        return (customerId, categoryId, brandId, artisanId);
    }
}

