using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ETPrinter.Models;
using ETPrinter.Services;

namespace ETPrinter.ViewModels;

public record RecentFileItem(string FilePath, string DisplayName);

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

    private bool _printGridLines;
    private double _calibrationOffsetX;
    private double _calibrationOffsetY;
    private string? _currentFilePath;
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
        PrintCalibrationCommand = new RelayCommand(PrintCalibration);
        NewProjectCommand = new RelayCommand(NewProject);
        SaveCommand = new RelayCommand(SaveProject);
        SaveAsCommand = new RelayCommand(SaveProjectAs);
        OpenCommand = new RelayCommand(OpenProject);
        OpenRecentCommand = new RelayCommand<string>(OpenRecentFile);
        UpdateHeaderCommand = new RelayCommand(UpdateHeader, () => SelectedLabel is not null);

        LoadCalibration();
        RefreshRecentFiles();
        InitializeLabels();
    }

    public ObservableCollection<FormatInfo> AvailableFormats { get; }
    public ObservableCollection<LabelViewModel> Labels { get; }
    public ObservableCollection<RecentFileItem> RecentFiles { get; } = new();
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
                OnPropertyChanged(nameof(RowNumbers));
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

    // Zeilennummern fuer rechten Rand (wie physisches A4-Blatt: 20 oben, 1 unten)
    public int[] RowNumbers => Enumerable.Range(1, _selectedFormat.LabelRows).Reverse().ToArray();

    public string WindowTitle => _currentFilePath is not null
        ? $"ET-Printer - {Path.GetFileName(_currentFilePath)}"
        : $"ET-Printer - {_selectedFormat.DisplayName}";

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

    public bool PrintGridLines
    {
        get => _printGridLines;
        set => SetProperty(ref _printGridLines, value);
    }

    public double CalibrationOffsetX
    {
        get => _calibrationOffsetX;
        set => SetProperty(ref _calibrationOffsetX, value);
    }

    public double CalibrationOffsetY
    {
        get => _calibrationOffsetY;
        set => SetProperty(ref _calibrationOffsetY, value);
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
    public ICommand PrintCalibrationCommand { get; }
    public ICommand NewProjectCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand OpenRecentCommand { get; }
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
        _currentFilePath = null;
        _settings.Reset();
        ResetSettings();
        GenModuleName = string.Empty;
        GenStartByte = 0;
        GenCount = 2;
        SelectedFormat = FormatDefinitions.All[0];
        OnPropertyChanged(nameof(WindowTitle));
        StatusMessage = "Neues Projekt erstellt";
    }

    private void SaveProject()
    {
        if (_currentFilePath is null) { SaveProjectAs(); return; }
        DoSave(_currentFilePath);
    }

    private void SaveProjectAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "ET-Printer Projekt (*.etprint)|*.etprint",
            DefaultExt = ".etprint",
            FileName = Path.GetFileNameWithoutExtension(_currentFilePath ?? "Projekt")
        };
        if (dialog.ShowDialog() == true)
            DoSave(dialog.FileName);
    }

    private void DoSave(string filePath)
    {
        try
        {
            var project = new LabelProject
            {
                Format = _selectedFormat.Format,
                Settings = _settings.Clone(),
                Labels = Labels.Select(vm => vm.GetCell()).ToList(),
                CalibrationOffsetX = CalibrationOffsetX,
                CalibrationOffsetY = CalibrationOffsetY,
                PrintGridLines = PrintGridLines
            };
            ProjectService.Save(project, filePath);
            _currentFilePath = filePath;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshRecentFiles();
            StatusMessage = $"Gespeichert: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Speicherfehler: {ex.Message}";
        }
    }

    private void OpenProject()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "ET-Printer Projekt (*.etprint)|*.etprint",
            DefaultExt = ".etprint"
        };
        if (dialog.ShowDialog() == true)
            DoOpen(dialog.FileName);
    }

    private void OpenRecentFile(string? filePath)
    {
        if (filePath is not null && File.Exists(filePath))
            DoOpen(filePath);
        else
            StatusMessage = "Datei nicht gefunden";
    }

    private void DoOpen(string filePath)
    {
        try
        {
            var project = ProjectService.Load(filePath);

            // Format setzen (loest InitializeLabels aus)
            var formatInfo = FormatDefinitions.Get(project.Format);
            SelectedFormat = formatInfo;

            // Labels befuellen
            for (int i = 0; i < project.Labels.Count && i < Labels.Count; i++)
            {
                var src = project.Labels[i];
                Labels[i].Header = src.Header;
                Labels[i].Line1 = src.Line1;
                Labels[i].Line2 = src.Line2;
                Labels[i].CellFontSize = src.FontSize;
                Labels[i].CellIsBold = src.IsBold;
                Labels[i].CellIsItalic = src.IsItalic;
            }

            // Einstellungen
            _settings.MarginTop = project.Settings.MarginTop;
            _settings.MarginLeft = project.Settings.MarginLeft;
            _settings.MarginBottom = project.Settings.MarginBottom;
            _settings.MarginRight = project.Settings.MarginRight;
            InputMarginTop = project.Settings.MarginTop;
            InputMarginLeft = project.Settings.MarginLeft;
            InputMarginBottom = project.Settings.MarginBottom;
            InputMarginRight = project.Settings.MarginRight;
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(PreviewMargin));

            // Kalibrierung + Druckoptionen
            CalibrationOffsetX = project.CalibrationOffsetX;
            CalibrationOffsetY = project.CalibrationOffsetY;
            PrintGridLines = project.PrintGridLines;

            _currentFilePath = filePath;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshRecentFiles();
            if (Labels.Count > 0) SelectedLabel = Labels[0];
            StatusMessage = $"Geladen: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ladefehler: {ex.Message}";
        }
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var path in ProjectService.LoadRecentFiles())
            RecentFiles.Add(new RecentFileItem(path, Path.GetFileName(path)));
    }

    private void PrintLabels()
    {
        try
        {
            SaveCalibration();
        PrintService.Print(Labels, _selectedFormat, _settings, PrintGridLines,
            CalibrationOffsetX, CalibrationOffsetY);
            StatusMessage = "Druckauftrag gesendet";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Druckfehler: {ex.Message}";
            System.Windows.MessageBox.Show(
                $"Fehler beim Drucken:\n{ex.Message}",
                "Druckfehler",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void PrintCalibration()
    {
        try
        {
            SaveCalibration();
            PrintService.PrintCalibrationPage(_selectedFormat, _settings,
                CalibrationOffsetX, CalibrationOffsetY);
            StatusMessage = "Kalibrierungsseite gedruckt";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Druckfehler: {ex.Message}";
        }
    }

    private void LoadCalibration()
    {
        var cal = CalibrationService.Load();
        _calibrationOffsetX = cal.OffsetX;
        _calibrationOffsetY = cal.OffsetY;
    }

    private void SaveCalibration()
    {
        CalibrationService.Save(new CalibrationData
        {
            OffsetX = CalibrationOffsetX,
            OffsetY = CalibrationOffsetY
        });
    }

    public void SelectLabel(LabelViewModel label)
    {
        SelectedLabel = label;
    }
}
