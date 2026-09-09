using ETPrinter.Controls;
using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP9d: inkrementelles Rendern der MP-Vorschau — Aenderungen
/// an einem Modul zeichnen nur dessen Ebene neu.</summary>
public class MpPreviewTests
{
    private static (MainViewModel vm, MpPreviewControl preview) Setup(ProductFamily family = ProductFamily.S71500_ET200MP)
    {
        var vm = new MainViewModel(new SilentDialogService()) { SuppressContentLossConfirm = true };
        vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(family);
        var preview = new MpPreviewControl { DataContext = vm };
        preview.FlushRender();
        return (vm, preview);
    }

    private static System.Windows.Controls.Canvas Canvas(MpPreviewControl p) =>
        (System.Windows.Controls.Canvas)p.Content;

    [Fact]
    public void InitialRender_OneLayerPerModule()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            Assert.Equal(1, preview.FullRenderCount);
            Assert.Equal(vm.MpModules.Count, Canvas(preview).Children.Count);   // 10 Ebenen
            Assert.All(Canvas(preview).Children.Cast<System.Windows.Controls.Canvas>(),
                layer => Assert.True(layer.Children.Count > 0));
        });
    }

    [Fact]
    public void HeaderTyping_RedrawsOnlyThatModule()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            var layers = Canvas(preview).Children.Cast<System.Windows.Controls.Canvas>().ToList();
            int othersBefore = layers[3].Children.Count;

            vm.MpModules[0].HeaderText = "D";
            vm.MpModules[0].HeaderText = "DI";
            vm.MpModules[0].HeaderText = "DI32";
            preview.FlushRender();

            Assert.Equal(1, preview.FullRenderCount);          // kein Vollaufbau
            Assert.Equal(1, preview.ModuleRenderCount);        // drei Tastendrücke = ein Neuaufbau
            Assert.Same(layers[0], Canvas(preview).Children[0]); // Ebene bleibt bestehen
            Assert.Equal(othersBefore, layers[3].Children.Count);
            Assert.Contains(layers[0].Children.OfType<System.Windows.Controls.Border>(),
                b => b.Child is System.Windows.Controls.TextBlock tb && tb.Text == "DI32");
        });
    }

    [Fact]
    public void CellText_And_Selection_RedrawOnlyAffectedModules()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            vm.SelectedMpModule = vm.MpModules[0];
            preview.FlushRender();
            int renders = preview.ModuleRenderCount;

            var cell = vm.MpModules[2].AddressCells.First(c => c.IsEditable);
            cell.Text = "E 0.0";
            preview.FlushRender();
            Assert.Equal(renders + 1, preview.ModuleRenderCount);   // nur Modul 3

            vm.SelectedMpModule = vm.MpModules[2];                  // alt (0) + neu (2)
            preview.FlushRender();
            Assert.Equal(renders + 3, preview.ModuleRenderCount);
            Assert.Equal(1, preview.FullRenderCount);
        });
    }

    [Fact]
    public void Generate_RedrawsOneModule_NoFullRender()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            vm.SelectedMpModule = vm.MpModules[1];
            vm.MpEditor.SelectedArticle = MpModuleCatalog.Find("6ES7521-1BL00-0AB0");
            preview.FlushRender();
            int full = preview.FullRenderCount;
            int renders = preview.ModuleRenderCount;

            vm.Generator.Count = 4;
            vm.GenerateAndApplyCommand.Execute(null);                // 32 Zellen + Header, Auswahl rueckt auf Modul 3
            preview.FlushRender();

            Assert.Equal(full, preview.FullRenderCount);
            Assert.InRange(preview.ModuleRenderCount - renders, 1, 2); // Modul 2 (+ neues Auswahlmodul 3)
            var layer = (System.Windows.Controls.Canvas)Canvas(preview).Children[1];
            Assert.Contains(layer.Children.OfType<System.Windows.Controls.Border>(),
                b => b.Child is System.Windows.Controls.TextBlock tb && tb.Text == "E 3.7");
        });
    }

    [Fact]
    public void GlobalChanges_TriggerFullRender()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            vm.Panel.MarginTop = 30;                                 // Geometrie -> alles neu
            preview.FlushRender();
            Assert.Equal(2, preview.FullRenderCount);

            vm.AddPageCommand.Execute(null);                         // Seitenwechsel
            preview.FlushRender();
            Assert.Equal(3, preview.FullRenderCount);
            Assert.Equal(vm.MpModules.Count, Canvas(preview).Children.Count);

            vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP_25mm);
            preview.FlushRender();
            Assert.Equal(20, Canvas(preview).Children.Count);
        });
    }

    [Fact]
    public void VariantChange_RebindsCells_AndRedraws()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            vm.SelectedMpModule = vm.MpModules[4];
            preview.FlushRender();
            int renders = preview.ModuleRenderCount;

            vm.MpEditor.SelectedVariant = MpModuleLayoutFactory.GetLayout(MpModuleVariant.DI_DQ_32);
            preview.FlushRender();
            Assert.True(preview.ModuleRenderCount > renders);
            int afterVariant = preview.ModuleRenderCount;

            // Neue Zell-VMs muessen wieder abonniert sein
            vm.MpModules[4].AddressCells.First(c => c.IsEditable).Text = "E 9.0";
            preview.FlushRender();
            Assert.Equal(afterVariant + 1, preview.ModuleRenderCount);
            var layer = (System.Windows.Controls.Canvas)Canvas(preview).Children[4];
            Assert.Contains(layer.Children.OfType<System.Windows.Controls.Border>(),
                b => b.Child is System.Windows.Controls.TextBlock tb && tb.Text == "E 9.0");
        });
    }

    [Fact]
    public void Undo_RestoresPreview()
    {
        Sta.Run(() =>
        {
            var (vm, preview) = Setup();
            vm.SelectedMpModule = vm.MpModules[0];
            vm.MpModules[0].HeaderText = "WEG";
            preview.FlushRender();
            vm.Undo();
            preview.FlushRender();
            var texts = Canvas(preview).Children.Cast<System.Windows.Controls.Canvas>()
                .SelectMany(l => l.Children.OfType<System.Windows.Controls.Border>())
                .Select(b => (b.Child as System.Windows.Controls.TextBlock)?.Text).ToList();
            Assert.DoesNotContain("WEG", texts);
        });
    }
}
