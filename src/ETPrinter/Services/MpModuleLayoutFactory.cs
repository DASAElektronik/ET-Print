using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// Erzeugt Zellen-Layouts fuer ET200MP Modultyp-Varianten.
/// Klemmenbelegungen aus Siemens Equipment Manuals.
/// Jedes Modul hat 2 Haelften (Half 0 = oben, Half 1 = unten).
/// </summary>
public static class MpModuleLayoutFactory
{
    // Spaltenproportionen (aus Excel-Spaltenbreiten, Gesamt 4570)
    public const double Col0Ratio = 1499.0 / 4570.0;  // 32.8% — Klemmen links
    public const double Col1Ratio = 1499.0 / 4570.0;  // 32.8% — Klemmen rechts
    public const double Col2Ratio = 804.0 / 4570.0;   // 17.6% — Netzadresse
    public const double Col3Ratio = 768.0 / 4570.0;   // 16.8% — CPU-Name

    public const int RowsPerHalf = 20;
    public const int NetAddrBlockRows = 10;

    private static readonly Dictionary<MpModuleVariant, MpModuleLayout> _layouts = new()
    {
        [MpModuleVariant.DI_DQ_32] = new(MpModuleVariant.DI_DQ_32, "32 DI/DQ",
            CreateLayout_DI_DQ_32()),

        [MpModuleVariant.DI_DQ_16] = new(MpModuleVariant.DI_DQ_16, "16 DI/DQ",
            CreateLayout_DI_DQ_16()),

        [MpModuleVariant.DI_230V_16] = new(MpModuleVariant.DI_230V_16, "16 DI 230V",
            CreateLayout_DI_230V_16()),

        [MpModuleVariant.DQ_230V_8] = new(MpModuleVariant.DQ_230V_8, "8 DQ 230V",
            CreateLayout_DQ_230V_8()),

        [MpModuleVariant.AI_AQ_8] = new(MpModuleVariant.AI_AQ_8, "8 AI/AQ",
            CreateLayout_AI_AQ_8()),

        [MpModuleVariant.AQ_4] = new(MpModuleVariant.AQ_4, "4 AQ",
            CreateLayout_AQ_4()),
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

    // =================================================================
    // DI 32x24VDC HF (6ES7521-1BL00-0AB0)
    // 32 Kanaele, 4 Bytes, 40 Klemmen
    // Quelle: Siemens Equipment Manual, Figure 3-1
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_DQ_32()
    {
        var cells = new List<MpCellDefinition>();

        // Obere Haelfte (Half 0): Gruppe a (links CH0-7) + Gruppe c (rechts CH16-23)
        // Klemmen 1-8: CH0-CH7 (links)
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: 0, ColSpan: 1, IsEditable: true));
        // Klemme 9: M (Masse)
        cells.Add(new(Half: 0, StartRow: 8, RowSpan: 1, StartCol: 0, ColSpan: 1, IsEditable: false, Label: "M"));
        // Klemme 10: (leer)
        cells.Add(new(Half: 0, StartRow: 9, RowSpan: 1, StartCol: 0, ColSpan: 1, IsEditable: false));

