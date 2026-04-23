using System.Windows;
using ETPrinter.Models;

namespace ETPrinter.ViewModels;

public class LabelViewModel : ViewModelBase
{
    private readonly LabelCell _cell;
    private bool _isSelected;
    private bool _isChecked;

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
        set { _cell.Line1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); OnPropertyChanged(nameof(Line1Parts)); OnPropertyChanged(nameof(EffectiveLine2Parts)); }
    }

    public string Line2
    {
        get => _cell.Line2;
        set { _cell.Line2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); OnPropertyChanged(nameof(Line2Parts)); OnPropertyChanged(nameof(HasLine2)); OnPropertyChanged(nameof(EffectiveLine2Parts)); }
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

    /// <summary>Einzelne Adressen aus Line1 (getrennt durch doppeltes Leerzeichen).
    /// Leere Strings bleiben erhalten — so sind bei analog Platzhalter-Slots zwischen
    /// und hinter den Adressen sichtbar (z.B. EW 2    EW 6 → ["EW 2", "", "EW 6"]).</summary>
    public string[] Line1Parts => string.IsNullOrEmpty(Line1) ? [] : Line1.Split("  ", StringSplitOptions.None);

    /// <summary>Einzelne Adressen aus Line2 (getrennt durch doppeltes Leerzeichen).</summary>
    public string[] Line2Parts => string.IsNullOrEmpty(Line2) ? [] : Line2.Split("  ", StringSplitOptions.None);

    /// <summary>True wenn Line2 befuellt ist (digital odd/even Split).
    /// Analog nutzt nur Line1, untere Reihe bleibt als leere Platzhalter sichtbar.</summary>
    public bool HasLine2 => Line2Parts.Length > 0;

    /// <summary>Darstellung fuer die untere Reihe in vertikalen Etiketten.
    /// Bei digital = Line2Parts (odd/even Split). Bei analog = leere Strings
    /// mit gleicher Anzahl wie Line1Parts, damit die Schreibplaetze als
    /// leere Rechtecke sichtbar bleiben (fuer Blanko-A4-Druck mit Rahmen).</summary>
    public string[] EffectiveLine2Parts
    {
        get
        {
            if (Line2Parts.Length > 0) return Line2Parts;
            var l1 = Line1Parts;
            if (l1.Length == 0) return [];
            var placeholders = new string[l1.Length];
            for (int i = 0; i < l1.Length; i++) placeholders[i] = string.Empty;
            return placeholders;
        }
    }

    public bool HasText => _cell.HasText;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>True wenn das Etikett Teil einer Multi-Selection fuer Copy ist (Strg+Klick / Shift+Klick).</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public LabelCell GetCell() => _cell;

    /// <summary>Uebernimmt Inhalt einer anderen Zelle (fuer Paste). Index bleibt.</summary>
    public void SetCell(LabelCell source)
    {
        _cell.CopyFrom(source);
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Line1));
        OnPropertyChanged(nameof(Line2));
        OnPropertyChanged(nameof(Line1Parts));
        OnPropertyChanged(nameof(Line2Parts));
        OnPropertyChanged(nameof(HasLine2));
        OnPropertyChanged(nameof(EffectiveLine2Parts));
        OnPropertyChanged(nameof(CellFontSize));
        OnPropertyChanged(nameof(CellIsBold));
        OnPropertyChanged(nameof(CellIsItalic));
        OnPropertyChanged(nameof(CellFontFamily));
        OnPropertyChanged(nameof(IsPrintEnabled));
        OnPropertyChanged(nameof(PrintOpacity));
        OnPropertyChanged(nameof(HasText));
        OnPropertyChanged(nameof(PreviewFontSize));
        OnPropertyChanged(nameof(PreviewFontWeight));
        OnPropertyChanged(nameof(PreviewFontStyle));
        OnPropertyChanged(nameof(PreviewFontFamily));
    }

    public void Clear()
    {
        _cell.Clear();
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(Line1));
        OnPropertyChanged(nameof(Line2));
        OnPropertyChanged(nameof(Line1Parts));
        OnPropertyChanged(nameof(Line2Parts));
        OnPropertyChanged(nameof(HasLine2));
        OnPropertyChanged(nameof(EffectiveLine2Parts));
        OnPropertyChanged(nameof(HasText));
    }
}
