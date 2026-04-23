namespace ETPrinter.Models;

/// <summary>
/// Ein einzelnes ET200MP-Modul = 1 physischer Beschriftungsstreifen.
/// Hat 2 Haelften (obere + untere), beide gehoeren zum selben Modul.
/// Obere Haelfte = Klemmen 1-20 (Gruppen a+c), Untere = Klemmen 11-20/21-40 (Gruppen b+d).
/// 5 Module pro A4-Seite (5 Spalten).
/// </summary>
public class MpModule
{
    public int ModuleIndex { get; set; }    // 0-4 auf der Seite (= Spalte)
    public MpModuleVariant Variant { get; set; } = MpModuleVariant.DI_DQ_16;

    // Header (erscheint ZWEIMAL — oben in Haelfte 1 und oben in Haelfte 2)
    public string HeaderText { get; set; } = string.Empty;

    // Adress-Zellen (obere Haelfte + untere Haelfte zusammen)
    public List<MpAddressCell> AddressCells { get; set; } = [];

    // Netzadresse (Col 2, 90 Grad rotiert) — 4 Bloecke (2 pro Haelfte)
    public string NetAddress1 { get; set; } = string.Empty;  // Obere Haelfte, Block 1 (Zeilen 0-9)
    public string NetAddress2 { get; set; } = string.Empty;  // Obere Haelfte, Block 2 (Zeilen 10-19)
    public string NetAddress3 { get; set; } = string.Empty;  // Untere Haelfte, Block 1
    public string NetAddress4 { get; set; } = string.Empty;  // Untere Haelfte, Block 2

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
        || !string.IsNullOrWhiteSpace(CpuName);

    /// <summary>Kopiert alle Inhalte + Variante in eine neue Instanz.
    /// ModuleIndex wird NICHT kopiert (kommt vom Ziel). AddressCells werden deep-kopiert.</summary>
    public MpModule CloneContent() => new()
    {
        Variant = Variant,
        HeaderText = HeaderText,
        AddressCells = AddressCells.Select(c => c.Clone()).ToList(),
        NetAddress1 = NetAddress1,
        NetAddress2 = NetAddress2,
        NetAddress3 = NetAddress3,
        NetAddress4 = NetAddress4,
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
