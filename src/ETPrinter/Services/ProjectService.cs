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
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Save(LabelProject project, string filePath)
    {
        // Ensure LegacyLabels is null so it won't be serialized in v2 format
        project.LegacyLabels = null;
        var json = JsonSerializer.Serialize(project, JsonOptions);
        File.WriteAllText(filePath, json);
        AddRecentFile(filePath);
    }

    public static LabelProject Load(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var project = JsonSerializer.Deserialize<LabelProject>(json, JsonOptions)
            ?? throw new InvalidDataException("Ungueltige Projektdatei.");

        // v1 migration: flat Labels list -> single LabelPage
        if (project.Version < 2 || (project.Pages.Count == 0 && project.LegacyLabels?.Count > 0))
        {
            project.Pages = [new LabelPage { Labels = project.LegacyLabels ?? [] }];
            project.Version = 2;
        }

        // v2 -> v3 migration: add ProductFamily (default ET200SP)
        if (project.Version < 3)
        {
            project.ProductFamily = ProductFamily.ET200SP;
            project.Version = 3;
        }

        // v3 -> v4 migration: add MpPages support
        if (project.Version < 4)
        {
            project.Version = 4;
            // MpPages bleibt null fuer ET200SP-Projekte
        }

        // Ensure at least one page exists
        if (project.Pages.Count == 0)
        {
            project.Pages = [new LabelPage()];
        }

        // Clear legacy data after migration
        project.LegacyLabels = null;

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
