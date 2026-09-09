using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP9c: Undo/Redo (Memento-Verlauf).</summary>
[Collection("RecentFiles")] // gemeinsame recent.json: nicht parallel
public class UndoRedoTests
{
    private static MainViewModel NewVm() =>
        new(new SilentDialogService()) { SuppressContentLossConfirm = true };

    // ---- UndoHistory<T> ---------------------------------------------------------

    [Fact]
    public void History_RecordUndoRedo_RoundTrip()
    {
        var h = new UndoHistory<string>();
        h.Reset("s0");
        Assert.False(h.CanUndo);
        h.Record("s1", "a");
        h.Record("s2", "b");
        Assert.Equal(2, h.UndoCount);
        Assert.Equal("b", h.NextUndoLabel);

        var u = h.Undo();
        Assert.Equal(("s1", "b"), u!.Value);
        Assert.Equal("s1", h.Current);
        Assert.Equal("b", h.NextRedoLabel);

        Assert.Equal(("s0", "a"), h.Undo()!.Value);
        Assert.Null(h.Undo());

        Assert.Equal(("s1", "a"), h.Redo()!.Value);
        Assert.Equal(("s2", "b"), h.Redo()!.Value);
        Assert.Null(h.Redo());
        Assert.Equal("s2", h.Current);
    }

    [Fact]
    public void History_RecordAfterUndo_ClearsRedo()
    {
        var h = new UndoHistory<string>();
        h.Reset("s0");
        h.Record("s1", "a");
        h.Undo();
        Assert.True(h.CanRedo);
        h.Record("s1b", "c");
        Assert.False(h.CanRedo);
        Assert.Equal(("s0", "c"), h.Undo()!.Value);
    }

    [Fact]
    public void History_CoalescesSameKeyWithinWindow()
    {
        var now = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
        var h = new UndoHistory<string>(coalesceKeys: ["typing"]) { Clock = () => now };
        h.Reset("s0");
        h.Record("s1", "typing");
        now = now.AddMilliseconds(500);
        h.Record("s2", "typing");
        now = now.AddMilliseconds(500);
        h.Record("s3", "typing");
        Assert.Equal(1, h.UndoCount);               // ein Schritt fuer die ganze Eingabe
        Assert.Equal(("s0", "typing"), h.Undo()!.Value);

        // nach dem Zeitfenster: neuer Schritt
        h.Redo();
        now = now.AddSeconds(5);
        h.Record("s4", "typing");
        Assert.Equal(2, h.UndoCount);

        // anderer Schluessel wird nie zusammengefasst
        h.Record("s5", "other");
        h.Record("s6", "other");
        Assert.Equal(4, h.UndoCount);
    }

    [Fact]
    public void History_Capacity_DropsOldest()
    {
        var h = new UndoHistory<string>(capacity: 3);
        h.Reset("s0");
        for (int i = 1; i <= 5; i++) h.Record($"s{i}", "x");
        Assert.Equal(3, h.UndoCount);
        Assert.Equal("s4", h.Undo()!.Value.State);
        Assert.Equal("s3", h.Undo()!.Value.State);
        Assert.Equal("s2", h.Undo()!.Value.State);
        Assert.Null(h.Undo());
    }

    // ---- MainViewModel ------------------------------------------------------------

    [Fact]
    public void Apply_ThenUndo_RestoresLabelAndCleanState()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            Assert.False(vm.UndoCommand.CanExecute(null));
            vm.SelectedLabel = vm.Labels[3];
            vm.InputHeader = "K"; vm.InputLine1 = "E 0.0"; vm.InputLine2 = "E 0.1";
            vm.ApplyCommand.Execute(null);
            Assert.Equal("E 0.0", vm.Labels[3].Line1);
            Assert.True(vm.IsDirty);
            Assert.True(vm.UndoCommand.CanExecute(null));
            Assert.Equal("Übertragen", vm.History.NextUndoLabel);

            vm.UndoCommand.Execute(null);
            Assert.False(vm.Labels[3].HasText);
            Assert.False(vm.IsDirty);                 // zurueck auf dem sauberen Stand
            Assert.Same(vm.Labels[3], vm.SelectedLabel);
            Assert.StartsWith("Rückgängig: Übertragen", vm.StatusMessage);

