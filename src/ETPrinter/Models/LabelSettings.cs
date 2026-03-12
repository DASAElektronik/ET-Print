namespace ETPrinter.Models;

public class LabelSettings
{
    public int FontSize { get; set; } = 7;
    public bool IsBold { get; set; } = false;
    public bool IsItalic { get; set; } = false;
    public string FontFamily { get; set; } = "Arial";
    public double MarginTop { get; set; } = 23.5;    // mm (20x12.5=250mm Etikettenhoehe)
    public double MarginLeft { get; set; } = 25.0;   // mm (5x32=160mm Etikettenbreite)
    public double MarginBottom { get; set; } = 23.5;  // mm
    public double MarginRight { get; set; } = 25.0;   // mm

    public void Reset()
    {
        FontSize = 7;
        IsBold = false;
        IsItalic = false;
        FontFamily = "Arial";
        MarginTop = 23.5;
        MarginLeft = 25.0;
        MarginBottom = 23.5;
        MarginRight = 25.0;
    }

    public LabelSettings Clone() => new()
    {
        FontSize = FontSize,
        IsBold = IsBold,
        IsItalic = IsItalic,
        FontFamily = FontFamily,
        MarginTop = MarginTop,
        MarginLeft = MarginLeft,
        MarginBottom = MarginBottom,
        MarginRight = MarginRight
    };
}
