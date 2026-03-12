using System.Windows;
using ETPrinter.Models;

namespace ETPrinter.ViewModels;

public class LabelViewModel : ViewModelBase
{
    private readonly LabelCell _cell;
    private bool _isSelected;

    public LabelViewModel(LabelCell cell)
    {
        _cell = cell;
    }

    public int Index => _cell.Index;
    public int DisplayPosition => _cell.Index + 1;

    public string Header
    {
        get => _cell.Header;
        set { _cell.Header = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public string Line1
    {
        get => _cell.Line1;
        set { _cell.Line1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); OnPropertyChanged(nameof(Line1Parts)); }
    }

    public string Line2
    {
        get => _cell.Line2;
        set { _cell.Line2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); OnPropertyChanged(nameof(Line2Parts)); }
    }

    // Per-Etikett Schrift-Einstellungen
    public int CellFontSize
    {
        get => _cell.FontSize;
        set
        {
            _cell.FontSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewFontSize));
        }
    }

    public bool CellIsBold
    {
        get => _cell.IsBold;
        set
        {
            _cell.IsBold = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewFontWeight));
        }
    }

    public bool CellIsItalic
    {
        get => _cell.IsItalic;
        set
        {
            _cell.IsItalic = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewFontStyle));
        }
    }

    public string CellFontFamily
    {
        get => _cell.FontFamily;
        set { _cell.FontFamily = value; OnPropertyChanged(); OnPropertyChanged(nameof(PreviewFontFamily)); }
    }

    public bool IsPrintEnabled
    {
        get => _cell.IsPrintEnabled;
        set { _cell.IsPrintEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrintOpacity)); }
    }

    public double PrintOpacity => IsPrintEnabled ? 1.0 : 0.4;

    // Vorschau-Eigenschaften (skaliert fuer A4-Preview)
    public double PreviewFontSize => _cell.FontSize * 1.06;
    public FontWeight PreviewFontWeight => _cell.IsBold ? FontWeights.Bold : FontWeights.Normal;
    public FontStyle PreviewFontStyle => _cell.IsItalic ? FontStyles.Italic : FontStyles.Normal;
    public System.Windows.Media.FontFamily PreviewFontFamily => new(_cell.FontFamily);

    /// <summary>Einzelne Adressen aus Line1 (getrennt durch doppeltes Leerzeichen)</summary>
    public string[] Line1Parts => string.IsNullOrWhiteSpace(Line1) ? [] : Line1.Split("  ", StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Einzelne Adressen aus Line2 (getrennt durch doppeltes Leerzeichen)</summary>
    public string[] Line2Parts => string.IsNullOrWhiteSpace(Line2) ? [] : Line2.Split("  ", StringSplitOptions.RemoveEmptyEntries);

    public bool HasText => _cell.HasText;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public LabelCell GetCell() => _cell;

    public void Clear()
    {
        _cell.Clear();
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Line1));
        OnPropertyChanged(nameof(Line2));
        OnPropertyChanged(nameof(Line1Parts));
        OnPropertyChanged(nameof(Line2Parts));
        OnPropertyChanged(nameof(HasText));
    }
}
