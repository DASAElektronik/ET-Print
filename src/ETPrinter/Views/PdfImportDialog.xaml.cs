using System.Windows;
using System.Windows.Data;
using ETPrinter.ViewModels;

namespace ETPrinter.Views;

public partial class PdfImportDialog : Window
{
    public PdfImportViewModel ViewModel { get; }

    public PdfImportDialog(PdfImportViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = ViewModel;

        Resources.Add("StringToVisibilityConverter", new StringToVisibilityConverter());

        InitializeComponent();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

internal class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
