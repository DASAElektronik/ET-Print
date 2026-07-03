using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using ETPrinter.Services;
using ETPrinter.ViewModels;

namespace ETPrinter;

public partial class App : Application
{
    private TestAutomationService? _testService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // WPF-Bindings parsen/formatieren sonst mit en-US statt der OS-Culture:
        // auf de-DE-Systemen wurde die Komma-Eingabe "20,5" als 205 interpretiert
        // (Komma = en-US-Tausendertrenner) — Raender und Kalibrierung liefen aus dem Blatt.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));

        MainWindow = new MainWindow();
        MainWindow.Show();

        // Test-Automation nur starten wenn explizit angefordert:
        //   --test-automation oder Umgebungsvariable ETPRINTER_TEST=1
        bool testMode =
            e.Args.Any(a => string.Equals(a, "--test-automation", StringComparison.OrdinalIgnoreCase))
            || string.Equals(Environment.GetEnvironmentVariable("ETPRINTER_TEST"), "1", StringComparison.Ordinal);

        if (testMode)
        {
            var viewModel = (MainViewModel)MainWindow.DataContext;
            _testService = new TestAutomationService(MainWindow, viewModel);
            _testService.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _testService?.Dispose();
        base.OnExit(e);
    }
}
