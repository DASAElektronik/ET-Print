using System.Collections.ObjectModel;
using System.IO;
using ETPrinter.Models;
using ETPrinter.Services;

namespace ETPrinter.ViewModels;

public record RecentFileItem(string FilePath, string DisplayName);

/// <summary>
/// Projektdatei-Sitzung (ABSCHLUSSPLAN AP9, Schnitt "ProjectSession"): aktueller
/// Dateipfad, Dirty-Zustand, Zuletzt-geoeffnet-Liste sowie die Ablaeufe Neu / Oeffnen /
/// Speichern / Speichern unter mit allen Rueckfragen. Den eigentlichen Zustand liefert
/// bzw. uebernimmt der Aufrufer ueber <see cref="Snapshot"/> und <see cref="Restore"/>
/// (Haupt-ViewModel: BuildProject / ApplyLoadedProject).
/// </summary>
public sealed class ProjectSession : ViewModelBase
{
    internal const string ProjectFileFilter = "ET-Printer Projekt (*.etprint)|*.etprint";

    private readonly IDialogService _dialogs;
    private string? _currentFilePath;
    private bool _isDirty;

    public ProjectSession(IDialogService dialogs)
    {
        _dialogs = dialogs;
        RefreshRecentFiles();
    }

    /// <summary>Liefert den kompletten Projektzustand (zum Speichern).</summary>
    public Func<LabelProject>? Snapshot { get; set; }

    /// <summary>Uebernimmt ein geladenes Projekt vollstaendig (Datei-Pfad zur Anzeige).</summary>
    public Action<LabelProject, string?>? Restore { get; set; }

    /// <summary>Setzt den Arbeitsbereich auf ein leeres Projekt zurueck.</summary>
    public Action? ResetToEmpty { get; set; }

    /// <summary>Seitenzahl fuer Statusmeldungen.</summary>
    public Func<int>? PageCountProvider { get; set; }

    public event Action<string>? StatusRequested;

