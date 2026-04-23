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
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ETPrinter", "calibration.json");

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
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warn($"{ex.GetType().Name}: {ex.Message}");
        }
        return new();
    }

    public static bool Save(CalibrationData data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
