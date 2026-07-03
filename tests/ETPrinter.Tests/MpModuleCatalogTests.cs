using ETPrinter.Models;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

public class MpModuleCatalogTests
{
    [Fact]
    public void Entries_HaveUniqueArticleNumbers()
    {
        var numbers = MpModuleCatalog.Entries.Select(e => e.ArticleNo).ToList();
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }

    [Fact]
    public void Entries_CellCountMatchesVariantLayout()
    {
        // Persistenz und Zellen-Migration setzen voraus, dass Katalog-Belegungen
        // dieselbe Zellenzahl haben wie das Varianten-Layout.
        foreach (var entry in MpModuleCatalog.Entries.Where(e => e.ArticleNo != ""))
        {
            var variantCells = MpModuleLayoutFactory.GetLayout(entry.Variant).AddressCells;
            Assert.Equal(variantCells.Length, entry.Cells.Length);
        }
    }

    // Klemmenbelegungen aus den Equipment-Manual-Blockdiagrammen pinnen.
    private static string LabelAt(MpCellDefinition[] cells, int startRow, int startCol = 0) =>
        cells.First(c => c.StartRow == startRow && c.StartCol == startCol && !c.IsEditable).Label;

    [Fact]
    public void Dq16St_HasSupplyOnK9K10AndK19K20()
    {
        var cells = MpModuleCatalog.Find("6ES7522-1BH00-0AB0")!.Cells;
        Assert.Equal("1L+", LabelAt(cells, 8));
        Assert.Equal("1M", LabelAt(cells, 9));
        Assert.Equal("2L+", LabelAt(cells, 18));
        Assert.Equal("2M", LabelAt(cells, 19));
    }

    [Fact]
    public void Di16Ba_HasOnlyGroundOnK20()
    {
        var cells = MpModuleCatalog.Find("6ES7521-1BH10-0AA0")!.Cells;
        Assert.Equal("", LabelAt(cells, 8));
        Assert.Equal("", LabelAt(cells, 9));
        Assert.Equal("", LabelAt(cells, 18));
        Assert.Equal("M", LabelAt(cells, 19));
    }

    [Fact]
    public void Di32Hf_HasEmptyK9K29_SupplyPerGroup()
    {
        var cells = MpModuleCatalog.Find("6ES7521-1BL00-0AB0")!.Cells;
        Assert.Equal("", LabelAt(cells, 8, startCol: 0));   // K9
        Assert.Equal("", LabelAt(cells, 8, startCol: 1));   // K29
        Assert.Equal("1L+", LabelAt(cells, 18, startCol: 0)); // K19
        Assert.Equal("2L+", LabelAt(cells, 18, startCol: 1)); // K39
        Assert.Equal("1M", LabelAt(cells, 19, startCol: 0));  // K20
        Assert.Equal("2M", LabelAt(cells, 19, startCol: 1));  // K40
    }

    [Fact]
    public void GetDefinitions_WithArticle_ReturnsCatalogCells()
    {
        var module = new MpModule
        {
            Variant = MpModuleVariant.DI_DQ_16,
            ArticleNumber = "6ES7522-1BH00-0AB0"
        };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);
        Assert.Equal("1L+", LabelAt(defs, 8));
    }

    [Fact]
    public void GetDefinitions_WithoutArticle_ReturnsVariantDefault()
    {
        var module = new MpModule { Variant = MpModuleVariant.DI_DQ_16 };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);
        Assert.Equal("", LabelAt(defs, 8));      // generisch: K9 leer
        Assert.Equal("L+", LabelAt(defs, 18));   // generisch: K19 = L+
    }

    [Fact]
    public void GetDefinitions_ArticleVariantMismatch_FallsBackToVariant()
    {
        // Artikel gehoert zu DI_DQ_16, Modul steht aber auf DI_DQ_32:
        // Varianten-Layout gewinnt (Katalog-Zellenzahl wuerde nicht passen).
        var module = new MpModule
        {
            Variant = MpModuleVariant.DI_DQ_32,
            ArticleNumber = "6ES7522-1BH00-0AB0"
        };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);
        Assert.Equal(MpModuleLayoutFactory.GetLayout(MpModuleVariant.DI_DQ_32).AddressCells.Length,
            defs.Length);
    }
}
