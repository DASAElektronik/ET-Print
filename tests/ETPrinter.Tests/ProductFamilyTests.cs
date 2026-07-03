using ETPrinter.Models;
using Xunit;

namespace ETPrinter.Tests;

public class ProductFamilyTests
{
    // 25mm-Familie: 20 Positionen (10 Spalten x 2 Baender), KEINE Net-Address-Spalte.
    [Fact]
    public void MP25_Has20Modules_10Columns_NoNetAddress()
    {
        var info = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP_25mm);

        Assert.Equal(20, info.ModulesPerPage);
        Assert.Equal(10, info.ColumnsPerPage);
        Assert.False(info.HasNetAddressColumn);
        Assert.Equal(0.0, info.Col2Ratio);
        // Adresse + CPU fuellen die Modulbreite
        Assert.Equal(1.0, info.Col0Ratio + info.Col1Ratio + info.Col3Ratio, precision: 3);
    }

    // 35mm-Familie: 10 Positionen (5 Spalten x 2 Baender), MIT Net-Address-Spalte.
    [Fact]
    public void MP35_Has10Modules_5Columns_WithNetAddress()
    {
        var info = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP);

        Assert.Equal(10, info.ModulesPerPage);
        Assert.Equal(5, info.ColumnsPerPage);
        Assert.True(info.HasNetAddressColumn);
        // Alle 4 Spalten fuellen die Modulbreite
        Assert.Equal(1.0,
            info.Col0Ratio + info.Col1Ratio + info.Col2Ratio + info.Col3Ratio, precision: 2);
    }

    [Theory]
    [InlineData(0, 0, 0)]   // Position 0 -> Band 0, Spalte 0
    [InlineData(4, 0, 4)]   // Position 4 -> Band 0, Spalte 4
    [InlineData(5, 1, 0)]   // Position 5 -> Band 1, Spalte 0
    [InlineData(9, 1, 4)]   // Position 9 -> Band 1, Spalte 4
    public void MP35_BandAndColumn(int moduleIndex, int expectedBand, int expectedCol)
    {
        var info = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP);
        Assert.Equal(expectedBand, info.BandOf(moduleIndex));
        Assert.Equal(expectedCol, info.ColumnOf(moduleIndex));
    }

    [Theory]
    [InlineData(9, 0, 9)]    // 25mm: Position 9 -> Band 0, Spalte 9
    [InlineData(10, 1, 0)]   // Position 10 -> Band 1, Spalte 0
    [InlineData(19, 1, 9)]   // Position 19 -> Band 1, Spalte 9
    public void MP25_BandAndColumn(int moduleIndex, int expectedBand, int expectedCol)
    {
        var info = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP_25mm);
        Assert.Equal(expectedBand, info.BandOf(moduleIndex));
        Assert.Equal(expectedCol, info.ColumnOf(moduleIndex));
    }
}
