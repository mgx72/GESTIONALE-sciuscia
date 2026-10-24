using Sciuscia.Foundations;

namespace Sciuscia.Orders;

/// <summary>Una riga per "Presso artigiani" (specifica, sezione 7): un articolo con un affidamento
/// esterno ancora attivo, con l'eventuale lavorazione esterna affidata in quella riga (un articolo
/// può averne più di una, quindi compare più volte, una per lavorazione: la pagina le raggruppa per
/// articolo e poi per artigiano, stesso principio già usato per "Pronti da ritirare"). Un articolo
/// senza nessuna lavorazione esterna ancora attiva collegata all'affidamento (caso raro, es. tutte
/// annullate) compare comunque una volta, con <see cref="ExternalWorkName"/> nullo.</summary>
public sealed record ArtisanWorkRow(
    ItemRecord Item,
    string OrderId,
    string OrderVisibleCode,
    string CustomerName,
    string CategoryName,
    string ExternalAssignmentId,
    int AssignmentRowVersion,
    string ArtisanId,
    string ArtisanName,
    string EntrustedAt,
    string? ExpectedReturnDate,
    int? SolicitAfterDays,
    string? ExternalWorkName);

/// <summary>
/// Blocco "Presso artigiani" (specifica, sezione 7): tutti gli articoli delle commesse aperte che
/// hanno in questo momento un affidamento esterno attivo (non rientrato, non annullato). Qui c'è
/// solo la lettura dell'elenco; il raggruppamento per artigiano/articolo, l'evidenziazione dei
/// ritardi e l'ordinamento restano alla pagina, come già per Agenda lavori e "Pronti da ritirare".
/// </summary>
public sealed class ArtisanWorkService(AppPaths paths)
{
    public async Task<IReadOnlyList<ArtisanWorkRow>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.item_id, i.order_id, i.item_sequence, i.visible_code, i.category_id, i.brand_id, i.description,
                   i.due_date, i.is_urgent, i.is_priority, i.workflow_status, i.label_revision, i.row_version,
                   o.visible_code AS order_visible_code,
                   trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name,
                   cat.name AS category_name,
                   ea.external_assignment_id, ea.row_version AS assignment_row_version,
                   a.artisan_id, a.display_name AS artisan_name,
                   ea.entrusted_at, ea.expected_return_date, a.solicit_after_days,
                   iw.applied_name
            FROM items i
            JOIN orders o ON o.order_id = i.order_id
            JOIN customers c ON c.customer_id = o.customer_id
            JOIN external_assignments ea ON ea.item_id = i.item_id AND ea.returned_at IS NULL AND ea.canceled_at IS NULL
            JOIN artisans a ON a.artisan_id = ea.artisan_id
            JOIN categories cat ON cat.category_id = i.category_id
            LEFT JOIN item_works iw ON iw.external_assignment_id = ea.external_assignment_id AND iw.canceled_at IS NULL
            WHERE o.status = 'APERTA'
            ORDER BY ea.entrusted_at, iw.sort_order;
            """;

        var results = new List<ArtisanWorkRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new ItemRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetInt64(8) == 1, reader.GetInt64(9) == 1,
                reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12));

            results.Add(new ArtisanWorkRow(
                item, reader.GetString(1), reader.GetString(13), reader.GetString(14),
                reader.GetString(15),
                reader.GetString(16), reader.GetInt32(17),
                reader.GetString(18), reader.GetString(19),
                reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21),
                reader.IsDBNull(22) ? null : reader.GetInt32(22),
                reader.IsDBNull(23) ? null : reader.GetString(23)));
        }

        return results;
    }
}
