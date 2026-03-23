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
    double EstimatedHeaderHeight,
    double EstimatedSeparatorHeight,
    int ModulesPerPage,       // 5 fuer ET200MP (jedes Modul hat 2 Haelften)
    int RowsPerHalf = 20     // Datenzeilen pro Modulhaelfte
);

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
            EstimatedSeparatorHeight: 0,
            ModulesPerPage: 100,  // ET200SP: 100 einzelne Etiketten
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
            EstimatedSeparatorHeight: 20.6,
            ModulesPerPage: 5,            // 5 Spalten = 5 Module (Band1+Band2 = 1 Modul)
            RowsPerHalf: 20),

        [ProductFamily.S71500_ET200MP_25mm] = new ProductFamilyInfo(
            ProductFamily.S71500_ET200MP_25mm,
            "ET 200MP 25mm",
            "6ES7592-2AX00-0AA0",
            DefaultMarginTop: 14.0,
            DefaultMarginLeft: 25.0,
            DefaultMarginBottom: 19.0,
            DefaultMarginRight: 12.0,
            EstimatedModuleWidth: 17.3,
            EstimatedChannelRowHeight: 5.6,
            EstimatedHeaderHeight: 25.7,
            EstimatedSeparatorHeight: 20.6,
            ModulesPerPage: 10,           // 10 Spalten bei 25mm
            RowsPerHalf: 20),
    };

    public static ProductFamilyInfo Get(ProductFamily family) => _families[family];
    public static IReadOnlyList<ProductFamilyInfo> All => _families.Values.ToList();
}
