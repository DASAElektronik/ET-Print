using System.Windows;

namespace ETPrinter.Services;

/// <summary>Antwort einer "Speichern? Ja / Nein / Abbrechen"-Rueckfrage.</summary>
public enum SaveDecision { Save, Discard, Cancel }

/// <summary>
/// Alle Dialoge des ViewModels laufen ueber diese Schnittstelle (ABSCHLUSSPLAN AP9):
/// MessageBoxen und Datei-Dialoge sind damit aus der Logik herausgeloest, Tests und
/// die Test-Automation koennen eine stumme Implementierung einsetzen.
/// </summary>
public interface IDialogService
{
    /// <summary>Ja/Nein-Rueckfrage (Warnung). True = Ja.</summary>
    bool Confirm(string message, string title);

    /// <summary>Speichern? Ja / Nein / Abbrechen.</summary>
    SaveDecision ConfirmSave(string message, string title);

    void ShowInfo(string message, string title);
    void ShowError(string message, string title);

    /// <summary>Datei-oeffnen-Dialog; null bei Abbruch.</summary>
    string? OpenFile(string filter, string title);

    /// <summary>Datei-speichern-Dialog; null bei Abbruch.</summary>
    string? SaveFile(string filter, string defaultExt, string defaultFileName);
}

/// <summary>Produktive Implementierung ueber WPF-MessageBox und Microsoft.Win32-Dialoge.</summary>
public sealed class WpfDialogService : IDialogService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public SaveDecision ConfirmSave(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => SaveDecision.Save,
            MessageBoxResult.No => SaveDecision.Discard,
            _ => SaveDecision.Cancel
        };

    public void ShowInfo(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public string? OpenFile(string filter, string title)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = title };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? SaveFile(string filter, string defaultExt, string defaultFileName)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            DefaultExt = defaultExt,
            FileName = defaultFileName
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}

/// <summary>
/// Stumme Implementierung fuer Tests und Test-Automation: keine Fenster, alle
/// Rueckfragen werden mit dem konfigurierten Ergebnis beantwortet, Meldungen
/// werden nur gesammelt.
/// </summary>
public sealed class SilentDialogService : IDialogService
{
    public bool ConfirmAnswer { get; set; } = true;
    public SaveDecision SaveAnswer { get; set; } = SaveDecision.Discard;
    public string? NextOpenFile { get; set; }
    public string? NextSaveFile { get; set; }
    public List<string> Messages { get; } = [];

    public bool Confirm(string message, string title) { Messages.Add($"[Confirm] {title}: {message}"); return ConfirmAnswer; }
    public SaveDecision ConfirmSave(string message, string title) { Messages.Add($"[ConfirmSave] {title}: {message}"); return SaveAnswer; }
    public void ShowInfo(string message, string title) => Messages.Add($"[Info] {title}: {message}");
    public void ShowError(string message, string title) => Messages.Add($"[Error] {title}: {message}");
    public string? OpenFile(string filter, string title) => NextOpenFile;
    public string? SaveFile(string filter, string defaultExt, string defaultFileName) => NextSaveFile;
}
