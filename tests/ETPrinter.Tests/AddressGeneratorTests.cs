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

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void GenerateAnalog_UpTo4Channels_Line2IsEmpty(int channelCount)
    {
        var label = AddressGenerator.GenerateAnalog("AI", "EW", startByte: 0, channelCount: channelCount);

        Assert.Empty(label.Line2);
        Assert.NotEmpty(label.Line1);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void GenerateAnalog_MoreThan4Channels_BothLinesPopulated(int channelCount)
    {
        var label = AddressGenerator.GenerateAnalog("AI", "EW", startByte: 0, channelCount: channelCount);

        Assert.NotEmpty(label.Line1);
        Assert.NotEmpty(label.Line2);
    }

    [Fact]
    public void GenerateAnalog_AddressesStepBy2Bytes()
    {
        var label = AddressGenerator.GenerateAnalog("AI4", "EW", startByte: 100, channelCount: 3);

        Assert.Contains("EW 100", label.Line1);
        Assert.Contains("EW 102", label.Line1);
        Assert.Contains("EW 104", label.Line1);
    }

    [Fact]
    public void GenerateAnalog_8Channels_SplitsAtHalf()
    {
        var label = AddressGenerator.GenerateAnalog("AI8", "EW", startByte: 0, channelCount: 8);

        // half = 4, so Line1 has channels 0-3, Line2 has channels 4-7
        var line1Items = label.Line1.Split("  ", StringSplitOptions.RemoveEmptyEntries);
        var line2Items = label.Line2.Split("  ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, line1Items.Length);
        Assert.Equal(4, line2Items.Length);
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

        Assert.Contains(expectedPrefix + " 0", label.Line1);
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
