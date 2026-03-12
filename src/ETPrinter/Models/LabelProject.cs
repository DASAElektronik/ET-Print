namespace ETPrinter.Models;

public class LabelProject
{
    public LabelFormat Format { get; set; } = LabelFormat.HorizontalDouble;
    public LabelSettings Settings { get; set; } = new();
    public List<LabelCell> Labels { get; set; } = [];
}
