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
        set { _cell.Line1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public string Line2
    {
        get => _cell.Line2;
        set { _cell.Line2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

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
        OnPropertyChanged(nameof(HasText));
    }
}
