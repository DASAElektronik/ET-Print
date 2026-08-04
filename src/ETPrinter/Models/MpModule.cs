using ETPrinter.Services;

namespace ETPrinter.Models;

/// <summary>
/// Ein einzelnes ET200MP-Modul = 1 Beschriftungsstreifen-Position auf dem A4-Bogen.
/// Der Bogen hat 10 Positionen: Band 0 (oben) + Band 1 (unten) mit je 5 Spalten.
/// ModuleIndex 0-4 = Band 0, 5-9 = Band 1 (Band/Spalte via ProductFamilyInfo).
/// </summary>
public class MpModule
{
    public int ModuleIndex { get; set; }    // Position 0-9 auf der Seite
    public MpModuleVariant Variant { get; set; } = MpModuleVariant.DI_DQ_16;

    // Konkretes Siemens-Modul aus MpModuleCatalog (Artikelnummer);
    // null = benutzerdefiniert, nur die Variante bestimmt die Belegung.
    public string? ArticleNumber { get; set; }

    // Modultyp (DI/DO/AI/AO). Ohne Katalog-Artikel bestimmt er die generischen
    // Struktur-Klemmen-Labels: Ausgabemodule haben Versorgung je Kanalgruppe,
    // Eingabemodule nur am Gruppenende. Alte Projektdateien ohne das Feld
    // deserialisieren zu DI — dem bisherigen Verhalten.
    public ModuleType IoType { get; set; } = ModuleType.DI;

    // Header (oben im Streifen)
    public string HeaderText { get; set; } = string.Empty;

    // Adress-Zellen (20 Klemmenzeilen des Streifens)
    public List<MpAddressCell> AddressCells { get; set; } = [];

    // Col 2, 90 Grad rotiert: Net Address (Zeilen 1-10) + Net Name (Zeilen 11-20)
    public string NetAddress1 { get; set; } = string.Empty;
    public string NetAddress2 { get; set; } = string.Empty;

    // Obsolet seit 10-Positionen-Architektur (v5): stammten aus der Aera
    // "1 Modul = 2 Baender". Nur noch fuer die Deserialisierung von v4-Dateien;
    // werden weder angezeigt noch gedruckt noch kopiert.
    public string NetAddress3 { get; set; } = string.Empty;
    public string NetAddress4 { get; set; } = string.Empty;

    // CPU-Name (Col 3, 90 Grad rotiert) — 2 Bloecke (1 pro Haelfte)
    public string CpuName { get; set; } = string.Empty;

    // Font-Einstellungen
    public int FontSize { get; set; } = 7;
    public string FontFamily { get; set; } = "Arial";
    public bool IsBold { get; set; }

    // Selektiver Druck
    public bool IsPrintEnabled { get; set; } = true;

    public bool HasText =>
        !string.IsNullOrWhiteSpace(HeaderText)
        || AddressCells.Any(c => !string.IsNullOrWhiteSpace(c.Text))
        || !string.IsNullOrWhiteSpace(NetAddress1)
        || !string.IsNullOrWhiteSpace(NetAddress2)
        || !string.IsNullOrWhiteSpace(CpuName);

    /// <summary>Kopiert alle Inhalte + Variante in eine neue Instanz.
    /// ModuleIndex wird NICHT kopiert (kommt vom Ziel). AddressCells werden deep-kopiert.</summary>
    public MpModule CloneContent() => new()
    {
        Variant = Variant,
        ArticleNumber = ArticleNumber,
        IoType = IoType,
        HeaderText = HeaderText,
        AddressCells = AddressCells.Select(c => c.Clone()).ToList(),
        NetAddress1 = NetAddress1,
        NetAddress2 = NetAddress2,
        CpuName = CpuName,
        FontSize = FontSize,
        FontFamily = FontFamily,
        IsBold = IsBold,
        IsPrintEnabled = IsPrintEnabled,
    };
}

/// <summary>
/// Eine einzelne Adress-/Klemmen-Zelle innerhalb eines ET200MP-Moduls.
/// </summary>
public class MpAddressCell
{
    public int CellIndex { get; set; }
    public string Text { get; set; } = string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public void Clear() => Text = string.Empty;

    public MpAddressCell Clone() => new() { CellIndex = CellIndex, Text = Text };
}

/// <summary>
/// Container fuer eine Seite von ET200MP-Modulen (5 Module pro Seite).
/// </summary>
public class MpModulePage
{
    public List<MpModule> Modules { get; set; } = [];
}
