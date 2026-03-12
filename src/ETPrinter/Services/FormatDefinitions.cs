using ETPrinter.Models;

namespace ETPrinter.Services;

public static class FormatDefinitions
{
    // A4 Seitengroesse in mm
    public const double PageWidth = 210.0;
    public const double PageHeight = 297.0;

    private static readonly Dictionary<LabelFormat, FormatInfo> _formats = new()
    {
        [LabelFormat.HorizontalDoubleHeader] = new FormatInfo(
            LabelFormat.HorizontalDoubleHeader,
            "Horizontal zweizeilig + Kopfzeile",
            Columns: 10, RowsPerLabel: 2, HasHeader: true, IsVertical: false,
            LabelsPerRow: 5, LabelRows: 20),

        [LabelFormat.HorizontalDouble] = new FormatInfo(
            LabelFormat.HorizontalDouble,
            "Horizontal zweizeilig",
            Columns: 5, RowsPerLabel: 2, HasHeader: false, IsVertical: false,
            LabelsPerRow: 5, LabelRows: 20),

        [LabelFormat.HorizontalSingle] = new FormatInfo(
            LabelFormat.HorizontalSingle,
            "Horizontal einzeilig",
            Columns: 7, RowsPerLabel: 1, HasHeader: false, IsVertical: false,
            LabelsPerRow: 7, LabelRows: 20),

        [LabelFormat.VerticalDoubleHeader] = new FormatInfo(
            LabelFormat.VerticalDoubleHeader,
            "Vertikal zweizeilig + Kopfzeile",
            Columns: 10, RowsPerLabel: 2, HasHeader: true, IsVertical: true,
            LabelsPerRow: 5, LabelRows: 20),

        [LabelFormat.VerticalDouble] = new FormatInfo(
            LabelFormat.VerticalDouble,
            "Vertikal zweizeilig",
            Columns: 5, RowsPerLabel: 2, HasHeader: false, IsVertical: true,
            LabelsPerRow: 5, LabelRows: 20),

        [LabelFormat.VerticalSingle] = new FormatInfo(
            LabelFormat.VerticalSingle,
            "Vertikal einzeilig",
            Columns: 7, RowsPerLabel: 1, HasHeader: false, IsVertical: true,
            LabelsPerRow: 7, LabelRows: 20),
    };

    public static FormatInfo Get(LabelFormat format) => _formats[format];

    public static IReadOnlyList<FormatInfo> All => _formats.Values.ToList();

    /// <summary>
    /// Berechnet die Zellengroessen (mm) basierend auf Druckbereich und Format.
    /// </summary>
    public static (double cellWidth, double cellHeight, double headerWidth) GetCellSize(
        FormatInfo format, LabelSettings settings)
    {
        double printWidth = PageWidth - settings.MarginLeft - settings.MarginRight;
        double printHeight = PageHeight - settings.MarginTop - settings.MarginBottom;

        double cellWidth, cellHeight, headerWidth = 0;

        if (format.HasHeader)
        {
            // Header-Spalte ~20% der Gruppenbreite
            double groupWidth = printWidth / format.LabelsPerRow;
            headerWidth = groupWidth * 0.2;
            cellWidth = groupWidth - headerWidth;
        }
        else
        {
            cellWidth = printWidth / format.LabelsPerRow;
        }

        cellHeight = printHeight / format.LabelRows;

        return (cellWidth, cellHeight, headerWidth);
    }
}
