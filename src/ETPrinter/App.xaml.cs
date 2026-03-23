using System.Windows;
using ETPrinter.Services;
using ETPrinter.ViewModels;

namespace ETPrinter;

public partial class App : Application
{
    private TestAutomationService? _testService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Test-Automation starten (Named Pipe Server)
        MainWindow = new MainWindow();
        MainWindow.Show();

        var viewModel = (MainViewModel)MainWindow.DataContext;
        _testService = new TestAutomationService(MainWindow, viewModel);
        _testService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _testService?.Dispose();
        base.OnExit(e);
    }
}
