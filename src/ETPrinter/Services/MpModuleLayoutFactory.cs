using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// Erzeugt Zellen-Layouts fuer die 6 ET200MP Modultyp-Varianten.
/// Alle Merge-Patterns sind exakt aus dem Siemens Excel-Template abgeleitet
/// (Excel_Template_S71500_ET200MP.xls, Beitrags-ID 83681795).
/// </summary>
public static class MpModuleLayoutFactory
{
    // Spaltenproportionen innerhalb eines Moduls (aus Excel-Spaltenbreiten)
    // Gesamtbreite pro Modul: 1499 + 1499 + 804 + 768 = 4570 Einheiten
    public const double Col0Ratio = 1499.0 / 4570.0;  // 32.8% — Adresse Links
    public const double Col1Ratio = 1499.0 / 4570.0;  // 32.8% — Adresse Rechts
    public const double Col2Ratio = 804.0 / 4570.0;   // 17.6% — Netzadresse
    public const double Col3Ratio = 768.0 / 4570.0;   // 16.8% — CPU-Name

    public const int RowsPerBand = 20;
    public const int NetAddrBlockRows = 10;  // 2 Bloecke a 10 Zeilen

    private static readonly Dictionary<MpModuleVariant, MpModuleLayout> _layouts = new()
    {
        // === 32 DI/DQ ===
        // Col 0+1 getrennt, keine Merges, 1 Zeile pro Adresse
        // Col 0: Q 0.1-Q 0.7 (7 Adressen, rows 0-6)
        // Col 1: Q 1.0-Q 1.6 (7 Adressen, rows 0-6)
        // Rows 7-19: leer
        [MpModuleVariant.DI_DQ_32] = new(MpModuleVariant.DI_DQ_32, "32 DI/DQ",
            CreateGrid_Separate(rowsPerAddr: 1)),

        // === 16 DI/DQ ===
        // Col 0+1 gemergt (2x1), 1 Zeile pro Adresse, 20 Adressen
        [MpModuleVariant.DI_DQ_16] = new(MpModuleVariant.DI_DQ_16, "16 DI/DQ",
            CreateGrid_Merged(rowsPerAddr: 1)),

        // === 16 DI 230V ===
        // Col 0+1 getrennt, 2 Zeilen pro Adresse (1x2)
        // Zeilen 9-10 und 19-20 sind gemergt (2x2) = Struktur-/Leerzellen
        [MpModuleVariant.DI_230V_16] = new(MpModuleVariant.DI_230V_16, "16 DI 230V",
            CreateGrid_DI230V()),

        // === 8 DQ 230V ===
        // Gemischtes Pattern: 1x2, 2x2 und 2x4 Merges
        [MpModuleVariant.DQ_230V_8] = new(MpModuleVariant.DQ_230V_8, "8 DQ 230V",
            CreateGrid_DQ230V()),

        // === 8 AI/AQ ===
        // Col 0+1 getrennt, 4 Zeilen pro Adresse (1x4)
        // 5 Adressen pro Spalte x 2 Spalten = 10 Adresszellen
        [MpModuleVariant.AI_AQ_8] = new(MpModuleVariant.AI_AQ_8, "8 AI/AQ",
            CreateGrid_Separate(rowsPerAddr: 4)),

        // === 4 AQ ===
        // Col 0+1 gemergt (2x4), 4 Zeilen pro Adresse
        // 5 grosse Bloecke, 4 befuellbar + 1 leer
        [MpModuleVariant.AQ_4] = new(MpModuleVariant.AQ_4, "4 AQ",
            CreateGrid_Merged(rowsPerAddr: 4)),
    };

    public static MpModuleLayout GetLayout(MpModuleVariant variant) => _layouts[variant];
    public static IReadOnlyList<MpModuleLayout> All => _layouts.Values.ToList();

    /// <summary>
    /// Erzeugt leere Adress-Zellen passend zum Modultyp.
    /// </summary>
    public static List<MpAddressCell> CreateCells(MpModuleVariant variant)
    {
        var layout = GetLayout(variant);
        return layout.AddressCells
            .Select((def, i) => new MpAddressCell { CellIndex = i })
            .ToList();
    }

    // === Grid-Generatoren ===

    /// <summary>
    /// Getrennte Spalten (Col 0 + Col 1 einzeln), gleichmaessiger Raster.
    /// Fuer: 32 DI/DQ (1 Zeile), 8 AI/AQ (4 Zeilen)
    /// </summary>
    private static MpCellDefinition[] CreateGrid_Separate(int rowsPerAddr)
    {
        var cells = new List<MpCellDefinition>();
        int cellsPerCol = RowsPerBand / rowsPerAddr;

        for (int col = 0; col < 2; col++)
        {
            for (int i = 0; i < cellsPerCol; i++)
            {
                cells.Add(new MpCellDefinition(
                    StartRow: i * rowsPerAddr,
                    RowSpan: rowsPerAddr,
                    StartCol: col,
                    ColSpan: 1,
                    IsEditable: true));
            }
        }
        return cells.ToArray();
    }

