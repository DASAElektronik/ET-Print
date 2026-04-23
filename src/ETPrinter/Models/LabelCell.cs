namespace ETPrinter.Models;

public class LabelCell
{
    public int Index { get; set; }
    public string Header { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;

    // Per-Etikett Schrift-Einstellungen
    public int FontSize { get; set; } = 7;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public string FontFamily { get; set; } = "Arial";

    // Selektiver Druck
    public bool IsPrintEnabled { get; set; } = true;

    public bool HasText => !string.IsNullOrWhiteSpace(Header)
                        || !string.IsNullOrWhiteSpace(Line1)
                        || !string.IsNullOrWhiteSpace(Line2);

    public void Clear()
    {
        Header = string.Empty;
        Line1 = string.Empty;
        Line2 = string.Empty;
    }

    /// <summary>Kopiert alle Werte in eine neue Instanz. Index wird NICHT kopiert
    /// (der wird vom Ziel-Etikett uebernommen).</summary>
    public LabelCell CloneContent() => new()
    {
        Header = Header,
        Line1 = Line1,
        Line2 = Line2,
        FontSize = FontSize,
        IsBold = IsBold,
        IsItalic = IsItalic,
        FontFamily = FontFamily,
        IsPrintEnabled = IsPrintEnabled,
    };

    /// <summary>Uebernimmt alle Inhalte aus einer anderen Zelle. Index bleibt.</summary>
    public void CopyFrom(LabelCell source)
    {
        Header = source.Header;
        Line1 = source.Line1;
        Line2 = source.Line2;
        FontSize = source.FontSize;
        IsBold = source.IsBold;
        IsItalic = source.IsItalic;
        FontFamily = source.FontFamily;
        IsPrintEnabled = source.IsPrintEnabled;
    }
}
