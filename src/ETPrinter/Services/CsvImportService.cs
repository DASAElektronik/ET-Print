using System.IO;
using System.Text;
using ETPrinter.Models;

namespace ETPrinter.Services;

public static class CsvImportService
{
    private static readonly string[] HeaderNames = ["header", "kopfzeile"];
    private static readonly string[] Line1Names = ["zeile1", "line1", "zeile 1", "line 1"];
    private static readonly string[] Line2Names = ["zeile2", "line2", "zeile 2", "line 2"];

    public static List<LabelCell> Import(string filePath)
    {
        var lines = ReadFileLines(filePath);

        if (lines.Count == 0)
            return [];

        var separator = DetectSeparator(lines[0]);
        var headerRow = ParseLine(lines[0], separator);
        var columnMap = DetectColumns(headerRow);

        int dataStartRow = columnMap.HasHeaders ? 1 : 0;
        var cells = new List<LabelCell>();

        for (int i = dataStartRow; i < lines.Count; i++)
        {
            var fields = ParseLine(lines[i], separator);
            if (fields.All(string.IsNullOrWhiteSpace))
                continue;

            var cell = new LabelCell
            {
                Index = cells.Count,
                Header = GetField(fields, columnMap.HeaderIndex),
                Line1 = GetField(fields, columnMap.Line1Index),
                Line2 = GetField(fields, columnMap.Line2Index)
            };

            cells.Add(cell);
        }

        return cells;
    }

    private static List<string> ReadFileLines(string filePath)
    {
        try
        {
            var lines = File.ReadAllLines(filePath, Encoding.UTF8).ToList();
            if (lines.Count > 0 && !ContainsValidText(lines[0]))
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                lines = File.ReadAllLines(filePath, Encoding.GetEncoding(1252)).ToList();
            }
            return lines;
        }
        catch
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return File.ReadAllLines(filePath, Encoding.GetEncoding(1252)).ToList();
        }
    }

    private static bool ContainsValidText(string text)
    {
        return !text.Contains('\uFFFD');
    }

    private static char DetectSeparator(string firstLine)
    {
        int semicolons = firstLine.Count(c => c == ';');
        int commas = firstLine.Count(c => c == ',');
        return semicolons >= commas ? ';' : ',';
    }

    private static string[] ParseLine(string line, char separator)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == separator && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());
        return [.. fields];
    }

    private static ColumnMap DetectColumns(string[] headerRow)
    {
        int headerIndex = -1;
        int line1Index = -1;
        int line2Index = -1;

        for (int i = 0; i < headerRow.Length; i++)
        {
            var lower = headerRow[i].Trim().ToLowerInvariant();

            if (HeaderNames.Contains(lower))
                headerIndex = i;
            else if (Line1Names.Contains(lower))
                line1Index = i;
            else if (Line2Names.Contains(lower))
                line2Index = i;
        }

        bool hasHeaders = headerIndex >= 0 || line1Index >= 0 || line2Index >= 0;

        if (!hasHeaders)
        {
            return new ColumnMap(false, 0, 1, 2);
        }

        return new ColumnMap(true, headerIndex, line1Index >= 0 ? line1Index : -1, line2Index >= 0 ? line2Index : -1);
    }

    private static string GetField(string[] fields, int index)
    {
        if (index < 0 || index >= fields.Length)
            return string.Empty;
        return fields[index];
    }

    private record ColumnMap(bool HasHeaders, int HeaderIndex, int Line1Index, int Line2Index);
}
