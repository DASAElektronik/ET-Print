namespace ETPrinter.Models;

public class LabelCell
{
    public int Index { get; set; }
    public string Header { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;

    public bool HasText => !string.IsNullOrWhiteSpace(Header)
                        || !string.IsNullOrWhiteSpace(Line1)
                        || !string.IsNullOrWhiteSpace(Line2);

    public void Clear()
    {
        Header = string.Empty;
        Line1 = string.Empty;
        Line2 = string.Empty;
    }
}
