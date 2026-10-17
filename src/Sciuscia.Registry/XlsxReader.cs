using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Sciuscia.Registry;

/// <summary>
/// Lettore minimale di file .xlsx (formato Office Open XML), scritto senza dipendenze esterne:
/// un file .xlsx è semplicemente un archivio zip di file XML. Serve solo per leggere i fogli del
/// file di importazione del Listino (celle di testo e numeriche semplici, niente formule) — non è
/// un lettore Excel generico. Evitare una libreria NuGet esterna qui tiene il progetto senza nuove
/// dipendenze da versionare e aggiornare.
/// </summary>
public sealed class XlsxReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private readonly Dictionary<string, string> sheetNameToEntry = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> sharedStrings = [];
    private readonly ZipArchive archive;

    private XlsxReader(ZipArchive archive)
    {
        this.archive = archive;

        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is not null)
        {
            using var stream = sharedStringsEntry.Open();
            var document = XDocument.Load(stream);
            foreach (var si in document.Root!.Elements(Main + "si"))
            {
                sharedStrings.Add(string.Concat(si.Descendants(Main + "t").Select(t => t.Value)));
            }
        }

        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("File .xlsx non valido: manca xl/workbook.xml.");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");

        var relIdToTarget = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (relsEntry is not null)
        {
            using var stream = relsEntry.Open();
            var relsDocument = XDocument.Load(stream);
            foreach (var relationship in relsDocument.Root!.Elements())
            {
                var id = relationship.Attribute("Id")?.Value;
                var target = relationship.Attribute("Target")?.Value;
                if (id is not null && target is not null)
                {
                    relIdToTarget[id] = target.TrimStart('/');
                }
            }
        }

        using var workbookStream = workbookEntry.Open();
        var workbookDocument = XDocument.Load(workbookStream);
        foreach (var sheet in workbookDocument.Root!.Element(Main + "sheets")!.Elements(Main + "sheet"))
        {
            var name = sheet.Attribute("name")?.Value;
            var relId = sheet.Attribute(Rel + "id")?.Value;
            if (name is null || relId is null || !relIdToTarget.TryGetValue(relId, out var target))
            {
                continue;
            }

            sheetNameToEntry[name] = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
        }
    }

    public static XlsxReader Open(byte[] fileBytes)
    {
        var archive = new ZipArchive(new MemoryStream(fileBytes), ZipArchiveMode.Read);
        return new XlsxReader(archive);
    }

    public bool HasSheet(string sheetName) => sheetNameToEntry.ContainsKey(sheetName);

    /// <summary>
    /// Legge un foglio come elenco di righe, ciascuna un dizionario "nome colonna" → "valore testuale".
    /// La prima riga del foglio è l'intestazione (i nomi delle colonne); le righe completamente vuote
    /// vengono saltate. I numeri vengono restituiti come testo con '.' come separatore decimale.
    /// </summary>
    public IReadOnlyList<Dictionary<string, string>> ReadSheetAsRows(string sheetName)
    {
        if (!sheetNameToEntry.TryGetValue(sheetName, out var entryPath))
        {
            throw new InvalidOperationException($"Il file non contiene un foglio chiamato «{sheetName}».");
        }

        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidOperationException($"File .xlsx non valido: manca {entryPath}.");

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        var sheetData = document.Root!.Element(Main + "sheetData");
        if (sheetData is null)
        {
            return [];
        }

        var righeGrezze = new List<Dictionary<int, string>>();
        foreach (var row in sheetData.Elements(Main + "row"))
        {
            var celle = new Dictionary<int, string>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                var riferimento = cell.Attribute("r")?.Value;
                if (riferimento is null)
                {
                    continue;
                }

                var colonna = ColonnaDaRiferimento(riferimento);
                celle[colonna] = LeggiValoreCella(cell);
            }

            righeGrezze.Add(celle);
        }

        if (righeGrezze.Count == 0)
        {
            return [];
        }

        var intestazione = righeGrezze[0];
        var risultato = new List<Dictionary<string, string>>();
        for (var i = 1; i < righeGrezze.Count; i++)
        {
            var grezza = righeGrezze[i];
            if (grezza.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var mappata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (colonna, nomeColonna) in intestazione)
            {
                mappata[nomeColonna] = grezza.TryGetValue(colonna, out var valore) ? valore : "";
            }

            risultato.Add(mappata);
        }

        return risultato;
    }

    private string LeggiValoreCella(XElement cell)
    {
        var tipo = cell.Attribute("t")?.Value;
        if (tipo == "inlineStr")
        {
            var inline = cell.Element(Main + "is");
            return inline is null ? "" : string.Concat(inline.Descendants(Main + "t").Select(t => t.Value));
        }

        var valueElement = cell.Element(Main + "v");
        if (valueElement is null)
        {
            return "";
        }

        if (tipo == "s")
        {
            return int.TryParse(valueElement.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var indice)
                && indice >= 0 && indice < sharedStrings.Count
                ? sharedStrings[indice]
                : "";
        }

        // Numero, booleano ("b") o stringa di formula ("str"): il testo grezzo va bene in ogni caso.
        return valueElement.Value;
    }

    /// <summary>Estrae l'indice di colonna (0-based) dal riferimento di cella (es. "C5" → 2).</summary>
    private static int ColonnaDaRiferimento(string riferimentoCella)
    {
        var indice = 0;
        foreach (var carattere in riferimentoCella)
        {
            if (!char.IsLetter(carattere))
            {
                break;
            }

            indice = indice * 26 + (char.ToUpperInvariant(carattere) - 'A' + 1);
        }

        return indice - 1;
    }
}