            vm.RedoCommand.Execute(null);
            Assert.Equal("E 0.0", vm.Labels[3].Line1);
            Assert.Equal("K", vm.Labels[3].Header);
            Assert.True(vm.IsDirty);
        });
    }

    [Fact]
    public void Generate_IsOneStep_DespiteManyCellWrites()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP);
            int before = vm.History.UndoCount;         // Familienwechsel = 1 Schritt
            Assert.Equal(1, before);
            vm.SelectedMpModule = vm.MpModules[0];
            vm.Generator.ModuleName = "DI32";
            vm.Generator.Count = 4;
            vm.MpEditor.SelectedArticle = MpModuleCatalog.Find("6ES7521-1BL00-0AB0");
            Assert.Equal(2, vm.History.UndoCount);     // Artikelwahl = 1 Schritt
            vm.GenerateAndApplyCommand.Execute(null);
            Assert.Equal(3, vm.History.UndoCount);     // 32 Zellen + Header = 1 Schritt
            Assert.Equal(32, vm.MpModules[0].AddressCells.Count(c => c.HasText));

            vm.UndoCommand.Execute(null);
            Assert.Equal(0, vm.MpModules[0].AddressCells.Count(c => c.HasText));
            Assert.Equal("6ES7521-1BL00-0AB0", vm.MpModules[0].ArticleNumber);
            vm.UndoCommand.Execute(null);
            Assert.Null(vm.MpModules[0].ArticleNumber);
            vm.UndoCommand.Execute(null);
            Assert.Equal(ProductFamily.ET200SP, vm.SelectedProductFamily);
            Assert.False(vm.IsDirty);
        });
    }

    [Fact]
    public void MpCellTyping_CoalescesIntoOneStep()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP);
            vm.SelectedMpModule = vm.MpModules[1];
            var cell = vm.MpModules[1].AddressCells.First(c => c.IsEditable);
            cell.Text = "E";
            cell.Text = "E 1";
            cell.Text = "E 1.0";
            vm.MpModules[1].HeaderText = "DI";
            Assert.Equal(2, vm.History.UndoCount);     // Familie + Tippen
            vm.Undo();
            Assert.Equal(string.Empty, vm.MpModules[1].AddressCells.First(c => c.IsEditable).Text);
            Assert.Equal(string.Empty, vm.MpModules[1].HeaderText);
            Assert.Same(vm.MpModules[1], vm.SelectedMpModule);
        });
    }

    [Fact]
    public void AddPage_Undo_RemovesPage_RestoresCursor()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.SelectedLabel = vm.Labels[7];
            vm.AddPageCommand.Execute(null);
            Assert.Equal(2, vm.PageCount);
            Assert.Equal(1, vm.CurrentPageIndex);
            vm.Undo();
            Assert.Equal(1, vm.PageCount);
            Assert.Equal(0, vm.CurrentPageIndex);
            Assert.Equal(7, vm.SelectedLabel!.Index);
            vm.Redo();
            Assert.Equal(2, vm.PageCount);
            Assert.Equal(1, vm.CurrentPageIndex);
        });
    }

    [Fact]
    public void ClearAll_Undo_RestoresAllPages()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.InputLine1 = "a";
            vm.ApplyCommand.Execute(null);          // Etikett 1, Auswahl rueckt auf 2
            vm.AddPageCommand.Execute(null);
            vm.InputLine1 = "b";
            vm.ApplyCommand.Execute(null);          // Seite 2, Etikett 1
            vm.ClearAllCommand.Execute(null);
            Assert.Equal(1, vm.PageCount);
            Assert.Equal(0, vm.Labels.Count(l => l.HasText));
            vm.Undo();
            Assert.Equal(2, vm.PageCount);
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal("b", vm.Labels[0].Line1);
            Assert.Equal(1, vm.SelectedLabel!.Index);  // Cursor wie vor "Alle löschen"
            vm.PrevPageCommand.Execute(null);
            Assert.Equal("a", vm.Labels[0].Line1);
        });
    }

    [Fact]
    public void Margins_Typing_Coalesces_And_UndoRestoresSettings()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            double original = vm.Panel.MarginTop;
            vm.Panel.MarginTop = 2;
            vm.Panel.MarginTop = 25;
            vm.Panel.MarginTop = 25.5;
            Assert.Equal(1, vm.History.UndoCount);
            vm.Undo();
            Assert.Equal(original, vm.Panel.MarginTop);
            Assert.Equal(original, vm.Settings.MarginTop);
            Assert.False(vm.IsDirty);
        });
    }

    [Fact]
    public void Load_ResetsHistory_SaveMarksCleanPoint()
    {
        Sta.Run(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), $"etprinter_undo_{Guid.NewGuid():N}.etprint");
            try
            {
                var dialogs = new SilentDialogService { NextSaveFile = path };
                var vm = new MainViewModel(dialogs) { SuppressContentLossConfirm = true };
                vm.Labels[0].Line1 = "x";
                vm.MarkChanged("Test");
                vm.Labels[1].Line1 = "y";
                vm.MarkChanged("Test2");
                vm.SaveAsCommand.Execute(null);        // sauberer Punkt = Zustand mit x,y
                Assert.False(vm.IsDirty);
                Assert.Equal(2, vm.History.UndoCount);  // Speichern loescht den Verlauf nicht

                vm.Undo();
                Assert.True(vm.IsDirty);                // weicht vom gespeicherten Stand ab
                Assert.Equal(string.Empty, vm.Labels[1].Line1);
                vm.Redo();
                Assert.False(vm.IsDirty);               // wieder exakt der gespeicherte Stand

                var vm2 = new MainViewModel(dialogs) { SuppressContentLossConfirm = true };
                vm2.Labels[5].Line1 = "z";
                vm2.MarkChanged("Test");
                vm2.OpenFile(path);
                Assert.False(vm2.History.CanUndo);      // Laden beginnt den Verlauf neu
                Assert.Equal("x", vm2.Labels[0].Line1);
            }
            finally { File.Delete(path); ProjectService.RemoveRecentFile(path); }
        });
    }

    [Fact]
    public void NewProject_ClearsHistory()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Labels[0].Line1 = "x";
            vm.MarkChanged("Test");
            vm.NewProjectWithoutConfirm();
            Assert.False(vm.History.CanUndo);
            Assert.False(vm.History.CanRedo);
            Assert.False(vm.IsDirty);
        });
    }

    [Fact]
    public void Import_IsOneStep()
    {
        Sta.Run(() =>
        {
            var vm = NewVm();
            vm.Import.ImportParsedLines(["+K1 DI 8", "E 0.0  E 0.1", "+K2 DQ 8", "A 0.0", "+K3 DI 16", "E 2.0"]);
            Assert.Equal(3, vm.Labels.Count(l => l.HasText));
            Assert.Equal(1, vm.History.UndoCount);
            Assert.Equal("Import", vm.History.NextUndoLabel);
            vm.Undo();
            Assert.Equal(0, vm.Labels.Count(l => l.HasText));
        });
    }
}
