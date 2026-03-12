namespace ETPrinter.Models;

public class LabelProject
{
    public int Version { get; set; } = 1;
    public LabelFormat Format { get; set; } = LabelFormat.HorizontalDouble;
    public LabelSettings Settings { get; set; } = new();
    public List<LabelCell> Labels { get; set; } = [];
    public double CalibrationOffsetX { get; set; }
    public double CalibrationOffsetY { get; set; }
    public bool PrintGridLines { get; set; }
}
