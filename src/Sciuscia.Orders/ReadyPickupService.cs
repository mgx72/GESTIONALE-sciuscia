using Sciuscia.Foundations;

namespace Sciuscia.Orders;

/// <summary>Un articolo pronto per il cliente (specifica, sezione 6 "Pronti da ritirare"), con i
/// dati di commessa/cliente/categoria già pronti (stesso spirito di <see cref="AgendaService"/>):
/// la pagina raggruppa per commessa, decide l'evidenziazione di chi è ancora da avvisare e
/// l'ordinamento, senza bisogno di altre richieste.</summary>
public sealed record ReadyPickupRow(
    ItemRecord Item,
    string OrderId,
    string OrderVisibleCode,
    string CustomerId,
    string CustomerName,
    string? CustomerMobile,
    string CategoryName,
    string UpdatedAt);

/// <summary>
/// Blocco "Pronti da ritirare" (specifica, sezione 6): tutti gli articoli delle commesse aperte
/// che sono pronti per il cliente e non ancora consegnati, cioè ancora "Da avvisare"
/// (PRONTO_DA_AVVISARE) o già "Avvisato" (AVVISATO). Qui c'è solo la lettura dell'elenco; il
/// raggruppamento per cliente/commessa, l'evidenziazione e l'ordinamento (prima chi è ancora da
/// avvisare, poi la data di completamento) restano alla pagina, come già per Agenda lavori.
/// </summary>
public sealed class ReadyPickupService(AppPaths paths)
{
    public async Task<IReadOnlyList<ReadyPickupRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.item_id, i.order_id, i.item_sequence, i.visible_code, i.category_id, i.brand_id, i.description,
                   i.due_date, i.is_urgent, i.is_priority, i.workflow_status, i.label_revision, i.row_version,
                   o.visible_code AS order_visible_code,
                   c.customer_id, trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name,
                   c.mobile_original, cat.name AS category_name, i.updated_at
            FROM items i
            JOIN orders o ON o.order_id = i.order_id
            JOIN customers c ON c.customer_id = o.customer_id
            JOIN categories cat ON cat.category_id = i.category_id
            WHERE i.workflow_status IN ('PRONTO_DA_AVVISARE','AVVISATO') AND o.status = 'APERTA'
            ORDER BY i.updated_at;
            """;

        var results = new List<ReadyPickupRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new ItemRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt64(8) == 1, reader.GetInt64(9) == 1,
                reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12));

            results.Add(new ReadyPickupRow(
                item, reader.GetString(1), reader.GetString(13),
                reader.GetString(14), reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.GetString(17), reader.GetString(18)));
        }

        return results;
    }
}
