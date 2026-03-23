namespace ETPrinter.Models;

/// <summary>
/// Die 6 Modultyp-Varianten fuer S7-1500 / ET 200MP.
/// Jede Variante hat ein unterschiedliches Zellen-Merge-Pattern
/// innerhalb der 4 Spalten x 20 Zeilen eines Moduls.
/// </summary>
public enum MpModuleVariant
{
    DI_DQ_32,       // 32 DI/DQ: Col 0+1 getrennt, 1 Zeile pro Adresse
    DI_DQ_16,       // 16 DI/DQ: Col 0+1 gemergt, 1 Zeile pro Adresse
    DI_230V_16,     // 16 DI 230V: Col 0+1 getrennt, 2 Zeilen pro Adresse, Leer-Paare
    DQ_230V_8,      // 8 DQ 230V: Gemischt (1x2, 2x2, 2x4 Merges)
    AI_AQ_8,        // 8 AI/AQ: Col 0+1 getrennt, 4 Zeilen pro Adresse
    AQ_4            // 4 AQ: Col 0+1 gemergt, 4 Zeilen pro Adresse
}

/// <summary>
/// Definition einer einzelnen Adress-Zelle innerhalb eines Moduls.
/// Position relativ zum Modul (Zeilen 0-19, Spalten 0-1 im Adressbereich).
/// </summary>
public record MpCellDefinition(
    int StartRow,       // 0-19 (relativ zum Band-Datenbereich)
    int RowSpan,        // 1, 2 oder 4
    int StartCol,       // 0 = links, 1 = rechts (im Adressbereich)
    int ColSpan,        // 1 = einzelne Spalte, 2 = Col 0+1 gemergt
    bool IsEditable     // false = Leer-/Struktur-Zelle (nicht editierbar)
);

/// <summary>
/// Beschreibt das komplette Zellen-Layout eines ET200MP-Modultyps.
/// Die Zellen-Definitionen sind exakt aus dem Siemens Excel-Template abgeleitet.
/// </summary>
public record MpModuleLayout(
    MpModuleVariant Variant,
    string DisplayName,
    MpCellDefinition[] AddressCells    // Alle Adress-Zellen (Col 0+1 Bereich)
    // Col 2 (NetAddr) und Col 3 (CpuName) sind immer gleich:
    // NetAddr: 2 Bloecke a 10 Zeilen (rows 0-9, 10-19)
    // CpuName: 1 Block ueber 20 Zeilen
);
