using System.IO;
using System.Text.Json;

namespace ETPrinter.Services;

public record CalibrationData
{
    public double OffsetX { get; init; } // mm, positiv = nach rechts
    public double OffsetY { get; init; } // mm, positiv = nach unten
}

public static class CalibrationService
{
    private static readonly string FilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "calibration.json");

    public static CalibrationData Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<CalibrationData>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    public static void Save(CalibrationData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
