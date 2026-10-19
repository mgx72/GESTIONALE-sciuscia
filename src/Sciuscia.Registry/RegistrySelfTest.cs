using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Registry;

/// <summary>
/// Autotest del blocco Anagrafiche: crea un database Fondazioni isolato e temporaneo (con lo
/// stesso meccanismo usato da FoundationSelfTest) e verifica le regole principali su clienti,
/// categorie, marche, artigiani e listino lavorazioni.
/// </summary>
public static class RegistrySelfTest
{
    public static async Task RunAsync()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"Sciuscia-RegistrySelfTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var maintenanceGate = new SemaphoreSlim(1, 1);

        try
        {
            var paths = new AppPaths(testRoot);
            var options = new FoundationOptions(testRoot);
            var database = new DatabaseInitializer(paths, options, maintenanceGate);
            var installation = await database.EnsureCreatedAsync();

            var registry = new RegistryRuntime(paths, maintenanceGate);

            // --- Categorie ---
            var scarpe = await registry.Categories.CreateAsync("Scarpe");
            try
            {
                await registry.Categories.CreateAsync("scarpe"); // stesso nome, maiuscole diverse
                throw new InvalidDataException("Il nome categoria duplicato avrebbe dovuto essere rifiutato.");
            }
            catch (InvalidOperationException)
            {
                // atteso: nome categoria univoco (case-insensitive).
            }

            // --- Marche ---
            var brandA = await registry.Brands.CreateAsync("Esempio Uno");
            var brandB = await registry.Brands.CreateAsync("Esempio Uno Duplicato");
            var brands = await registry.Brands.ListAsync();
            if (!brands.Any(b => b.CanonicalName == "Senza marca" && b.Protected))
            {
                throw new InvalidDataException("La marca protetta «Senza marca» seminata dalle Fondazioni non è stata trovata.");
            }

            var protectedBrand = brands.Single(b => b.CanonicalName == "Senza marca");
            try
            {
                await registry.Brands.SetActiveAsync(protectedBrand.BrandId, false, protectedBrand.RowVersion);
                throw new InvalidDataException("La marca protetta non avrebbe dovuto poter essere disattivata.");
            }
            catch (InvalidOperationException)
            {
                // atteso.
            }

            // --- Artigiani ---
            var artisan = await registry.Artisans.CreateAsync("Calzoleria Prova", null, "0000000000", null, null);

            // --- Listino lavorazioni ---
            var workTypeFixed = await registry.WorkTypes.CreateAsync(
                "Risuolatura", "Risuolatura", "INTERNA", "FISSO", standardPriceCents: 2500, standardCostCents: 1000, defaultDurationMinutes: 45);
            var workTypeBands = await registry.WorkTypes.CreateAsync(
                "Tintura", "Tintura", "ESTERNA", "FASCE", standardPriceCents: null, standardCostCents: null, defaultDurationMinutes: null);
            await registry.WorkTypes.SetPriceBandAsync(workTypeBands.WorkTypeId, "A", 1500, 500);
            await registry.WorkTypes.SetPriceBandAsync(workTypeBands.WorkTypeId, "B", 2000, 700);
            await registry.WorkTypes.SetPriceBandAsync(workTypeBands.WorkTypeId, "C", 2500, 900);
            await registry.WorkTypes.SetCategoryCompatibilityAsync(scarpe.CategoryId, workTypeFixed.WorkTypeId, allowed: true, warningText: null);

            // --- Clienti ---
            var customer = await registry.Customers.CreateAsync("Rossi", "Mario", "333 1234567", "mario.rossi@example.com");
            try
            {
                await registry.Customers.CreateAsync("Bianchi", "Luca", "+39 333 1234567", null);
                throw new InvalidDataException("Il cellulare duplicato avrebbe dovuto essere rifiutato.");
            }
            catch (DuplicateMobileException)
            {
                // atteso: stesso numero normalizzato di Mario Rossi.
            }

            try
            {
                await registry.Customers.UpdateAsync(
                    customer.CustomerId, "Rossi", "Mario", "333 1234567", null, "NON_REGISTRATA",
                    expectedRowVersion: 999, cancellationToken: default);
                throw new InvalidDataException("Un row_version sbagliato avrebbe dovuto essere rifiutato.");
            }
            catch (ConcurrencyConflictException)
            {
                // atteso: controllo di concorrenza ottimistica.
            }

            var foundByName = await registry.Customers.SearchAsync("Rossi");
            var foundByMobile = await registry.Customers.SearchAsync("3331234567");
            if (foundByName.Count != 1 || foundByMobile.Count != 1)
            {
                throw new InvalidDataException("Ricerca cliente per cognome o cellulare non riuscita.");
            }

            // --- Fusione marche: un articolo intestato alla marca duplicata deve seguire la fusione ---
            var (orderId, itemId) = await CreateMinimalOrderAndItemAsync(
                paths, installation.SiteId, customer.CustomerId, scarpe.CategoryId, brandB.BrandId);

            await registry.Brands.MergeAsync(brandB.BrandId, brandA.BrandId, operatorId: null);

            await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath))
            {
                await using var check = connection.CreateCommand();
                check.CommandText = "SELECT brand_id FROM items WHERE item_id = $itemId;";
                check.Parameters.AddWithValue("$itemId", itemId);
                var actualBrandId = Convert.ToString(await check.ExecuteScalarAsync());
                if (actualBrandId != brandA.BrandId)
                {
                    throw new InvalidDataException("La fusione marche non ha aggiornato l'articolo esistente.");
                }

                await using var mergedBrandCheck = connection.CreateCommand();
                mergedBrandCheck.CommandText = "SELECT active FROM brands WHERE brand_id = $brandId;";
                mergedBrandCheck.Parameters.AddWithValue("$brandId", brandB.BrandId);
                if (Convert.ToInt64(await mergedBrandCheck.ExecuteScalarAsync()) != 0)
                {
                    throw new InvalidDataException("La marca rimossa dalla fusione avrebbe dovuto risultare disattivata.");
                }
            }

            Console.WriteLine("AUTOTEST ANAGRAFICHE SUPERATO");
            Console.WriteLine($"Categorie: {(await registry.Categories.ListAsync()).Count}");
            Console.WriteLine($"Marche attive: {(await registry.Brands.ListAsync()).Count}");
            Console.WriteLine($"Artigiani: {artisan.DisplayName}");
            Console.WriteLine($"Lavorazioni: {workTypeFixed.Name}, {workTypeBands.Name} (a fasce)");
            Console.WriteLine($"Cliente: {customer.Surname} {customer.FirstName}");
            _ = orderId;
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

    /// <summary>
    /// Inserisce una commessa e un articolo minimi con SQL diretto: la numerazione e la
    /// creazione delle commesse sono responsabilità del blocco Commesse, non ancora
    /// implementato. Serve solo per avere un articolo reale su cui verificare la fusione marche.
    /// </summary>
    private static async Task<(string OrderId, string ItemId)> CreateMinimalOrderAndItemAsync(
        AppPaths paths, string siteId, string customerId, string categoryId, string brandId)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath);
        var orderId = Guid.NewGuid().ToString();
        var itemId = Guid.NewGuid().ToString();

        await using (var insertOrder = connection.CreateCommand())
        {
            insertOrder.CommandText =
                """
                INSERT INTO orders(order_id, site_id, customer_id, order_year, order_number, visible_code, received_at)
                VALUES ($orderId, $siteId, $customerId, 2026, 1, 'LAB-1', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            insertOrder.Parameters.AddWithValue("$orderId", orderId);
            insertOrder.Parameters.AddWithValue("$siteId", siteId);
            insertOrder.Parameters.AddWithValue("$customerId", customerId);
            await insertOrder.ExecuteNonQueryAsync();
        }

        await using (var insertItem = connection.CreateCommand())
        {
            insertItem.CommandText =
                """
                INSERT INTO items(item_id, order_id, item_sequence, visible_code, category_id, brand_id, description)
                VALUES ($itemId, $orderId, 1, 'LAB-1/1', $categoryId, $brandId, 'Articolo di prova per autotest');
                """;
            insertItem.Parameters.AddWithValue("$itemId", itemId);
            insertItem.Parameters.AddWithValue("$orderId", orderId);
            insertItem.Parameters.AddWithValue("$categoryId", categoryId);
            insertItem.Parameters.AddWithValue("$brandId", brandId);
            await insertItem.ExecuteNonQueryAsync();
        }

        return (orderId, itemId);
    }
}
