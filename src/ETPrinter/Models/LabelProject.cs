using System.Text.Json.Serialization;

namespace ETPrinter.Models;

public class LabelProject
{
    public int Version { get; set; } = 4;
    public ProductFamily ProductFamily { get; set; } = ProductFamily.ET200SP;
    public LabelFormat Format { get; set; } = LabelFormat.HorizontalDouble;
    public LabelSettings Settings { get; set; } = new();
    public List<LabelPage> Pages { get; set; } = [];
    public double CalibrationOffsetX { get; set; }
    public double CalibrationOffsetY { get; set; }
    public bool PrintGridLines { get; set; }

    /// <summary>
    /// ET200MP Modul-Seiten (nur fuer modulbasierte Formate).
    /// Null bei ET200SP-Projekten.
    /// </summary>
    public List<MpModulePage>? MpPages { get; set; }

    /// <summary>
    /// Legacy property for deserializing v1 projects that had a flat Labels list.
    /// Not serialized in v2+ output.
    /// </summary>
    [JsonPropertyName("Labels")]
    public List<LabelCell>? LegacyLabels { get; set; }

    /// <summary>
    /// Convenience property: flattened list of all labels across all pages.
    /// </summary>
    [JsonIgnore]
    public List<LabelCell> AllLabels => Pages.SelectMany(p => p.Labels).ToList();
}
