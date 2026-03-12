using System.Collections.ObjectModel;
using System.Windows.Input;
using ETPrinter.Models;
using ETPrinter.Services;

namespace ETPrinter.ViewModels;

public class MainViewModel : ViewModelBase
{
    private FormatInfo _selectedFormat;
    private LabelViewModel? _selectedLabel;
    private LabelSettings _settings;
    private double _zoom = 1.5;

    // Eingabefelder
    private string _inputHeader = string.Empty;
    private string _inputLine1 = string.Empty;
    private string _inputLine2 = string.Empty;

    // Einstellungen Eingabefelder
    private int _inputFontSize = 7;
    private bool _inputIsBold;
    private bool _inputIsItalic;
    private double _inputMarginTop = 20.0;
    private double _inputMarginLeft = 30.0;
    private double _inputMarginBottom = 21.0;
    private double _inputMarginRight = 25.0;

    private string _statusMessage = "Bereit";

    public MainViewModel()
    {
        _settings = new LabelSettings();
        _selectedFormat = FormatDefinitions.All[0];

        AvailableFormats = new ObservableCollection<FormatInfo>(FormatDefinitions.All);
        Labels = new ObservableCollection<LabelViewModel>();
        FontSizes = [6, 7, 8, 9, 10];

        ApplyCommand = new RelayCommand(ApplyToLabel, () => SelectedLabel is not null);
        ClearAllCommand = new RelayCommand(ClearAllLabels);
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        ApplySettingsCommand = new RelayCommand(ApplySettings);
        PrintCommand = new RelayCommand(PrintLabels);
        NewProjectCommand = new RelayCommand(NewProject);

        InitializeLabels();
    }

    public ObservableCollection<FormatInfo> AvailableFormats { get; }
    public ObservableCollection<LabelViewModel> Labels { get; }
    public int[] FontSizes { get; }

    public FormatInfo SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetProperty(ref _selectedFormat, value))
            {
                InitializeLabels();
                OnPropertyChanged(nameof(HasHeader));
                OnPropertyChanged(nameof(IsDoubleLine));
                OnPropertyChanged(nameof(LabelsPerRow));
                OnPropertyChanged(nameof(LabelRows));
                OnPropertyChanged(nameof(WindowTitle));
                StatusMessage = $"Format: {value.DisplayName} ({value.LabelsPerPage} Etiketten)";
            }
        }
    }

    public LabelViewModel? SelectedLabel
    {
        get => _selectedLabel;
        set
        {
            if (_selectedLabel is not null)
                _selectedLabel.IsSelected = false;

            if (SetProperty(ref _selectedLabel, value))
            {
                if (value is not null)
                {
                    value.IsSelected = true;
                    // Lade Text des ausgewaehlten Etiketts in die Eingabefelder
                    InputHeader = value.Header;
                    InputLine1 = value.Line1;
                    InputLine2 = value.Line2;
                    StatusMessage = $"Etikett {value.DisplayPosition}/{Labels.Count}";
                }
                OnPropertyChanged(nameof(SelectedLabelInfo));
            }
        }
    }

    public string SelectedLabelInfo => SelectedLabel is not null
        ? $"Etikett {SelectedLabel.DisplayPosition} von {Labels.Count}"
        : "Kein Etikett ausgewaehlt";

    public bool HasHeader => _selectedFormat.HasHeader;
    public bool IsDoubleLine => _selectedFormat.RowsPerLabel == 2;
    public int LabelsPerRow => _selectedFormat.LabelsPerRow;
    public int LabelRows => _selectedFormat.LabelRows;

    public string WindowTitle => $"ET-Printer - {_selectedFormat.DisplayName}";

    // Eingabefelder
    public string InputHeader
    {
        get => _inputHeader;
        set => SetProperty(ref _inputHeader, value);
    }

    public string InputLine1
    {
        get => _inputLine1;
        set => SetProperty(ref _inputLine1, value);
    }

    public string InputLine2
    {
        get => _inputLine2;
        set => SetProperty(ref _inputLine2, value);
    }

    // Einstellungen
    public int InputFontSize
    {
        get => _inputFontSize;
        set => SetProperty(ref _inputFontSize, value);
    }

    public bool InputIsBold
    {
        get => _inputIsBold;
        set => SetProperty(ref _inputIsBold, value);
    }

    public bool InputIsItalic
    {
        get => _inputIsItalic;
        set => SetProperty(ref _inputIsItalic, value);
    }

    public double InputMarginTop
    {
        get => _inputMarginTop;
        set => SetProperty(ref _inputMarginTop, value);
    }

    public double InputMarginLeft
    {
        get => _inputMarginLeft;
        set => SetProperty(ref _inputMarginLeft, value);
    }

    public double InputMarginBottom
    {
        get => _inputMarginBottom;
        set => SetProperty(ref _inputMarginBottom, value);
    }

    public double InputMarginRight
    {
        get => _inputMarginRight;
        set => SetProperty(ref _inputMarginRight, value);
    }

    public double Zoom
    {
        get => _zoom;
        set => SetProperty(ref _zoom, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public LabelSettings Settings => _settings;

    // Commands
    public ICommand ApplyCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand ApplySettingsCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand NewProjectCommand { get; }

    private void InitializeLabels()
    {
        Labels.Clear();
        SelectedLabel = null;

        int count = _selectedFormat.LabelsPerPage;
        for (int i = 0; i < count; i++)
        {
            Labels.Add(new LabelViewModel(new LabelCell { Index = i }));
        }

        if (Labels.Count > 0)
            SelectedLabel = Labels[0];
    }

    private void ApplyToLabel()
    {
        if (SelectedLabel is null) return;

        SelectedLabel.Header = InputHeader;
        SelectedLabel.Line1 = InputLine1;
        SelectedLabel.Line2 = InputLine2;

        // Zum naechsten Etikett springen
        int nextIndex = SelectedLabel.Index + 1;
        if (nextIndex < Labels.Count)
        {
            SelectedLabel = Labels[nextIndex];
        }
    }

    private void ClearAllLabels()
    {
        foreach (var label in Labels)
            label.Clear();
        InputHeader = string.Empty;
        InputLine1 = string.Empty;
        InputLine2 = string.Empty;
        StatusMessage = "Alle Etiketten geloescht";
    }

    private void ResetSettings()
    {
        InputFontSize = 7;
        InputIsBold = false;
        InputIsItalic = false;
        InputMarginTop = 20.0;
        InputMarginLeft = 30.0;
        InputMarginBottom = 21.0;
        InputMarginRight = 25.0;
        StatusMessage = "Einstellungen zurueckgesetzt";
    }

    private void ApplySettings()
    {
        _settings.FontSize = InputFontSize;
        _settings.IsBold = InputIsBold;
        _settings.IsItalic = InputIsItalic;
        _settings.MarginTop = InputMarginTop;
        _settings.MarginLeft = InputMarginLeft;
        _settings.MarginBottom = InputMarginBottom;
        _settings.MarginRight = InputMarginRight;
        OnPropertyChanged(nameof(Settings));
        StatusMessage = "Einstellungen uebernommen";
    }

    private void NewProject()
    {
        _settings.Reset();
        ResetSettings();
        SelectedFormat = FormatDefinitions.All[0];
        StatusMessage = "Neues Projekt erstellt";
    }

    private void PrintLabels()
    {
        StatusMessage = "Druckfunktion wird in Phase 5 implementiert...";
    }

    public void SelectLabel(LabelViewModel label)
    {
        SelectedLabel = label;
    }
}
