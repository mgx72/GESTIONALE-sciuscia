using Sciuscia.Foundations;

namespace Sciuscia.Orders;

/// <summary>Un articolo da lavorare internamente, con i dati di commessa/cliente/categoria/marca
/// già pronti (stesso spirito di <see cref="OrderService.ListOpenAsync"/>): evita che la pagina
/// debba fare altre richieste solo per mostrare l'elenco.</summary>
public sealed record AgendaItemRow(
    ItemRecord Item,
    string OrderId,
    string OrderVisibleCode,
    string CustomerName,
    string CategoryName,
    string BrandName);

/// <summary>
/// Blocco Agenda lavori (specifica, sezione 5): organizza in un calendario settimanale gli
/// articoli con lavorazioni interne ancora da fare. Qui c'è solo la lettura dell'elenco; lo stato
/// dell'articolo (Pronto, ecc.) resta un comando di Sciuscia.Orders.ItemService, già esistente.
/// </summary>
public sealed class AgendaService(AppPaths paths)
{
    /// <summary>
    /// Tutti gli articoli ancora "Da lavorare" (lavorazione interna non completata) delle
    /// commesse aperte. Non filtra per data: la pagina decide come raggrupparli per giorno,
    /// evidenziare gli arretrati e mettere in fondo quelli senza data prevista — stesso principio
    /// di <see cref="OrderService.ListOpenAsync"/>, che restituisce tutte le commesse aperte e
    /// lascia alla pagina la suddivisione.
    /// </summary>
    public async Task<IReadOnlyList<AgendaItemRow>> ListDaLavorareAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.item_id, i.order_id, i.item_sequence, i.visible_code, i.category_id, i.brand_id, i.description,
                   i.due_date, i.is_urgent, i.is_priority, i.workflow_status, i.label_revision, i.row_version,
                   o.visible_code AS order_visible_code,
                   trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name,
                   cat.name AS category_name, br.canonical_name AS brand_name
            FROM items i
            JOIN orders o ON o.order_id = i.order_id
            JOIN customers c ON c.customer_id = o.customer_id
            JOIN categories cat ON cat.category_id = i.category_id
            JOIN brands br ON br.brand_id = i.brand_id
            WHERE i.workflow_status = 'DA_LAVORARE' AND o.status = 'APERTA'
            ORDER BY (i.due_date IS NULL), i.due_date, i.item_sequence;
            """;

        var results = new List<AgendaItemRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new ItemRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt64(8) == 1, reader.GetInt64(9) == 1,
                reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12));

            results.Add(new AgendaItemRow(
                item, reader.GetString(1), reader.GetString(13), reader.GetString(14),
                reader.GetString(15), reader.GetString(16)));
        }

        return results;
    }
}
