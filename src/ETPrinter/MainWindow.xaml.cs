using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ETPrinter.Services;
using ETPrinter.ViewModels;

namespace ETPrinter;

public partial class MainWindow : Window
{
    private const double PreviewPageHeightPx = 891; // A4 bei 3 px/mm
    private const double PreviewPageWidthPx = 630;

    public MainWindow()
    {
        InitializeComponent();
        RestoreUiState();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        ViewModel.FitZoomRequested += FitZoom;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    // ------------------------------------------------------------------
    // Fensterzustand (Groesse, Position, Zoom, Splitter)
    // ------------------------------------------------------------------

    private UiState _uiState = new();

    private void RestoreUiState()
    {
        _uiState = UiStateService.Load();
        var (left, top, width, height) = UiStateService.Sanitize(_uiState,
            SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight,
            MinWidth, MinHeight);
        Width = width;
        Height = height;
        if (!double.IsNaN(left) && !double.IsNaN(top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        if (_uiState.IsMaximized) WindowState = WindowState.Maximized;
        if (_uiState.LeftPanelWidth >= 200 && _uiState.LeftPanelWidth <= 800)
            LeftPanelColumn.Width = new GridLength(_uiState.LeftPanelWidth);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Startzoom: gespeicherter Wert, sonst ganze Seite sichtbar
        if (!double.IsNaN(_uiState.Zoom) && _uiState.Zoom >= 0.3 && _uiState.Zoom <= 5.0)
            ViewModel.Zoom = _uiState.Zoom;
        else
            FitZoom();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        UiStateService.Save(new UiState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized,
            Zoom = ViewModel.Zoom,
            LeftPanelWidth = LeftPanelColumn.ActualWidth
        });
    }

    /// <summary>Test-Automation: ausstehende Vorschau-Neuaufbauten sofort ausfuehren.</summary>
    public void FlushPreview()
    {
        MpPreview.FlushRender();
        UpdateLayout();
    }

    // ------------------------------------------------------------------
    // Zoom
    // ------------------------------------------------------------------

    /// <summary>Zoom so setzen, dass die ganze A4-Seite in den Vorschaubereich passt.</summary>
    public void FitZoom()
    {
        var viewer = ViewModel.IsModuleBased ? MpScrollViewer : SpScrollViewer;
        double w = viewer.ActualWidth, h = viewer.ActualHeight;
        if (w <= 0 || h <= 0) { w = PreviewHost.ActualWidth; h = PreviewHost.ActualHeight - 70; }
        if (w <= 0 || h <= 0) return;
        // Border-Margin 20 + Schatten
        double zoom = Math.Min((w - 50) / PreviewPageWidthPx, (h - 50) / PreviewPageHeightPx);
        ViewModel.Zoom = Math.Clamp(Math.Floor(zoom * 20) / 20, 0.3, 5.0);
    }

    private void FitZoom_Click(object sender, RoutedEventArgs e) => FitZoom();

    private void Preview_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
        double step = e.Delta > 0 ? 0.1 : -0.1;
        ViewModel.Zoom = Math.Clamp(Math.Round((ViewModel.Zoom + step) * 10) / 10, 0.3, 5.0);
        e.Handled = true;
    }

    // ------------------------------------------------------------------
    // Drag & Drop von .etprint-Dateien
    // ------------------------------------------------------------------

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedProjectPath(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        var path = GetDroppedProjectPath(e);
        if (path is not null) ViewModel.OpenFile(path);
    }

    private static string? GetDroppedProjectPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return null;
        return files.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".etprint", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------------
    // Etiketten-Klicks (SP-Vorschau)
    // ------------------------------------------------------------------

    private void Label_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not LabelViewModel label)
            return;

        var mods = Keyboard.Modifiers;
        if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            // Shift+Klick: Range von letzter Selection bis aktuellem Label
            ViewModel.CheckRangeToLabel(label);
        }
        else if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // Strg+Klick: Toggle Checked-Status (Multi-Select), Anker wird mitgenommen
            ViewModel.ToggleCheck(label);
        }
        else
        {
            // Normaler Klick: Single-Selection, alle Checks aufheben
            ViewModel.ClearAllLabelChecks();
            ViewModel.SelectLabel(label);
            Line1TextBox?.Focus();
        }
    }

    /// <summary>Rechtsklick selektiert das Etikett, damit "Einfügen"/"Druck umschalten"
    /// im Kontextmenue das angeklickte Etikett treffen, nicht das zuvor markierte.</summary>
    private void Label_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is LabelViewModel label)
            ViewModel.SelectLabel(label);
    }

    private void TogglePrint_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TogglePrintCommand.CanExecute(null))
            ViewModel.TogglePrintCommand.Execute(null);
    }

    // ------------------------------------------------------------------
    // Menue
    // ------------------------------------------------------------------

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        string versionText = version is null ? "" : $"v{version.Major}.{version.Minor}.{version.Build}";
        MessageBox.Show(
            $"ET-Printer {versionText}\n\n" +
            "Beschriftungsstreifen für Siemens ET 200SP und\n" +
            "S7-1500 / ET 200MP (35 mm und 25 mm) auf A4 drucken.\n\n" +
            "Etikettenbögen: 6ES7193-6LA10-0AA0 (ET 200SP),\n" +
            "6ES7592-1AX00-0AA0 (35 mm), 6ES7592-2AX00-0AA0 (25 mm)\n\n" +
            "Basierend auf den Siemens Excel-Templates\n" +
            "(Beitrags-IDs 81524595 und 83681795)\n\n" +
            $"Logdatei: {Log.FilePath}",
            "Info",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
