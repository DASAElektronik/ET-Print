using ETPrinter.Models;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

public class ClipboardAndCloneTests
{
    // ---- LabelCell.CloneContent / CopyFrom ----

    [Fact]
    public void LabelCell_CloneContent_CopiesAllFieldsExceptIndex()
    {
        var source = new LabelCell
        {
            Index = 42,
            Header = "Hdr",
            Line1 = "L1",
            Line2 = "L2",
            FontSize = 9,
            IsBold = true,
            IsItalic = true,
            FontFamily = "Courier New",
            IsPrintEnabled = false,
        };

        var clone = source.CloneContent();

        Assert.Equal(0, clone.Index); // Index NICHT kopiert
        Assert.Equal("Hdr", clone.Header);
        Assert.Equal("L1", clone.Line1);
        Assert.Equal("L2", clone.Line2);
        Assert.Equal(9, clone.FontSize);
        Assert.True(clone.IsBold);
        Assert.True(clone.IsItalic);
        Assert.Equal("Courier New", clone.FontFamily);
        Assert.False(clone.IsPrintEnabled);
    }

    [Fact]
    public void LabelCell_CopyFrom_OverwritesAllExceptIndex()
    {
        var target = new LabelCell { Index = 7 };
        var source = new LabelCell
        {
            Index = 42,
            Header = "H",
            Line1 = "L1",
            FontSize = 6,
            IsBold = true,
        };

        target.CopyFrom(source);

        Assert.Equal(7, target.Index); // Index bleibt
        Assert.Equal("H", target.Header);
        Assert.Equal("L1", target.Line1);
        Assert.Equal(6, target.FontSize);
        Assert.True(target.IsBold);
    }

    // ---- MpModule.CloneContent ----

    [Fact]
    public void MpModule_CloneContent_DeepCopiesAddressCells()
    {
        var source = new MpModule
        {
            ModuleIndex = 3,
            Variant = MpModuleVariant.DI_DQ_16,
            HeaderText = "Mod-A",
            AddressCells = [
                new MpAddressCell { CellIndex = 0, Text = "E 0.0" },
                new MpAddressCell { CellIndex = 1, Text = "E 0.1" },
            ],
            NetAddress1 = "N1",
            CpuName = "CPU1",
            FontSize = 8,
        };

        var clone = source.CloneContent();

        Assert.Equal(0, clone.ModuleIndex); // ModuleIndex NICHT kopiert
        Assert.Equal(MpModuleVariant.DI_DQ_16, clone.Variant);
        Assert.Equal("Mod-A", clone.HeaderText);
        Assert.Equal(2, clone.AddressCells.Count);
        Assert.Equal("E 0.0", clone.AddressCells[0].Text);

        // Deep-Copy: Aendern des Clone-Cells darf Source nicht beeinflussen
        clone.AddressCells[0].Text = "MUTATED";
        Assert.Equal("E 0.0", source.AddressCells[0].Text);
    }

    // ---- ClipboardService ----

    [Fact]
    public void ClipboardService_SetLabels_StoresDeepCopy()
    {
        ClipboardService.Clear();
        var original = new LabelCell { Header = "Orig", Line1 = "L1" };

        ClipboardService.SetLabels([original]);
        original.Header = "MUTATED"; // nach Copy mutiert

        var retrieved = ClipboardService.GetLabels();
        Assert.Single(retrieved);
        Assert.Equal("Orig", retrieved[0].Header); // Unabhaengig vom Original
    }

    [Fact]
    public void ClipboardService_ModeExclusive_SetModulesClearsLabels()
    {
        ClipboardService.Clear();
        ClipboardService.SetLabels([new LabelCell { Header = "A" }]);
        Assert.True(ClipboardService.HasLabels);

        ClipboardService.SetModules([new MpModule { HeaderText = "M" }]);
        Assert.False(ClipboardService.HasLabels);
        Assert.True(ClipboardService.HasModules);
    }

    [Fact]
    public void ClipboardService_GetLabels_ReturnsFreshCopiesEachTime()
    {
        ClipboardService.Clear();
        ClipboardService.SetLabels([new LabelCell { Header = "Orig" }]);

        var first = ClipboardService.GetLabels();
        first[0].Header = "MUTATED";

        var second = ClipboardService.GetLabels();
        Assert.Equal("Orig", second[0].Header); // Zweiter Abruf unabhaengig
    }

    [Fact]
    public void ClipboardService_Clear_EmptiesBothBuffers()
    {
        ClipboardService.SetLabels([new LabelCell()]);
        ClipboardService.Clear();
        Assert.False(ClipboardService.HasLabels);
        Assert.False(ClipboardService.HasModules);
        Assert.Empty(ClipboardService.GetLabels());
        Assert.Empty(ClipboardService.GetModules());
    }
}