        // Klemmen 21-28: CH16-CH23 (rechts)
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: 1, ColSpan: 1, IsEditable: true));
        // Klemme 29: M (Masse)
        cells.Add(new(Half: 0, StartRow: 8, RowSpan: 1, StartCol: 1, ColSpan: 1, IsEditable: false, Label: "M"));
        // Klemme 30: (leer)
        cells.Add(new(Half: 0, StartRow: 9, RowSpan: 1, StartCol: 1, ColSpan: 1, IsEditable: false));

        // Rows 10-19 der oberen Haelfte: leer (kein Inhalt bei DI 32)
        // Diese werden nicht als Zellen angelegt — bleiben leer im Preview

        // Untere Haelfte (Half 1): Gruppe b (links CH8-15) + Gruppe d (rechts CH24-31)
        // Klemmen 11-18 → Rows 0-7 der unteren Haelfte
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 1, StartRow: i, RowSpan: 1, StartCol: 0, ColSpan: 1, IsEditable: true));
        // Klemme 18: 1L+ (Power)
        cells.Add(new(Half: 1, StartRow: 8, RowSpan: 1, StartCol: 0, ColSpan: 1, IsEditable: false, Label: "1L+"));
        // Klemme 19: 1M (Ground)
        cells.Add(new(Half: 1, StartRow: 9, RowSpan: 1, StartCol: 0, ColSpan: 1, IsEditable: false, Label: "1M"));

        // Klemmen 31-38: CH24-CH31 (rechts)
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 1, StartRow: i, RowSpan: 1, StartCol: 1, ColSpan: 1, IsEditable: true));
        // Klemme 39: 2M (Ground)
        cells.Add(new(Half: 1, StartRow: 8, RowSpan: 1, StartCol: 1, ColSpan: 1, IsEditable: false, Label: "2L+"));
        // Klemme 40: (leer)
        cells.Add(new(Half: 1, StartRow: 9, RowSpan: 1, StartCol: 1, ColSpan: 1, IsEditable: false, Label: "2M"));

        return cells.ToArray();
    }

    // =================================================================
    // DI 16x24VDC (6ES7521-1BH00/10)
    // 16 Kanaele, 2 Bytes, Col 0+1 gemergt
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_DQ_16()
    {
        var cells = new List<MpCellDefinition>();

        // Obere Haelfte: 20 Zeilen, Col 0+1 gemergt (2x1)
        for (int i = 0; i < 20; i++)
            cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: true));

        // Untere Haelfte: 20 Zeilen (gleiche Struktur, fuer 2. Modul oder leer)
        for (int i = 0; i < 20; i++)
            cells.Add(new(Half: 1, StartRow: i, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: true));

        return cells.ToArray();
    }

    // =================================================================
    // DI 16x230VAC (6ES7521-1FH00)
    // 16 Kanaele, getrennte Spalten, 2 Zeilen pro Kanal
    // Zeilen 8-9 und 18-19: Strukturzellen (2x2 gemergt)
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_230V_16()
    {
        var cells = new List<MpCellDefinition>();

        for (int half = 0; half < 2; half++)
        {
            // 4 Adresspaare (rows 0-7), dann Struktur (8-9)
            for (int pair = 0; pair < 4; pair++)
            {
                int row = pair * 2;
                cells.Add(new(half, row, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
                cells.Add(new(half, row, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
            }
            cells.Add(new(half, 8, 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));

            // 4 Adresspaare (rows 10-17), dann Struktur (18-19)
            for (int pair = 0; pair < 4; pair++)
            {
                int row = 10 + pair * 2;
                cells.Add(new(half, row, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
                cells.Add(new(half, row, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
            }
            cells.Add(new(half, 18, 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L+/M"));
        }

        return cells.ToArray();
    }

    // =================================================================
    // DQ 8x230VAC (6ES7522-5HF00)
    // 8 Kanaele, gemischtes Merge-Pattern
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DQ_230V_8()
    {
        var cells = new List<MpCellDefinition>();

        for (int half = 0; half < 2; half++)
        {
            // rows 0-1: 2 Adressen (col0, col1)
            cells.Add(new(half, 0, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(half, 0, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
            // rows 2-3: Struktur (2x2)
            cells.Add(new(half, 2, 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));
            // rows 4-5: 2 Adressen
            cells.Add(new(half, 4, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(half, 4, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
            // rows 6-9: Versorgung (2x4)
            cells.Add(new(half, 6, 4, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L+/M"));

            // rows 10-11: 2 Adressen
            cells.Add(new(half, 10, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(half, 10, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
            // rows 12-13: Struktur
            cells.Add(new(half, 12, 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));
            // rows 14-15: 2 Adressen
            cells.Add(new(half, 14, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(half, 14, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
            // rows 16-19: Versorgung
            cells.Add(new(half, 16, 4, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L+/M"));
        }

        return cells.ToArray();
    }

    // =================================================================
    // AI 8xU/I/RTD/TC ST (6ES7531-7KF00)
    // 8 Analogkanaele, 4 Zeilen pro Kanal (U+, U-, leer, leer)
    // CH0-3 links, CH4-7 rechts
    // =================================================================
    private static MpCellDefinition[] CreateLayout_AI_AQ_8()
    {
        var cells = new List<MpCellDefinition>();

        // Obere Haelfte: CH0-3 links (rows 0-15), CH4-7 rechts (rows 0-15)
        // MANA bei row 16, rest leer
        for (int col = 0; col < 2; col++)
        {
            for (int ch = 0; ch < 4; ch++)
            {
                int row = ch * 4;
                cells.Add(new(Half: 0, StartRow: row, RowSpan: 4, StartCol: col, ColSpan: 1, IsEditable: true));
            }
        }
        // MANA row 16
        cells.Add(new(Half: 0, StartRow: 16, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "MANA"));

        // Untere Haelfte: gleiche Struktur (oder leer bei nur 8 Kanaelen)
        for (int col = 0; col < 2; col++)
        {
            for (int ch = 0; ch < 4; ch++)
            {
                int row = ch * 4;
                cells.Add(new(Half: 1, StartRow: row, RowSpan: 4, StartCol: col, ColSpan: 1, IsEditable: true));
            }
        }
        cells.Add(new(Half: 1, StartRow: 16, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "MANA"));

        return cells.ToArray();
    }

    // =================================================================
    // AQ 4xU/I ST (6ES7532-5HD00)
    // 4 Analogausgaenge, 4 Zeilen pro Kanal (QV, S+, S-, MANA)
    // Alle 4 Kanaele NUR links, Col 0+1 gemergt
    // Rechte Seite komplett leer
    // =================================================================
    private static MpCellDefinition[] CreateLayout_AQ_4()
    {
        var cells = new List<MpCellDefinition>();

        // Obere Haelfte: 4 Kanaele (rows 0-15), rest leer
        for (int ch = 0; ch < 4; ch++)
        {
            int row = ch * 4;
            cells.Add(new(Half: 0, StartRow: row, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: true));
        }

        // Untere Haelfte: leer (nur Strukturzellen)
        cells.Add(new(Half: 1, StartRow: 0, RowSpan: 20, StartCol: 0, ColSpan: 2, IsEditable: false));

        return cells.ToArray();
    }
}
