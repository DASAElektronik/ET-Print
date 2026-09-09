using ETPrinter.Models;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

public class MpModuleLayoutFactoryTests
{
    public static IEnumerable<object[]> AllVariants =>
        Enum.GetValues<MpModuleVariant>().Select(v => new object[] { v });

    /// <summary>
    /// CreateCells must return at least one cell for every defined variant.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllVariants))]
    public void CreateCells_ReturnsNonEmptyList(MpModuleVariant variant)
    {
        var cells = MpModuleLayoutFactory.CreateCells(variant);

        Assert.NotEmpty(cells);
    }

    /// <summary>
    /// Invariant: ALLE Varianten nutzen nur Half 0 (ein Etikett pro Modul).
    /// Half 1 gehoert immer zu einem anderen Modul auf dem gleichen A4-Blatt.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllVariants))]
    public void CreateCells_AllCellDefinitions_HaveHalfZero(MpModuleVariant variant)
    {
        var layout = MpModuleLayoutFactory.GetLayout(variant);

        Assert.All(layout.AddressCells, cell => Assert.Equal(0, cell.Half));
    }

    /// <summary>
    /// CreateCells index must be sequential starting from zero.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllVariants))]
    public void CreateCells_CellIndicesAreSequential(MpModuleVariant variant)
    {
        var cells = MpModuleLayoutFactory.CreateCells(variant);

        for (int i = 0; i < cells.Count; i++)
            Assert.Equal(i, cells[i].CellIndex);
    }

    [Fact]
    public void All_ContainsAllVariants()
    {
        Assert.Equal(9, MpModuleLayoutFactory.All.Count);
    }

    // 25mm-Varianten (AP8, Blockdiagramme der 25mm-BA-Module): dieselbe
    // 40-Klemmen-Struktur wie 35mm — K9/K10 und K19/K20 sind Struktur (unbeschriftet).
    [Fact]
    public void MP25_16_Has16EditableRows_StructureAt8_9_18_19()
    {
        var cells = MpModuleLayoutFactory.GetLayout(MpModuleVariant.MP25_16).AddressCells;
        Assert.Equal(20, cells.Length);
        Assert.Equal(16, cells.Count(c => c.IsEditable));
        Assert.All(cells, c => Assert.Equal(2, c.ColSpan));
        Assert.All(cells.Where(c => !c.IsEditable), c => Assert.Contains(c.StartRow, new[] { 8, 9, 18, 19 }));
        Assert.All(cells.Where(c => !c.IsEditable), c => Assert.Equal("", c.Label));
    }

    [Fact]
    public void MP25_32_Has32EditableCells_ByteOrderLikeDiDq32()
    {
        var cells = MpModuleLayoutFactory.GetLayout(MpModuleVariant.MP25_32).AddressCells;
        Assert.Equal(40, cells.Length);
        Assert.Equal(32, cells.Count(c => c.IsEditable));
        Assert.Equal(20, cells.Count(c => c.StartCol == 0));
        Assert.Equal(20, cells.Count(c => c.StartCol == 1));
        // Reihenfolge: Spalte 0 komplett, dann Spalte 1 -> Byte 2 landet rechts
        var editable = cells.Where(c => c.IsEditable).ToList();
        Assert.All(editable.Take(16), c => Assert.Equal(0, c.StartCol));
        Assert.All(editable.Skip(16), c => Assert.Equal(1, c.StartCol));
    }

    [Fact]
    public void MP25_Plain20_AllEditable()
    {
        var cells = MpModuleLayoutFactory.CreateLayout_MP25_Plain20();
        Assert.Equal(20, cells.Length);
        Assert.All(cells, c => Assert.True(c.IsEditable));
    }

    [Fact]
    public void VariantsForFamily_25mm_OnlyMP25Variants()
    {
        var v25 = MpModuleLayoutFactory.VariantsForFamily(ProductFamily.S71500_ET200MP_25mm);
        Assert.Equal(2, v25.Count);
        Assert.All(v25, l => Assert.True(
            l.Variant is MpModuleVariant.MP25_16 or MpModuleVariant.MP25_32));

        var v35 = MpModuleLayoutFactory.VariantsForFamily(ProductFamily.S71500_ET200MP);
        Assert.Equal(7, v35.Count);
        Assert.DoesNotContain(v35, l =>
            l.Variant is MpModuleVariant.MP25_16 or MpModuleVariant.MP25_32);
    }

    [Fact]
    public void DefaultVariantFor_PicksFamilyAppropriateVariant()
    {
        Assert.Equal(MpModuleVariant.MP25_16,
            MpModuleLayoutFactory.DefaultVariantFor(ProductFamily.S71500_ET200MP_25mm));
        Assert.Equal(MpModuleVariant.DI_DQ_16,
            MpModuleLayoutFactory.DefaultVariantFor(ProductFamily.S71500_ET200MP));
    }

    // SIWAREX: fester Pinout, 20 Klemmen pro Spalte, beide Spalten identisch,
    // alle nicht-editierbar mit Funktionslabel (verifiziert A5E36695151A).
    [Fact]
    public void SIWAREX_HasFixedPinout_40TerminalsAllLabeled()
    {
        var cells = MpModuleLayoutFactory.GetLayout(MpModuleVariant.SIWAREX_WP52x).AddressCells;

        Assert.Equal(40, cells.Length);
        Assert.All(cells, c => Assert.False(c.IsEditable));
        Assert.All(cells, c => Assert.False(string.IsNullOrEmpty(c.Label)));
        // K1 = EXC+, K9 = DQ.L+, K20 = M (linke Spalte)
        Assert.Equal("EXC+", cells.First(c => c.StartCol == 0 && c.StartRow == 0).Label);
        Assert.Equal("DQ.L+", cells.First(c => c.StartCol == 0 && c.StartRow == 8).Label);
        Assert.Equal("M", cells.First(c => c.StartCol == 0 && c.StartRow == 19).Label);
        // Rechte Spalte identisch
        Assert.Equal("EXC+", cells.First(c => c.StartCol == 1 && c.StartRow == 0).Label);
    }

    // AI/AQ: Excel horizontal_8_AI_AQ/4_AQ haben 5 editierbare 4-Zeilen-Bloecke
    // pro Spalte, KEIN hartkodiertes MANA (Analog-Verdrahtung ist modusabhaengig).
    [Fact]
    public void AI_AQ_8_HasFiveEditableBlocksPerColumn_NoStructLabels()
    {
        var cells = MpModuleLayoutFactory.GetLayout(MpModuleVariant.AI_AQ_8).AddressCells;

        Assert.Equal(10, cells.Length);
        Assert.All(cells, c => Assert.True(c.IsEditable));
        Assert.All(cells, c => Assert.Equal(4, c.RowSpan));
        Assert.Equal(5, cells.Count(c => c.StartCol == 0));
        Assert.Equal(5, cells.Count(c => c.StartCol == 1));
    }

    [Fact]
    public void AQ_4_HasFiveMergedEditableBlocks_NoStructLabels()
    {
        var cells = MpModuleLayoutFactory.GetLayout(MpModuleVariant.AQ_4).AddressCells;

        Assert.Equal(5, cells.Length);
        Assert.All(cells, c => Assert.True(c.IsEditable));
        Assert.All(cells, c => Assert.Equal(2, c.ColSpan));
        Assert.All(cells, c => Assert.Equal(4, c.RowSpan));
    }

    [Fact]
    public void GetLayout_UnknownVariant_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            MpModuleLayoutFactory.GetLayout((MpModuleVariant)999));
    }
}
