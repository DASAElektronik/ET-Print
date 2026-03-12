using ClosedXML.Excel;
using ETPrinter.Models;

namespace ETPrinter.Services;

public static class ExcelImportService
{
    private static readonly string[] HeaderNames = ["header", "kopfzeile"];
    private static readonly string[] Line1Names = ["zeile1", "line1", "zeile 1", "line 1"];
    private static readonly string[] Line2Names = ["zeile2", "line2", "zeile 2", "line 2"];

    public static List<LabelCell> Import(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();

        var usedRange = worksheet.RangeUsed();
        if (usedRange == null)
            return [];

        int firstRow = usedRange.FirstRow().RowNumber();
        int lastRow = usedRange.LastRow().RowNumber();
        int lastCol = usedRange.LastColumn().ColumnNumber();

        var columnMap = DetectColumns(worksheet, firstRow, lastCol);
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

    private static ColumnMap DetectColumns(IXLWorksheet worksheet, int headerRow, int lastCol)
    {
        int headerCol = -1;
        int line1Col = -1;
        int line2Col = -1;

        for (int col = 1; col <= lastCol; col++)
        {
            var value = worksheet.Cell(headerRow, col).GetString().Trim().ToLowerInvariant();

            if (HeaderNames.Contains(value))
                headerCol = col;
            else if (Line1Names.Contains(value))
                line1Col = col;
            else if (Line2Names.Contains(value))
                line2Col = col;
        }

        bool hasHeaders = headerCol > 0 || line1Col > 0 || line2Col > 0;

        if (!hasHeaders)
        {
            return new ColumnMap(false, 1, 2, 3);
        }

        return new ColumnMap(true, headerCol, line1Col > 0 ? line1Col : -1, line2Col > 0 ? line2Col : -1);
    }

    private static string GetCellText(IXLWorksheet worksheet, int row, int col)
    {
        if (col < 1)
            return string.Empty;
        return worksheet.Cell(row, col).GetString().Trim();
    }

    private record ColumnMap(bool HasHeaders, int HeaderCol, int Line1Col, int Line2Col);
}
