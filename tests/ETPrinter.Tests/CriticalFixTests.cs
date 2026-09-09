using System.Text.Json;
using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>Tests zu ABSCHLUSSPLAN AP1 (kritische Bugs: falscher/fehlender Druck, Datenverlust).</summary>
public class CriticalFixTests
{
    private static MainViewModel NewVm() => Sta.Run(() =>
    {
        var vm = new MainViewModel { SuppressContentLossConfirm = true };
        return vm;
    });

    private static void SelectFamily(MainViewModel vm, ProductFamily family) =>
        vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(family);

    // ---- 1.1 "Neu" bei bereits aktivem Standardformat --------------------

    [Fact]
    public void NewProject_WithDefaultFormatAlreadyActive_ClearsAllLabels()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.Equal(LabelFormat.HorizontalDouble, vm.SelectedFormat.Format);
            vm.Labels[0].Line1 = "E 0.0";
            vm.Labels[7].Header = "M7";
            vm.MarkDirty("test");

            vm.NewProjectWithoutConfirm();

            Assert.All(vm.Labels, l => Assert.False(l.HasText));
            Assert.False(vm.IsDirty);
            Assert.Equal(1, vm.PageCount);
        });
    }

    // ---- 1.2 SIWAREX (reiner Pinout) wird gedruckt ------------------------

    [Fact]
    public void HasPrintableContent_SiwarexWithoutHeader_IsTrue()
    {
        var mod = new MpModule { Variant = MpModuleVariant.SIWAREX_WP52x };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        Assert.False(mod.HasText);
        Assert.True(mod.HasPrintableContent);
    }

    [Fact]
    public void HasPrintableContent_EmptyDiDq16_IsFalse()
    {
        // DI/DQ 16 hat feste "L+"/"M"-Labels — die duerfen ein leeres Modul NICHT druckbar machen
        var mod = new MpModule { Variant = MpModuleVariant.DI_DQ_16 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        Assert.False(mod.HasPrintableContent);
    }

    [Fact]
    public void BuildMpDocument_SiwarexOnly_ProducesOnePage()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            vm.MpModules[6].Variant = MpModuleVariant.SIWAREX_WP52x;

            var doc = vm.BuildPrintDocument();
            Assert.Equal(1, doc.Pages.Count);
            Assert.Equal([1], vm.PrintablePerPage());
        });
    }

    // ---- 1.3 Variantenwechsel loescht nicht passenden Katalog-Artikel ------

    [Fact]
    public void VariantChange_ClearsMismatchedArticle()
    {
        var mod = new MpModule { Variant = MpModuleVariant.DI_DQ_16 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        var vm = new MpModuleViewModel(mod) { ArticleNumber = "6ES7522-1BH00-0AB0" }; // DQ 16 ST
        Assert.Equal(MpModuleVariant.DI_DQ_16, vm.Variant);

        vm.Variant = MpModuleVariant.DI_DQ_32;

        Assert.Null(vm.ArticleNumber);
        Assert.Null(mod.ArticleNumber);
        // generische DI-Belegung: K9/K10 leer
        var defs = MpModuleLayoutFactory.GetDefinitions(mod);
        Assert.Equal("", defs[8].Label);
    }

    [Fact]
    public void ArticleSet_KeepsArticleWhenVariantMatches()
    {
        var mod = new MpModule { Variant = MpModuleVariant.DI_DQ_32 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        var vm = new MpModuleViewModel(mod);
        vm.ArticleNumber = "6ES7522-1BH00-0AB0"; // DQ 16 ST -> Variante wechselt auf DI_DQ_16
        Assert.Equal(MpModuleVariant.DI_DQ_16, vm.Variant);
        Assert.Equal("6ES7522-1BH00-0AB0", vm.ArticleNumber);
        Assert.Equal("1L+", MpModuleLayoutFactory.GetDefinitions(mod)[8].Label);
    }

    // ---- 1.4 Leere Seiten werden nicht gedruckt ----------------------------

    [Fact]
    public void BuildDocument_SkipsEmptyPages()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Labels[0].Line1 = "E 0.0";
            vm.AddPageCommand.Execute(null);   // Seite 2 leer
            vm.AddPageCommand.Execute(null);   // Seite 3 leer
            Assert.Equal(3, vm.PageCount);

            var doc = vm.BuildPrintDocument();
            Assert.Equal(1, doc.Pages.Count);
            Assert.Equal([1, 0, 0], vm.PrintablePerPage());
        });
    }

    [Fact]
    public void BuildDocument_NoContent_HasZeroPages()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.Equal(0, vm.BuildPrintDocument().Pages.Count);
        });
    }

    // ---- 1.5 PDF-Import: 32-Kanal-Modul auf zwei Etiketten ---------------

    [Fact]
    public void BuildImportCells_32ChannelDigital_SplitsIntoTwoLabels()
    {
        var cells = MainViewModel.BuildImportCells("DI32", ModuleType.DI, 0, 32, [], isDoubleLine: true);

        Assert.Equal(2, cells.Count);
        Assert.Contains("E 0.1", cells[0].Line1);
        Assert.Contains("E 1.7", cells[0].Line1);
        Assert.Contains("E 2.1", cells[1].Line1);
        Assert.Contains("E 3.6", cells[1].Line2);
        Assert.DoesNotContain("E 2.", cells[0].Line1 + cells[0].Line2);
    }

    [Fact]
    public void BuildImportCells_SingleLineFormat_MergesLine2IntoLine1()
    {
        var cells = MainViewModel.BuildImportCells("DI8", ModuleType.DI, 4, 8, [], isDoubleLine: false);

        Assert.Single(cells);
        Assert.Equal(string.Empty, cells[0].Line2);
        Assert.StartsWith("E 4.0  E 4.1", cells[0].Line1);
    }

    [Fact]
    public void BuildImportCells_Analog20Channels_SplitsAt16()
    {
        var cells = MainViewModel.BuildImportCells("AI", ModuleType.AI, 100, 20, [], isDoubleLine: true);
        Assert.Equal(2, cells.Count);
        Assert.Contains("EW 100", cells[0].Line2);
        Assert.Contains("EW 132", cells[1].Line2); // Kanal 16 = 100 + 16*2
    }

    // ---- 1.6 Atomares Speichern -------------------------------------------

    [Fact]
    public void WriteAtomic_LeavesNoTempFile_AndKeepsBackup()
    {
        string dir = Path.Combine(Path.GetTempPath(), "etprinter_atomic_" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "p.etprint");
        try
        {
            ProjectService.WriteAtomic(path, "v1", keepBackup: true);
            Assert.Equal("v1", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.False(File.Exists(path + ".bak"));

            ProjectService.WriteAtomic(path, "v2", keepBackup: true);
            Assert.Equal("v2", File.ReadAllText(path));
            Assert.Equal("v1", File.ReadAllText(path + ".bak"));
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ---- 2.20 Null-Felder in handeditierten Dateien -------------------------

    [Fact]
    public void Load_NullPagesAndSettings_DoesNotThrow()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_null_{Guid.NewGuid():N}.etprint");
        File.WriteAllText(path, """{"Version":5,"Pages":null,"Settings":null}""");
        try
        {
            var project = ProjectService.Load(path, addToRecent: false);
            Assert.NotNull(project.Settings);
            Assert.Single(project.Pages);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_V4Project25mm_PadsWithFamilyDefaultVariant()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_v4_25_{Guid.NewGuid():N}.etprint");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Version = 4,
            ProductFamily = "S71500_ET200MP_25mm",
            Format = "MP25_Horizontal",
            MpPages = new[] { new { Modules = new[] { new { ModuleIndex = 0, Variant = "MP25_16", HeaderText = "A" } } } }
        }));
        try
        {
            var project = ProjectService.Load(path, addToRecent: false);
            var modules = project.MpPages![0].Modules;
            Assert.Equal(20, modules.Count);
            Assert.All(modules.Skip(1), m => Assert.Equal(MpModuleVariant.MP25_16, m.Variant));
        }
        finally { File.Delete(path); }
    }

    // ---- 1.8 Zellauswahl folgt Modulwechsel und Zell-Neuaufbau ------------

    [Fact]
    public void SelectedMpCell_ResetsOnModuleChange()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            vm.SelectedMpModule = vm.MpModules[0];
            vm.SelectedMpCell = vm.MpModules[0].AddressCells[0];
            Assert.NotNull(vm.SelectedMpCell);

            vm.SelectedMpModule = vm.MpModules[1];
            Assert.Null(vm.SelectedMpCell);
        });
    }

    [Fact]
    public void SelectedMpCell_ReresolvedAfterVariantChange()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            var mod = vm.MpModules[0];
            vm.SelectedMpModule = mod;
            var oldCell = mod.AddressCells[3];
            vm.SelectedMpCell = oldCell;

            mod.Variant = MpModuleVariant.DI_DQ_32;

            Assert.NotNull(vm.SelectedMpCell);
            Assert.NotSame(oldCell, vm.SelectedMpCell);
            Assert.Equal(3, vm.SelectedMpCell!.CellIndex);
            Assert.Same(mod.AddressCells[3], vm.SelectedMpCell);
        });
    }

    // ---- 1.9 Schrift-Einstellungen wirken im MP-Modus --------------------

    [Fact]
    public void FontInputs_ApplyToSelectedMpModule()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            var mod = vm.MpModules[2];
            vm.SelectedMpModule = mod;

            vm.Panel.FontSize = 9;
            vm.Panel.IsBold = true;
            vm.Panel.IsItalic = true;

            Assert.Equal(9, mod.FontSize);
            Assert.True(mod.IsBold);
            Assert.True(mod.IsItalic);
            Assert.True(vm.IsDirty);

            // Modulwechsel laedt die Werte des neuen Moduls in die Eingabefelder
            vm.SelectedMpModule = vm.MpModules[3];
            Assert.Equal(7, vm.Panel.FontSize);
            Assert.False(vm.Panel.IsBold);
            Assert.Equal(7, vm.MpModules[3].FontSize); // Laden darf nicht zurueckschreiben
        });
    }

    // ---- 1.10 Import-Commands im MP-Modus ---------------------------------

    [Fact]
    public void CsvAndExcelImport_DisabledInModuleMode()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.True(vm.ImportCsvCommand.CanExecute(null));
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            Assert.False(vm.ImportCsvCommand.CanExecute(null));
            Assert.False(vm.ImportExcelCommand.CanExecute(null));
            Assert.True(vm.ImportSchematicCommand.CanExecute(null));
        });
    }

    // ---- 1.11 Laden aktualisiert Varianten/Artikel der Familie -------------

    [Fact]
    public void ApplyLoadedProject_25mm_RefreshesVariantsAndArticles()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.Contains(vm.AvailableMpVariants, v => v.Variant == MpModuleVariant.DI_DQ_16);

            var project = new LabelProject
            {
                ProductFamily = ProductFamily.S71500_ET200MP_25mm,
                Format = LabelFormat.MP25_Horizontal,
                MpPages = [new MpModulePage()]
            };
            vm.ApplyLoadedProject(project, null);

            Assert.All(vm.AvailableMpVariants, v => Assert.True(MpModuleLayoutFactory.Is25mmVariant(v.Variant)));
            Assert.Contains(vm.AvailableMpArticles, e => e.ArticleNo == "6ES7532-5NB00-0AB0");
            Assert.DoesNotContain(vm.AvailableMpArticles, e => e.ArticleNo == "6ES7521-1BL00-0AB0");
            Assert.Equal(20, vm.MpModules.Count);
        });
    }

    // ---- 1.12 Texte in gesperrten Zellen werden geleert -------------------

    [Fact]
    public void Rebuild_ClearsTextInNonEditableCells()
    {
        var mod = new MpModule { Variant = MpModuleVariant.AI_AQ_8 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        var vm = new MpModuleViewModel(mod);
        for (int i = 0; i < mod.AddressCells.Count; i++) mod.AddressCells[i].Text = $"T{i}";

        vm.Variant = MpModuleVariant.DI_DQ_16; // Zellen 8/9 und 18/19 sind Struktur

        var defs = MpModuleLayoutFactory.GetDefinitions(mod);
        for (int i = 0; i < mod.AddressCells.Count; i++)
        {
            if (!defs[i].IsEditable)
                Assert.Equal(string.Empty, mod.AddressCells[i].Text);
        }
        Assert.Equal("T0", mod.AddressCells[0].Text);
    }

    // ---- Generator-Kern (gemeinsam fuer Generator + PDF-Import) ------------

    [Fact]
    public void FillMpModuleAddresses_DiDq32_Fills32AndReturns4Bytes()
    {
        var mod = new MpModule { Variant = MpModuleVariant.DI_DQ_32 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        var vm = new MpModuleViewModel(mod);

        int consumed = MainViewModel.FillMpModuleAddresses(vm, ModuleType.DI, 10, 4);

        Assert.Equal(4, consumed);
        Assert.Equal(32, vm.AddressCells.Count(c => c.HasText));
        Assert.Equal("E 10.0", vm.AddressCells[0].Text);
        Assert.Equal("E 11.0", vm.AddressCells[10].Text); // Zeile 10 = Byte 1
        Assert.Equal("E 12.0", vm.AddressCells[20].Text); // Spalte 1 = Byte 2
    }

    [Fact]
    public void FillMpModuleAddresses_Mp25_16_UsesGenCount()
    {
        var mod = new MpModule { Variant = MpModuleVariant.MP25_16 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        var vm = new MpModuleViewModel(mod);

        int consumed = MainViewModel.FillMpModuleAddresses(vm, ModuleType.DO, 0, 1);

        Assert.Equal(1, consumed);
        Assert.Equal(8, vm.AddressCells.Count(c => c.HasText));
    }

    [Fact]
    public void FillMpModuleAddresses_Analog8_SplitsFourPerColumn()
    {
        var mod = new MpModule { Variant = MpModuleVariant.AI_AQ_8 };
        mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
        var vm = new MpModuleViewModel(mod);

        int consumed = MainViewModel.FillMpModuleAddresses(vm, ModuleType.AI, 256, 8);

        Assert.Equal(8, consumed);
        var left = vm.AddressCells.Where(c => c.StartCol == 0).ToList();
        var right = vm.AddressCells.Where(c => c.StartCol == 1).ToList();
        Assert.Equal(4, left.Count(c => c.HasText));
        Assert.Equal(4, right.Count(c => c.HasText));
        Assert.Equal("EW 256", left[0].Text);
        Assert.Equal("EW 264", right[0].Text);
    }

    // ---- Persistenz: IsItalic additiv -----------------------------------

    [Fact]
    public void MpModule_CloneContent_CopiesItalic()
    {
        var mod = new MpModule { IsItalic = true, IsBold = true };
        var clone = mod.CloneContent();
        Assert.True(clone.IsItalic);
        Assert.True(clone.IsBold);
    }
}
