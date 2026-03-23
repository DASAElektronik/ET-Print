namespace ETPrinter.Models;

public enum MpModuleVariant
{
    DI_DQ_32,       // 32 DI/DQ: 4 Bytes, 8 CH pro Gruppe, M/L+ Klemmen
    DI_DQ_16,       // 16 DI/DQ: 2 Bytes, Col 0+1 gemergt
    DI_230V_16,     // 16 DI 230V: 2 Zeilen pro Kanal-Paar
    DQ_230V_8,      // 8 DQ 230V: Gemischtes Pattern
    AI_AQ_8,        // 8 AI/AQ: 4 Zeilen pro Analogkanal, 2 Spalten
    AQ_4            // 4 AQ: 4 Zeilen pro Kanal, Col 0+1 gemergt
}

/// <summary>
/// Definition einer einzelnen Zelle im Beschriftungsstreifen.
/// Position relativ zur jeweiligen Modulhaelfte (0=oben, 1=unten).
/// </summary>
public record MpCellDefinition(
    int Half,           // 0 = obere Haelfte, 1 = untere Haelfte
    int StartRow,       // 0-19 (relativ zur Haelfte)
    int RowSpan,        // 1, 2 oder 4
    int StartCol,       // 0 = links, 1 = rechts
    int ColSpan,        // 1 = einzelne Spalte, 2 = Col 0+1 gemergt
    bool IsEditable,    // false = M/L+/leer (Struktur-Zelle)
    string Label = ""   // Vorbelegung fuer Strukturzellen (z.B. "M", "L+", "1M")
);

/// <summary>
/// Beschreibt das komplette Zellen-Layout eines ET200MP-Modultyps.
/// Umfasst BEIDE Haelften des Beschriftungsstreifens.
/// </summary>
public record MpModuleLayout(
    MpModuleVariant Variant,
    string DisplayName,
    MpCellDefinition[] AddressCells
);
