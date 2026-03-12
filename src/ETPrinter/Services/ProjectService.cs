using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ETPrinter.Models;

namespace ETPrinter.Services;

public static class ProjectService
{
    private const int MaxRecentFiles = 10;

    private static readonly string RecentFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ETPrinter", "recent.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(LabelProject project, string filePath)
    {
        var json = JsonSerializer.Serialize(project, JsonOptions);
        File.WriteAllText(filePath, json);
        AddRecentFile(filePath);
    }

    public static LabelProject Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var project = JsonSerializer.Deserialize<LabelProject>(json, JsonOptions)
            ?? throw new InvalidDataException("Ungueltige Projektdatei.");
        AddRecentFile(filePath);
        return project;
    }

    public static List<string> LoadRecentFiles()
    {
        try
        {
            if (File.Exists(RecentFilePath))
            {
                var json = File.ReadAllText(RecentFilePath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    private static void AddRecentFile(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var recent = LoadRecentFiles();
        recent.Remove(fullPath);
        recent.Insert(0, fullPath);
        if (recent.Count > MaxRecentFiles)
            recent = recent.GetRange(0, MaxRecentFiles);

        try
        {
            var dir = Path.GetDirectoryName(RecentFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(RecentFilePath, JsonSerializer.Serialize(recent, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
