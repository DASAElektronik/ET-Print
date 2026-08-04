using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// Ein konkretes Siemens-Modul mit exakter Klemmenbelegung.
/// Variant bestimmt das Zellen-Layout (Merges), Cells die Struktur-Labels
/// (M/L+/leer) gemaess Blockdiagramm des Equipment Manuals.
/// </summary>
public record MpCatalogEntry(
    string ArticleNo,
    string DisplayName,
    ModuleType IoType,
    MpModuleVariant Variant,
    MpCellDefinition[] Cells);

/// <summary>
/// Katalog konkreter S7-1500/ET200MP-Module. Belegungen sind an den
/// Blockdiagrammen der Equipment Manuals verifiziert (siehe PRINT-FORMATS.md).
/// Eintraege nur nach Datenblatt-Verifikation ergaenzen!
/// </summary>
public static class MpModuleCatalog
{
    /// <summary>Sentinel fuer "kein konkretes Modul" — nur die Layout-Variante
    /// bestimmt die (generische) Belegung.</summary>
    public static readonly MpCatalogEntry CustomEntry = new(
        ArticleNo: string.Empty,
        DisplayName: "Benutzerdefiniert (nur Layout-Variante)",
        IoType: ModuleType.DI,
        Variant: MpModuleVariant.DI_DQ_16,
        Cells: []);

    public static readonly IReadOnlyList<MpCatalogEntry> Entries =
    [
        CustomEntry,

        // Verifiziert: Blockdiagramm A5E03485935-AH (05/2022)
        new("6ES7521-1BL00-0AB0", "DI 32x24VDC HF", ModuleType.DI, MpModuleVariant.DI_DQ_32,
            MpModuleLayoutFactory.CreateLayout_DI_DQ_32(
                k9: "", k10: "", k19: "1L+", k20: "1M",
                k29: "", k30: "", k39: "2L+", k40: "2M")),

        // Verifiziert: Blockdiagramm (109480716), Potentialbruecken 9-29/10-30/19-39/20-40.
        // DQ 32 belegt K9/K10 (1L+/1M) und K19/K20 (2L+/2M) — anders als DI 32.
        new("6ES7522-1BL01-0AB0", "DQ 32x24VDC/0.5A HF", ModuleType.DO, MpModuleVariant.DI_DQ_32,
            MpModuleLayoutFactory.CreateLayout_DI_DQ_32(
                k9: "1L+", k10: "1M", k19: "2L+", k20: "2M",
                k29: "3L+", k30: "3M", k39: "4L+", k40: "4M")),

        // Verifiziert: Blockdiagramm im Equipment Manual (83501190)
        new("6ES7521-1BH10-0AA0", "DI 16x24VDC BA", ModuleType.DI, MpModuleVariant.DI_DQ_16,
            MpModuleLayoutFactory.CreateLayout_DI_DQ_16(k9: "", k10: "", k19: "", k20: "M")),

        // Verifiziert: Manual A5E03485952 ("supply voltage to terminals 19 and 20")
        new("6ES7521-1BH00-0AB0", "DI 16x24VDC HF", ModuleType.DI, MpModuleVariant.DI_DQ_16,
            MpModuleLayoutFactory.CreateLayout_DI_DQ_16(k9: "", k10: "", k19: "L+", k20: "M")),

        // Verifiziert: Blockdiagramm A5E03485531-AD (09/2016)
        new("6ES7522-1BH00-0AB0", "DQ 16x24VDC/0.5A ST", ModuleType.DO, MpModuleVariant.DI_DQ_16,
            MpModuleLayoutFactory.CreateLayout_DI_DQ_16(k9: "1L+", k10: "1M", k19: "2L+", k20: "2M")),

        // Verifiziert: Blockdiagramm Figure 3-1 (59193089). Nur K1-K8 sind Kanaele,
        // K11-K18 und K21-K40 unbelegt -> eigenes Layout mit gesperrten Zellen.
        new("6ES7522-1BF00-0AB0", "DQ 8x24VDC/2A HF", ModuleType.DO, MpModuleVariant.DI_DQ_32,
            MpModuleLayoutFactory.CreateLayout_DQ_8_2A()),

        // Verifiziert: Blockdiagramm Figure 3-1 (59193398). Kanaele auf den
        // ungeraden Klemmen, xN auf K8/K18/K28/K38; K9/K10 + K19/K20 unbelegt.
        // Alle Struktur-Bloecke bleiben leer (siehe CreateLayout_DI_230V_16).
        new("6ES7521-1FH00-0AA0", "DI 16x230VAC BA", ModuleType.DI, MpModuleVariant.DI_230V_16,
            MpModuleLayoutFactory.GetLayout(MpModuleVariant.DI_230V_16).AddressCells),

        // SIWAREX Waegemodule — fester Pinout (Variante liefert die Labels).
        // Verifiziert: Anschlussbelegung A5E36695151A (04/2016).
        new("7MH4980-1AA01", "SIWAREX WP521 ST (1 Kanal)", ModuleType.DI, MpModuleVariant.SIWAREX_WP52x,
            MpModuleLayoutFactory.GetLayout(MpModuleVariant.SIWAREX_WP52x).AddressCells),
        new("7MH4980-2AA01", "SIWAREX WP522 ST (2 Kanal)", ModuleType.DI, MpModuleVariant.SIWAREX_WP52x,
            MpModuleLayoutFactory.GetLayout(MpModuleVariant.SIWAREX_WP52x).AddressCells),

        // === 25mm-Module ===
        // Verifiziert: Blockdiagramme Figure 3-1/3-2 (91688388). Wie alle
        // Analogmodule OHNE feste Klemmen-Kanal-Zuordnung: Spannungsausgang
        // (2-/4-Draht) und Stromausgang belegen unterschiedliche Klemmen
        // (QV/QI auf K1, MANA auf K3; bei 4-Draht zusaetzlich S+/S- auf K5/K6).
        // Deshalb keine Struktur-Labels — der Streifen bleibt komplett editierbar.
        // Belegt sind nur K1-K7, der 20-zeilige MP25_16-Streifen reicht dafuer.
        new("6ES7532-5NB00-0AB0", "AQ 2xU/I ST", ModuleType.AO, MpModuleVariant.MP25_16,
            MpModuleLayoutFactory.GetLayout(MpModuleVariant.MP25_16).AddressCells),
    ];

    public static MpCatalogEntry? Find(string? articleNo) =>
        string.IsNullOrEmpty(articleNo)
            ? null
            : Entries.FirstOrDefault(e => e.ArticleNo == articleNo);

    /// <summary>Katalog-Eintraege, die zur Familie passen — ueber die Variante des
    /// Eintrags gefiltert (25mm-Varianten vs. 35mm). "Benutzerdefiniert" ist immer dabei.</summary>
    public static IReadOnlyList<MpCatalogEntry> EntriesForFamily(ProductFamily family)
    {
        bool is25 = family == ProductFamily.S71500_ET200MP_25mm;
        return Entries
            .Where(e => e == CustomEntry || MpModuleLayoutFactory.Is25mmVariant(e.Variant) == is25)
            .ToList();
    }
}
