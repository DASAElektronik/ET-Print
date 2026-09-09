using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using ETPrinter.Services;
using ETPrinter.ViewModels;

namespace ETPrinter;

public partial class App : Application
{
    private TestAutomationService? _testService;
    private bool _testMode;

    /// <summary>Notfall-Sicherung bei unbehandelten Exceptions; wird beim naechsten
    /// Start zur Wiederherstellung angeboten.</summary>
    public static readonly string RecoveryFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ETPrinter", "recovery.etprint");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Globale Fehlerbehandlung: ohne diese Handler beendet jede Laufzeit-Exception
        // die App kommentarlos und ohne Log-Eintrag — mit Verlust aller Eingaben.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        // WPF-Bindings parsen/formatieren sonst mit en-US statt der OS-Culture:
        // auf de-DE-Systemen wurde die Komma-Eingabe "20,5" als 205 interpretiert
        // (Komma = en-US-Tausendertrenner) — Raender und Kalibrierung liefen aus dem Blatt.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        // Test-Automation nur starten wenn explizit angefordert:
        //   --test-automation oder Umgebungsvariable ETPRINTER_TEST=1
        _testMode =
            e.Args.Any(a => string.Equals(a, "--test-automation", StringComparison.OrdinalIgnoreCase))
            || string.Equals(Environment.GetEnvironmentVariable("ETPRINTER_TEST"), "1", StringComparison.Ordinal);

        MainWindow = new MainWindow();
        MainWindow.Show();

        var viewModel = (MainViewModel)MainWindow.DataContext;

        if (_testMode)
        {
            _testService = new TestAutomationService(MainWindow, viewModel);
            _testService.Start();
            return;
        }

        OfferRecovery(viewModel);

        // Projektdatei als Startargument (Doppelklick auf .etprint, "Öffnen mit")
        var projectArg = e.Args.FirstOrDefault(a =>
            string.Equals(Path.GetExtension(a), ".etprint", StringComparison.OrdinalIgnoreCase));
        if (projectArg is not null)
            viewModel.OpenFile(projectArg);
    }

    private static void OfferRecovery(MainViewModel viewModel)
    {
        if (!File.Exists(RecoveryFilePath)) return;
        try
        {
            var result = MessageBox.Show(
                "ET-Printer wurde beim letzten Mal unerwartet beendet.\n" +
                "Es wurde eine Notfall-Sicherung der Eingaben gefunden.\n\n" +
                "Wiederherstellen?",
                "Wiederherstellung", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var project = ProjectService.Load(RecoveryFilePath, addToRecent: false);
                viewModel.ApplyLoadedProject(project, null);
                viewModel.MarkDirty("Notfall-Sicherung wiederhergestellt — bitte speichern");
            }
        }
        catch (Exception ex)
        {
            Log.Error("Wiederherstellung fehlgeschlagen", ex);
            MessageBox.Show($"Die Notfall-Sicherung konnte nicht geladen werden:\n{ex.Message}",
                "Wiederherstellung", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            try { File.Delete(RecoveryFilePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Warn(ex.Message); }
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Unbehandelte Exception (UI-Thread)", e.Exception);
        bool saved = TrySaveRecovery();
        e.Handled = true;

        if (_testMode) return; // headless: keine modale Box

        MessageBox.Show(
            $"Es ist ein unerwarteter Fehler aufgetreten:\n{e.Exception.Message}\n\n" +
            (saved ? "Ihre Eingaben wurden als Notfall-Sicherung gespeichert.\n" : "") +
            "Bitte speichern Sie das Projekt und starten Sie ET-Printer neu.\n" +
            $"Details: {Log.FilePath}",
            "Unerwarteter Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error("Unbehandelte Exception (Prozess)", e.ExceptionObject as Exception);
        TrySaveRecovery();
    }

    private bool TrySaveRecovery()
    {
        try
        {
            if (MainWindow?.DataContext is not MainViewModel vm) return false;
            if (!vm.HasAnyContentForRecovery) return false;
            ProjectService.Save(vm.BuildProject(), RecoveryFilePath, addToRecent: false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Notfall-Sicherung fehlgeschlagen", ex);
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _testService?.Dispose();
        base.OnExit(e);
    }
}
