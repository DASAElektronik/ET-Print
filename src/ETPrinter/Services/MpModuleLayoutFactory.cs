using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// Erzeugt Zellen-Layouts fuer ET200MP Modultyp-Varianten.
/// ALLE Varianten nutzen nur Half 0 (= ein Etikett).
/// Half 1 ist immer fuer ein ANDERES Modul auf dem gleichen A4-Blatt.
/// </summary>
public static class MpModuleLayoutFactory
{
    public const double Col0Ratio = 1499.0 / 4570.0;  // 32.8%
    public const double Col1Ratio = 1499.0 / 4570.0;  // 32.8%
    public const double Col2Ratio = 804.0 / 4570.0;   // 17.6%
    public const double Col3Ratio = 768.0 / 4570.0;   // 16.8%

    public const int RowsPerHalf = 20;
    public const int NetAddrBlockRows = 10;

    private static readonly Dictionary<MpModuleVariant, MpModuleLayout> _layouts = new()
    {
        [MpModuleVariant.DI_DQ_32] = new(MpModuleVariant.DI_DQ_32, "32 Kanal (2 Spalten)",
            CreateLayout_DI_DQ_32()),

        [MpModuleVariant.DI_DQ_16] = new(MpModuleVariant.DI_DQ_16, "16 Kanal (1 Spalte)",
            CreateLayout_DI_DQ_16()),

        [MpModuleVariant.DI_230V_16] = new(MpModuleVariant.DI_230V_16, "16 Kanal 230V (2er-Paare)",
            CreateLayout_DI_230V_16()),

        [MpModuleVariant.DQ_230V_8] = new(MpModuleVariant.DQ_230V_8, "8 Kanal 230V (gemischt)",
            CreateLayout_DQ_230V_8()),

        [MpModuleVariant.AI_AQ_8] = new(MpModuleVariant.AI_AQ_8, "8 Analog (4 Zeilen/Kanal)",
            CreateLayout_AI_AQ_8()),

        [MpModuleVariant.AQ_4] = new(MpModuleVariant.AQ_4, "4 Analog (breit)",
            CreateLayout_AQ_4()),

        [MpModuleVariant.SIWAREX_WP52x] = new(MpModuleVariant.SIWAREX_WP52x, "SIWAREX WP52x (Waegemodul)",
            CreateLayout_SIWAREX()),
    };

    public static MpModuleLayout GetLayout(MpModuleVariant variant) => _layouts[variant];
    public static IReadOnlyList<MpModuleLayout> All => _layouts.Values.ToList();

    public static List<MpAddressCell> CreateCells(MpModuleVariant variant)
    {
        var layout = GetLayout(variant);
        return layout.AddressCells
            .Select((def, i) => new MpAddressCell { CellIndex = i })
            .ToList();
    }

    /// <summary>
    /// Liefert die Zellen-Definitionen fuer ein Modul: Katalog-Belegung (exakte
    /// Klemmen-Labels des konkreten Siemens-Moduls) wenn ArticleNumber gesetzt,
    /// sonst die generische Varianten-Belegung. Zellenzahl ist je Variante identisch.
    /// </summary>
    public static MpCellDefinition[] GetDefinitions(MpModule module)
    {
        var entry = MpModuleCatalog.Find(module.ArticleNumber);
        if (entry is not null && entry.Variant == module.Variant)
            return entry.Cells;
        return GetLayout(module.Variant).AddressCells;
    }

    // =================================================================
    // DI 32x24VDC HF — 32 Kanaele, 4 Bytes
    // Col 0: Byte 0 (rows 0-7) + Byte 1 (rows 10-17), Col 1: Byte 2 + Byte 3
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_DQ_32() =>
        CreateLayout_DI_DQ_32(k9: "", k10: "", k19: "1L+", k20: "1M",
                              k29: "", k30: "", k39: "2L+", k40: "2M");

