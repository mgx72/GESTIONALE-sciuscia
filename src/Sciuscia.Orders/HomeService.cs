using Sciuscia.Foundations;

namespace Sciuscia.Orders;

public sealed record HomeLavoroRow(
    string OrderId, string OrderVisibleCode, string ItemId, string ItemVisibleCode,
    string CustomerName, string CategoryName, bool IsUrgent, string? DueDate);

public sealed record HomeCommessaSenzaFotoRow(
    string OrderId, string OrderVisibleCode, string CustomerName, string ReceivedAt);

public sealed record HomeProntoRow(
    string OrderId, string OrderVisibleCode, string ItemId, string ItemVisibleCode,
    string CustomerName, string CategoryName);

public sealed record HomePreventivoRow(
    string OrderId, string OrderVisibleCode, string ItemId, string CustomerName,
    string CategoryName, string LavorazioneNome, int GiorniAttesa);

public sealed record HomeRitiroRow(
    string OrderId, string OrderVisibleCode, string ItemId, string CustomerName,
    string CategoryName, int GiorniAttesa);

public sealed record HomeRiepilogo(
    IReadOnlyList<HomeLavoroRow> LavoriUrgentiInRitardo,
    IReadOnlyList<HomeCommessaSenzaFotoRow> CommesseSenzaFoto,
    IReadOnlyList<HomeProntoRow> ProntiDaAvvisare,
    IReadOnlyList<HomePreventivoRow> PreventiviScaduti,
    IReadOnlyList<HomeRitiroRow> RitiriDaSollecitare);

/// <summary>
/// Riepilogo operativo per la schermata Home (specifica, "riquadri operativi"): cinque liste che
/// segnalano cosa richiede attenzione oggi, tutte derivate da dati già presenti nello schema (nessuna
/// nuova tabella). Sola lettura, nessun semaforo di manutenzione necessario (come AgendaService).
/// </summary>
public sealed class HomeService(AppPaths paths)
{
    public async Task<HomeRiepilogo> GetRiepilogoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await DatabaseInitializer.OpenReadyConnectionAsync(paths.DatabasePath, cancellationToken: cancellationToken);

        var lavori = new List<HomeLavoroRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT o.order_id, o.visible_code, i.item_id, i.visible_code,
                       trim(c.surname || ' ' || COALESCE(c.first_name, '')) AS customer_name, cat.name,
                       i.is_urgent, i.due_date
                FROM items i
                JOIN orders o ON o.order_id = i.order_id
                JOIN customers c ON c.customer_id = o.customer_id
                JOIN categories cat ON cat.category_id = i.category_id
                WHERE o.status = 'APERTA'
                  AND i.workflow_status IN ('DA_LAVORARE','PRESSO_ARTIGIANO')
                  AND (i.is_urgent = 1 OR (i.due_date IS NOT NULL AND i.due_date < date('now')))
                ORDER BY (i.due_date IS NULL), i.due_date, i.is_urgent DESC;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                lavori.Add(new HomeLavoroRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetInt64(6) == 1,
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }

        var senzaFoto = new List<HomeCommessaSenzaFotoRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT o.order_id, o.visible_code, trim(c.surname || ' ' || COALESCE(c.first_name, '')), o.received_at
                FROM orders o
                JOIN customers c ON c.customer_id = o.customer_id
                WHERE o.status = 'APERTA'
                  AND NOT EXISTS (
                    SELECT 1 FROM items i
                    JOIN photos ph ON ph.item_id = i.item_id AND ph.deleted_at IS NULL
                    WHERE i.order_id = o.order_id AND i.workflow_status <> 'ANNULLATO'
                  )
                ORDER BY o.received_at;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                senzaFoto.Add(new HomeCommessaSenzaFotoRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
        }

        var pronti = new List<HomeProntoRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT o.order_id, o.visible_code, i.item_id, i.visible_code,
                       trim(c.surname || ' ' || COALESCE(c.first_name, '')), cat.name
                FROM items i
                JOIN orders o ON o.order_id = i.order_id
                JOIN customers c ON c.customer_id = o.customer_id
                JOIN categories cat ON cat.category_id = i.category_id
                WHERE o.status = 'APERTA' AND i.workflow_status = 'PRONTO_DA_AVVISARE'
                ORDER BY i.updated_at;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pronti.Add(new HomeProntoRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5)));
            }
        }

        var preventivi = new List<HomePreventivoRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT o.order_id, o.visible_code, i.item_id, trim(c.surname || ' ' || COALESCE(c.first_name, '')), cat.name,
                       iw.applied_name, CAST(julianday('now') - julianday(iw.updated_at) AS INTEGER) AS giorni
                FROM item_works iw
                JOIN items i ON i.item_id = iw.item_id
                JOIN orders o ON o.order_id = i.order_id
                JOIN customers c ON c.customer_id = o.customer_id
                JOIN categories cat ON cat.category_id = i.category_id
                WHERE iw.approval_status = 'PENDENTE' AND iw.canceled_at IS NULL AND o.status = 'APERTA'
                  AND julianday('now') - julianday(iw.updated_at) >= 7
                ORDER BY giorni DESC;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                preventivi.Add(new HomePreventivoRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetInt32(6)));
            }
        }

        var ritiri = new List<HomeRitiroRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT o.order_id, o.visible_code, i.item_id, trim(c.surname || ' ' || COALESCE(c.first_name, '')), cat.name,
                       CAST(julianday('now') - julianday(i.updated_at) AS INTEGER) AS giorni
                FROM items i
                JOIN orders o ON o.order_id = i.order_id
                JOIN customers c ON c.customer_id = o.customer_id
                JOIN categories cat ON cat.category_id = i.category_id
                WHERE i.workflow_status = 'AVVISATO'
                  AND julianday('now') - julianday(i.updated_at) >= 30
                ORDER BY giorni DESC;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                ritiri.Add(new HomeRitiroRow(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetInt32(5)));
            }
        }

        return new HomeRiepilogo(lavori, senzaFoto, pronti, preventivi, ritiri);
    }
}
