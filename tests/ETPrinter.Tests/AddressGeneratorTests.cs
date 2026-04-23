using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

public class AddressGeneratorTests
{
    // ---- GenerateDigital ------------------------------------------------

    [Fact]
    public void GenerateDigital_1Byte_Line1ContainsOddBits()
    {
        var label = AddressGenerator.GenerateDigital("DI8", "E", startByte: 0, byteCount: 1);

        // Line1 = odd bits: .1 .3 .5 .7
        Assert.Contains("E 0.1", label.Line1);
        Assert.Contains("E 0.3", label.Line1);
        Assert.Contains("E 0.5", label.Line1);
        Assert.Contains("E 0.7", label.Line1);
    }

    [Fact]
    public void GenerateDigital_1Byte_Line2ContainsEvenBits()
    {
        var label = AddressGenerator.GenerateDigital("DI8", "E", startByte: 0, byteCount: 1);

        // Line2 = even bits: .0 .2 .4 .6
        Assert.Contains("E 0.0", label.Line2);
        Assert.Contains("E 0.2", label.Line2);
        Assert.Contains("E 0.4", label.Line2);
        Assert.Contains("E 0.6", label.Line2);
    }

    [Fact]
    public void GenerateDigital_2Bytes_ProducesCorrectBitCount()
    {
        var label = AddressGenerator.GenerateDigital("DI16", "E", startByte: 0, byteCount: 2);

        // 2 bytes x 4 odd bits = 8 entries in Line1
        var line1Items = label.Line1.Split("  ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(8, line1Items.Length);
    }

    [Fact]
    public void GenerateDigital_RespectsStartByteOffset()
    {
        var label = AddressGenerator.GenerateDigital("DI8", "A", startByte: 10, byteCount: 1);

        Assert.Contains("A 10.1", label.Line1);
        Assert.Contains("A 10.0", label.Line2);
        Assert.DoesNotContain("A 0.", label.Line1);
    }

    [Theory]
    [InlineData(1, 4)]   // 1 byte  -> 4 odd bits in Line1
    [InlineData(2, 8)]   // 2 bytes -> 8 odd bits
    [InlineData(4, 16)]  // 4 bytes -> 16 odd bits
    public void GenerateDigital_Line1ItemCount_MatchesByteCount(int byteCount, int expectedItems)
    {
        var label = AddressGenerator.GenerateDigital("X", "E", startByte: 0, byteCount: byteCount);

        var items = label.Line1.Split("  ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expectedItems, items.Length);
    }

    // ---- GenerateAnalog -------------------------------------------------

    // ET200SP Analog: alternierend wie digital. Ungerade Kanaele oben, gerade unten.
    // Fix 8 Plaetze pro Reihe (BaseUnit hat 8 Klemmen pro Reihe); Restplaetze leer.
    [Fact]
    public void GenerateAnalog_8Channels_OddChannelsInLine1_EvenInLine2()
    {
        var label = AddressGenerator.GenerateAnalog("AI8", "EW", startByte: 0, channelCount: 8);

        // Oben (K1, K3, K5, K7): EW 2, EW 6, EW 10, EW 14, "", "", "", ""
        var line1 = label.Line1.Split("  ", StringSplitOptions.None);
        Assert.Equal(8, line1.Length);
        Assert.Equal("EW 2", line1[0]);
        Assert.Equal("EW 6", line1[1]);
        Assert.Equal("EW 10", line1[2]);
        Assert.Equal("EW 14", line1[3]);
        Assert.Equal("", line1[4]);
        Assert.Equal("", line1[7]);

        // Unten (K0, K2, K4, K6): EW 0, EW 4, EW 8, EW 12, "", "", "", ""
        var line2 = label.Line2.Split("  ", StringSplitOptions.None);
        Assert.Equal(8, line2.Length);
        Assert.Equal("EW 0", line2[0]);
        Assert.Equal("EW 4", line2[1]);
        Assert.Equal("EW 8", line2[2]);
        Assert.Equal("EW 12", line2[3]);
        Assert.Equal("", line2[4]);
    }

    [Fact]
    public void GenerateAnalog_4Channels_2OddInLine1_2EvenInLine2()
    {
        var label = AddressGenerator.GenerateAnalog("AI4", "EW", startByte: 0, channelCount: 4);

        var line1 = label.Line1.Split("  ", StringSplitOptions.None);
        var line2 = label.Line2.Split("  ", StringSplitOptions.None);

        Assert.Equal(8, line1.Length); // immer 8 Plaetze pro Reihe
        Assert.Equal("EW 2", line1[0]);
        Assert.Equal("EW 6", line1[1]);
        Assert.Equal("", line1[2]);

        Assert.Equal(8, line2.Length);
        Assert.Equal("EW 0", line2[0]);
        Assert.Equal("EW 4", line2[1]);
        Assert.Equal("", line2[2]);
    }

    [Fact]
    public void GenerateAnalog_AddressesStepBy2Bytes_FromStartByteOffset()
    {
        var label = AddressGenerator.GenerateAnalog("AI", "EW", startByte: 100, channelCount: 4);

        Assert.Contains("EW 100", label.Line2);
        Assert.Contains("EW 102", label.Line1);
        Assert.Contains("EW 104", label.Line2);
        Assert.Contains("EW 106", label.Line1);
    }

    // ---- Generate (dispatcher) ------------------------------------------

    [Theory]
    [InlineData(ModuleType.DI, "E")]
    [InlineData(ModuleType.DO, "A")]
    public void Generate_DigitalTypes_DispatchToDigitalGenerator(ModuleType type, string expectedPrefix)
    {
        var label = AddressGenerator.Generate("M", type, startByte: 0, count: 1);

        Assert.Contains(expectedPrefix + " 0.1", label.Line1);
    }

    [Theory]
    [InlineData(ModuleType.AI, "EW")]
    [InlineData(ModuleType.AO, "AW")]
    public void Generate_AnalogTypes_DispatchToAnalogGenerator(ModuleType type, string expectedPrefix)
    {
        var label = AddressGenerator.Generate("M", type, startByte: 0, count: 2);

        // K0 (gerade) in Line2 = EW/AW 0, K1 (ungerade) in Line1 = EW/AW 2
        Assert.Contains(expectedPrefix + " 0", label.Line2);
        Assert.Contains(expectedPrefix + " 2", label.Line1);
    }

    // ---- GetNextStartByte -----------------------------------------------

    [Theory]
    [InlineData(ModuleType.DI, 0, 2, 2)]
    [InlineData(ModuleType.DO, 10, 4, 14)]
    public void GetNextStartByte_Digital_AdvancesByCount(ModuleType type, int startByte, int count, int expected)
    {
        Assert.Equal(expected, AddressGenerator.GetNextStartByte(type, startByte, count));
    }

    [Theory]
    [InlineData(ModuleType.AI, 0, 4, 8)]
    [InlineData(ModuleType.AO, 10, 2, 14)]
    public void GetNextStartByte_Analog_AdvancesByCountTimes2(ModuleType type, int startByte, int count, int expected)
    {
        Assert.Equal(expected, AddressGenerator.GetNextStartByte(type, startByte, count));
    }
}