    /// <summary>Parametrisiert fuer den Modul-Katalog: Struktur-Klemmen-Labels
    /// (K9/K10/K19/K20 links, K29/K30/K39/K40 rechts) je konkretem Siemens-Modul.</summary>
    internal static MpCellDefinition[] CreateLayout_DI_DQ_32(
        string k9, string k10, string k19, string k20,
        string k29, string k30, string k39, string k40)
    {
        var cells = new List<MpCellDefinition>();

        for (int col = 0; col < 2; col++)
        {
            string r8Label = col == 0 ? k9 : k29;
            string r9Label = col == 0 ? k10 : k30;
            string r18Label = col == 0 ? k19 : k39;
            string r19Label = col == 0 ? k20 : k40;

            // Rows 0-7: 8 Kanaele (Byte 0 bzw. Byte 2)
            for (int i = 0; i < 8; i++)
                cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: true));
            // Rows 8-9: Struktur-Klemmen 9+10 bzw. 29+30
            cells.Add(new(Half: 0, StartRow: 8, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: r8Label));
            cells.Add(new(Half: 0, StartRow: 9, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: r9Label));
            // Rows 10-17: 8 Kanaele (Byte 1 bzw. Byte 3)
            for (int i = 0; i < 8; i++)
                cells.Add(new(Half: 0, StartRow: 10 + i, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: true));
            // Rows 18-19: Struktur-Klemmen 19+20 bzw. 39+40
            cells.Add(new(Half: 0, StartRow: 18, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: r18Label));
            cells.Add(new(Half: 0, StartRow: 19, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: r19Label));
        }

        return cells.ToArray();
    }

    // =================================================================
    // DI/DQ 16x24VDC — 16 Kanaele, 2 Bytes
    // Col 0+1 gemergt (breite Spalte), 1 Zeile pro Kanal
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_DQ_16() =>
        CreateLayout_DI_DQ_16(k9: "", k10: "", k19: "L+", k20: "M");

    /// <summary>Parametrisiert fuer den Modul-Katalog: DI16 BA (K19 leer),
    /// DI16 HF (K19 = L+), DQ16 ST (K9/K10 = 1L+/1M, K19/K20 = 2L+/2M).</summary>
    internal static MpCellDefinition[] CreateLayout_DI_DQ_16(
        string k9, string k10, string k19, string k20)
    {
        var cells = new List<MpCellDefinition>();

        // 16 Kanaele in 16 Zeilen (merged cols), Rest Struktur-Klemmen
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: true));
        cells.Add(new(Half: 0, StartRow: 8, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: k9));
        cells.Add(new(Half: 0, StartRow: 9, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: k10));
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 0, StartRow: 10 + i, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: true));
        cells.Add(new(Half: 0, StartRow: 18, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: k19));
        cells.Add(new(Half: 0, StartRow: 19, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: k20));

        return cells.ToArray();
    }

    // =================================================================
    // DI 16x230VAC BA — 16 Kanaele, 2 Zeilen pro Kanal (Excel-Struktur).
    // Col0/Col1 je 8 Kanalbloecke; colspan-2-Bloecke (rows 8-9, 18-19) sind im
    // Excel-Template LEER (kein M/L+-Label) — Verdrahtung 230VAC-modulspezifisch,
    // Datenblatt noch nicht extrahiert, daher keine geratenen Labels.
    // Verifiziert gegen Excel horizontal_16_DI_230V (2026-07-03).
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_230V_16()
    {
        var cells = new List<MpCellDefinition>();

        // Editierbare Zellen SPALTENWEISE: Kanalgruppe links, dann rechts
        for (int col = 0; col < 2; col++)
        {
            for (int pair = 0; pair < 4; pair++)
                cells.Add(new(Half: 0, StartRow: pair * 2, RowSpan: 2, StartCol: col, ColSpan: 1, IsEditable: true));
            for (int pair = 0; pair < 4; pair++)
                cells.Add(new(Half: 0, StartRow: 10 + pair * 2, RowSpan: 2, StartCol: col, ColSpan: 1, IsEditable: true));
        }
        // Struktur-Bloecke (im Excel leer)
        cells.Add(new(Half: 0, StartRow: 8, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false));
        cells.Add(new(Half: 0, StartRow: 18, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false));

        return cells.ToArray();
    }

    // =================================================================
    // DQ 8x230VAC/5A ST Relay (6ES7522-5HF00-0AB0) — verifiziert am Blockdiagramm
    // A5E03485590-AD (09/2016): 8 Relaiskanaele, jeder = 2 Klemmen (Kontakt).
    // Links CH0-3 (K1-2, K4-5, K11-12, K14-15), rechts CH4-7 (K21-22...).
    // Versorgung 24V DC auf K19/20 bzw. K39/40; Bloecke dazwischen sind Luecken.
    // Excel laesst alle colspan-2-Bloecke leer -> keine Labels.
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DQ_230V_8()
    {
        var cells = new List<MpCellDefinition>();

        // Relais-Kanaele (2 Zeilen) SPALTENWEISE: links CH0-3, rechts CH4-7
        for (int col = 0; col < 2; col++)
        {
            cells.Add(new(Half: 0, StartRow: 0, RowSpan: 2, StartCol: col, ColSpan: 1, IsEditable: true));
            cells.Add(new(Half: 0, StartRow: 4, RowSpan: 2, StartCol: col, ColSpan: 1, IsEditable: true));
            cells.Add(new(Half: 0, StartRow: 10, RowSpan: 2, StartCol: col, ColSpan: 1, IsEditable: true));
            cells.Add(new(Half: 0, StartRow: 14, RowSpan: 2, StartCol: col, ColSpan: 1, IsEditable: true));
        }
        // Struktur-/Luecken-Bloecke (im Excel leer)
        cells.Add(new(Half: 0, StartRow: 2, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false));
        cells.Add(new(Half: 0, StartRow: 6, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: false));
        cells.Add(new(Half: 0, StartRow: 12, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false));
        cells.Add(new(Half: 0, StartRow: 16, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: false));

        return cells.ToArray();
    }

    // =================================================================
    // AI 8xU/I/RTD/TC — 5 editierbare 4-Zeilen-Bloecke pro Spalte (Excel-Struktur).
    // Analog-Verdrahtung ist modusabhaengig (U/I/RTD/TC) — das Datenblatt zeigt
    // KEINE feste Klemmen-Kanal-Zuordnung, daher generische Bloecke ohne MANA-Label
    // (der 5. Block dient typ. fuer MANA/Reserve, wird vom Nutzer beschriftet).
    // Verifiziert gegen Excel horizontal_8_AI_AQ (2026-07-03).
    // =================================================================
    private static MpCellDefinition[] CreateLayout_AI_AQ_8()
    {
        var cells = new List<MpCellDefinition>();

        for (int col = 0; col < 2; col++)
            for (int block = 0; block < 5; block++)
                cells.Add(new(Half: 0, StartRow: block * 4, RowSpan: 4, StartCol: col, ColSpan: 1, IsEditable: true));

        return cells.ToArray();
    }

    // =================================================================
    // AQ 4xU/I — 5 editierbare 4-Zeilen-Bloecke, Col 0+1 gemergt (Excel-Struktur).
    // Verifiziert gegen Excel horizontal_4_AQ (2026-07-03).
    // =================================================================
    private static MpCellDefinition[] CreateLayout_AQ_4()
    {
        var cells = new List<MpCellDefinition>();

        for (int block = 0; block < 5; block++)
            cells.Add(new(Half: 0, StartRow: block * 4, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: true));

        return cells.ToArray();
    }

    // =================================================================
    // SIWAREX WP521/WP522 (7MH4980-1AA01/-2AA01) — verifiziert am Pinout
    // A5E36695151A (04/2016). Fester Frontstecker: 20 Klemmen pro Spalte, beide
    // Spalten identisch (Waegezelle A links = K1-20, Waegezelle B rechts = K21-40).
    // Alle Klemmen haben feste Funktionslabels (nicht editierbar) — fertiger
    // Pinout-Streifen. Klemmen 41-44 (Einspeiseelement) liegen ausserhalb des Streifens.
    // =================================================================
    private static MpCellDefinition[] CreateLayout_SIWAREX()
    {
        // Lokale Konstante (kein static field) — sonst Initialisierungsreihenfolge-
        // Falle: _layouts wird vor einem static-Array-Feld initialisiert (null).
        string[] terminals =
        [
            "EXC+", "EXC-", "SIG+", "SIG-", "SEN+", "SEN-", "D+", "D-",
            "DQ.L+", "DQ.M", "DQ.0", "DQ.1", "DQ.2", "DQ.3",
            "DI.0", "DI.1", "DI.2", "DI.M", "L+", "M"
        ];

        var cells = new List<MpCellDefinition>();
        for (int col = 0; col < 2; col++)
            for (int row = 0; row < terminals.Length; row++)
                cells.Add(new(Half: 0, StartRow: row, RowSpan: 1, StartCol: col, ColSpan: 1,
                    IsEditable: false, Label: terminals[row]));

        return cells.ToArray();
    }
}
