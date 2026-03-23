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
    // Default margins in mm (from Siemens documentation, adjustable later)
    double DefaultMarginTop,
    double DefaultMarginLeft,
    double DefaultMarginBottom,
    double DefaultMarginRight,
    // Estimated label dimensions in mm (to be corrected with steel ruler measurements)
    double EstimatedModuleWidth,
    double EstimatedChannelRowHeight,
    double EstimatedHeaderHeight,
    double EstimatedSeparatorHeight,
    int BandsPerPage,
    int ModulesPerBand
)
{
    public int ModulesPerPage => ModulesPerBand * BandsPerPage;
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
            EstimatedModuleWidth: 31.0,    // gemessen mit Stahllineal
            EstimatedChannelRowHeight: 12.8, // gemessen mit Stahllineal
            EstimatedHeaderHeight: 0,
            EstimatedSeparatorHeight: 0,
            BandsPerPage: 1,
            ModulesPerBand: 5),

        [ProductFamily.S71500_ET200MP] = new ProductFamilyInfo(
            ProductFamily.S71500_ET200MP,
            "S7-1500 / ET 200MP (35mm)",
            "6ES7592-1AX00-0AA0",
            DefaultMarginTop: 14.0,     // aus Siemens-Doku
            DefaultMarginLeft: 25.0,    // aus Siemens-Doku
            DefaultMarginBottom: 19.0,  // aus Siemens-Doku
            DefaultMarginRight: 12.0,   // aus Siemens-Doku
            EstimatedModuleWidth: 34.6,  // geschaetzt: (210-25-12)/5
            EstimatedChannelRowHeight: 5.6, // geschaetzt: 315 twips
            EstimatedHeaderHeight: 25.7,    // geschaetzt: 1455 twips
            EstimatedSeparatorHeight: 20.6, // geschaetzt: 1170 twips
            BandsPerPage: 2,
            ModulesPerBand: 5),

        [ProductFamily.S71500_ET200MP_25mm] = new ProductFamilyInfo(
            ProductFamily.S71500_ET200MP_25mm,
            "ET 200MP 25mm",
            "6ES7592-2AX00-0AA0",
            DefaultMarginTop: 14.0,     // geschaetzt (gleiche Doku)
            DefaultMarginLeft: 25.0,
            DefaultMarginBottom: 19.0,
            DefaultMarginRight: 12.0,
            EstimatedModuleWidth: 17.3,  // geschaetzt: (210-25-12)/10
            EstimatedChannelRowHeight: 5.6,
            EstimatedHeaderHeight: 25.7,
            EstimatedSeparatorHeight: 20.6,
            BandsPerPage: 2,
            ModulesPerBand: 10),
    };

    public static ProductFamilyInfo Get(ProductFamily family) => _families[family];
    public static IReadOnlyList<ProductFamilyInfo> All => _families.Values.ToList();
}
