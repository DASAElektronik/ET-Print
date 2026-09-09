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
        // 2 Baender pro Seite x 5 Spalten = 10 Streifen-Positionen (je 1 Modul),
        // 20 Kanalzeilen pro Band. Geometrie: SheetGeometry / ProductFamilyInfo.
        // Masse aus dem Excel-Template — Stahllineal-Verifikation offen (AP8).

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
    /// ET200SP-Zellengroessen (mm). Duenner Wrapper um <see cref="SheetGeometry"/> —
    /// der fruehere MP-Zweig hier (5,44 mm Zeilenhoehe) war toter Code mit einer
    /// Geometrie, die der MP-Druck nie erzeugt hat.
    /// </summary>
    public static (double cellWidth, double cellHeight, double headerWidth) GetCellSize(
        FormatInfo format, LabelSettings settings)
    {
        var geo = SheetGeometry.For(format, settings);
        return (geo.SpCellWidth, geo.SpCellHeight, geo.SpHeaderWidth);
    }
}
