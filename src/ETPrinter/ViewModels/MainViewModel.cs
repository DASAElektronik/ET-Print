using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.Views;
using MpModuleLayout = ETPrinter.Models.MpModuleLayout;

namespace ETPrinter.ViewModels;

public record RecentFileItem(string FilePath, string DisplayName);

public class MainViewModel : ViewModelBase
{
    private ProductFamily _selectedProductFamily = ProductFamily.ET200SP;
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
    private string _inputFontFamily = "Arial";
    private double _inputMarginTop = 20.0;
    private double _inputMarginLeft = 30.0;
    private double _inputMarginBottom = 21.0;
    private double _inputMarginRight = 25.0;

    private bool _printGridLines;
    private double _calibrationOffsetX;
    private double _calibrationOffsetY;
    private string? _currentFilePath;
    private string _statusMessage = "Bereit";
    private bool _isDirty;

    // Multi-page support
    private List<List<LabelViewModel>> _allPages = [];
    private int _currentPageIndex;

    // ET200MP Module-based support
    private List<List<MpModuleViewModel>> _allMpPages = [];
    private MpModuleViewModel? _selectedMpModule;
    private MpAddressCellViewModel? _selectedMpCell;

    public MainViewModel()
    {
        _settings = new LabelSettings();
        _selectedFormat = FormatDefinitions.GetDefaultFormat(ProductFamily.ET200SP);
        _genModuleType = AddressGenerator.ModuleTypes[0]; // DI

        AvailableProductFamilies = new ObservableCollection<ProductFamilyInfo>(ProductFamilyDefinitions.All);
        AvailableFormats = new ObservableCollection<FormatInfo>(FormatDefinitions.GetFormatsForFamily(ProductFamily.ET200SP));
        Labels = new ObservableCollection<LabelViewModel>();
        MpModules = new ObservableCollection<MpModuleViewModel>();
        AvailableMpVariants = new ObservableCollection<MpModuleLayout>(
            Services.MpModuleLayoutFactory.All);
        FontSizes = [4, 5, 6, 7, 8, 9, 10];
        AvailableFonts = new ObservableCollection<string>(
            Fonts.SystemFontFamilies
                .Select(f => f.Source)
                .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase));

        ApplyCommand = new RelayCommand(ApplyToLabel, () => SelectedLabel is not null || SelectedMpModule is not null);
        GenerateAndApplyCommand = new RelayCommand(GenerateAndApply, () => SelectedLabel is not null || SelectedMpModule is not null);
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
        UpdateHeaderCommand = new RelayCommand(UpdateHeader, () => SelectedLabel is not null || SelectedMpModule is not null);

        // Page navigation commands
        NextPageCommand = new RelayCommand(NextPage, () => _currentPageIndex < PageCount - 1);
        PrevPageCommand = new RelayCommand(PrevPage, () => _currentPageIndex > 0);
        AddPageCommand = new RelayCommand(AddPage);
        RemovePageCommand = new RelayCommand(RemovePage, () => PageCount > 1);

        // Import commands
        ImportCsvCommand = new RelayCommand(ImportCsv);
        ImportExcelCommand = new RelayCommand(ImportExcel);
        ImportSchematicCommand = new RelayCommand(ImportSchematic);

        // Selective print commands
        SelectAllForPrintCommand = new RelayCommand(SelectAllForPrint);
        DeselectAllForPrintCommand = new RelayCommand(DeselectAllForPrint);
        SelectFilledForPrintCommand = new RelayCommand(SelectFilledForPrint);
        TogglePrintCommand = new RelayCommand(TogglePrint, () => SelectedLabel is not null);