    /// <summary>
    /// Gemergte Spalten (Col 0+1 zusammen), gleichmaessiger Raster.
    /// Fuer: 16 DI/DQ (1 Zeile), 4 AQ (4 Zeilen)
    /// </summary>
    private static MpCellDefinition[] CreateGrid_Merged(int rowsPerAddr)
    {
        var cells = new List<MpCellDefinition>();
        int cellCount = RowsPerBand / rowsPerAddr;

        for (int i = 0; i < cellCount; i++)
        {
            cells.Add(new MpCellDefinition(
                StartRow: i * rowsPerAddr,
                RowSpan: rowsPerAddr,
                StartCol: 0,
                ColSpan: 2,
                IsEditable: true));
        }
        return cells.ToArray();
    }

    /// <summary>
    /// 16 DI 230V: Getrennte Spalten, 2 Zeilen pro Adresse.
    /// Zeilen 8-9 und 18-19 sind Struktur-Zellen (2x2 gemergt, nicht editierbar).
    /// Exakt aus Excel: rows 1-2(1x2), 3-4(1x2), 5-6(1x2), 7-8(1x2), 9-10(2x2),
    ///                   11-12(1x2), 13-14(1x2), 15-16(1x2), 17-18(1x2), 19-20(2x2)
    /// (Excel rows 1-20 = unsere rows 0-19)
    /// </summary>
    private static MpCellDefinition[] CreateGrid_DI230V()
    {
        var cells = new List<MpCellDefinition>();

        // Obere Haelfte (rows 0-9)
        for (int pair = 0; pair < 4; pair++)
        {
            int row = pair * 2;
            cells.Add(new(row, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(row, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        }
        // Struktur-Zelle rows 8-9 (2x2 gemergt)
        cells.Add(new(8, 2, StartCol: 0, ColSpan: 2, IsEditable: false));

        // Untere Haelfte (rows 10-19)
        for (int pair = 0; pair < 4; pair++)
        {
            int row = 10 + pair * 2;
            cells.Add(new(row, 2, StartCol: 0, ColSpan: 1, IsEditable: true));
            cells.Add(new(row, 2, StartCol: 1, ColSpan: 1, IsEditable: true));
        }
        // Struktur-Zelle rows 18-19 (2x2 gemergt)
        cells.Add(new(18, 2, StartCol: 0, ColSpan: 2, IsEditable: false));

        return cells.ToArray();
    }

    /// <summary>
    /// 8 DQ 230V: Gemischtes Merge-Pattern.
    /// Exakt aus Excel:
    ///   rows 0-1: col0(1x2), col1(1x2)  — 2 Adressen
    ///   rows 2-3: merged(2x2)           — Struktur
    ///   rows 4-5: col0(1x2), col1(1x2)  — 2 Adressen
    ///   rows 6-9: merged(2x4)           — Struktur (Versorgung)
    ///   rows 10-11: col0(1x2), col1(1x2) — 2 Adressen
    ///   rows 12-13: merged(2x2)          — Struktur
    ///   rows 14-15: col0(1x2), col1(1x2) — 2 Adressen
    ///   rows 16-19: merged(2x4)          — Struktur (Versorgung)
    /// </summary>
    private static MpCellDefinition[] CreateGrid_DQ230V()
    {
        return
        [
            // Block 1 (rows 0-9)
            new(0, 2, StartCol: 0, ColSpan: 1, IsEditable: true),
            new(0, 2, StartCol: 1, ColSpan: 1, IsEditable: true),
            new(2, 2, StartCol: 0, ColSpan: 2, IsEditable: false),  // Struktur
            new(4, 2, StartCol: 0, ColSpan: 1, IsEditable: true),
            new(4, 2, StartCol: 1, ColSpan: 1, IsEditable: true),
            new(6, 4, StartCol: 0, ColSpan: 2, IsEditable: false),  // Versorgung

            // Block 2 (rows 10-19)
            new(10, 2, StartCol: 0, ColSpan: 1, IsEditable: true),
            new(10, 2, StartCol: 1, ColSpan: 1, IsEditable: true),
            new(12, 2, StartCol: 0, ColSpan: 2, IsEditable: false), // Struktur
            new(14, 2, StartCol: 0, ColSpan: 1, IsEditable: true),
            new(14, 2, StartCol: 1, ColSpan: 1, IsEditable: true),
            new(16, 4, StartCol: 0, ColSpan: 2, IsEditable: false), // Versorgung
        ];
    }
}
