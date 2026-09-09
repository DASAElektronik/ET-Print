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
    public void Dq8_2aHf_OnlyEightChannelsEditable()
    {
        // Blockdiagramm 59193089: nur K1-K8 sind Kanaele. K11-K18 und die
        // komplette rechte Reihe sind unbelegt -> gesperrt, damit der Generator
        // ein Byte statt vier fuellt.
        var cells = MpModuleCatalog.Find("6ES7522-1BF00-0AB0")!.Cells;

        Assert.Equal(8, cells.Count(c => c.IsEditable));
        Assert.All(cells.Where(c => c.IsEditable), c =>
        {
            Assert.Equal(0, c.StartCol);
            Assert.InRange(c.StartRow, 0, 7);
        });
        Assert.All(cells.Where(c => c.StartCol == 1), c => Assert.False(c.IsEditable));
    }

    [Fact]
    public void Dq8_2aHf_HasSupplyOnK9K10AndK19K20()
    {
        var cells = MpModuleCatalog.Find("6ES7522-1BF00-0AB0")!.Cells;
        Assert.Equal("1L+", LabelAt(cells, 8));
        Assert.Equal("1M", LabelAt(cells, 9));
        Assert.Equal("2L+", LabelAt(cells, 18));
        Assert.Equal("2M", LabelAt(cells, 19));
        // rechte Klemmenreihe ohne Labels
        Assert.Equal("", LabelAt(cells, 8, startCol: 1));
        Assert.Equal("", LabelAt(cells, 18, startCol: 1));
    }

    [Fact]
    public void Di16_230V_Ba_HasNoStructureLabels()
    {
        // Manual 59193398: K9/K10 und K19/K20 sind unbelegt; xN teilt sich mit dem
        // letzten Kanal einen 2-Zeilen-Block -> keine Labels (wie im Excel-Template).
        var entry = MpModuleCatalog.Find("6ES7521-1FH00-0AA0")!;
        Assert.Equal(MpModuleVariant.DI_230V_16, entry.Variant);
        Assert.All(entry.Cells.Where(c => !c.IsEditable), c => Assert.Equal("", c.Label));
    }

    [Fact]
    public void Aq2_25mm_IsAnalogOutputAndFullyEditable()
    {
        // Analog ist modusabhaengig verdrahtet -> keine feste Klemmenbelegung.
        var entry = MpModuleCatalog.Find("6ES7532-5NB00-0AB0")!;
        Assert.Equal(ModuleType.AO, entry.IoType);
        Assert.Equal(MpModuleVariant.MP25_16, entry.Variant);
        Assert.All(entry.Cells, c => Assert.True(c.IsEditable));
    }

    [Fact]
    public void EntriesForFamily_SplitsBy25mmVariant()
    {
        var mm35 = MpModuleCatalog.EntriesForFamily(ProductFamily.S71500_ET200MP);
        var mm25 = MpModuleCatalog.EntriesForFamily(ProductFamily.S71500_ET200MP_25mm);

        Assert.Contains(MpModuleCatalog.CustomEntry, mm35);
        Assert.Contains(MpModuleCatalog.CustomEntry, mm25);

        Assert.Contains(mm35, e => e.ArticleNo == "6ES7521-1BL00-0AB0");   // 35mm DI 32
        Assert.DoesNotContain(mm35, e => e.ArticleNo == "6ES7532-5NB00-0AB0");

        Assert.Contains(mm25, e => e.ArticleNo == "6ES7532-5NB00-0AB0");   // 25mm AQ 2
        Assert.DoesNotContain(mm25, e => e.ArticleNo == "6ES7521-1BL00-0AB0");
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
    public void GetDefinitions_OutputModuleWithoutArticle_UsesDqSupplyLayout()
    {
        // Ohne Katalog-Artikel bestimmt der Modultyp die Struktur-Klemmen:
        // Ausgabemodule haben Versorgung je Kanalgruppe (K9/K10 + K19/K20).
        var module = new MpModule { Variant = MpModuleVariant.DI_DQ_16, IoType = ModuleType.DO };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);

        Assert.Equal("1L+", LabelAt(defs, 8));
        Assert.Equal("1M", LabelAt(defs, 9));
        Assert.Equal("2L+", LabelAt(defs, 18));
        Assert.Equal("2M", LabelAt(defs, 19));
    }

    [Fact]
    public void GetDefinitions_Input32WithoutArticle_KeepsDiSupplyLayout()
    {
        var module = new MpModule { Variant = MpModuleVariant.DI_DQ_32, IoType = ModuleType.DI };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);

        Assert.Equal("", LabelAt(defs, 8, startCol: 0));      // K9 unbelegt
        Assert.Equal("1L+", LabelAt(defs, 18, startCol: 0));  // K19
        Assert.Equal("2L+", LabelAt(defs, 18, startCol: 1));  // K39
    }

    [Fact]
    public void GetDefinitions_Output32WithoutArticle_HasFourSupplyGroups()
    {
        var module = new MpModule { Variant = MpModuleVariant.DI_DQ_32, IoType = ModuleType.DO };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);

        Assert.Equal("1L+", LabelAt(defs, 8, startCol: 0));   // K9
        Assert.Equal("2L+", LabelAt(defs, 18, startCol: 0));  // K19
        Assert.Equal("3L+", LabelAt(defs, 8, startCol: 1));   // K29
        Assert.Equal("4L+", LabelAt(defs, 18, startCol: 1));  // K39
    }

    [Theory]
    [InlineData(ModuleType.DI)]
    [InlineData(ModuleType.DO)]
    [InlineData(ModuleType.AI)]
    [InlineData(ModuleType.AO)]
    public void GetGenericDefinitions_CellCountIndependentOfModuleType(ModuleType ioType)
    {
        // Zellenzahl muss konstant bleiben, sonst verliert der Typwechsel Texte.
        foreach (var layout in MpModuleLayoutFactory.All)
        {
            var defs = MpModuleLayoutFactory.GetGenericDefinitions(layout.Variant, ioType);
            Assert.Equal(layout.AddressCells.Length, defs.Length);
        }
    }

    [Fact]
    public void GetDefinitions_ArticleWins_OverModuleType()
    {
        // Katalog-Belegung stammt aus dem Datenblatt und darf vom Modultyp
        // nicht ueberschrieben werden.
        var module = new MpModule
        {
            Variant = MpModuleVariant.MP25_16,
            ArticleNumber = "6ES7521-1BH10-0AA0",   // DI 16 BA (25mm): nur K20 = M
            IoType = ModuleType.DO
        };
        var defs = MpModuleLayoutFactory.GetDefinitions(module);

        Assert.Equal("", LabelAt(defs, 8));
        Assert.Equal("M", LabelAt(defs, 19));
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
