using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP9b: herausgeloeste Teile des Haupt-ViewModels
/// (PageDocument, ProjectSession, IDialogService).</summary>
[Collection("RecentFiles")] // gemeinsame recent.json: nicht parallel
public class DecompositionTests
{
    // ---- PageDocument<T> ---------------------------------------------------------

    [Fact]
    public void PageDocument_ResetShowsFirstPage()
    {
        var doc = new PageDocument<string>();
        doc.Reset(["a", "b"]);
        Assert.Equal(1, doc.PageCount);
        Assert.Equal(0, doc.CurrentIndex);
        Assert.Equal(["a", "b"], doc.Visible);
    }

    [Fact]
    public void PageDocument_AddPage_DoesNotChangeVisible()
    {
        var doc = new PageDocument<string>();
        doc.Reset(["a"]);
        doc.AddPage(["b"]);
        Assert.Equal(2, doc.PageCount);
        Assert.Equal(["a"], doc.Visible);
        Assert.True(doc.Show(1));
        Assert.Equal(["b"], doc.Visible);
        Assert.False(doc.Show(2));
        Assert.Equal(1, doc.CurrentIndex);
    }

    [Fact]
    public void PageDocument_RemovePage_KeepsLastPage_AndClampsIndex()
    {
        var doc = new PageDocument<string>();
        doc.Reset(["a"]);
        Assert.False(doc.RemovePage(0));       // letzte Seite bleibt
        doc.AddPage(["b"]);
        doc.AddPage(["c"]);
        doc.Show(2);
        Assert.True(doc.RemovePage(2));
        Assert.Equal(1, doc.CurrentIndex);     // auf die neue letzte Seite
        Assert.Equal(["b"], doc.Visible);
    }

    [Fact]
    public void PageDocument_EnsurePage_CreatesMissingPages()
    {
        var doc = new PageDocument<int>();
        doc.Reset([1]);
        int created = 0;
        doc.EnsurePage(3, () => { created++; return [created]; });
        Assert.Equal(4, doc.PageCount);
        Assert.Equal(3, created);
        Assert.Equal([1, 1, 2, 3], doc.AllItems);
    }

    // ---- ProjectSession ----------------------------------------------------------

    private static (ProjectSession session, SilentDialogService dialogs, List<string> status) NewSession()
    {
        var dialogs = new SilentDialogService();
        var status = new List<string>();
        var session = new ProjectSession(dialogs)
        {
            Snapshot = () => new LabelProject(),
            Restore = (_, _) => { },
            ResetToEmpty = () => { },
            PageCountProvider = () => 1
        };
        session.StatusRequested += status.Add;
        return (session, dialogs, status);
    }

    [Fact]
    public void Session_ConfirmDiscard_CleanProjectNeedsNoDialog()
    {
        var (session, dialogs, _) = NewSession();
        Assert.True(session.ConfirmDiscardChanges());
        Assert.Empty(dialogs.Messages);
    }

    [Fact]
    public void Session_ConfirmDiscard_CancelBlocks_DiscardContinues()
    {
        var (session, dialogs, _) = NewSession();
        session.IsDirty = true;
        dialogs.SaveAnswer = SaveDecision.Cancel;
        Assert.False(session.ConfirmDiscardChanges());
        dialogs.SaveAnswer = SaveDecision.Discard;
        Assert.True(session.ConfirmDiscardChanges());
        Assert.True(session.IsDirty); // Verwerfen aendert den Zustand nicht
    }