    public ObservableCollection<RecentFileItem> RecentFiles { get; } = [];

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (SetProperty(ref _currentFilePath, value))
                OnPropertyChanged(nameof(FileName));
        }
    }

    /// <summary>Dateiname ohne Pfad, null bei ungespeichertem Projekt.</summary>
    public string? FileName => _currentFilePath is null ? null : Path.GetFileName(_currentFilePath);

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    private void Status(string message) => StatusRequested?.Invoke(message);

    private int PageCount => PageCountProvider?.Invoke() ?? 0;

    // ---- Neu -------------------------------------------------------------------

    /// <summary>"Neues Projekt": Rueckfrage bei Aenderungen, dann Arbeitsbereich leeren.</summary>
    public void NewProject()
    {
        if (!ConfirmDiscardChanges()) return;
        NewProjectWithoutConfirm();
    }

    /// <summary>Test-Automation: "Neues Projekt" ohne Rueckfrage.</summary>
    public void NewProjectWithoutConfirm()
    {
        CurrentFilePath = null;
        ResetToEmpty?.Invoke();
        IsDirty = false;
        Status("Neues Projekt erstellt");
    }

    // ---- Speichern ---------------------------------------------------------------

    public void Save()
    {
        if (_currentFilePath is null) { SaveAs(); return; }
        DoSave(_currentFilePath);
    }

    public void SaveAs()
    {
        var path = _dialogs.SaveFile(ProjectFileFilter, ".etprint",
            Path.GetFileNameWithoutExtension(_currentFilePath ?? "Projekt"));
        if (path is not null)
            DoSave(path);
    }

    /// <summary>Speichert nach <paramref name="filePath"/>; false bei Fehler (modal gemeldet,
    /// weil beim Schliessen/Neu/Oeffnen die Statusleiste nicht mehr sichtbar ist).</summary>
    public bool DoSave(string filePath)
    {
        if (Snapshot is null) return false;
        try
        {
            ProjectService.Save(Snapshot(), filePath);
            CurrentFilePath = filePath;
            IsDirty = false;
            RefreshRecentFiles();
            Status($"Gespeichert: {Path.GetFileName(filePath)} ({PageCount} Seiten)");
            return true;
        }
        catch (Exception ex)
        {
            Status($"Speicherfehler: {ex.Message}");
            _dialogs.ShowError($"Das Projekt konnte nicht gespeichert werden:\n{ex.Message}", "Speicherfehler");
            return false;
        }
    }

    // ---- Oeffnen -------------------------------------------------------------------

    public void Open()
    {
        if (!ConfirmDiscardChanges()) return;
        var path = _dialogs.OpenFile(ProjectFileFilter, "Projekt oeffnen");
        if (path is not null)
            DoOpen(path);
    }

    public void OpenRecent(string? filePath)
    {
        if (filePath is null) return;
        if (!File.Exists(filePath))
        {
            // Toter Eintrag: anbieten, ihn aus der Liste zu entfernen
            if (_dialogs.Confirm(
                $"Die Datei wurde nicht gefunden:\n{filePath}\n\nEintrag aus der Liste entfernen?",
                "Datei nicht gefunden"))
            {
                ProjectService.RemoveRecentFile(filePath);
                RefreshRecentFiles();
            }
            Status("Datei nicht gefunden");
            return;
        }
        if (!ConfirmDiscardChanges()) return;
        DoOpen(filePath);
    }

    /// <summary>Oeffnet eine Projektdatei (Drag&amp;Drop, Kommandozeile) mit Rueckfrage bei
    /// ungespeicherten Aenderungen.</summary>
    public void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            Status("Datei nicht gefunden");
            return;
        }
        if (!ConfirmDiscardChanges()) return;
        DoOpen(path);
    }

    private void DoOpen(string filePath)
    {
        if (Restore is null) return;
        try
        {
            // Recent-Eintrag erst nach erfolgreichem Laden (eine kaputte Datei
            // wanderte sonst an die Spitze der Liste)
            var project = ProjectService.Load(filePath, addToRecent: false);
            Restore(project, filePath);
            MarkLoaded(filePath);
            ProjectService.AddRecentFile(filePath);
            RefreshRecentFiles();
            Status($"Geladen: {Path.GetFileName(filePath)} ({PageCount} Seiten)");
        }
        catch (Exception ex)
        {
            Status($"Ladefehler: {ex.Message}");
            _dialogs.ShowError($"Das Projekt konnte nicht geladen werden:\n{filePath}\n\n{ex.Message}", "Ladefehler");
        }
    }

    /// <summary>Nach einem Restore (auch Test-Automation/Recovery): Pfad uebernehmen,
    /// Projekt gilt als unveraendert.</summary>
    public void MarkLoaded(string? filePath)
    {
        CurrentFilePath = filePath;
        IsDirty = false;
        RefreshRecentFiles();
    }

    public void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in ProjectService.LoadRecentFiles())
            RecentFiles.Add(new RecentFileItem(path, Path.GetFileName(path)));
    }

    // ---- Rueckfragen ------------------------------------------------------------------

    /// <summary>Fragt, ob ungespeicherte Aenderungen verworfen werden sollen. True =
    /// fortfahren (gespeichert oder verworfen).</summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_isDirty) return true;

        return _dialogs.ConfirmSave(
            "Es gibt ungespeicherte Aenderungen.\nMoechten Sie diese speichern?",
            "Ungespeicherte Aenderungen") switch
        {
            SaveDecision.Save => DoSaveAndConfirm(),
            SaveDecision.Discard => true,
            _ => false // Cancel
        };
    }

    private bool DoSaveAndConfirm()
    {
        if (_currentFilePath is null)
        {
            var path = _dialogs.SaveFile(ProjectFileFilter, ".etprint", "Projekt");
            return path is not null && DoSave(path);
        }
        return DoSave(_currentFilePath);
    }
}
