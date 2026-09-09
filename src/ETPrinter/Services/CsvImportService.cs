using System.IO;
using System.Text;
using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// CSV-Import fuer ET200SP-Etiketten (Kopfzeile / Zeile 1 / Zeile 2).
/// Zeichenweiser Parser ueber den gesamten Text (RFC-4180-artig): Zeilenumbrueche in
/// Anfuehrungszeichen bleiben Teil des Feldes, "" ist ein escaptes Anfuehrungszeichen.
/// Trenner: ; , oder Tab (automatisch), Encoding: BOM (UTF-8/UTF-16), sonst strikt
/// UTF-8 mit Fallback auf Windows-1252.
/// </summary>
public static class CsvImportService
{
    private static readonly string[] HeaderNames = ["header", "kopfzeile", "kopf"];
    private static readonly string[] Line1Names = ["zeile1", "line1", "zeile 1", "line 1", "adresse", "adressen"];
    private static readonly string[] Line2Names = ["zeile2", "line2", "zeile 2", "line 2"];

    public static List<LabelCell> Import(string filePath)
    {
        var text = DecodeText(File.ReadAllBytes(filePath));
        return Parse(text);
    }

    /// <summary>Parst CSV-Text (testbar ohne Datei).</summary>
    public static List<LabelCell> Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        char separator = DetectSeparator(text);
        var records = ParseRecords(text, separator);
        if (records.Count == 0)
            return [];

        var columnMap = DetectColumns(records[0]);
        int dataStart = columnMap.HasHeaders ? 1 : 0;
        var cells = new List<LabelCell>();

        for (int i = dataStart; i < records.Count; i++)
        {
            var fields = records[i];
            if (fields.All(string.IsNullOrWhiteSpace))
                continue;

            cells.Add(new LabelCell
            {
                Index = cells.Count,
                Header = GetField(fields, columnMap.HeaderIndex),
                Line1 = GetField(fields, columnMap.Line1Index),
                Line2 = GetField(fields, columnMap.Line2Index)
            });
        }

        return cells;
    }

    /// <summary>
    /// Bytes -> Text. BOM entscheidet (UTF-8, UTF-16 LE/BE — Excel "Unicode Text"
    /// schreibt UTF-16 LE; NUL-Bytes sind gueltiges UTF-8 und wurden frueher als
    /// "K\0o\0p\0f" importiert). Ohne BOM: strikt UTF-8, bei ungueltigen Bytes
    /// Windows-1252 (ANSI mit Umlauten).
    /// </summary>
    public static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        // UTF-16 LE ohne BOM (jedes zweite Byte 0) heuristisch erkennen
        if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0 && bytes[0] != 0 && bytes[2] != 0)
            return Encoding.Unicode.GetString(bytes);

        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    /// <summary>Trenner aus der ersten Zeile: Zeichen AUSSERHALB von Anfuehrungszeichen
    /// zaehlen — sonst kippen Kommas in quoted Feldern ("Stoerung, Luefter") die Erkennung.</summary>
    public static char DetectSeparator(string text)
    {
        int semicolons = 0, commas = 0, tabs = 0;
        bool inQuotes = false;
        foreach (char c in text)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && (c == '\n' || c == '\r')) break;
            else if (!inQuotes && c == ';') semicolons++;
            else if (!inQuotes && c == ',') commas++;
            else if (!inQuotes && c == '\t') tabs++;
        }
        if (tabs > semicolons && tabs > commas) return '\t';
        return semicolons >= commas ? ';' : ',';
    }

    /// <summary>Zeichenweiser Parser: Datensaetze koennen ueber Zeilenumbrueche in
    /// Anfuehrungszeichen hinweggehen. Leere Zeilen liefern keinen Datensatz.</summary>
    public static List<string[]> ParseRecords(string text, char separator)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        bool fieldStarted = false; // unterscheidet leere Zeile von leerem Feld

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { current.Append('"'); i++; }
                    else inQuotes = false;
                }
                else current.Append(c);
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
                fieldStarted = true;
            }
            else if (c == separator)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
                fieldStarted = true;
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                if (fieldStarted || current.Length > 0)
                {
                    fields.Add(current.ToString().Trim());
                    records.Add([.. fields]);
                }
                fields.Clear();
                current.Clear();
                fieldStarted = false;
            }
            else
            {
                current.Append(c);
                fieldStarted = true;
            }
        }

        if (fieldStarted || current.Length > 0 || fields.Count > 0)
        {
            fields.Add(current.ToString().Trim());
            records.Add([.. fields]);
        }

        return records;
    }

    private static ColumnMap DetectColumns(string[] headerRow)
    {
        int headerIndex = -1, line1Index = -1, line2Index = -1;

        for (int i = 0; i < headerRow.Length; i++)
        {
            var lower = headerRow[i].Trim().ToLowerInvariant();
            if (HeaderNames.Contains(lower)) headerIndex = i;
            else if (Line1Names.Contains(lower)) line1Index = i;
            else if (Line2Names.Contains(lower)) line2Index = i;
        }

        bool hasHeaders = headerIndex >= 0 || line1Index >= 0 || line2Index >= 0;
        if (!hasHeaders)
            return new ColumnMap(false, 0, 1, 2);

        return new ColumnMap(true, headerIndex, line1Index, line2Index);
    }

    private static string GetField(string[] fields, int index) =>
        index < 0 || index >= fields.Length ? string.Empty : fields[index];

    private record ColumnMap(bool HasHeaders, int HeaderIndex, int Line1Index, int Line2Index);
}