    [Fact]
    public void Session_ConfirmDiscard_SaveWritesFileAndClearsDirty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_session_{Guid.NewGuid():N}.etprint");
        try
        {
            var (session, dialogs, status) = NewSession();
            session.IsDirty = true;
            dialogs.SaveAnswer = SaveDecision.Save;
            dialogs.NextSaveFile = path;

            Assert.True(session.ConfirmDiscardChanges());
            Assert.True(File.Exists(path));
            Assert.False(session.IsDirty);
            Assert.Equal(path, session.CurrentFilePath);
            Assert.Equal(Path.GetFileName(path), session.FileName);
            Assert.Contains(status, m => m.StartsWith("Gespeichert:"));
        }
        finally { File.Delete(path); ProjectService.RemoveRecentFile(path); }
    }

    [Fact]
    public void Session_SaveAsCancelled_KeepsState()
    {
        var (session, dialogs, _) = NewSession();
        session.IsDirty = true;
        dialogs.NextSaveFile = null;
        session.SaveAs();
        Assert.True(session.IsDirty);
        Assert.Null(session.CurrentFilePath);
    }

    [Fact]
    public void Session_OpenFile_Missing_ReportsWithoutDialog()
    {
        var (session, dialogs, status) = NewSession();
        session.OpenFile(Path.Combine(Path.GetTempPath(), "gibt_es_nicht.etprint"));
        Assert.Contains("Datei nicht gefunden", status);
        Assert.Empty(dialogs.Messages);
    }

    [Fact]
    public void Session_OpenRecent_DeadEntry_OffersRemoval()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_dead_{Guid.NewGuid():N}.etprint");
        ProjectService.AddRecentFile(path);
        try
        {
            var (session, dialogs, _) = NewSession();
            dialogs.ConfirmAnswer = true;
            session.OpenRecent(path);
            Assert.Contains(dialogs.Messages, m => m.StartsWith("[Confirm] Datei nicht gefunden"));
            Assert.DoesNotContain(session.RecentFiles, r => string.Equals(r.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        }
        finally { ProjectService.RemoveRecentFile(path); }
    }

    [Fact]
    public void Session_Open_CorruptFile_ShowsErrorAndKeepsRecentClean()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_corrupt_{Guid.NewGuid():N}.etprint");
        File.WriteAllText(path, "{ kaputt");
        try
        {
            var (session, dialogs, status) = NewSession();
            session.OpenFile(path);
            Assert.Contains(dialogs.Messages, m => m.StartsWith("[Error] Ladefehler"));
            Assert.Contains(status, m => m.StartsWith("Ladefehler"));
            Assert.Null(session.CurrentFilePath);
            Assert.DoesNotContain(ProjectService.LoadRecentFiles(),
                p => string.Equals(p, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Session_NewProject_ResetsPathAndDirty()
    {
        var (session, _, status) = NewSession();
        bool reset = false;
        session.ResetToEmpty = () => { reset = true; session.IsDirty = true; };
        session.IsDirty = false;
        session.NewProject();
        Assert.True(reset);
        Assert.False(session.IsDirty);   // Reset darf keinen Dirty-Rest hinterlassen
        Assert.Null(session.CurrentFilePath);
        Assert.Contains("Neues Projekt erstellt", status);
    }

    // ---- Haupt-ViewModel mit stummen Dialogen ----------------------------------------

    [Fact]
    public void MainViewModel_WindowTitle_FollowsSession()
    {
        Sta.Run(() =>
        {
            var dialogs = new SilentDialogService();
            var vm = new MainViewModel(dialogs) { SuppressContentLossConfirm = true };
            Assert.StartsWith("ET-Printer - ", vm.WindowTitle);
            Assert.DoesNotContain("*", vm.WindowTitle);

            var titles = new List<string>();
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.WindowTitle)) titles.Add(vm.WindowTitle); };
            vm.Labels[0].Line1 = "x";
            vm.PrintGridLines = true;           // IsDirty=true ueber die Session
            Assert.EndsWith(" *", vm.WindowTitle);
            Assert.NotEmpty(titles);
        });
    }

    [Fact]
    public void MainViewModel_NewProject_DirtyCancel_KeepsContent()
    {
        Sta.Run(() =>
        {
            var dialogs = new SilentDialogService { SaveAnswer = SaveDecision.Cancel };
            var vm = new MainViewModel(dialogs) { SuppressContentLossConfirm = true };
            vm.Labels[0].Line1 = "bleibt";
            vm.MarkDirty("test");
            vm.NewProjectCommand.Execute(null);
            Assert.Equal("bleibt", vm.Labels[0].Line1);
            Assert.True(vm.IsDirty);

            dialogs.SaveAnswer = SaveDecision.Discard;
            vm.NewProjectCommand.Execute(null);
            Assert.Equal(string.Empty, vm.Labels[0].Line1);
            Assert.False(vm.IsDirty);
            Assert.Equal(1, vm.PageCount);
        });
    }

    [Fact]
    public void MainViewModel_RemovePage_NavigatesToNeighbour()
    {
        Sta.Run(() =>
        {
            var vm = new MainViewModel(new SilentDialogService()) { SuppressContentLossConfirm = true };
            vm.AddPageCommand.Execute(null);
            vm.AddPageCommand.Execute(null);
            Assert.Equal(2, vm.CurrentPageIndex);
            vm.Labels[0].Line1 = "S3";
            vm.PrevPageCommand.Execute(null);   // Seite 2
            vm.RemovePageCommand.Execute(null);
            Assert.Equal(2, vm.PageCount);
            Assert.Equal(1, vm.CurrentPageIndex);
            Assert.Equal("S3", vm.Labels[0].Line1); // Seite 3 rueckt nach
            Assert.Same(vm.Labels[0], vm.SelectedLabel);
        });
    }
}

