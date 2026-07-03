namespace ETPrinter.Models;

public enum ProductFamily
{
    ET200SP,
    S71500_ET200MP,
    S71500_ET200MP_25mm
}

public record ProductFamilyInfo(
    ProductFamily Family,
    string DisplayName,
    string LabelSheetPartNumber,
    double DefaultMarginTop,
    double DefaultMarginLeft,
    double DefaultMarginBottom,
    double DefaultMarginRight,
    double EstimatedModuleWidth,
    double EstimatedChannelRowHeight,
    double EstimatedHeaderHeight,      // Header Band 1 (Excel: 72.8pt = 25.7mm)
    double EstimatedBand2HeaderHeight, // Header Band 2 (Excel: 58.5pt = 20.6mm)
    int ModulesPerPage,       // Gesamt-Positionen pro A4 (MP-35mm: 10 = 2 Baender x 5 Spalten)
    int ColumnsPerPage,       // Streifen-Spalten pro A4 (MP-35mm: 5)
    // Spalten-Anteile innerhalb eines Moduls: Adresse-links / Adresse-rechts /
    // Net-Address / CPU-Name. 25mm hat KEINE Net-Address-Spalte (Col2 = 0).
    double Col0Ratio = 1499.0 / 4570.0,   // 35mm Standard
    double Col1Ratio = 1499.0 / 4570.0,
    double Col2Ratio = 804.0 / 4570.0,
    double Col3Ratio = 768.0 / 4570.0,
    int RowsPerHalf = 20      // Datenzeilen pro Band/Streifen
)
{
    /// <summary>Band einer Positions-Nr (0 = oben, 1 = unten).</summary>
    public int BandOf(int moduleIndex) => moduleIndex / ColumnsPerPage;

    /// <summary>Spalte einer Positions-Nr (0-basiert).</summary>
    public int ColumnOf(int moduleIndex) => moduleIndex % ColumnsPerPage;

    /// <summary>True wenn Module eine Net-Address-Spalte haben (35mm: ja, 25mm: nein).</summary>
    public bool HasNetAddressColumn => Col2Ratio > 0.0001;
}

public static class ProductFamilyDefinitions
{
    private static readonly Dictionary<ProductFamily, ProductFamilyInfo> _families = new()
    {
        [ProductFamily.ET200SP] = new ProductFamilyInfo(
            ProductFamily.ET200SP,
            "ET 200SP (12.8\u00d731mm)",
            "6ES7193-6LA10-0AA0",
            DefaultMarginTop: 20.5,
            DefaultMarginLeft: 27.5,
            DefaultMarginBottom: 20.5,
            DefaultMarginRight: 27.5,
            EstimatedModuleWidth: 31.0,
            EstimatedChannelRowHeight: 12.8,
            EstimatedHeaderHeight: 0,
            EstimatedBand2HeaderHeight: 0,
            ModulesPerPage: 100,  // ET200SP: 100 einzelne Etiketten
            ColumnsPerPage: 5,
            RowsPerHalf: 1),

        [ProductFamily.S71500_ET200MP] = new ProductFamilyInfo(
            ProductFamily.S71500_ET200MP,
            "S7-1500 / ET 200MP (35mm)",
            "6ES7592-1AX00-0AA0",
            DefaultMarginTop: 14.0,
            DefaultMarginLeft: 25.0,
            DefaultMarginBottom: 19.0,
            DefaultMarginRight: 12.0,
            EstimatedModuleWidth: 34.6,   // (210-25-12)/5
            EstimatedChannelRowHeight: 5.6,
            EstimatedHeaderHeight: 25.7,
            EstimatedBand2HeaderHeight: 20.6,
            ModulesPerPage: 10,           // 2 Baender x 5 Spalten = 10 Streifen-Positionen
            ColumnsPerPage: 5,
            RowsPerHalf: 20),

        [ProductFamily.S71500_ET200MP_25mm] = new ProductFamilyInfo(
            ProductFamily.S71500_ET200MP_25mm,
            "ET 200MP 25mm",
            "6ES7592-2AX00-0AA0",
            DefaultMarginTop: 14.0,
            DefaultMarginLeft: 25.0,
            DefaultMarginBottom: 19.0,
            DefaultMarginRight: 12.0,
            EstimatedModuleWidth: 17.36,  // vermessen: 49.2pt = 17.36mm (3 Excel-Spalten)
            EstimatedChannelRowHeight: 5.6,
            EstimatedHeaderHeight: 25.7,
            EstimatedBand2HeaderHeight: 20.6,
            // 25mm: 20 Module/Bogen (10 Spalten x 2 Baender), Modulstruktur ANDERS als
            // 35mm: Adresse (A+B) + CPU-Name, KEINE Net-Address-Spalte (Col2 = 0).
            // Spaltenbreiten vermessen: A=17.7 B=17.7 C=13.8pt -> 0.36/0.36/0/0.28.
            ModulesPerPage: 20,
            ColumnsPerPage: 10,
            Col0Ratio: 17.7 / 49.2, Col1Ratio: 17.7 / 49.2,
            Col2Ratio: 0.0, Col3Ratio: 13.8 / 49.2,
            RowsPerHalf: 20),
    };

    public static ProductFamilyInfo Get(ProductFamily family) => _families[family];
    public static IReadOnlyList<ProductFamilyInfo> All => _families.Values.ToList();
}
