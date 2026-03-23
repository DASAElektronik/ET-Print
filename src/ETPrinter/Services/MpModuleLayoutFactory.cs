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
    // DI 32x24VDC HF — 32 Kanaele, 4 Bytes
    // Col 0: Byte 0 (rows 0-7) + Byte 1 (rows 10-17), Col 1: Byte 2 + Byte 3
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_DQ_32()
    {
        var cells = new List<MpCellDefinition>();

        for (int col = 0; col < 2; col++)
        {
            string prefix = col == 0 ? "1" : "2";

            // Rows 0-7: 8 Kanaele (Byte 0 bzw. Byte 2)
            for (int i = 0; i < 8; i++)
                cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: true));
            // Row 8: M
            cells.Add(new(Half: 0, StartRow: 8, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: "M"));
            // Row 9: (leer)
            cells.Add(new(Half: 0, StartRow: 9, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false));
            // Rows 10-17: 8 Kanaele (Byte 1 bzw. Byte 3)
            for (int i = 0; i < 8; i++)
                cells.Add(new(Half: 0, StartRow: 10 + i, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: true));
            // Row 18: L+
            cells.Add(new(Half: 0, StartRow: 18, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: $"{prefix}L+"));
            // Row 19: M
            cells.Add(new(Half: 0, StartRow: 19, RowSpan: 1, StartCol: col, ColSpan: 1, IsEditable: false, Label: $"{prefix}M"));
        }

        return cells.ToArray();
    }

    // =================================================================
    // DI/DQ 16x24VDC — 16 Kanaele, 2 Bytes
    // Col 0+1 gemergt (breite Spalte), 1 Zeile pro Kanal
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_DQ_16()
    {
        var cells = new List<MpCellDefinition>();

        // 16 Kanaele in 16 Zeilen (merged cols), Rest leer/Struktur
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 0, StartRow: i, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: true));
        // Row 8: M
        cells.Add(new(Half: 0, StartRow: 8, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));
        // Row 9: (leer)
        cells.Add(new(Half: 0, StartRow: 9, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false));
        // Rows 10-17: 8 weitere Kanaele
        for (int i = 0; i < 8; i++)
            cells.Add(new(Half: 0, StartRow: 10 + i, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: true));
        // Row 18: L+
        cells.Add(new(Half: 0, StartRow: 18, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L+"));
        // Row 19: M
        cells.Add(new(Half: 0, StartRow: 19, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));

        return cells.ToArray();
    }

    // =================================================================
    // DI 16x230VAC — 16 Kanaele, 2 Zeilen pro Kanal-Paar
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DI_230V_16()
    {
        var cells = new List<MpCellDefinition>();

        // 4 Adresspaare (rows 0-7)
        for (int pair = 0; pair < 4; pair++)
        {
            int row = pair * 2;
            cells.Add(new(Half: 0, StartRow: row, RowSpan: 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(Half: 0, StartRow: row, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        }
        // Row 8-9: M (merged)
        cells.Add(new(Half: 0, StartRow: 8, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));

        // 4 Adresspaare (rows 10-17)
        for (int pair = 0; pair < 4; pair++)
        {
            int row = 10 + pair * 2;
            cells.Add(new(Half: 0, StartRow: row, RowSpan: 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(Half: 0, StartRow: row, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        }
        // Row 18-19: L+/M
        cells.Add(new(Half: 0, StartRow: 18, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L+/M"));

        return cells.ToArray();
    }

    // =================================================================
    // DQ 8x230VAC — 8 Kanaele, gemischtes Pattern
    // =================================================================
    private static MpCellDefinition[] CreateLayout_DQ_230V_8()
    {
        var cells = new List<MpCellDefinition>();

        // Rows 0-1: 2 Adressen
        cells.Add(new(Half: 0, StartRow: 0, RowSpan: 2, StartCol: 0, ColSpan: 1, IsEditable: true));
        cells.Add(new(Half: 0, StartRow: 0, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        // Rows 2-3: M
        cells.Add(new(Half: 0, StartRow: 2, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));
        // Rows 4-5: 2 Adressen
        cells.Add(new(Half: 0, StartRow: 4, RowSpan: 2, StartCol: 0, ColSpan: 1, IsEditable: true));
        cells.Add(new(Half: 0, StartRow: 4, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        // Rows 6-9: Versorgung
        cells.Add(new(Half: 0, StartRow: 6, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L1/N/PE"));

        // Rows 10-11: 2 Adressen
        cells.Add(new(Half: 0, StartRow: 10, RowSpan: 2, StartCol: 0, ColSpan: 1, IsEditable: true));
        cells.Add(new(Half: 0, StartRow: 10, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        // Rows 12-13: M
        cells.Add(new(Half: 0, StartRow: 12, RowSpan: 2, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "M"));
        // Rows 14-15: 2 Adressen
        cells.Add(new(Half: 0, StartRow: 14, RowSpan: 2, StartCol: 0, ColSpan: 1, IsEditable: true));
        cells.Add(new(Half: 0, StartRow: 14, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        // Rows 16-19: Versorgung
        cells.Add(new(Half: 0, StartRow: 16, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "L1/N/PE"));

        return cells.ToArray();
    }

    // =================================================================
    // AI 8xU/I/RTD/TC — 8 Analogkanaele, 4 Zeilen pro Kanal
    // CH0-3 links, CH4-7 rechts
    // =================================================================
    private static MpCellDefinition[] CreateLayout_AI_AQ_8()
    {
        var cells = new List<MpCellDefinition>();

        for (int col = 0; col < 2; col++)
        {
            // 4 Kanaele pro Spalte (rows 0-15)
            for (int ch = 0; ch < 4; ch++)
            {
                int row = ch * 4;
                cells.Add(new(Half: 0, StartRow: row, RowSpan: 4, StartCol: col, ColSpan: 1, IsEditable: true));
            }
        }
        // Row 16: MANA
        cells.Add(new(Half: 0, StartRow: 16, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "MANA"));
        // Rows 17-19: leer
        cells.Add(new(Half: 0, StartRow: 17, RowSpan: 3, StartCol: 0, ColSpan: 2, IsEditable: false));

        return cells.ToArray();
    }

    // =================================================================
    // AQ 4xU/I — 4 Analogausgaenge, 4 Zeilen pro Kanal
    // Col 0+1 gemergt, nur 4 Kanaele
    // =================================================================
    private static MpCellDefinition[] CreateLayout_AQ_4()
    {
        var cells = new List<MpCellDefinition>();

        // 4 Kanaele (rows 0-15)
        for (int ch = 0; ch < 4; ch++)
        {
            int row = ch * 4;
            cells.Add(new(Half: 0, StartRow: row, RowSpan: 4, StartCol: 0, ColSpan: 2, IsEditable: true));
        }
        // Row 16: MANA
        cells.Add(new(Half: 0, StartRow: 16, RowSpan: 1, StartCol: 0, ColSpan: 2, IsEditable: false, Label: "MANA"));
        // Rows 17-19: leer
        cells.Add(new(Half: 0, StartRow: 17, RowSpan: 3, StartCol: 0, ColSpan: 2, IsEditable: false));

        return cells.ToArray();
    }
}
