namespace ETPrinter.Models;

/// <summary>
/// Ein einzelnes ET200MP-Modul (4 Spalten x 20 Zeilen auf dem Beschriftungsstreifen).
/// Ein Band hat 5 Module, eine Seite hat 2 Baender = 10 Module.
/// </summary>
public class MpModule
{
    public int ModuleIndex { get; set; }    // 0-9 auf der Seite (0-4 = Band 1, 5-9 = Band 2)
    public MpModuleVariant Variant { get; set; } = MpModuleVariant.DI_DQ_16;

    // Header (Zeile 0 des Bandes: merged ueber 4 Spalten)
    public string HeaderText { get; set; } = string.Empty;  // "Device Name\nModule Name\nRack / Slot"

    // Adress-Zellen (Anzahl und Layout abhaengig von Variant)
    public List<MpAddressCell> AddressCells { get; set; } = [];

    // Netzadresse (Col 2, 90 Grad rotiert)
    public string NetAddress1 { get; set; } = string.Empty;  // Block 1: Zeilen 0-9
    public string NetAddress2 { get; set; } = string.Empty;  // Block 2: Zeilen 10-19 (Net Name)

    // CPU-Name (Col 3, 90 Grad rotiert, ueber alle 20 Zeilen)
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
}

/// <summary>
/// Eine einzelne Adress-Zelle innerhalb eines ET200MP-Moduls.
/// Die physische Position und Groesse wird durch die MpCellDefinition bestimmt.
/// </summary>
public class MpAddressCell
{
    public int CellIndex { get; set; }
    public string Text { get; set; } = string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public void Clear() => Text = string.Empty;
}

/// <summary>
/// Container fuer eine Seite von ET200MP-Modulen (10 Module pro Seite).
/// </summary>
public class MpModulePage
{
    public List<MpModule> Modules { get; set; } = [];
}
