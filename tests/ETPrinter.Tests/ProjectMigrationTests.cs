using System.Text.Json;
using ETPrinter.Models;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

public class ProjectMigrationTests
{
    private static string WriteTempProject(object project)
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_test_{Guid.NewGuid():N}.etprint");
        File.WriteAllText(path, JsonSerializer.Serialize(project));
        return path;
    }

    // v4-MP-Projekte hatten 5 Module pro Seite (nur oberes Band). Seit v5 hat der
    // Bogen 10 Streifen-Positionen — Laden muss auf ModulesPerPage auffuellen.
    [Fact]
    public void Load_V4MpProject_PadsPagesToTenModules()
    {
        var path = WriteTempProject(new
        {
            Version = 4,
            ProductFamily = "S71500_ET200MP",
            Format = "MP_Horizontal",
            MpPages = new[]
            {
                new
                {
                    Modules = Enumerable.Range(0, 5).Select(i => new
                    {
                        ModuleIndex = i,
                        Variant = "DI_DQ_32",
                        HeaderText = $"M{i + 1}",
                    }).ToArray()
                }
            }
        });

        try
        {
            var project = ProjectService.Load(path);

            Assert.Equal(5, project.Version);
            Assert.NotNull(project.MpPages);
            var modules = project.MpPages![0].Modules;
            Assert.Equal(10, modules.Count);
            // Bestehende Module unveraendert, Indizes konsistent zur Position
            Assert.Equal("M1", modules[0].HeaderText);
            Assert.Equal("M5", modules[4].HeaderText);
            for (int i = 0; i < modules.Count; i++)
                Assert.Equal(i, modules[i].ModuleIndex);
            // Aufgefuellte Module sind leer, aber mit Zellen initialisiert
            Assert.False(modules[5].HasText);
            Assert.NotEmpty(modules[5].AddressCells);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_V4SpProject_WithoutMpPages_MigratesCleanly()
    {
        var path = WriteTempProject(new
        {
            Version = 4,
            ProductFamily = "ET200SP",
            Format = "HorizontalDouble",
            Pages = new[] { new { Labels = Array.Empty<object>() } }
        });

        try
        {
            var project = ProjectService.Load(path);
            Assert.Equal(5, project.Version);
            Assert.Null(project.MpPages);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_V5MpProject_WithTenModules_IsUnchanged()
    {
        var path = WriteTempProject(new
        {
            Version = 5,
            ProductFamily = "S71500_ET200MP",
            Format = "MP_Horizontal",
            MpPages = new[]
            {
                new
                {
                    Modules = Enumerable.Range(0, 10).Select(i => new
                    {
                        ModuleIndex = i,
                        Variant = "DI_DQ_16",
                        HeaderText = i == 7 ? "Band2-Modul" : "",
                    }).ToArray()
                }
            }
        });

        try
        {
            var project = ProjectService.Load(path);
            Assert.Equal(10, project.MpPages![0].Modules.Count);
            Assert.Equal("Band2-Modul", project.MpPages[0].Modules[7].HeaderText);
        }
        finally { File.Delete(path); }
    }
}
