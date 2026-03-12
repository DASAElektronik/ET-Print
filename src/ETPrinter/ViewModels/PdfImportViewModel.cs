using System.Collections.ObjectModel;
using System.Windows.Input;
using ETPrinter.Models;

namespace ETPrinter.ViewModels;

public class SelectableParsedModule : ViewModelBase
{
    private bool _isSelected = true;

    public ParsedModule Module { get; }

    public SelectableParsedModule(ParsedModule module)
    {
        Module = module;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string ModuleName => Module.ModuleName;
    public string ModuleType => Module.ModuleType;
    public int StartByte => Module.StartByte;
    public int ChannelCount => Module.ChannelCount;
}

public class PdfImportViewModel : ViewModelBase
{
    private string _warningsText = string.Empty;

    public ObservableCollection<SelectableParsedModule> Items { get; } = [];

    public string WarningsText
    {
        get => _warningsText;
        set => SetProperty(ref _warningsText, value);
    }

    public ICommand SelectAllCommand { get; }
    public ICommand SelectNoneCommand { get; }

    public PdfImportViewModel()
    {
        SelectAllCommand = new RelayCommand(() => SetAll(true));
        SelectNoneCommand = new RelayCommand(() => SetAll(false));
    }

    public void LoadFromResult(SchematicParseResult result)
    {
        Items.Clear();

        foreach (var module in result.Modules)
        {
            Items.Add(new SelectableParsedModule(module));
        }

        WarningsText = result.Warnings.Count > 0
            ? string.Join(Environment.NewLine, result.Warnings)
            : string.Empty;
    }

    public List<ParsedModule> GetSelectedModules()
    {
        return Items
            .Where(i => i.IsSelected)
            .Select(i => i.Module)
            .ToList();
    }

    private void SetAll(bool selected)
    {
        foreach (var item in Items)
        {
            item.IsSelected = selected;
        }
    }
}
