using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP9b: herausgeloeste Teile des Haupt-ViewModels
/// (PageDocument, ProjectSession, IDialogService).</summary>
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
