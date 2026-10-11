using Microsoft.Data.Sqlite;
using Sciuscia.Foundations;

namespace Sciuscia.Operations;

/// <summary>
/// Autotest del blocco Operazioni: crea un database Fondazioni isolato e temporaneo e verifica
/// pagamenti (con correzione, non modifica/eliminazione), ritiri (un solo ritiro per articolo,
/// passaggio automatico a "Consegnato"), messaggi (collegati agli articoli, non modificabili) e
/// stampe (aggiornamento di last_printed_label_revision dopo una stampa etichetta riuscita).
/// </summary>
public static class OperationsSelfTest
{
    public static async Task RunAsync()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"Sciuscia-OperationsSelfTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
        var maintenanceGate = new SemaphoreSlim(1, 1);

        try
        {
            var paths = new AppPaths(testRoot);
            var options = new FoundationOptions(testRoot);
            var database = new DatabaseInitializer(paths, options, maintenanceGate);
            await database.EnsureCreatedAsync();
            var operations = new OperationsRuntime(paths, maintenanceGate);

            var (orderId, item1Id, item2Id) = await SeedMinimalOrderAsync(paths);

            // --- Pagamenti: parziale, poi saldo, poi correzione (regola: mai riscrivere, solo compensare) ---
            await operations.Payments.AddAsync(orderId, 1200, "CONTANTI");
            var full = await operations.Payments.AddAsync(orderId, 800, "CARTA");

            var (totalCents, paidCents, balanceCents) = await ReadFinancialsAsync(paths, orderId);
            if (totalCents != 2000 || paidCents != 2000 || balanceCents != 0)
            {
                throw new InvalidDataException($"Totali commessa inattesi dopo i pagamenti: totale={totalCents}, pagato={paidCents}, saldo={balanceCents}.");
            }

            try
            {
                await operations.Payments.AddCorrectionAsync(orderId, full.PaymentEventId, -800, string.Empty);
                throw new InvalidDataException("Una correzione senza motivo avrebbe dovuto essere rifiutata.");
            }
            catch (ArgumentException)
            {
                // atteso: il motivo della correzione è obbligatorio.
            }

            await operations.Payments.AddCorrectionAsync(orderId, full.PaymentEventId, -800, "Pagamento con carta rifiutato dal POS, da rifare in contanti");
            (_, paidCents, balanceCents) = await ReadFinancialsAsync(paths, orderId);
            if (paidCents != 1200 || balanceCents != 800)
            {
                throw new InvalidDataException("La correzione del pagamento non ha aggiornato correttamente il saldo.");
            }

            await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath))
            {
                await AssertImmutableAsync(connection, "payment_events", "amount_cents = 1", $"payment_event_id = '{full.PaymentEventId}'", "pagamento");
            }

            // --- Ritiri: un articolo non può essere ritirato due volte, e viene segnato Consegnato ---
            await operations.Pickups.CreateAsync(orderId, [item1Id]);
            var item1Status = await ReadItemStatusAsync(paths, item1Id);
            if (item1Status != "CONSEGNATO")
            {
                throw new InvalidDataException($"L'articolo ritirato avrebbe dovuto risultare Consegnato, risulta invece {item1Status}.");
            }

            try
            {
                await operations.Pickups.CreateAsync(orderId, [item1Id]);
                throw new InvalidDataException("Un secondo ritiro dello stesso articolo avrebbe dovuto essere rifiutato.");
            }
            catch (InvalidOperationException)
            {
                // atteso: pickup_items ha un vincolo di unicità sull'articolo.
            }

            await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath))
            {
                await AssertImmutableAsync(connection, "pickups", "notes = 'x'", $"order_id = '{orderId}'", "ritiro");
            }

            // --- Messaggi: collegati agli articoli, non modificabili ---
            var message = await operations.Messages.SendAsync(
                orderId, "WHATSAPP", "AVVISO_PRONTO", "Gentile cliente, il suo articolo è pronto per il ritiro.", [item1Id]);
            var messages = await operations.Messages.ListByOrderAsync(orderId);
            if (messages.Count != 1 || messages[0].ItemIds.Count != 1 || messages[0].ItemIds[0] != item1Id)
            {
                throw new InvalidDataException("Il messaggio inviato non risulta collegato correttamente all'articolo.");
            }

            await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath))
            {
                await AssertImmutableAsync(connection, "messages", "final_text = 'x'", $"message_id = '{message.MessageId}'", "messaggio");
            }

            // --- Stampe: una stampa etichetta riuscita allinea last_printed_label_revision ---
            var beforePrint = await operations.Prints.ListItemsToReprintLabelAsync();
            if (!beforePrint.Any(i => i.ItemId == item2Id))
            {
                throw new InvalidDataException("L'articolo mai stampato avrebbe dovuto comparire tra quelli da (ri)stampare.");
            }

            await operations.Prints.RecordAsync(orderId, item2Id, "ETICHETTA", "v1", "RIUSCITA");
            var afterPrint = await operations.Prints.ListItemsToReprintLabelAsync();
            if (afterPrint.Any(i => i.ItemId == item2Id))
            {
                throw new InvalidDataException("Dopo una stampa etichetta riuscita, l'articolo non dovrebbe più risultare da ristampare.");
            }

            try
            {
                await operations.Prints.RecordAsync(null, item2Id, "ETICHETTA", "v1", "VALORE_NON_VALIDO");
                throw new InvalidDataException("Un esito di stampa non ammesso avrebbe dovuto essere rifiutato.");
            }
            catch (InvalidOperationException)
            {
                // atteso: il database ammette solo RIUSCITA/ERRORE/ANNULLATA come esito.
            }

            await using (var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath))
            {
                await AssertImmutableAsync(connection, "print_events", "outcome = 'ERRORE'", $"item_id = '{item2Id}'", "evento di stampa");
                await DatabaseInitializer.VerifyIntegrityAsync(connection);
            }

            Console.WriteLine("AUTOTEST OPERAZIONI SUPERATO");
            Console.WriteLine($"Commessa: {orderId} — pagato {paidCents} centesimi, saldo {balanceCents} centesimi");
            Console.WriteLine($"Articolo ritirato: {item1Id}; articolo con etichetta ristampata: {item2Id}");
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

    /// <summary>Verifica che una riga storica non si possa né modificare né eliminare (eventi immutabili).</summary>
    private static async Task AssertImmutableAsync(SqliteConnection connection, string table, string setClause, string whereClause, string description)
    {
        await using (var update = connection.CreateCommand())
        {
            update.CommandText = $"UPDATE {table} SET {setClause} WHERE {whereClause};";
            try
            {
                await update.ExecuteNonQueryAsync();
                throw new InvalidDataException($"La modifica di un {description} avrebbe dovuto essere rifiutata dal database.");
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                // atteso.
            }
        }

        await using var delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM {table} WHERE {whereClause};";
        try
        {
            await delete.ExecuteNonQueryAsync();
            throw new InvalidDataException($"L'eliminazione di un {description} avrebbe dovuto essere rifiutata dal database.");
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            // atteso.
        }
    }

    private static async Task<(int TotalCents, int PaidCents, int BalanceCents)> ReadFinancialsAsync(AppPaths paths, string orderId)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT total_cents, paid_cents, balance_cents FROM v_order_financials WHERE order_id = $id;";
        command.Parameters.AddWithValue("$id", orderId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            throw new InvalidDataException("Commessa di prova non trovata nella vista dei totali.");
        }

        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static async Task<string> ReadItemStatusAsync(AppPaths paths, string itemId)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT workflow_status FROM items WHERE item_id = $id;";
        command.Parameters.AddWithValue("$id", itemId);
        return Convert.ToString(await command.ExecuteScalarAsync())
            ?? throw new InvalidDataException("Articolo di prova non trovato.");
    }

    /// <summary>
    /// Inserisce con SQL diretto una commessa minima con due articoli (il blocco Operazioni non
    /// referenzia Anagrafiche né Commesse, per non creare un ciclo tra i progetti): il primo
    /// articolo ha una lavorazione interna approvata a prezzo definito (per verificare i totali
    /// dei pagamenti), il secondo resta senza lavorazioni (per verificare solo le stampe).
    /// </summary>
    private static async Task<(string OrderId, string Item1Id, string Item2Id)> SeedMinimalOrderAsync(AppPaths paths)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath);

        var customerId = Guid.NewGuid().ToString();
        var categoryId = Guid.NewGuid().ToString();
        var orderId = Guid.NewGuid().ToString();
        var item1Id = Guid.NewGuid().ToString();
        var item2Id = Guid.NewGuid().ToString();
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

        await using (var readBrand = connection.CreateCommand())
        {
            readBrand.CommandText = "SELECT brand_id FROM brands WHERE canonical_name = 'Senza marca';";
            brandId = Convert.ToString(await readBrand.ExecuteScalarAsync())
                ?? throw new InvalidDataException("Marca «Senza marca» seminata dalle Fondazioni non trovata.");
        }

        await using (var insertOrder = connection.CreateCommand())
        {
            insertOrder.CommandText =
                """
                INSERT INTO orders(order_id, site_id, customer_id, order_year, order_number, visible_code, received_at)
                VALUES ($orderId, (SELECT site_id FROM sites LIMIT 1), $customerId, 2026, 1, 'LAB-1', strftime('%Y-%m-%dT%H:%M:%fZ','now'));
                """;
            insertOrder.Parameters.AddWithValue("$orderId", orderId);
            insertOrder.Parameters.AddWithValue("$customerId", customerId);
            await insertOrder.ExecuteNonQueryAsync();
        }

        await using (var insertItem1 = connection.CreateCommand())
        {
            insertItem1.CommandText =
                """
                INSERT INTO items(item_id, order_id, item_sequence, visible_code, category_id, brand_id, description)
                VALUES ($itemId, $orderId, 1, 'LAB-1/1', $categoryId, $brandId, 'Articolo di prova 1');
                """;
            insertItem1.Parameters.AddWithValue("$itemId", item1Id);
            insertItem1.Parameters.AddWithValue("$orderId", orderId);
            insertItem1.Parameters.AddWithValue("$categoryId", categoryId);
            insertItem1.Parameters.AddWithValue("$brandId", brandId);
            await insertItem1.ExecuteNonQueryAsync();
        }

        await using (var insertItem2 = connection.CreateCommand())
        {
            insertItem2.CommandText =
                """
                INSERT INTO items(item_id, order_id, item_sequence, visible_code, category_id, brand_id, description)
                VALUES ($itemId, $orderId, 2, 'LAB-1/2', $categoryId, $brandId, 'Articolo di prova 2');
                """;
            insertItem2.Parameters.AddWithValue("$itemId", item2Id);
            insertItem2.Parameters.AddWithValue("$orderId", orderId);
            insertItem2.Parameters.AddWithValue("$categoryId", categoryId);
            insertItem2.Parameters.AddWithValue("$brandId", brandId);
            await insertItem2.ExecuteNonQueryAsync();
        }

        await using (var insertWork = connection.CreateCommand())
        {
            insertWork.CommandText =
                """
                INSERT INTO item_works(item_work_id, item_id, applied_name, applied_label_text, price_cents)
                VALUES ($id, $itemId, 'Risuolatura', 'Risuolatura', 2000);
                """;
            insertWork.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            insertWork.Parameters.AddWithValue("$itemId", item1Id);
            await insertWork.ExecuteNonQueryAsync();
        }

        return (orderId, item1Id, item2Id);
    }
}
