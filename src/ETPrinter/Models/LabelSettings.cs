namespace ETPrinter.Models;

public class LabelSettings
{
    public int FontSize { get; set; } = 7;
    public bool IsBold { get; set; } = false;
    public bool IsItalic { get; set; } = false;
    public double MarginTop { get; set; } = 20.0;    // mm
    public double MarginLeft { get; set; } = 30.0;   // mm
    public double MarginBottom { get; set; } = 21.0;  // mm
    public double MarginRight { get; set; } = 25.0;   // mm

    public void Reset()
    {
        FontSize = 7;
        IsBold = false;
        IsItalic = false;
        MarginTop = 20.0;
        MarginLeft = 30.0;
        MarginBottom = 21.0;
        MarginRight = 25.0;
    }

    public LabelSettings Clone() => new()
    {
        FontSize = FontSize,
        IsBold = IsBold,
        IsItalic = IsItalic,
        MarginTop = MarginTop,
        MarginLeft = MarginLeft,
        MarginBottom = MarginBottom,
        MarginRight = MarginRight
    };
}
