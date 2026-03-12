using System.Collections.ObjectModel;
using System.Windows;
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

    // Eingabefelder (manuell)
    private string _inputHeader = string.Empty;
    private string _inputLine1 = string.Empty;
    private string _inputLine2 = string.Empty;

    // Adress-Generator Felder
    private string _genModuleName = string.Empty;
    private ModuleTypeInfo _genModuleType;
    private int _genStartByte;
    private int _genCount = 2;
    private bool _genAutoAdvanceAddress = true;
    private string _genPreviewLine1 = string.Empty;
    private string _genPreviewLine2 = string.Empty;

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
        _genModuleType = AddressGenerator.ModuleTypes[0]; // DI

        AvailableFormats = new ObservableCollection<FormatInfo>(FormatDefinitions.All);
        Labels = new ObservableCollection<LabelViewModel>();
        FontSizes = [4, 5, 6, 7, 8, 9, 10];

        ApplyCommand = new RelayCommand(ApplyToLabel, () => SelectedLabel is not null);
        GenerateAndApplyCommand = new RelayCommand(GenerateAndApply, () => SelectedLabel is not null);
        GeneratePreviewCommand = new RelayCommand(UpdateGeneratorPreview);
        ClearAllCommand = new RelayCommand(ClearAllLabels);
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        ApplySettingsCommand = new RelayCommand(ApplySettings);
        PrintCommand = new RelayCommand(PrintLabels);
        NewProjectCommand = new RelayCommand(NewProject);
        UpdateHeaderCommand = new RelayCommand(UpdateHeader, () => SelectedLabel is not null);

        InitializeLabels();
    }

    public ObservableCollection<FormatInfo> AvailableFormats { get; }
    public ObservableCollection<LabelViewModel> Labels { get; }
    public int[] FontSizes { get; }

    // Modultypen fuer ComboBox
    public ModuleTypeInfo[] AvailableModuleTypes => AddressGenerator.ModuleTypes;

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
                OnPropertyChanged(nameof(IsVertical));
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
                    InputHeader = value.Header;
                    InputLine1 = value.Line1;
                    InputLine2 = value.Line2;
                    GenModuleName = value.Header; // Kopfzeile auch im Generator laden
                    // Schrift-Einstellungen des Etiketts laden
                    InputFontSize = value.CellFontSize;
                    InputIsBold = value.CellIsBold;
                    InputIsItalic = value.CellIsItalic;
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
    public bool IsVertical => _selectedFormat.IsVertical;
    public int LabelsPerRow => _selectedFormat.LabelsPerRow;
    public int LabelRows => _selectedFormat.LabelRows;

    public string WindowTitle => $"ET-Printer - {_selectedFormat.DisplayName}";

    // === Manuelle Eingabefelder ===
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

    // === Adress-Generator Felder ===
    public string GenModuleName
    {
        get => _genModuleName;
        set { if (SetProperty(ref _genModuleName, value)) UpdateGeneratorPreview(); }
    }

    public ModuleTypeInfo GenModuleType
    {
        get => _genModuleType;
        set
        {
            if (SetProperty(ref _genModuleType, value))
            {
                OnPropertyChanged(nameof(GenCountLabel));
                OnPropertyChanged(nameof(GenTypicalCounts));
                UpdateGeneratorPreview();
            }
        }
    }

    public int GenStartByte
    {
        get => _genStartByte;
        set { if (SetProperty(ref _genStartByte, value)) UpdateGeneratorPreview(); }
    }

    public int GenCount
    {
        get => _genCount;
        set { if (SetProperty(ref _genCount, value)) UpdateGeneratorPreview(); }
    }

    public bool GenAutoAdvanceAddress
    {
        get => _genAutoAdvanceAddress;
        set => SetProperty(ref _genAutoAdvanceAddress, value);
    }

    public string GenPreviewLine1
    {
        get => _genPreviewLine1;
        private set => SetProperty(ref _genPreviewLine1, value);
    }

    public string GenPreviewLine2
    {
        get => _genPreviewLine2;
        private set => SetProperty(ref _genPreviewLine2, value);
    }

    public string GenCountLabel => AddressGenerator.GetCountLabel(_genModuleType.Type);
    public int[] GenTypicalCounts => AddressGenerator.GetTypicalCounts(_genModuleType.Type);

    // === Einstellungen ===
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

    // Seitenraender fuer A4-Preview (3px/mm Skalierung)
    public Thickness PreviewMargin => new(
        _settings.MarginLeft * 3, _settings.MarginTop * 3,
        _settings.MarginRight * 3, _settings.MarginBottom * 3);

    // === Commands ===
    public ICommand ApplyCommand { get; }
    public ICommand GenerateAndApplyCommand { get; }
    public ICommand GeneratePreviewCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand ApplySettingsCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand NewProjectCommand { get; }
    public ICommand UpdateHeaderCommand { get; }

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
        ApplyFontToLabel(SelectedLabel);

        AdvanceToNextLabel();
    }

    private void GenerateAndApply()
    {
        if (SelectedLabel is null) return;

        var result = AddressGenerator.Generate(GenModuleName, GenModuleType.Type, GenStartByte, GenCount);

        // In Eingabefelder und direkt aufs Etikett
        InputHeader = result.Header;
        InputLine1 = result.Line1;
        InputLine2 = result.Line2;

        SelectedLabel.Header = result.Header;
        SelectedLabel.Line1 = result.Line1;
        SelectedLabel.Line2 = result.Line2;
        ApplyFontToLabel(SelectedLabel);

        StatusMessage = $"Generiert: {GenModuleName} ({GenModuleType.DisplayName}) ab Byte {GenStartByte}";

        // Startadresse fuer naechstes Modul automatisch weiterschalten
        if (GenAutoAdvanceAddress)
        {
            GenStartByte = AddressGenerator.GetNextStartByte(GenModuleType.Type, GenStartByte, GenCount);
        }

        AdvanceToNextLabel();
    }

    private void UpdateGeneratorPreview()
    {
        if (GenCount <= 0) return;

        try
        {
            var result = AddressGenerator.Generate(
                string.IsNullOrWhiteSpace(GenModuleName) ? "..." : GenModuleName,
                GenModuleType.Type, GenStartByte, GenCount);

            GenPreviewLine1 = result.Line1;
            GenPreviewLine2 = result.Line2;
        }
        catch
        {
            GenPreviewLine1 = string.Empty;
            GenPreviewLine2 = string.Empty;
        }
    }

    private void AdvanceToNextLabel()
    {
        if (SelectedLabel is null) return;

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
        // Seitenraender global
        _settings.MarginTop = InputMarginTop;
        _settings.MarginLeft = InputMarginLeft;
        _settings.MarginBottom = InputMarginBottom;
        _settings.MarginRight = InputMarginRight;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(PreviewMargin));

        // Schrift aufs ausgewaehlte Etikett
        if (SelectedLabel is not null)
        {
            ApplyFontToLabel(SelectedLabel);
            StatusMessage = $"Schrift fuer Etikett {SelectedLabel.DisplayPosition} uebernommen";
        }
        else
        {
            StatusMessage = "Seitenraender uebernommen";
        }
    }

    private void UpdateHeader()
    {
        if (SelectedLabel is null) return;
        SelectedLabel.Header = GenModuleName;
        InputHeader = GenModuleName;
        StatusMessage = $"Kopfzeile von Etikett {SelectedLabel.DisplayPosition} geaendert";
    }

    private void ApplyFontToLabel(LabelViewModel label)
    {
        label.CellFontSize = InputFontSize;
        label.CellIsBold = InputIsBold;
        label.CellIsItalic = InputIsItalic;
    }

    private void NewProject()
    {
        _settings.Reset();
        ResetSettings();
        GenModuleName = string.Empty;
        GenStartByte = 0;
        GenCount = 2;
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