/// <summary>AP9b Schritt 3: MpEditorViewModel und ImportCoordinator.</summary>
[Collection("RecentFiles")] // gemeinsame recent.json: nicht parallel
public class DecompositionTests2
{
    private static MainViewModel NewMpVm(SilentDialogService? dialogs = null)
    {
        var vm = new MainViewModel(dialogs ?? new SilentDialogService()) { SuppressContentLossConfirm = true };
        vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP);
        return vm;
    }

    [Fact]
    public void MpEditor_SelectionForwardsToMainViewModel()
    {
        Sta.Run(() =>
        {
            var vm = NewMpVm();
            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);
            vm.MpEditor.SelectedModule = vm.MpModules[2];
            Assert.Same(vm.MpModules[2], vm.SelectedMpModule);
            Assert.Contains(nameof(MainViewModel.SelectedMpModule), raised);
            Assert.Contains(nameof(MainViewModel.HasSelection), raised);
            Assert.StartsWith("Modul 3 / 10", vm.MpEditor.SelectionInfo);
            Assert.Equal(vm.MpEditor.SelectionInfo, vm.EditTargetInfo);
        });
    }

    [Fact]
    public void MpEditor_ModuleChange_DropsCellOfOldModule()
    {
        Sta.Run(() =>
        {
            var vm = NewMpVm();
            vm.SelectedMpModule = vm.MpModules[0];
            vm.SelectedMpCell = vm.MpModules[0].AddressCells.First(c => c.IsEditable);
            Assert.NotNull(vm.SelectedMpCell);
            vm.SelectedMpModule = vm.MpModules[1];
            Assert.Null(vm.SelectedMpCell);
        });
    }

    [Fact]
    public void MpEditor_VariantChange_ClearsArticle_AndRebindsCell()
    {
        Sta.Run(() =>
        {
            var vm = NewMpVm();
            var mod = vm.MpModules[0];
            vm.SelectedMpModule = mod;
            vm.MpEditor.SelectedArticle = MpModuleCatalog.Find("6ES7521-1BL00-0AB0"); // DI 32 HF
            Assert.Equal(MpModuleVariant.DI_DQ_32, mod.Variant);
            var cell = mod.AddressCells.First(c => c.IsEditable);
            vm.SelectedMpCell = cell;

            vm.MpEditor.SelectedVariant = MpModuleLayoutFactory.GetLayout(MpModuleVariant.DI_DQ_16);
            Assert.Null(mod.ArticleNumber);
            Assert.Equal(MpModuleCatalog.CustomEntry, vm.MpEditor.SelectedArticle);
            Assert.NotNull(vm.SelectedMpCell);                  // per CellIndex neu aufgeloest
            Assert.NotSame(cell, vm.SelectedMpCell);
            Assert.Equal(cell.CellIndex, vm.SelectedMpCell!.CellIndex);
        });
    }

    [Fact]
    public void MpEditor_FamilySwitch_UpdatesVariantsAndArticles()
    {
        Sta.Run(() =>
        {
            var vm = NewMpVm();
            Assert.Contains(vm.MpEditor.AvailableVariants, v => v.Variant == MpModuleVariant.DI_DQ_32);
            vm.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(ProductFamily.S71500_ET200MP_25mm);
            Assert.All(vm.MpEditor.AvailableVariants, v => Assert.True(MpModuleLayoutFactory.Is25mmVariant(v.Variant)));
            Assert.All(vm.MpEditor.AvailableArticles.Where(e => e.ArticleNo != ""),
                e => Assert.True(MpModuleLayoutFactory.Is25mmVariant(e.Variant)));
        });
    }

    // ---- ImportCoordinator ------------------------------------------------------------

    private sealed class FakeTarget : IImportTarget
    {
        public bool IsModuleBased { get; set; }
        public bool IsDoubleLine { get; set; } = true;
        public List<LabelCell>? Cells;
        public List<ParsedModule>? Modules;
        public void PopulateLabels(List<LabelCell> cells) => Cells = cells;
        public void PopulateModules(List<ParsedModule> modules) => Modules = modules;
    }

    private static readonly string[] SchematicLines =
    [
        "+K10 DI 16x24VDC",
        "E 4.0  E 4.1  E 4.2  E 4.3  E 4.4  E 4.5  E 4.6  E 4.7",
        "E 5.0  E 5.1  E 5.2  E 5.3  E 5.4  E 5.5  E 5.6  E 5.7",
    ];

    [Fact]
    public void Import_ParsedLines_SpTarget_GetsCells()
    {
        var target = new FakeTarget();
        var import = new ImportCoordinator(new SilentDialogService(), target);
        int n = import.ImportParsedLines(SchematicLines);
        Assert.True(n >= 1);
        Assert.NotNull(target.Cells);
        Assert.Null(target.Modules);
        Assert.Contains("E 4.0", target.Cells![0].Line1 + target.Cells[0].Line2);
    }

    [Fact]
    public void Import_ParsedLines_MpTarget_GetsModules()
    {
        var target = new FakeTarget { IsModuleBased = true };
        var import = new ImportCoordinator(new SilentDialogService(), target);
        import.ImportParsedLines(SchematicLines);
        Assert.NotNull(target.Modules);
        Assert.Null(target.Cells);
    }

    [Fact]
    public async Task Import_CsvDialogCancelled_DoesNothing()
    {
        var target = new FakeTarget();
        var dialogs = new SilentDialogService { NextOpenFile = null };
        var import = new ImportCoordinator(dialogs, target);
        var status = new List<string>();
        import.StatusRequested += status.Add;
        await Sta.RunAsync(import.ImportCsvAsync);
        Assert.Null(target.Cells);
        Assert.Empty(status);
    }

    [Fact]
    public async Task Import_CsvFile_ViaDialog_PopulatesAndReports()
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_import_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, "Kopf;Zeile1;Zeile2\nM1;E 0.0;E 0.1\nM2;E 0.2;E 0.3\n");
        try
        {
            var target = new FakeTarget();
            var dialogs = new SilentDialogService { NextOpenFile = path };
            var import = new ImportCoordinator(dialogs, target);
            var status = new List<string>();
            import.StatusRequested += status.Add;
            await Sta.RunAsync(import.ImportCsvAsync);
            Assert.NotNull(target.Cells);
            Assert.Equal(2, target.Cells!.Count);
            Assert.Contains(status, m => m.Contains("2 Etiketten aus CSV importiert"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Import_CsvParserError_ShowsErrorDialog()
    {
        var target = new FakeTarget();
        var dialogs = new SilentDialogService { NextOpenFile = Path.Combine(Path.GetTempPath(), "fehlt_" + Guid.NewGuid().ToString("N") + ".csv") };
        var import = new ImportCoordinator(dialogs, target);
        await Sta.RunAsync(import.ImportCsvAsync);
        Assert.Contains(dialogs.Messages, m => m.StartsWith("[Error] Importfehler"));
        Assert.Null(target.Cells);
    }

    [Fact]
    public void MainViewModel_ImportCommands_LockedInModuleMode()
    {
        Sta.Run(() =>
        {
            var vm = NewMpVm();
            Assert.False(vm.ImportCsvCommand.CanExecute(null));
            Assert.False(vm.ImportExcelCommand.CanExecute(null));
            Assert.True(vm.ImportSchematicCommand.CanExecute(null));
            Assert.True(vm.Import.ImportParsedLines(SchematicLines) >= 1);
            Assert.Equal("+K10", vm.MpModules[0].HeaderText);
        });
    }
}
