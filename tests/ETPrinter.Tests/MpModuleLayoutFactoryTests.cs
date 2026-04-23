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
    public void All_ContainsAllSixVariants()
    {
        Assert.Equal(6, MpModuleLayoutFactory.All.Count);
    }

    [Fact]
    public void GetLayout_UnknownVariant_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            MpModuleLayoutFactory.GetLayout((MpModuleVariant)999));
    }
}
