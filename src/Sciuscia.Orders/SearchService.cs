using Sciuscia.Foundations;

namespace Sciuscia.Orders;

/// <summary>Una commessa trovata dalla Ricerca (aperta, chiusa o annullata), con cliente,
/// totali e un riassunto degli articoli già pronti per la pagina.</summary>
public sealed record SearchOrderRow(
    string OrderId,
    string OrderVisibleCode,
    string Status,
    string ReceivedAt,
    string CustomerName,
    string? CustomerMobile,
    int ItemCount,
    int TotalCents,
    int BalanceCents,
    string? ItemsSummary);

/// <summary>Un articolo trovato dalla Ricerca per tipo/periodo, con la commessa e il cliente a
/// cui appartiene.</summary>
public sealed record SearchItemRow(
    string ItemId,
    string ItemVisibleCode,
    string OrderId,
    string OrderVisibleCode,
    string OrderStatus,
    string ReceivedAt,
    string CustomerName,
    string? CustomerMobile,
    string CategoryName,
    string BrandName,
    string Description,
    string WorkflowStatus);

/// <summary>
/// Ricerca in tutto l'archivio (anche nelle commesse già chiuse, che non compaiono più nell'elenco
/// delle commesse aperte): per cliente, numero di telefono, codice commessa/articolo, oppure per
/// tipo di articolo in un periodo. Sola lettura, come AgendaService e HomeService.
/// </summary>
public sealed class SearchService(AppPaths paths)
{
    /// <summary>
    /// Commesse il cui cliente (cognome/nome o telefono) o il cui codice (commessa o articolo)
    /// contiene il testo cercato. Le più recenti per prime.
    /// </summary>
    public async Task<IReadOnlyList<SearchOrderRow>> SearchOrdersAsync(string text, CancellationToken cancellationToken = default)
    {
        var query = text.Trim();
        if (query.Length < 2)
        {
            return [];
        }

        // Solo le cifre del testo cercato, senza prefisso internazionale, per confrontarle con il
        // cellulare normalizzato del cliente; meno di 4 cifre non si considerano un telefono.
        var digits = new string(query.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0039", StringComparison.Ordinal))
        {
            digits = digits[4..];
        }
        else if (digits.StartsWith("39", StringComparison.Ordinal) && digits.Length > 10)
        {
            digits = digits[2..];
        }

        if (digits.Length < 4)
        {
            digits = string.Empty;
        }

        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT o.order_id, o.visible_code, o.status, o.received_at,
                   trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name,
                   c.mobile_original,
                   (SELECT COUNT(*) FROM items i WHERE i.order_id = o.order_id AND i.workflow_status <> 'ANNULLATO'),
                   COALESCE(f.total_cents, 0), COALESCE(f.balance_cents, 0),
                   (SELECT group_concat(cat.name || ' ' || i2.description, '; ')
                      FROM items i2
                      JOIN categories cat ON cat.category_id = i2.category_id
                     WHERE i2.order_id = o.order_id AND i2.workflow_status <> 'ANNULLATO')
            FROM orders o
            JOIN customers c ON c.customer_id = o.customer_id
            LEFT JOIN v_order_financials f ON f.order_id = o.order_id
            WHERE (c.surname || ' ' || COALESCE(c.first_name, '')) LIKE '%' || $query || '%'
               OR (COALESCE(c.first_name, '') || ' ' || c.surname) LIKE '%' || $query || '%'
               OR o.visible_code LIKE '%' || $query || '%'
               OR EXISTS (SELECT 1 FROM items i3 WHERE i3.order_id = o.order_id AND i3.visible_code LIKE '%' || $query || '%')
               OR ($digits <> '' AND c.mobile_normalized LIKE '%' || $digits || '%')
            ORDER BY o.received_at DESC
            LIMIT 200;
            """;
        command.Parameters.AddWithValue("$query", query);
        command.Parameters.AddWithValue("$digits", digits);

        var results = new List<SearchOrderRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SearchOrderRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt32(6), reader.GetInt32(7), reader.GetInt32(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return results;
    }

    /// <summary>
    /// Articoli per categoria, marca, testo (nella descrizione o nel nome di una lavorazione) e
    /// periodo di ricevimento della commessa: ogni filtro è facoltativo. I più recenti per primi.
    /// </summary>
    public async Task<IReadOnlyList<SearchItemRow>> SearchItemsAsync(
        string? categoryId,
        string? brandId,
        string? text,
        string? fromDate,
        string? toDate,
        CancellationToken cancellationToken = default)
    {
        var testo = string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT i.item_id, i.visible_code, o.order_id, o.visible_code, o.status, o.received_at,
                   trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name,
                   c.mobile_original, cat.name, br.canonical_name, i.description, i.workflow_status
            FROM items i
            JOIN orders o ON o.order_id = i.order_id
            JOIN customers c ON c.customer_id = o.customer_id
            JOIN categories cat ON cat.category_id = i.category_id
            JOIN brands br ON br.brand_id = i.brand_id
            WHERE ($categoryId IS NULL OR i.category_id = $categoryId)
              AND ($brandId IS NULL OR i.brand_id = $brandId)
              AND ($fromDate IS NULL OR substr(o.received_at, 1, 10) >= $fromDate)
              AND ($toDate IS NULL OR substr(o.received_at, 1, 10) <= $toDate)
              AND ($text IS NULL
                   OR i.description LIKE '%' || $text || '%'
                   OR EXISTS (SELECT 1 FROM item_works iw
                               WHERE iw.item_id = i.item_id AND iw.canceled_at IS NULL
                                 AND iw.applied_name LIKE '%' || $text || '%'))
            ORDER BY o.received_at DESC, i.item_sequence
            LIMIT 300;
            """;
        command.Parameters.AddWithValue("$categoryId", (object?)categoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$brandId", (object?)brandId ?? DBNull.Value);
        command.Parameters.AddWithValue("$fromDate", (object?)fromDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$toDate", (object?)toDate ?? DBNull.Value);
        command.Parameters.AddWithValue("$text", (object?)testo ?? DBNull.Value);

        var results = new List<SearchItemRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SearchItemRow(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8), reader.GetString(9), reader.GetString(10), reader.GetString(11)));
        }

        return results;
    }
}
