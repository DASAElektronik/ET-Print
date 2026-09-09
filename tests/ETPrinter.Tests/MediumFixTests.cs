using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>Tests zu ABSCHLUSSPLAN AP2 (mittlere Bugs).</summary>
public class MediumFixTests
{
    private static MainViewModel NewVm() => new() { SuppressContentLossConfirm = true };

    private static void SelectFamily(MainViewModel vm, ProductFamily family) =>
        vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(family);

    // ---- 2.16 Leerslots in horizontalen Formaten -------------------------

    [Theory]
    [InlineData("E 0.1  E 0.3  E 0.5  E 0.7        ", "E 0.1  E 0.3  E 0.5  E 0.7")]
    [InlineData("EW 2    EW 6  ", "EW 2  EW 6")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Zeile 1", "Zeile 1")]
    public void CompactSlots_RemovesEmptySlotsAndTrailingSpaces(string input, string expected)
    {
        Assert.Equal(expected, LabelViewModel.CompactSlots(input));
    }

    // ---- 2.11 Strg+Klick nimmt den Anker mit -----------------------------

    [Fact]
    public void ToggleCheck_FirstCtrlClick_ChecksAnchorToo()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.SelectedLabel = vm.Labels[2];
            vm.ToggleCheck(vm.Labels[4]);

            Assert.True(vm.Labels[2].IsChecked);
            Assert.True(vm.Labels[4].IsChecked);
            Assert.Same(vm.Labels[4], vm.SelectedLabel);

            // zweiter Strg+Klick auf 4: nur 4 abwaehlen
            vm.ToggleCheck(vm.Labels[4]);
            Assert.True(vm.Labels[2].IsChecked);
            Assert.False(vm.Labels[4].IsChecked);
        });
    }

    // ---- 2.12 GenCount folgt dem Modultyp ----------------------------------

    [Fact]
    public void GenModuleTypeChange_ResetsInvalidGenCount()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.GenCount = 1; // gueltig fuer DI
            vm.GenModuleType = AddressGenerator.ModuleTypes.First(t => t.Type == ModuleType.AI);
            Assert.Contains(vm.GenCount, vm.GenTypicalCounts);
            Assert.Equal(2, vm.GenCount);
        });
    }

    // ---- 2.7 Dirty-Tracking -----------------------------------------------

    [Fact]
    public void PrintGridLines_MarksDirty()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.False(vm.IsDirty);
            vm.PrintGridLines = true;
            Assert.True(vm.IsDirty);
        });
    }

    [Fact]
    public void LabelPrintFlag_ViaProperty_MarksDirty()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Labels[0].IsPrintEnabled = false; // wie Kontextmenue "Druck umschalten"
            Assert.True(vm.IsDirty);
        });
    }

    [Fact]
    public void FormatChange_WithContent_MarksDirty()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Labels[0].Line1 = "x";
            vm.SelectedFormat = FormatDefinitions.Get(LabelFormat.VerticalDouble);
            Assert.True(vm.IsDirty);
        });
    }

    [Fact]
    public void FormatChange_WithoutContent_StaysClean()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.SelectedFormat = FormatDefinitions.Get(LabelFormat.VerticalDouble);
            Assert.False(vm.IsDirty);
        });
    }

    // ---- 2.5 Druckauswahl im MP-Modus ------------------------------------

    [Fact]
    public void TogglePrint_InModuleMode_AffectsSelectedModule()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            vm.SelectedMpModule = vm.MpModules[3];
            Assert.True(vm.TogglePrintCommand.CanExecute(null));

            vm.TogglePrintCommand.Execute(null);
            Assert.False(vm.MpModules[3].IsPrintEnabled);
            Assert.True(vm.IsDirty);
        });
    }

    [Fact]
    public void SelectFilledForPrint_InModuleMode_UsesPrintableContent()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            vm.MpModules[0].HeaderText = "A";
            vm.MpModules[5].Variant = MpModuleVariant.SIWAREX_WP52x;

            vm.DeselectAllForPrintCommand.Execute(null);
            Assert.All(vm.MpModules, m => Assert.False(m.IsPrintEnabled));

            vm.SelectFilledForPrintCommand.Execute(null);
            Assert.True(vm.MpModules[0].IsPrintEnabled);
            Assert.True(vm.MpModules[5].IsPrintEnabled);
            Assert.False(vm.MpModules[1].IsPrintEnabled);

            Assert.Equal([2], vm.PrintablePerPage());
        });
    }

    // ---- 2.9 Recent-Files -------------------------------------------------

    [Fact]
    public void RemoveRecentFile_RemovesEntry()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_recent_{Guid.NewGuid():N}.etprint");
        ProjectService.AddRecentFile(path);
        Assert.Contains(ProjectService.LoadRecentFiles(), p => string.Equals(p, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));

        ProjectService.RemoveRecentFile(path);
        Assert.DoesNotContain(ProjectService.LoadRecentFiles(), p => string.Equals(p, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
    }

    // ---- 2.14 Rand unten im MP-Modus gesperrt ------------------------------

    [Fact]
    public void MarginBottom_NotEditableInModuleMode()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.True(vm.IsMarginBottomEditable);
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            Assert.False(vm.IsMarginBottomEditable);
        });
    }

    // ---- 2.13 Infozeilen familienbewusst -----------------------------------

    [Fact]
    public void LayoutInfo_ModuleMode_ShowsStrips()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.Contains("100 Etiketten", vm.LayoutInfo);
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            Assert.Contains("10 Streifen", vm.LayoutInfo);
            Assert.StartsWith("Modul:", vm.StatusSelectionInfo);
        });
    }

    // ---- Paste-Status bei Seitenende ----------------------------------------

    [Fact]
    public void PasteLabels_AtPageEnd_ReportsPartial()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Labels[0].Line1 = "a";
            vm.Labels[1].Line1 = "b";
            vm.Labels[2].Line1 = "c";
            vm.Labels[0].IsChecked = true; vm.Labels[1].IsChecked = true; vm.Labels[2].IsChecked = true;
            vm.CopyCommand.Execute(null);

            vm.ClearAllLabelChecks();
            vm.SelectedLabel = vm.Labels[^1]; // letztes Etikett
            vm.PasteCommand.Execute(null);

            Assert.Contains("1 von 3", vm.StatusMessage);
            Assert.Equal("a", vm.Labels[^1].Line1);
        });
    }
}
