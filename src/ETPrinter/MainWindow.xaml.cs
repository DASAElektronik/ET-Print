using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ETPrinter.ViewModels;

namespace ETPrinter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.ConfirmDiscardChanges())
            e.Cancel = true;
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    /// <summary>Test-Automation: ausstehende Vorschau-Neuaufbauten sofort ausfuehren.</summary>
    public void FlushPreview()
    {
        MpPreview.FlushRender();
        UpdateLayout();
    }

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

    /// <summary>Rechtsklick selektiert das Etikett, damit "Einfuegen"/"Druck umschalten"
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

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "ET-Printer v1.0\n\n" +
            "Beschriftungsstreifen fuer Siemens ET 200SP\n" +
            "auf A4-Blaetter drucken.\n\n" +
            "Basierend auf dem Siemens Excel Template\n" +
            "(Beitrags-ID: 81524595)",
            "Info",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
