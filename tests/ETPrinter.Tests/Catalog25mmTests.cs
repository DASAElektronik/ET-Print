using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP8: 25mm-BA-Module (Datenblatt-verifiziert 2026-09-09) und
/// Byte-Verteilung des Generators je Variante/Artikel (Golden-Tests).</summary>
public class Catalog25mmTests
{
    private static MpModuleViewModel Module(MpModuleVariant variant, string? article = null, ModuleType io = ModuleType.DI)
    {
        var m = new MpModule { Variant = variant, IoType = io, ArticleNumber = article };
        m.AddressCells = MpModuleLayoutFactory.CreateCells(variant);
        return new MpModuleViewModel(m);
    }

    private static string Label(MpCellDefinition[] cells, int row, int col = 0) =>
        cells.First(c => c.StartRow == row && c.StartCol == col && !c.IsEditable).Label;

    private static string TextAt(MpModuleViewModel vm, int row, int col) =>
        vm.AddressCells.First(c => c.StartRow == row && c.StartCol == col).Text;

    // ---- Familienzuordnung ----------------------------------------------------

    [Theory]
    [InlineData("6ES7521-1BH10-0AA0")] // DI 16 BA
    [InlineData("6ES7522-1BH10-0AA0")] // DQ 16 BA
    [InlineData("6ES7521-1BL10-0AA0")] // DI 32 BA
    [InlineData("6ES7522-1BL10-0AA0")] // DQ 32 BA
    [InlineData("6ES7523-1BL00-0AA0")] // DI16/DQ16 BA
    [InlineData("6ES7532-5NB00-0AB0")] // AQ 2 ST
    public void BaModules_Are25mmFamily(string article)
    {
        var entry = MpModuleCatalog.Find(article)!;
        Assert.True(MpModuleLayoutFactory.Is25mmVariant(entry.Variant));
        Assert.Contains(entry, MpModuleCatalog.EntriesForFamily(ProductFamily.S71500_ET200MP_25mm));
        Assert.DoesNotContain(entry, MpModuleCatalog.EntriesForFamily(ProductFamily.S71500_ET200MP));
    }

    [Fact]
    public void Catalog_Has18Entries()
    {
        Assert.Equal(18, MpModuleCatalog.Entries.Count(e => e.ArticleNo != ""));
    }

    // AP9: Analogmodule per Modulname waehlbar, ohne feste Klemmenbelegung
    [Theory]
    [InlineData("6ES7531-7KF00-0AB0", MpModuleVariant.AI_AQ_8, ModuleType.AI, 10)]
    [InlineData("6ES7531-7NF00-0AB0", MpModuleVariant.AI_AQ_8, ModuleType.AI, 10)]
    [InlineData("6ES7531-7QF00-0AB0", MpModuleVariant.AI_AQ_8, ModuleType.AI, 10)]
    [InlineData("6ES7532-5HD00-0AB0", MpModuleVariant.AQ_4, ModuleType.AO, 5)]
    public void AnalogEntries_FullyEditable35mm(string article, MpModuleVariant variant, ModuleType io, int editable)
    {
        var e = MpModuleCatalog.Find(article)!;
        Assert.Equal(variant, e.Variant);
        Assert.Equal(io, e.IoType);
        Assert.Equal(editable, e.Cells.Count(c => c.IsEditable));
        Assert.Contains(e, MpModuleCatalog.EntriesForFamily(ProductFamily.S71500_ET200MP));
    }

    [Fact]
    public void Generator_Analog8_WithArticle_SplitsColumns()
    {
        var vm = Module(MpModuleVariant.AI_AQ_8, "6ES7531-7NF00-0AB0", ModuleType.AI);
        Assert.Equal(8, MainViewModel.FillMpModuleAddresses(vm, ModuleType.AI, 100, 8));
        Assert.Equal("EW 100", TextAt(vm, 0, 0));
        Assert.Equal("EW 108", TextAt(vm, 0, 1));
    }

    // ---- Klemmenbelegungen --------------------------------------------------------

