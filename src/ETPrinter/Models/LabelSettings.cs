namespace ETPrinter.Models;

public class LabelSettings
{
    public int FontSize { get; set; } = 7;
    public bool IsBold { get; set; } = false;
    public bool IsItalic { get; set; } = false;
    public string FontFamily { get; set; } = "Arial";
    public double MarginTop { get; set; } = 20.5;    // mm (20x12.8=256mm Etikettenhoehe)
    public double MarginLeft { get; set; } = 27.5;   // mm (5x31=155mm Etikettenbreite)
    public double MarginBottom { get; set; } = 20.5;  // mm
    public double MarginRight { get; set; } = 27.5;   // mm

    public void Reset() => ResetForFamily(ProductFamily.ET200SP);

    public void ResetForFamily(ProductFamily family)
    {
        FontSize = 7;
        IsBold = false;
        IsItalic = false;
        FontFamily = "Arial";

        var info = ProductFamilyDefinitions.Get(family);
        MarginTop = info.DefaultMarginTop;
        MarginLeft = info.DefaultMarginLeft;
        MarginBottom = info.DefaultMarginBottom;
        MarginRight = info.DefaultMarginRight;
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
