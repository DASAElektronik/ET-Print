using System.Text.Json;
using ETPrinter.Models;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP3: Migration v1-v3 und vollstaendiger Roundtrip.</summary>
[Collection("RecentFiles")] // gemeinsame recent.json: nicht parallel
public class PersistenceTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"etprinter_persist_{Guid.NewGuid():N}.etprint");

    private static LabelProject LoadJson(object json)
    {
        string path = TempPath();
        File.WriteAllText(path, JsonSerializer.Serialize(json));
        try { return ProjectService.Load(path, addToRecent: false); }
        finally { File.Delete(path); }
    }

    // ---- v1: flache Labels-Liste ---------------------------------------------

    [Fact]
    public void Load_V1FlatLabels_BecomesSinglePage()
    {
        var project = LoadJson(new
        {
            Version = 1,
            Format = "HorizontalDouble",
            Labels = new object[]
            {
                new { Index = 0, Line1 = "E 0.0" },
                new { Index = 1, Line1 = "E 0.1", Header = "M1" }
            }
        });

        Assert.Equal(5, project.Version);
        Assert.Single(project.Pages);
        Assert.Equal(2, project.Pages[0].Labels.Count);
        Assert.Equal("E 0.1", project.Pages[0].Labels[1].Line1);
        Assert.Equal(ProductFamily.ET200SP, project.ProductFamily);
        Assert.Null(project.MpPages);
        Assert.Null(project.LegacyLabels);
    }

    [Fact]
    public void Load_MissingVersion_TreatedAsLegacyWhenLabelsPresent()
    {
        // Datei ohne "Version", aber mit altem "Labels"-Feld -> Migration greift
        var project = LoadJson(new { Labels = new[] { new { Index = 0, Line1 = "x" } } });
        Assert.Single(project.Pages);
        Assert.Equal("x", project.Pages[0].Labels[0].Line1);
    }

    // ---- v2: Pages, aber keine Familie ----------------------------------------

    [Fact]
    public void Load_V2Pages_GetsProductFamily()
    {
        var project = LoadJson(new
        {
            Version = 2,
            Format = "VerticalSingle",
            Pages = new[] { new { Labels = new[] { new { Index = 0, Line1 = "a" } } },
                            new { Labels = new[] { new { Index = 0, Line1 = "b" } } } }
        });

        Assert.Equal(5, project.Version);
        Assert.Equal(ProductFamily.ET200SP, project.ProductFamily);
        Assert.Equal(2, project.Pages.Count);
        Assert.Equal(LabelFormat.VerticalSingle, project.Format);
    }

    // ---- v3: Familie, keine MpPages -------------------------------------------

    [Fact]
    public void Load_V3_KeepsFamilyAndSettings()
    {
        var project = LoadJson(new
        {
            Version = 3,
            ProductFamily = "ET200SP",
            Format = "HorizontalSingle",
            Settings = new { FontSize = 8, MarginTop = 21.0, MarginLeft = 28.0 },
            Pages = new[] { new { Labels = Array.Empty<object>() } }
        });

        Assert.Equal(5, project.Version);
        Assert.Equal(8, project.Settings.FontSize);
        Assert.Equal(21.0, project.Settings.MarginTop);
        Assert.Equal(9, project.Settings.HeaderFontSize); // Default fuer fehlendes Feld
        Assert.Null(project.MpPages);
    }

    [Fact]
    public void Load_EmptyPagesList_GetsOnePage()
    {
        var project = LoadJson(new { Version = 5, Pages = Array.Empty<object>() });
        Assert.Single(project.Pages);
    }

    // ---- Roundtrip mit allen v5-Feldern ----------------------------------------

    [Fact]
    public void SaveLoad_Roundtrip_PreservesAllFields()
    {
        var mod = new MpModule
        {
            ModuleIndex = 3,
            Variant = MpModuleVariant.DI_DQ_16,
            ArticleNumber = "6ES7522-1BH00-0AB0",
            IoType = ModuleType.DO,
            HeaderText = "DQ16\nSlot 4",
            NetAddress1 = "192.168.0.1",
            CpuName = "PLC1",
            FontSize = 8,
            IsBold = true,
            IsItalic = true,
            IsPrintEnabled = false
        };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        mod.AddressCells[0].Text = "A 4.0";

        var page = new MpModulePage();
        for (int i = 0; i < 10; i++)
        {
            if (i == 3) { page.Modules.Add(mod); continue; }
            var m = new MpModule { ModuleIndex = i };
            m.AddressCells = MpModuleLayoutFactory.CreateCells(m.Variant);
            page.Modules.Add(m);
        }

        var project = new LabelProject
        {
            ProductFamily = ProductFamily.S71500_ET200MP,
            Format = LabelFormat.MP_Vertical,
            Settings = new LabelSettings { FontSize = 6, HeaderFontSize = 10, HeaderIsBold = false, MarginTop = 15.5 },
            Pages = [new LabelPage { Labels = [new LabelCell { Index = 0, Line1 = "x", IsPrintEnabled = false, IsItalic = true }] }],
            MpPages = [page],
            CalibrationOffsetX = 1.5,
            CalibrationOffsetY = -0.5,
            PrintGridLines = true
        };

        string path = TempPath();
        try
        {
            ProjectService.Save(project, path, addToRecent: false);
            var loaded = ProjectService.Load(path, addToRecent: false);

            Assert.Equal(ProductFamily.S71500_ET200MP, loaded.ProductFamily);
            Assert.Equal(LabelFormat.MP_Vertical, loaded.Format);
            Assert.Equal(6, loaded.Settings.FontSize);
            Assert.Equal(10, loaded.Settings.HeaderFontSize);
            Assert.False(loaded.Settings.HeaderIsBold);
            Assert.Equal(15.5, loaded.Settings.MarginTop);
            Assert.Equal(1.5, loaded.CalibrationOffsetX);
            Assert.Equal(-0.5, loaded.CalibrationOffsetY);
            Assert.True(loaded.PrintGridLines);

            var l = loaded.Pages[0].Labels[0];
            Assert.False(l.IsPrintEnabled);
            Assert.True(l.IsItalic);

            var m = loaded.MpPages![0].Modules[3];
            Assert.Equal("6ES7522-1BH00-0AB0", m.ArticleNumber);
            Assert.Equal(ModuleType.DO, m.IoType);
            Assert.Equal("DQ16\nSlot 4", m.HeaderText);
            Assert.Equal("192.168.0.1", m.NetAddress1);
            Assert.Equal("PLC1", m.CpuName);
            Assert.Equal(8, m.FontSize);
            Assert.True(m.IsBold);
            Assert.True(m.IsItalic);
            Assert.False(m.IsPrintEnabled);
            Assert.Equal("A 4.0", m.AddressCells[0].Text);
            Assert.Equal(3, m.ModuleIndex);

            // .bak erst beim zweiten Speichern, .tmp nie
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }

    [Fact]
    public void Save_DoesNotWriteLegacyLabels()
    {
        var project = new LabelProject { LegacyLabels = [new LabelCell { Line1 = "alt" }] };
        string path = TempPath();
        try
        {
            ProjectService.Save(project, path, addToRecent: false);
            string json = File.ReadAllText(path);
            Assert.DoesNotContain("\"Labels\"", json);
            Assert.Contains("\"Version\": 5", json);
        }
        finally { File.Delete(path); }
    }
}