    [Fact]
    public void Dq16Ba_SupplyPerGroup()
    {
        var c = MpModuleCatalog.Find("6ES7522-1BH10-0AA0")!.Cells;
        Assert.Equal("1L+", Label(c, 8)); Assert.Equal("1M", Label(c, 9));
        Assert.Equal("2L+", Label(c, 18)); Assert.Equal("2M", Label(c, 19));
    }

    [Fact]
    public void Di32Ba_OnlyGroundOnK20AndK40()
    {
        var c = MpModuleCatalog.Find("6ES7521-1BL10-0AA0")!.Cells;
        Assert.Equal("", Label(c, 8, 0)); Assert.Equal("", Label(c, 18, 0)); Assert.Equal("M", Label(c, 19, 0));
        Assert.Equal("", Label(c, 8, 1)); Assert.Equal("", Label(c, 18, 1)); Assert.Equal("M", Label(c, 19, 1));
    }

    [Fact]
    public void Dq32Ba_FourSupplyGroups()
    {
        var c = MpModuleCatalog.Find("6ES7522-1BL10-0AA0")!.Cells;
        Assert.Equal("1L+", Label(c, 8, 0)); Assert.Equal("2L+", Label(c, 18, 0));
        Assert.Equal("3L+", Label(c, 8, 1)); Assert.Equal("4M", Label(c, 19, 1));
    }

    [Fact]
    public void Di16Dq16Ba_InputsLeftOutputsRight()
    {
        var e = MpModuleCatalog.Find("6ES7523-1BL00-0AA0")!;
        Assert.True(e.MixedOutputRightColumn);
        Assert.Equal("", Label(e.Cells, 8, 0)); Assert.Equal("1M", Label(e.Cells, 19, 0));
        Assert.Equal("2L+", Label(e.Cells, 8, 1)); Assert.Equal("2M", Label(e.Cells, 9, 1));
        Assert.Equal("3L+", Label(e.Cells, 18, 1)); Assert.Equal("3M", Label(e.Cells, 19, 1));
    }

    // ---- Generator-Golden-Tests ---------------------------------------------------

    [Fact]
    public void Generator_Mp25_32_Byte2GoesToRightColumn()
    {
        var vm = Module(MpModuleVariant.MP25_32);
        int consumed = MainViewModel.FillMpModuleAddresses(vm, ModuleType.DI, 0, 4);
        Assert.Equal(4, consumed);
        Assert.Equal("E 0.0", TextAt(vm, 0, 0));
        Assert.Equal("E 0.7", TextAt(vm, 7, 0));
        Assert.Equal("E 1.0", TextAt(vm, 10, 0));
        Assert.Equal("E 1.7", TextAt(vm, 17, 0));
        Assert.Equal("E 2.0", TextAt(vm, 0, 1));   // frueher: Zeile 16 links
        Assert.Equal("E 3.7", TextAt(vm, 17, 1));
        Assert.Equal(32, vm.AddressCells.Count(c => c.HasText));
    }

    [Fact]
    public void Generator_Mp25_16_TwoBytes()
    {
        var vm = Module(MpModuleVariant.MP25_16, "6ES7521-1BH10-0AA0");
        Assert.Equal(2, MainViewModel.FillMpModuleAddresses(vm, ModuleType.DI, 4, 2));
        Assert.Equal("E 4.0", TextAt(vm, 0, 0));
        Assert.Equal("E 5.7", TextAt(vm, 17, 0));
        Assert.Equal(16, vm.AddressCells.Count(c => c.HasText));
    }

    [Fact]
    public void Generator_RespectsGenCountBelowCapacity()
    {
        var vm = Module(MpModuleVariant.DI_DQ_32);
        Assert.Equal(1, MainViewModel.FillMpModuleAddresses(vm, ModuleType.DI, 0, 1));
        Assert.Equal(8, vm.AddressCells.Count(c => c.HasText));
        Assert.Equal(4, MainViewModel.FillMpModuleAddresses(vm, ModuleType.DI, 0, 9)); // gekappt
        Assert.Equal(32, vm.AddressCells.Count(c => c.HasText));
    }

