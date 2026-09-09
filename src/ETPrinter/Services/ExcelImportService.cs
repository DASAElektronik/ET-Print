using ClosedXML.Excel;
using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// Excel-Import (.xlsx) fuer ET200SP-Etiketten. Nimmt das erste SICHTBARE Blatt mit
/// Inhalt (ein verstecktes Deckblatt lieferte frueher "keine Daten"). Zahlen- und
/// Datumszellen werden mit ihrem Excel-Anzeigeformat gelesen, damit "1.1" nicht
/// als "01.01." oder 1,5 als "1.5" ankommt.
/// </summary>
public static class ExcelImportService
{
    private static readonly string[] HeaderNames = ["header", "kopfzeile", "kopf"];
    private static readonly string[] Line1Names = ["zeile1", "line1", "zeile 1", "line 1", "adresse", "adressen"];
    private static readonly string[] Line2Names = ["zeile2", "line2", "zeile 2", "line 2"];

    public static List<LabelCell> Import(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = SelectWorksheet(workbook);
        if (worksheet is null)
            return [];
        return ImportSheet(worksheet);
    }

    /// <summary>Erstes sichtbares Blatt mit Inhalt; sonst erstes Blatt mit Inhalt; sonst null.</summary>
    internal static IXLWorksheet? SelectWorksheet(XLWorkbook workbook)
    {
        return workbook.Worksheets.FirstOrDefault(ws => ws.Visibility == XLWorksheetVisibility.Visible && ws.RangeUsed() != null)
            ?? workbook.Worksheets.FirstOrDefault(ws => ws.RangeUsed() != null);
    }

    internal static List<LabelCell> ImportSheet(IXLWorksheet worksheet)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
            return [];

        int firstRow = usedRange.FirstRow().RowNumber();
        int lastRow = usedRange.LastRow().RowNumber();
        int firstCol = usedRange.FirstColumn().ColumnNumber();
        int lastCol = usedRange.LastColumn().ColumnNumber();

        var columnMap = DetectColumns(worksheet, firstRow, firstCol, lastCol);
        int dataStartRow = columnMap.HasHeaders ? firstRow + 1 : firstRow;

        var cells = new List<LabelCell>();

        for (int row = dataStartRow; row <= lastRow; row++)
        {
            var header = GetCellText(worksheet, row, columnMap.HeaderCol);
            var line1 = GetCellText(worksheet, row, columnMap.Line1Col);
            var line2 = GetCellText(worksheet, row, columnMap.Line2Col);

            if (string.IsNullOrWhiteSpace(header) && string.IsNullOrWhiteSpace(line1) && string.IsNullOrWhiteSpace(line2))
                continue;

            cells.Add(new LabelCell
            {
                Index = cells.Count,
                Header = header,
                Line1 = line1,
                Line2 = line2
            });
        }

        return cells;
    }

    private static ColumnMap DetectColumns(IXLWorksheet worksheet, int headerRow, int firstCol, int lastCol)
    {
        int headerCol = -1, line1Col = -1, line2Col = -1;

        for (int col = firstCol; col <= lastCol; col++)
        {
            var value = worksheet.Cell(headerRow, col).GetString().Trim().ToLowerInvariant();
            if (HeaderNames.Contains(value)) headerCol = col;
            else if (Line1Names.Contains(value)) line1Col = col;
            else if (Line2Names.Contains(value)) line2Col = col;
        }

        bool hasHeaders = headerCol > 0 || line1Col > 0 || line2Col > 0;

        if (!hasHeaders)
        {
            // Ohne Kopfzeile: die ersten 3 belegten Spalten ab firstCol als
            // Header/Zeile1/Zeile2 — NICHT fix A/B/C, sonst verschieben sich
            // Daten, die erst ab Spalte B beginnen, und die 3. Spalte geht verloren.
            return new ColumnMap(false, firstCol, firstCol + 1, firstCol + 2);
        }

        return new ColumnMap(true, headerCol, line1Col, line2Col);
    }

    /// <summary>Zellinhalt als Text, wie Excel ihn anzeigt (Zahlformat/Datumsformat),
    /// Formeln ueber ihren zwischengespeicherten Wert.</summary>
    internal static string GetCellText(IXLWorksheet worksheet, int row, int col)
    {
        if (col < 1)
            return string.Empty;
        var cell = worksheet.Cell(row, col);
        try
        {
            return cell.DataType is XLDataType.Number or XLDataType.DateTime or XLDataType.TimeSpan
                ? cell.GetFormattedString().Trim()
                : cell.GetString().Trim();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            Log.Warn($"Zelle {cell.Address}: {ex.Message}");
            return cell.GetString().Trim();
        }
    }

    private record ColumnMap(bool HasHeaders, int HeaderCol, int Line1Col, int Line2Col);
}
