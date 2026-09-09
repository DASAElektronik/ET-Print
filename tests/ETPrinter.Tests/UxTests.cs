using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP5: Komfort/UX.</summary>
public class UxTests
{
    private static MainViewModel NewVm() => new() { SuppressContentLossConfirm = true };

    private static void SelectFamily(MainViewModel vm, ProductFamily family) =>
        vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(family);

    // ---- 5.5 Wertebereiche --------------------------------------------------

    [Fact]
    public void Margins_AreClampedTo0_60()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Panel.MarginTop = 99;
            Assert.Equal(60, vm.Panel.MarginTop);
            Assert.Equal(60, vm.Settings.MarginTop);
            vm.Panel.MarginLeft = -5;
            Assert.Equal(0, vm.Panel.MarginLeft);
            vm.Panel.MarginRight = double.NaN;
            Assert.Equal(0, vm.Panel.MarginRight);
            Assert.Contains("begrenzt", vm.StatusMessage);
        });
    }

    [Fact]
    public void Calibration_IsClampedToPlusMinus10()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.CalibrationOffsetX = 25;
            vm.CalibrationOffsetY = -25;
            Assert.Equal(10, vm.CalibrationOffsetX);
            Assert.Equal(-10, vm.CalibrationOffsetY);
        });
    }

    [Fact]
    public void GenStartByte_NeverNegative()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Generator.StartByte = -3;
            Assert.Equal(0, vm.Generator.StartByte);
        });
    }

    // ---- 5.7 MP-Tab automatisch -------------------------------------------

    [Fact]
    public void InputTabIndex_FollowsFormatKind()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.Equal(0, vm.InputTabIndex);
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            Assert.Equal(2, vm.InputTabIndex);
            SelectFamily(vm, ProductFamily.ET200SP);
            Assert.Equal(0, vm.InputTabIndex);
        });
    }

    // ---- 5.6 Schrift auf alle ----------------------------------------------

    [Fact]
    public void ApplyFontToAll_SetsEveryLabelOnEveryPage()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.AddPageCommand.Execute(null);
            vm.Panel.FontSize = 9;
            vm.Panel.IsBold = true;
            vm.ApplyFontToAllCommand.Execute(null);

            var project = vm.BuildProject();
            Assert.Equal(2, project.Pages.Count);
            Assert.All(project.Pages.SelectMany(p => p.Labels), l =>
            {
                Assert.Equal(9, l.FontSize);
                Assert.True(l.IsBold);
            });
            Assert.Equal(9, vm.Settings.FontSize);
        });
    }

    [Fact]
    public void ApplyFontToAll_ModuleMode()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            vm.Panel.FontFamily = "Consolas";
            vm.Panel.IsItalic = true;
            vm.ApplyFontToAllCommand.Execute(null);
            Assert.All(vm.MpModules, m => { Assert.Equal("Consolas", m.FontFamily); Assert.True(m.IsItalic); });
        });
    }

    // ---- Auswahl leeren -------------------------------------------------------

    [Fact]
    public void ClearSelected_EmptiesLabelAndInputs()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.SelectedLabel = vm.Labels[3];
            vm.Labels[3].Header = "H"; vm.Labels[3].Line1 = "E 0.0";
            vm.InputLine1 = "E 0.0";
            vm.ClearSelectedCommand.Execute(null);
            Assert.False(vm.Labels[3].HasText);
            Assert.Equal(string.Empty, vm.InputLine1);
            Assert.True(vm.IsDirty);
        });
    }

    [Fact]
    public void ClearSelected_ModuleKeepsVariantAndArticle()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            SelectFamily(vm, ProductFamily.S71500_ET200MP);
            var mod = vm.MpModules[1];
            vm.SelectedMpModule = mod;
            mod.ArticleNumber = "6ES7521-1BL00-0AB0";
            mod.HeaderText = "DI32";
            mod.AddressCells[0].Text = "E 0.0";
            vm.ClearSelectedCommand.Execute(null);
            Assert.False(mod.HasText);
            Assert.Equal("6ES7521-1BL00-0AB0", mod.ArticleNumber);
            Assert.Equal(MpModuleVariant.DI_DQ_32, mod.Variant);
        });
    }

    // ---- 5.8 Nur aktuelle Seite -----------------------------------------------

    [Fact]
    public void BuildCurrentPageDocument_OnlyCurrentPage()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Labels[0].Line1 = "a";
            vm.AddPageCommand.Execute(null);   // Seite 2, wird aktuell
            vm.Labels[0].Line1 = "b";
            Assert.Equal(2, vm.BuildPrintDocument().Pages.Count);
            Assert.Equal(1, vm.BuildCurrentPageDocument().Pages.Count);
            vm.PrevPageCommand.Execute(null);
            Assert.Equal(1, vm.BuildCurrentPageDocument().Pages.Count);
        });
    }

    // ---- 5.2 Datei oeffnen --------------------------------------------------

    [Fact]
    public void OpenFile_LoadsProject()
    {
        Sta.Run(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"etprinter_open_{Guid.NewGuid():N}.etprint");
            try
            {
                var vm = NewVm();
                vm.Labels[4].Line1 = "E 1.1";
                ProjectService.Save(vm.BuildProject(), path, addToRecent: false);

                var vm2 = NewVm();
                vm2.OpenFile(path);
                Assert.Equal("E 1.1", vm2.Labels[4].Line1);
                Assert.Equal(path, vm2.CurrentFilePath);
                Assert.False(vm2.IsDirty);
            }
            finally { File.Delete(path); }
        });
    }

    // ---- 5.1 Fensterzustand ---------------------------------------------------

    [Fact]
    public void UiState_Sanitize_OffscreenPosition_ReturnsNaN()
    {
        var s = new UiState { Left = 5000, Top = 100, Width = 1400, Height = 900 };
        var (left, top, w, h) = UiStateService.Sanitize(s, 0, 0, 2560, 1440, 1000, 700);
        Assert.True(double.IsNaN(left));
        Assert.True(double.IsNaN(top));
        Assert.Equal(1400, w);
        Assert.Equal(900, h);
    }

    [Fact]
    public void UiState_Sanitize_KeepsVisiblePosition_ClampsSize()
    {
        var s = new UiState { Left = 100, Top = 50, Width = 4000, Height = 300 };
        var (left, top, w, h) = UiStateService.Sanitize(s, 0, 0, 2560, 1440, 1000, 700);
        Assert.Equal(100, left);
        Assert.Equal(50, top);
        Assert.Equal(2560, w);
        Assert.Equal(700, h);
    }

    [Fact]
    public void FontsList_StartsWithArialAndFills()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.Contains("Arial", vm.AvailableFonts);
            Assert.True(vm.AvailableFonts.Count > 1); // headless: synchron geladen
        });
    }
}
