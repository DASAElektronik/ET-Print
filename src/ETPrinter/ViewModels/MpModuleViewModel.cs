using System.Collections.ObjectModel;
using ETPrinter.Models;
using ETPrinter.Services;

namespace ETPrinter.ViewModels;

public class MpAddressCellViewModel : ViewModelBase
{
    private readonly MpAddressCell _cell;
    private readonly MpCellDefinition _definition;
    private bool _isSelected;

    public MpAddressCellViewModel(MpAddressCell cell, MpCellDefinition definition)
    {
        _cell = cell;
        _definition = definition;
    }

    public int CellIndex => _cell.CellIndex;
    public int StartRow => _definition.StartRow;
    public int RowSpan => _definition.RowSpan;
    public int StartCol => _definition.StartCol;
    public int ColSpan => _definition.ColSpan;
    public bool IsEditable => _definition.IsEditable;

    public string Text
    {
        get => _cell.Text;
        set { _cell.Text = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public bool HasText => _cell.HasText;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public MpAddressCell GetCell() => _cell;
}

public class MpModuleViewModel : ViewModelBase
{
    private readonly MpModule _module;
    private bool _isSelected;

    public MpModuleViewModel(MpModule module)
    {
        _module = module;
        RebuildCellViewModels();
    }

    public int ModuleIndex => _module.ModuleIndex;
    public int Band => _module.ModuleIndex / 5;  // 0-4 = Band 0, 5-9 = Band 1
    public int ColumnInBand => _module.ModuleIndex % 5;

    public MpModuleVariant Variant
    {
        get => _module.Variant;
        set
        {
            if (_module.Variant != value)
            {
                _module.Variant = value;
                RebuildCellViewModels();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasText));
            }
        }
    }

    public string HeaderText
    {
        get => _module.HeaderText;
        set { _module.HeaderText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public string NetAddress1
    {
        get => _module.NetAddress1;
        set { _module.NetAddress1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public string NetAddress2
    {
        get => _module.NetAddress2;
        set { _module.NetAddress2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public string CpuName
    {
        get => _module.CpuName;
        set { _module.CpuName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); }
    }

    public int FontSize
    {
        get => _module.FontSize;
        set { _module.FontSize = value; OnPropertyChanged(); }
    }

    public string FontFamily
    {
        get => _module.FontFamily;
        set { _module.FontFamily = value; OnPropertyChanged(); }
    }

    public bool IsBold
    {
        get => _module.IsBold;
        set { _module.IsBold = value; OnPropertyChanged(); }
    }

    public bool IsPrintEnabled
    {
        get => _module.IsPrintEnabled;
        set { _module.IsPrintEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrintOpacity)); }
    }

    public double PrintOpacity => IsPrintEnabled ? 1.0 : 0.4;

    public ObservableCollection<MpAddressCellViewModel> AddressCells { get; } = [];

    public bool HasText => _module.HasText;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public MpModule GetModule() => _module;

    private void RebuildCellViewModels()
    {
        AddressCells.Clear();
        var layout = MpModuleLayoutFactory.GetLayout(_module.Variant);
        _module.AddressCells = MpModuleLayoutFactory.CreateCells(_module.Variant);

        for (int i = 0; i < _module.AddressCells.Count; i++)
        {
            AddressCells.Add(new MpAddressCellViewModel(
                _module.AddressCells[i], layout.AddressCells[i]));
        }
    }
}