    [Fact]
    public void Generator_MixedDiDq_RightColumnGetsOutputPrefix()
    {
        var vm = Module(MpModuleVariant.MP25_32, "6ES7523-1BL00-0AA0");
        int consumed = MainViewModel.FillMpModuleAddresses(vm, ModuleType.DI, 10, 2);
        Assert.Equal(2, consumed);
        Assert.Equal("E 10.0", TextAt(vm, 0, 0));
        Assert.Equal("E 11.7", TextAt(vm, 17, 0));
        Assert.Equal("A 10.0", TextAt(vm, 0, 1));
        Assert.Equal("A 11.7", TextAt(vm, 17, 1));
        Assert.Equal(32, vm.AddressCells.Count(c => c.HasText));
    }

    [Theory]
    [InlineData(MpModuleVariant.DI_DQ_16, null, ModuleType.DI, 16, 2)]
    [InlineData(MpModuleVariant.DI_DQ_32, null, ModuleType.DO, 32, 4)]
    [InlineData(MpModuleVariant.DI_230V_16, "6ES7521-1FH00-0AA0", ModuleType.DI, 16, 2)]
    [InlineData(MpModuleVariant.DI_DQ_32, "6ES7522-1BF00-0AB0", ModuleType.DO, 8, 1)]
    [InlineData(MpModuleVariant.MP25_16, "6ES7522-1BH10-0AA0", ModuleType.DO, 16, 2)]
    [InlineData(MpModuleVariant.MP25_32, "6ES7522-1BL10-0AA0", ModuleType.DO, 32, 4)]
    public void Generator_FillsCapacity(MpModuleVariant variant, string? article, ModuleType io, int filled, int bytes)
    {
        var vm = Module(variant, article, io);
        Assert.Equal(bytes, MainViewModel.FillMpModuleAddresses(vm, io, 0, 8));
        Assert.Equal(filled, vm.AddressCells.Count(c => c.HasText));
    }

    // ---- Laden: Artikel muss zur Familie passen -----------------------------------

    [Fact]
    public void ApplyLoadedProject_DropsArticleOfWrongFamily()
    {
        Sta.Run(() =>
        {
            var page = new MpModulePage();
            for (int i = 0; i < 10; i++)
            {
                var m = new MpModule { ModuleIndex = i, Variant = MpModuleVariant.DI_DQ_16 };
                m.AddressCells = MpModuleLayoutFactory.CreateCells(m.Variant);
                if (i == 0) { m.ArticleNumber = "6ES7521-1BH10-0AA0"; m.HeaderText = "DI16"; m.AddressCells[0].Text = "E 0.0"; }
                page.Modules.Add(m);
            }
            var project = new LabelProject
            {
                ProductFamily = ProductFamily.S71500_ET200MP,
                Format = LabelFormat.MP_Horizontal,
                MpPages = [page]
            };
            var vm = new MainViewModel { SuppressContentLossConfirm = true };
            vm.ApplyLoadedProject(project, null);

            Assert.Null(vm.MpModules[0].ArticleNumber);          // 25mm-Artikel auf 35mm-Bogen abgewaehlt
            Assert.Equal("DI16", vm.MpModules[0].HeaderText);    // Inhalt bleibt
            Assert.Equal("E 0.0", vm.MpModules[0].AddressCells[0].Text);
        });
    }

    [Fact]
    public void SelectingArticle_SetsGenCountToModuleBytes()
    {
        Sta.Run(() =>
        {
            var vm = new MainViewModel { SuppressContentLossConfirm = true };
            vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP_25mm);
            vm.SelectedMpModule = vm.MpModules[0];
            vm.SelectedMpArticle = MpModuleCatalog.Find("6ES7521-1BL10-0AA0"); // DI 32 BA
            Assert.Equal(4, vm.GenCount);
            vm.SelectedMpArticle = MpModuleCatalog.Find("6ES7523-1BL00-0AA0"); // DI16/DQ16
            Assert.Equal(2, vm.GenCount);
        });
    }
}
