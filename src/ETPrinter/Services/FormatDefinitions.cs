using ETPrinter.Models;

namespace ETPrinter.Services;

public static class FormatDefinitions
{
    // A4 Seitengroesse in mm
    public const double PageWidth = 210.0;
    public const double PageHeight = 297.0;

    private static readonly Dictionary<LabelFormat, FormatInfo> _formats = new()
    {
        // === ET 200SP Formate (bestehend, unveraendert) ===

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
            Columns: 5, RowsPerLabel: 1, HasHeader: false, IsVertical: false,
            LabelsPerRow: 5, LabelRows: 20),

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
            Columns: 5, RowsPerLabel: 1, HasHeader: false, IsVertical: true,
            LabelsPerRow: 5, LabelRows: 20),

        // === S7-1500 / ET 200MP Standard (35mm Module) ===
        // 2 Baender pro Seite, 5 Module pro Band, 20 Kanalzeilen pro Band
        // Jedes Modul: 2 Adress-Spalten + Header (Netzadresse/CPU)
        // Geschaetzte Masse — werden spaeter per Stahllineal korrigiert

        [LabelFormat.MP_Horizontal] = new FormatInfo(
            LabelFormat.MP_Horizontal,
            "ET200MP Horizontal",
            Columns: 4, RowsPerLabel: 1, HasHeader: true, IsVertical: false,
            LabelsPerRow: 5, LabelRows: 20,
            Family: ProductFamily.S71500_ET200MP,
            BandsPerPage: 2, ChannelRowsPerBand: 20,
            IsModuleBased: true),

        [LabelFormat.MP_Vertical] = new FormatInfo(
            LabelFormat.MP_Vertical,
            "ET200MP Vertikal",
            Columns: 4, RowsPerLabel: 1, HasHeader: true, IsVertical: true,
            LabelsPerRow: 5, LabelRows: 20,
            Family: ProductFamily.S71500_ET200MP,
            BandsPerPage: 2, ChannelRowsPerBand: 20,
            IsModuleBased: true),

        // === ET 200MP 25mm Module ===
        // Schmalere Module, 10 Module pro Band

        [LabelFormat.MP25_Horizontal] = new FormatInfo(
            LabelFormat.MP25_Horizontal,
            "ET200MP 25mm Horizontal",
            Columns: 3, RowsPerLabel: 1, HasHeader: true, IsVertical: false,
            LabelsPerRow: 10, LabelRows: 20,
            Family: ProductFamily.S71500_ET200MP_25mm,
            BandsPerPage: 2, ChannelRowsPerBand: 20,
            IsModuleBased: true),

        [LabelFormat.MP25_Vertical] = new FormatInfo(
            LabelFormat.MP25_Vertical,
            "ET200MP 25mm Vertikal",
            Columns: 3, RowsPerLabel: 1, HasHeader: true, IsVertical: true,
            LabelsPerRow: 10, LabelRows: 20,
            Family: ProductFamily.S71500_ET200MP_25mm,
            BandsPerPage: 2, ChannelRowsPerBand: 20,
            IsModuleBased: true),
    };

    public static FormatInfo Get(LabelFormat format) => _formats[format];

    public static IReadOnlyList<FormatInfo> All => _formats.Values.ToList();

    public static IReadOnlyList<FormatInfo> GetFormatsForFamily(ProductFamily family)
        => _formats.Values.Where(f => f.Family == family).ToList();

    public static FormatInfo GetDefaultFormat(ProductFamily family) => family switch
    {
        ProductFamily.ET200SP => _formats[LabelFormat.HorizontalDouble],
        ProductFamily.S71500_ET200MP => _formats[LabelFormat.MP_Horizontal],
        ProductFamily.S71500_ET200MP_25mm => _formats[LabelFormat.MP25_Horizontal],
        _ => _formats[LabelFormat.HorizontalDouble]
    };

    /// <summary>
    /// Berechnet die Zellengroessen (mm) basierend auf Druckbereich und Format.
    /// </summary>
    public static (double cellWidth, double cellHeight, double headerWidth) GetCellSize(
        FormatInfo format, LabelSettings settings)
    {
        double printWidth = PageWidth - settings.MarginLeft - settings.MarginRight;
        double printHeight = PageHeight - settings.MarginTop - settings.MarginBottom;

        double cellWidth, cellHeight, headerWidth = 0;

        if (format.BandsPerPage > 1)
        {
            // ET200MP: 2 Baender mit Header-Zeile und Trennband
            var familyInfo = ProductFamilyDefinitions.Get(format.Family);
            double totalBandHeight = printHeight - familyInfo.EstimatedSeparatorHeight
                - (familyInfo.EstimatedHeaderHeight * format.BandsPerPage);
            double bandHeight = totalBandHeight / format.BandsPerPage;
            cellHeight = bandHeight / format.ChannelRowsPerBand;

            double groupWidth = printWidth / format.LabelsPerRow;
            // ET200MP: Header-Spalte ~17% der Modulbreite (804/4570 aus Excel)
            headerWidth = groupWidth * 0.17;
            cellWidth = groupWidth - headerWidth;
        }
        else if (format.HasHeader)
        {
            // ET200SP mit Header: ~20% der Gruppenbreite
            double groupWidth = printWidth / format.LabelsPerRow;
            headerWidth = groupWidth * 0.2;
            cellWidth = groupWidth - headerWidth;
            cellHeight = printHeight / format.LabelRows;
        }
        else
        {
            cellWidth = printWidth / format.LabelsPerRow;
            cellHeight = printHeight / format.LabelRows;
        }

        return (cellWidth, cellHeight, headerWidth);
    }
}