        LoadCalibration();
        RefreshRecentFiles();
        InitializeLabels();
    }

    public ObservableCollection<ProductFamilyInfo> AvailableProductFamilies { get; }
    public ObservableCollection<FormatInfo> AvailableFormats { get; }
    public ObservableCollection<MpModuleViewModel> MpModules { get; }
    public ObservableCollection<MpModuleLayout> AvailableMpVariants { get; }
    public ObservableCollection<LabelViewModel> Labels { get; }
    public ObservableCollection<RecentFileItem> RecentFiles { get; } = new();
    public ObservableCollection<string> AvailableFonts { get; }
    public int[] FontSizes { get; }

    // Modultypen fuer ComboBox
    public ModuleTypeInfo[] AvailableModuleTypes => AddressGenerator.ModuleTypes;

    public ProductFamilyInfo SelectedProductFamilyInfo
    {
        get => ProductFamilyDefinitions.Get(_selectedProductFamily);
        set
        {
            if (value is not null && SetProperty(ref _selectedProductFamily, value.Family))
            {
                // Ränder auf Family-Defaults setzen
                _settings.ResetForFamily(_selectedProductFamily);
                InputMarginTop = _settings.MarginTop;
                InputMarginLeft = _settings.MarginLeft;
                InputMarginBottom = _settings.MarginBottom;
                InputMarginRight = _settings.MarginRight;
                OnPropertyChanged(nameof(Settings));
                OnPropertyChanged(nameof(PreviewMargin));

                // Formate filtern
                AvailableFormats.Clear();
                foreach (var fmt in FormatDefinitions.GetFormatsForFamily(_selectedProductFamily))
                    AvailableFormats.Add(fmt);

                // Erstes Format der neuen Familie waehlen
                SelectedFormat = FormatDefinitions.GetDefaultFormat(_selectedProductFamily);
                OnPropertyChanged(nameof(SelectedProductFamily));
                OnPropertyChanged(nameof(BandsPerPage));
                OnPropertyChanged(nameof(IsMultiBand));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public ProductFamily SelectedProductFamily => _selectedProductFamily;
    public int BandsPerPage => _selectedFormat.BandsPerPage;
    public bool IsMultiBand => _selectedFormat.BandsPerPage > 1;
    public bool IsModuleBased => _selectedFormat.IsModuleBased;

    public MpModuleViewModel? SelectedMpModule
    {
        get => _selectedMpModule;
        set
        {
            if (_selectedMpModule is not null)
                _selectedMpModule.IsSelected = false;
            if (SetProperty(ref _selectedMpModule, value))
            {
                if (value is not null)
                    value.IsSelected = true;
                OnPropertyChanged(nameof(SelectedMpModuleInfo));
            }
        }
    }

    public MpAddressCellViewModel? SelectedMpCell
    {
        get => _selectedMpCell;
        set
        {
            if (_selectedMpCell is not null)
                _selectedMpCell.IsSelected = false;
            if (SetProperty(ref _selectedMpCell, value))
            {
                if (value is not null)
                    value.IsSelected = true;
            }
        }
    }

    public string SelectedMpModuleInfo => _selectedMpModule is not null
        ? $"Modul {_selectedMpModule.ModuleIndex + 1} / {MpModules.Count} (Spalte {_selectedMpModule.ModuleIndex + 1})"
        : "Kein Modul ausgewaehlt";

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
                OnPropertyChanged(nameof(BandsPerPage));
                OnPropertyChanged(nameof(IsMultiBand));
                OnPropertyChanged(nameof(TotalVisualRows));
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
                    InputFontFamily = value.CellFontFamily;
                    StatusMessage = $"Etikett {value.DisplayPosition}/{Labels.Count} (Seite {_currentPageIndex + 1}/{PageCount})";
                }
                OnPropertyChanged(nameof(SelectedLabelInfo));
            }
        }
    }

    public string SelectedLabelInfo => SelectedLabel is not null
        ? $"Etikett {SelectedLabel.DisplayPosition} von {Labels.Count} (Seite {_currentPageIndex + 1})"
        : "Kein Etikett ausgewaehlt";

    public bool HasHeader => _selectedFormat.HasHeader;
    public bool IsDoubleLine => _selectedFormat.RowsPerLabel == 2;
    public bool IsVertical => _selectedFormat.IsVertical;
    public int LabelsPerRow => _selectedFormat.LabelsPerRow;
    public int LabelRows => _selectedFormat.LabelRows;

    // Gesamt-Zeilen fuer die Preview-Darstellung (ET200SP: 20, ET200MP: 40)
    public int TotalVisualRows => _selectedFormat.ChannelRowsPerBand * _selectedFormat.BandsPerPage;

    // Zeilennummern fuer rechten Rand (wie physisches A4-Blatt: 20 oben, 1 unten)
    // Bei 2 Baendern: 20..1 (Band oben) dann nochmal 20..1 (Band unten)
    public int[] RowNumbers
    {
        get
        {
            var bandNums = Enumerable.Range(1, _selectedFormat.ChannelRowsPerBand).Reverse().ToArray();
            if (_selectedFormat.BandsPerPage <= 1) return bandNums;
            // Fuer Multi-Band: Nummern wiederholen
            var result = new int[bandNums.Length * _selectedFormat.BandsPerPage];
            for (int b = 0; b < _selectedFormat.BandsPerPage; b++)
                Array.Copy(bandNums, 0, result, b * bandNums.Length, bandNums.Length);
            return result;
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
                OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle
    {
        get
        {
            var dirty = _isDirty ? " *" : "";
            return _currentFilePath is not null
                ? $"ET-Printer - {Path.GetFileName(_currentFilePath)}{dirty}"
                : $"ET-Printer - {_selectedFormat.DisplayName}{dirty}";
        }
    }

    // === Multi-page properties ===
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (SetProperty(ref _currentPageIndex, value))
            {
                OnPropertyChanged(nameof(PageCount));
                OnPropertyChanged(nameof(PageIndicator));
            }
        }
    }

    public int PageCount => _selectedFormat.IsModuleBased ? _allMpPages.Count : _allPages.Count;

    public string PageIndicator => $"Seite {_currentPageIndex + 1} / {PageCount}";

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

    public string InputFontFamily
    {
        get => _inputFontFamily;
        set => SetProperty(ref _inputFontFamily, value);
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

    // Page navigation commands
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand AddPageCommand { get; }
    public ICommand RemovePageCommand { get; }

    // Import commands
    public ICommand ImportCsvCommand { get; }
    public ICommand ImportExcelCommand { get; }
    public ICommand ImportSchematicCommand { get; }

    // Selective print commands
    public ICommand SelectAllForPrintCommand { get; }
    public ICommand DeselectAllForPrintCommand { get; }
    public ICommand SelectFilledForPrintCommand { get; }
    public ICommand TogglePrintCommand { get; }

    private void InitializeLabels()
    {
        Labels.Clear();
        MpModules.Clear();
        SelectedLabel = null;
        SelectedMpModule = null;
        SelectedMpCell = null;
        _allPages.Clear();
        _allMpPages.Clear();

        if (_selectedFormat.IsModuleBased)
        {
            // ET200MP: Module erstellen
            var familyInfo = ProductFamilyDefinitions.Get(_selectedFormat.Family);
            int modulesPerPage = familyInfo.ModulesPerPage;
            var firstPage = CreateEmptyMpPage(modulesPerPage);
            _allMpPages.Add(firstPage);
            foreach (var mod in firstPage)
                MpModules.Add(mod);
        }
        else
        {
            // ET200SP: bisherige Logik
            int count = _selectedFormat.LabelsPerPage;
            var firstPage = CreateEmptyPage(count);
            _allPages.Add(firstPage);
            foreach (var lvm in firstPage)
                Labels.Add(lvm);
        }

        _currentPageIndex = 0;
        NotifyPageProperties();
        OnPropertyChanged(nameof(IsModuleBased));

        if (!_selectedFormat.IsModuleBased && Labels.Count > 0)
            SelectedLabel = Labels[0];
        else if (_selectedFormat.IsModuleBased && MpModules.Count > 0)
            SelectedMpModule = MpModules[0];
    }

    private List<MpModuleViewModel> CreateEmptyMpPage(int moduleCount)
    {
        var page = new List<MpModuleViewModel>(moduleCount);
        for (int i = 0; i < moduleCount; i++)
        {
            var module = new MpModule { ModuleIndex = i };
            module.AddressCells = MpModuleLayoutFactory.CreateCells(module.Variant);
            page.Add(new MpModuleViewModel(module));
        }
        return page;
    }

    private List<LabelViewModel> CreateEmptyPage(int count)
    {
        var page = new List<LabelViewModel>(count);
        for (int i = 0; i < count; i++)
            page.Add(new LabelViewModel(new LabelCell { Index = i }));
        return page;
    }

    private void NavigateToPage(int index)
    {
        if (_selectedFormat.IsModuleBased)
        {
            if (index < 0 || index >= _allMpPages.Count) return;
            _currentPageIndex = index;
            MpModules.Clear();
            SelectedMpModule = null;
            SelectedMpCell = null;
            foreach (var mod in _allMpPages[index])
                MpModules.Add(mod);
            NotifyPageProperties();
            if (MpModules.Count > 0)
                SelectedMpModule = MpModules[0];
        }
        else
        {
            if (index < 0 || index >= _allPages.Count) return;
            _currentPageIndex = index;
            Labels.Clear();
            SelectedLabel = null;
            foreach (var lvm in _allPages[index])
                Labels.Add(lvm);
            NotifyPageProperties();
            if (Labels.Count > 0)
                SelectedLabel = Labels[0];
        }
    }

    private void NotifyPageProperties()
    {
        OnPropertyChanged(nameof(CurrentPageIndex));
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(PageIndicator));
    }

    private void NextPage()
    {
        if (_currentPageIndex < PageCount - 1)
            NavigateToPage(_currentPageIndex + 1);
    }

    private void PrevPage()
    {
        if (_currentPageIndex > 0)
            NavigateToPage(_currentPageIndex - 1);
    }

    private void AddPage()
    {
        if (_selectedFormat.IsModuleBased)
        {
            var familyInfo = ProductFamilyDefinitions.Get(_selectedFormat.Family);
            var newPage = CreateEmptyMpPage(familyInfo.ModulesPerPage);
            _allMpPages.Add(newPage);
            NavigateToPage(_allMpPages.Count - 1);
        }
        else
        {
            int count = _selectedFormat.LabelsPerPage;
            var newPage = CreateEmptyPage(count);
            _allPages.Add(newPage);
            NavigateToPage(_allPages.Count - 1);
        }
        IsDirty = true;
        StatusMessage = $"Seite {PageCount} hinzugefuegt";
    }

    private void RemovePage()
    {
        if (_selectedFormat.IsModuleBased)
        {
            if (_allMpPages.Count <= 1) return;
            int removedIndex = _currentPageIndex;
            _allMpPages.RemoveAt(removedIndex);
            NavigateToPage(Math.Min(removedIndex, _allMpPages.Count - 1));
        }
        else
        {
            if (_allPages.Count <= 1) return;
            int removedIndex = _currentPageIndex;
            _allPages.RemoveAt(removedIndex);
            NavigateToPage(Math.Min(removedIndex, _allPages.Count - 1));
        }
        IsDirty = true;
        StatusMessage = $"Seite entfernt ({PageCount} Seiten verbleibend)";
    }

    private void ApplyToLabel()
    {
        if (_selectedFormat.IsModuleBased && SelectedMpModule is not null)
        {
            // ET200MP: Manuelle Eingabe auf Header anwenden
            SelectedMpModule.HeaderText = InputHeader;
            if (SelectedMpCell is not null && SelectedMpCell.IsEditable)
                SelectedMpCell.Text = InputLine1;
            IsDirty = true;
            return;
        }

        if (SelectedLabel is null) return;

        SelectedLabel.Header = InputHeader;
        SelectedLabel.Line1 = InputLine1;
        SelectedLabel.Line2 = InputLine2;
        ApplyFontToLabel(SelectedLabel);
        IsDirty = true;

        AdvanceToNextLabel();
    }

    private void GenerateAndApply()
    {
        var result = AddressGenerator.Generate(GenModuleName, GenModuleType.Type, GenStartByte, GenCount);

        if (_selectedFormat.IsModuleBased && SelectedMpModule is not null)
        {
            // ET200MP: Adressen sequenziell pro Byte erzeugen und klemmengerecht verteilen
            SelectedMpModule.HeaderText = result.Header;

            // Fuer MP: Eigene sequenzielle Adressgenerierung (nicht ET200SP odd/even!)
            var info = AddressGenerator.ModuleTypes.First(m => m.Type == GenModuleType.Type);
            var editableCells = SelectedMpModule.AddressCells.Where(c => c.IsEditable).ToList();

            if (info.IsBitAddressed)
            {
                // Digital: Byte-Zuordnung nach physischer Klemmenbelegung
                // DI 32 (4 Bytes): Zellenreihenfolge in LayoutFactory:
                //   Half 0 Col 0 (8 Zellen) = Gruppe a = Byte 0
                //   Half 0 Col 1 (8 Zellen) = Gruppe c = Byte 2
                //   Half 1 Col 0 (8 Zellen) = Gruppe b = Byte 1
                //   Half 1 Col 1 (8 Zellen) = Gruppe d = Byte 3
                // Byte-Reihenfolge: 0, 2, 1, 3 (physisch: links-oben, rechts-oben, links-unten, rechts-unten)
                int[] byteOrder = GenCount == 4
                    ? [0, 2, 1, 3]  // 32 Kanaele: a, c, b, d
                    : GenCount == 2
                        ? [0, 1]    // 16 Kanaele: einfach sequenziell
                        : Enumerable.Range(0, GenCount).ToArray();

                var addresses = new List<string>();
                for (int group = 0; group < byteOrder.Length; group++)
                {
                    int byteNum = GenStartByte + byteOrder[group];
                    for (int bit = 0; bit < 8; bit++)
                        addresses.Add($"{info.Prefix} {byteNum}.{bit}");
                }
                for (int i = 0; i < editableCells.Count && i < addresses.Count; i++)
                    editableCells[i].Text = addresses[i];
            }
            else
            {
                // Analog: Wort-Adressen sequenziell auf editierbare Zellen
                var addresses = new List<string>();
                for (int ch = 0; ch < GenCount; ch++)
                    addresses.Add($"{info.Prefix} {GenStartByte + ch * 2}");
                for (int i = 0; i < editableCells.Count && i < addresses.Count; i++)
                    editableCells[i].Text = addresses[i];
            }

            IsDirty = true;
            int filledCount = editableCells.Count(c => c.HasText);
            StatusMessage = $"Generiert: {GenModuleName} ({GenModuleType.DisplayName}) ab Byte {GenStartByte} → {filledCount} Adressen auf Modul {SelectedMpModule.ModuleIndex + 1}";

            // Startadresse weiterschalten
            if (GenAutoAdvanceAddress)
                GenStartByte = AddressGenerator.GetNextStartByte(GenModuleType.Type, GenStartByte, GenCount);

            // Zum naechsten Modul weiterschalten
            int nextIdx = SelectedMpModule.ModuleIndex + 1;
            if (nextIdx < MpModules.Count)
                SelectedMpModule = MpModules[nextIdx];
        }
        else if (SelectedLabel is not null)
        {
            // ET200SP: bisherige Logik
            InputHeader = result.Header;
            InputLine1 = result.Line1;
            InputLine2 = result.Line2;

            SelectedLabel.Header = result.Header;
            SelectedLabel.Line1 = result.Line1;
            SelectedLabel.Line2 = result.Line2;
            ApplyFontToLabel(SelectedLabel);

            IsDirty = true;
            StatusMessage = $"Generiert: {GenModuleName} ({GenModuleType.DisplayName}) ab Byte {GenStartByte}";

            if (GenAutoAdvanceAddress)
                GenStartByte = AddressGenerator.GetNextStartByte(GenModuleType.Type, GenStartByte, GenCount);

            AdvanceToNextLabel();
        }
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
        else if (_currentPageIndex < PageCount - 1)
        {
            // At last label of current page, advance to first label of next page
            NavigateToPage(_currentPageIndex + 1);
        }
        else
        {
            // At last label of last page, auto-create new page
            AddPage();
        }
    }

    private void ClearAllLabels()
    {
        // Clear all pages and reset to single empty page
        _allPages.Clear();
        Labels.Clear();
        SelectedLabel = null;

        int count = _selectedFormat.LabelsPerPage;
        var firstPage = CreateEmptyPage(count);
        _allPages.Add(firstPage);
        _currentPageIndex = 0;

        foreach (var lvm in firstPage)
            Labels.Add(lvm);

        NotifyPageProperties();

        InputHeader = string.Empty;
        InputLine1 = string.Empty;
        InputLine2 = string.Empty;

        if (Labels.Count > 0)
            SelectedLabel = Labels[0];

        IsDirty = true;
        StatusMessage = "Alle Etiketten geloescht";
    }

    private void ResetSettings()
    {
        _settings.ResetForFamily(_selectedProductFamily);
        InputFontSize = _settings.FontSize;
        InputIsBold = _settings.IsBold;
        InputIsItalic = _settings.IsItalic;
        InputFontFamily = _settings.FontFamily;
        InputMarginTop = _settings.MarginTop;
        InputMarginLeft = _settings.MarginLeft;
        InputMarginBottom = _settings.MarginBottom;
        InputMarginRight = _settings.MarginRight;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(PreviewMargin));
        StatusMessage = "Einstellungen zurueckgesetzt";
    }

    private void ApplySettings()
    {
        // Seitenraender global
        _settings.MarginTop = InputMarginTop;
        _settings.MarginLeft = InputMarginLeft;
        _settings.MarginBottom = InputMarginBottom;
        _settings.MarginRight = InputMarginRight;
        _settings.FontSize = InputFontSize;
        _settings.IsBold = InputIsBold;
        _settings.IsItalic = InputIsItalic;
        _settings.FontFamily = InputFontFamily;
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
        IsDirty = true;
    }

    private void UpdateHeader()
    {
        if (_selectedFormat.IsModuleBased && SelectedMpModule is not null)
        {
            SelectedMpModule.HeaderText = GenModuleName;
            IsDirty = true;
            StatusMessage = $"Kopfzeile von Modul {SelectedMpModule.ModuleIndex + 1} geaendert";
            return;
        }
        if (SelectedLabel is null) return;
        SelectedLabel.Header = GenModuleName;
        InputHeader = GenModuleName;
        IsDirty = true;
        StatusMessage = $"Kopfzeile von Etikett {SelectedLabel.DisplayPosition} geaendert";
    }

    private void ApplyFontToLabel(LabelViewModel label)
    {
        label.CellFontSize = InputFontSize;
        label.CellIsBold = InputIsBold;
        label.CellIsItalic = InputIsItalic;
        label.CellFontFamily = InputFontFamily;
    }

    private void NewProject()
    {
        if (!ConfirmDiscardChanges()) return;
        _currentFilePath = null;
        _selectedProductFamily = ProductFamily.ET200SP;
        AvailableFormats.Clear();
        foreach (var fmt in FormatDefinitions.GetFormatsForFamily(ProductFamily.ET200SP))
            AvailableFormats.Add(fmt);
        OnPropertyChanged(nameof(SelectedProductFamilyInfo));
        OnPropertyChanged(nameof(SelectedProductFamily));
        _settings.Reset();
        ResetSettings();
        GenModuleName = string.Empty;
        GenStartByte = 0;
        GenCount = 2;
        SelectedFormat = FormatDefinitions.GetDefaultFormat(ProductFamily.ET200SP);
        IsDirty = false;
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
            // Build pages from _allPages (LabelViewModels -> LabelCells)
            var pages = _allPages.Select(pageVms => new LabelPage
            {
                Labels = pageVms.Select(vm => vm.GetCell()).ToList()
            }).ToList();

            // MP-Module serialisieren
            List<MpModulePage>? mpPages = null;
            if (_selectedFormat.IsModuleBased)
            {
                mpPages = _allMpPages.Select(pageMods => new MpModulePage
                {
                    Modules = pageMods.Select(vm => vm.GetModule()).ToList()
                }).ToList();
            }

            var project = new LabelProject
            {
                ProductFamily = _selectedProductFamily,
                Format = _selectedFormat.Format,
                Settings = _settings.Clone(),
                Pages = pages,
                MpPages = mpPages,
                CalibrationOffsetX = CalibrationOffsetX,
                CalibrationOffsetY = CalibrationOffsetY,
                PrintGridLines = PrintGridLines
            };
            ProjectService.Save(project, filePath);
            _currentFilePath = filePath;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshRecentFiles();
            StatusMessage = $"Gespeichert: {Path.GetFileName(filePath)} ({PageCount} Seiten)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Speicherfehler: {ex.Message}";
        }
    }

    private void OpenProject()
    {
        if (!ConfirmDiscardChanges()) return;
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
        {
            if (!ConfirmDiscardChanges()) return;
            DoOpen(filePath);
        }
        else
            StatusMessage = "Datei nicht gefunden";
    }

    private void DoOpen(string filePath)
    {
        try
        {
            var project = ProjectService.Load(filePath);

            // ProductFamily setzen (filtert Formate, setzt Raender)
            _selectedProductFamily = project.ProductFamily;
            AvailableFormats.Clear();
            foreach (var fmt in FormatDefinitions.GetFormatsForFamily(_selectedProductFamily))
                AvailableFormats.Add(fmt);
            OnPropertyChanged(nameof(SelectedProductFamilyInfo));
            OnPropertyChanged(nameof(SelectedProductFamily));
            OnPropertyChanged(nameof(BandsPerPage));
            OnPropertyChanged(nameof(IsMultiBand));

            // Format setzen — sicherstellen dass Format zur Family passt
            var formatInfo = FormatDefinitions.Get(project.Format);
            if (formatInfo.Family != _selectedProductFamily)
                formatInfo = FormatDefinitions.GetDefaultFormat(_selectedProductFamily);
            SelectedFormat = formatInfo;

            if (_selectedFormat.IsModuleBased)
            {
                // Build _allMpPages from project.MpPages
                _allMpPages.Clear();
                MpModules.Clear();
                SelectedMpModule = null;
                SelectedMpCell = null;

                if (project.MpPages != null && project.MpPages.Count > 0)
                {
                    foreach (var mpPage in project.MpPages)
                    {
                        var pageVms = mpPage.Modules
                            .Select(mod =>
                            {
                                // Zellen neu generieren falls noetig
                                if (mod.AddressCells.Count == 0)
                                    mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
                                return new MpModuleViewModel(mod);
                            })
                            .ToList();
                        _allMpPages.Add(pageVms);
                    }
                }

                if (_allMpPages.Count == 0)
                {
                    var familyInfo = ProductFamilyDefinitions.Get(_selectedFormat.Family);
                    _allMpPages.Add(CreateEmptyMpPage(familyInfo.ModulesPerPage));
                }

                _currentPageIndex = 0;
                foreach (var mod in _allMpPages[0])
                    MpModules.Add(mod);
                NotifyPageProperties();
                OnPropertyChanged(nameof(IsModuleBased));
                if (MpModules.Count > 0)
                    SelectedMpModule = MpModules[0];
            }
            else
            {
                // Build _allPages from project.Pages (ET200SP)
                _allPages.Clear();
                int labelsPerPage = _selectedFormat.LabelsPerPage;

                foreach (var projectPage in project.Pages)
                {
                    var pageVms = new List<LabelViewModel>(labelsPerPage);
                    for (int i = 0; i < labelsPerPage; i++)
                    {
                        var cell = new LabelCell { Index = i };
                        if (i < projectPage.Labels.Count)
                        {
                            var src = projectPage.Labels[i];
                            cell.Header = src.Header;
                            cell.Line1 = src.Line1;
                            cell.Line2 = src.Line2;
                            cell.FontSize = src.FontSize;
                            cell.IsBold = src.IsBold;
                            cell.IsItalic = src.IsItalic;
                            cell.FontFamily = src.FontFamily;
                            cell.IsPrintEnabled = src.IsPrintEnabled;
                        }
                        pageVms.Add(new LabelViewModel(cell));
                    }
                    _allPages.Add(pageVms);
                }

                if (_allPages.Count == 0)
                    _allPages.Add(CreateEmptyPage(labelsPerPage));

                _currentPageIndex = 0;
                Labels.Clear();
                SelectedLabel = null;
                foreach (var lvm in _allPages[0])
                    Labels.Add(lvm);
                NotifyPageProperties();
                OnPropertyChanged(nameof(IsModuleBased));
            }

            // Einstellungen
            _settings.MarginTop = project.Settings.MarginTop;
            _settings.MarginLeft = project.Settings.MarginLeft;
            _settings.MarginBottom = project.Settings.MarginBottom;
            _settings.MarginRight = project.Settings.MarginRight;
            _settings.FontSize = project.Settings.FontSize;
            _settings.IsBold = project.Settings.IsBold;
            _settings.IsItalic = project.Settings.IsItalic;
            _settings.FontFamily = project.Settings.FontFamily;
            InputMarginTop = project.Settings.MarginTop;
            InputMarginLeft = project.Settings.MarginLeft;
            InputMarginBottom = project.Settings.MarginBottom;
            InputMarginRight = project.Settings.MarginRight;
            InputFontSize = project.Settings.FontSize;
            InputIsBold = project.Settings.IsBold;
            InputIsItalic = project.Settings.IsItalic;
            InputFontFamily = project.Settings.FontFamily;
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(PreviewMargin));

            // Kalibrierung + Druckoptionen
            CalibrationOffsetX = project.CalibrationOffsetX;
            CalibrationOffsetY = project.CalibrationOffsetY;
            PrintGridLines = project.PrintGridLines;

            _currentFilePath = filePath;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshRecentFiles();
            if (Labels.Count > 0) SelectedLabel = Labels[0];
            StatusMessage = $"Geladen: {Path.GetFileName(filePath)} ({PageCount} Seiten)";
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

            if (_selectedFormat.IsModuleBased)
            {
                var mpPrintPages = _allMpPages
                    .Select(page => (IReadOnlyList<MpModuleViewModel>)page.AsReadOnly())
                    .ToList();
                PrintService.PrintMp(mpPrintPages, _selectedFormat, _settings, PrintGridLines,
                    CalibrationOffsetX, CalibrationOffsetY);
            }
            else
            {
                var printPages = _allPages
                    .Select(page => (IReadOnlyList<LabelViewModel>)page.AsReadOnly())
                    .ToList();
                PrintService.Print(printPages, _selectedFormat, _settings, PrintGridLines,
                    CalibrationOffsetX, CalibrationOffsetY);
            }
            StatusMessage = $"Druckauftrag gesendet ({PageCount} Seiten)";
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

    // === Import Methods ===

    private void ImportCsv()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV-Dateien|*.csv|Alle Dateien|*.*",
            Title = "CSV-Datei importieren"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var cells = CsvImportService.Import(dlg.FileName);
                if (cells.Count == 0)
                {
                    StatusMessage = "CSV-Datei enthaelt keine Daten";
                    return;
                }
                PopulateFromImportedCells(cells);
                StatusMessage = $"{cells.Count} Etiketten aus CSV importiert";
            }
            catch (Exception ex)
            {
                StatusMessage = $"CSV-Importfehler: {ex.Message}";
                MessageBox.Show(
                    $"Fehler beim CSV-Import:\n{ex.Message}",
                    "Importfehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void ImportExcel()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel-Dateien|*.xlsx|Alle Dateien|*.*",
            Title = "Excel-Datei importieren"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var cells = ExcelImportService.Import(dlg.FileName);
                if (cells.Count == 0)
                {
                    StatusMessage = "Excel-Datei enthaelt keine Daten";
                    return;
                }
                PopulateFromImportedCells(cells);
                StatusMessage = $"{cells.Count} Etiketten aus Excel importiert";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Excel-Importfehler: {ex.Message}";
                MessageBox.Show(
                    $"Fehler beim Excel-Import:\n{ex.Message}",
                    "Importfehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void ImportSchematic()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PDF-Dateien|*.pdf|Alle Dateien|*.*",
            Title = "PDF-Schaltplan importieren"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var result = SchematicParserService.Parse(dlg.FileName);
                var importVm = new PdfImportViewModel();
                importVm.LoadFromResult(result);
                var dialog = new PdfImportDialog(importVm)
                {
                    Owner = Application.Current.MainWindow
                };
                if (dialog.ShowDialog() == true)
                {
                    var selectedModules = importVm.GetSelectedModules();
                    if (selectedModules.Count == 0)
                    {
                        StatusMessage = "Keine Module ausgewaehlt";
                        return;
                    }
                    PopulateFromParsedModules(selectedModules);
                    StatusMessage = $"{selectedModules.Count} Module aus Schaltplan importiert";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"PDF-Importfehler: {ex.Message}";
                MessageBox.Show(
                    $"Fehler beim PDF-Import:\n{ex.Message}",
                    "Importfehler",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void PopulateFromImportedCells(List<LabelCell> cells)
    {
        int labelsPerPage = _selectedFormat.LabelsPerPage;
        int startIndex = SelectedLabel?.Index ?? 0;
        int startPage = _currentPageIndex;
        int cellIndex = 0;

        int pageIdx = startPage;
        int labelIdx = startIndex;

        while (cellIndex < cells.Count)
        {
            // Ensure page exists
            while (pageIdx >= _allPages.Count)
            {
                int count = _selectedFormat.LabelsPerPage;
                _allPages.Add(CreateEmptyPage(count));
            }

            var pageVms = _allPages[pageIdx];
            if (labelIdx < pageVms.Count)
            {
                var src = cells[cellIndex];
                var target = pageVms[labelIdx];
                target.Header = src.Header;
                target.Line1 = src.Line1;
                target.Line2 = src.Line2;
                cellIndex++;
            }

            labelIdx++;
            if (labelIdx >= labelsPerPage)
            {
                labelIdx = 0;
                pageIdx++;
            }
        }

        // Navigate to the start page to show results
        NavigateToPage(startPage);
        NotifyPageProperties();
        IsDirty = true;

        MessageBox.Show(
            $"{cells.Count} Etiketten importiert.\n" +
            $"Verteilt auf {_allPages.Count} Seite(n).",
            "Import abgeschlossen",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PopulateFromParsedModules(List<ParsedModule> modules)
    {
        // Convert parsed modules to label cells using the address generator
        var cells = new List<LabelCell>();

        foreach (var module in modules)
        {
            // Map parsed module type to AddressGenerator ModuleType
            ModuleType? moduleType = module.ModuleType.ToUpperInvariant() switch
            {
                "DI" => ModuleType.DI,
                "DO" => ModuleType.DO,
                "AI" => ModuleType.AI,
                "AO" => ModuleType.AO,
                _ => null
            };

            if (moduleType is not null)
            {
                var info = AddressGenerator.ModuleTypes.First(m => m.Type == moduleType.Value);

                // Determine count parameter based on module type
                int count;
                if (info.IsBitAddressed)
                {
                    // Digital modules: channels / 8 = bytes
                    count = Math.Max(1, module.ChannelCount / 8);
                    if (count == 0) count = 1;
                }
                else
                {
                    // Analog modules: count = channel count
                    count = Math.Max(1, module.ChannelCount);
                }

                var generated = AddressGenerator.Generate(module.ModuleName, moduleType.Value, module.StartByte, count);
                cells.Add(new LabelCell
                {
                    Header = generated.Header,
                    Line1 = generated.Line1,
                    Line2 = generated.Line2
                });
            }
            else
            {
                // Unknown type: just use the module name and channel addresses
                var addresses = module.Channels.Select(c => c.Address).ToList();
                int half = (addresses.Count + 1) / 2;

                cells.Add(new LabelCell
                {
                    Header = module.ModuleName,
                    Line1 = string.Join("  ", addresses.Take(half)),
                    Line2 = addresses.Count > half ? string.Join("  ", addresses.Skip(half)) : string.Empty
                });
            }
        }

        if (cells.Count > 0)
            PopulateFromImportedCells(cells);
    }

    // === Selective Print Methods ===

    private void SelectAllForPrint()
    {
        foreach (var page in _allPages)
            foreach (var label in page)
                label.IsPrintEnabled = true;
        IsDirty = true;
        StatusMessage = "Alle Etiketten zum Drucken aktiviert";
    }

    private void DeselectAllForPrint()
    {
        foreach (var page in _allPages)
            foreach (var label in page)
                label.IsPrintEnabled = false;
        IsDirty = true;
        StatusMessage = "Alle Etiketten vom Drucken ausgeschlossen";
    }

    private void SelectFilledForPrint()
    {
        foreach (var page in _allPages)
            foreach (var label in page)
                label.IsPrintEnabled = label.HasText;
        IsDirty = true;
        StatusMessage = "Nur befuellte Etiketten zum Drucken aktiviert";
    }

    private void TogglePrint()
    {
        if (SelectedLabel is null) return;
        SelectedLabel.IsPrintEnabled = !SelectedLabel.IsPrintEnabled;
        IsDirty = true;
        StatusMessage = SelectedLabel.IsPrintEnabled
            ? $"Etikett {SelectedLabel.DisplayPosition}: Druck aktiviert"
            : $"Etikett {SelectedLabel.DisplayPosition}: Druck deaktiviert";
    }

    public void SelectLabel(LabelViewModel label)
    {
        SelectedLabel = label;
    }

    /// <summary>
    /// Fragt den Benutzer ob ungespeicherte Aenderungen verworfen werden sollen.
    /// Gibt true zurueck wenn fortgefahren werden darf.
    /// </summary>
    public bool ConfirmDiscardChanges()
    {
        if (!_isDirty) return true;

        var result = MessageBox.Show(
            "Es gibt ungespeicherte Aenderungen.\nMoechten Sie diese speichern?",
            "Ungespeicherte Aenderungen",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => DoSaveAndConfirm(),
            MessageBoxResult.No => true,
            _ => false // Cancel
        };
    }

    private bool DoSaveAndConfirm()
    {
        if (_currentFilePath is null)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "ET-Printer Projekt (*.etprint)|*.etprint",
                DefaultExt = ".etprint",
                FileName = "Projekt"
            };
            if (dialog.ShowDialog() != true)
                return false;
            DoSave(dialog.FileName);
        }
        else
        {
            DoSave(_currentFilePath);
        }
        return true;
    }
}
