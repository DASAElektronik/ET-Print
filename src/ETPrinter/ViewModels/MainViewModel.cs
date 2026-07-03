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
    private int _inputHeaderFontSize = 9;
    private bool _inputHeaderIsBold = true;
    // Startwerte = LabelSettings-Defaults (ET200SP), sonst verstellt der erste
    // Live-Apply drei nicht angefasste Raender auf die abweichenden Input-Werte.
    private double _inputMarginTop = 20.5;
    private double _inputMarginLeft = 27.5;
    private double _inputMarginBottom = 20.5;
    private double _inputMarginRight = 27.5;

    // Guard: blockiert Auto-Apply waehrend die Input-Felder programmatisch
    // geladen werden (z.B. beim Label-Wechsel oder Projekt-Laden).
    private bool _suspendLiveApply;

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
            Services.MpModuleLayoutFactory.VariantsForFamily(_selectedProductFamily));
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

        // Copy / Paste
        CopyCommand = new RelayCommand(CopySelection, CanCopy);
        PasteCommand = new RelayCommand(PasteFromClipboard, CanPaste);

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

    // Unterdrueckt die Inhaltsverlust-Rueckfrage bei programmatischen Wechseln
    // (Projekt laden, Neues Projekt, Test-Automation).
    private bool _suppressContentLossConfirm;
    internal bool SuppressContentLossConfirm
    {
        get => _suppressContentLossConfirm;
        set => _suppressContentLossConfirm = value;
    }

    private bool HasAnyContent() =>
        _allPages.Any(p => p.Any(l => l.HasText))
        || _allMpPages.Any(p => p.Any(m => m.HasText));

    /// <summary>Warnt vor Format-/Familienwechsel, wenn befuellte Etiketten/Module
    /// verworfen wuerden. True = fortfahren.</summary>
    private bool ConfirmContentLoss()
    {
        if (_suppressContentLossConfirm || !HasAnyContent()) return true;
        var result = MessageBox.Show(
            "Beim Wechsel von Druckformat oder Produktfamilie werden alle\n" +
            "befuellten Etiketten/Module verworfen.\n\nFortfahren?",
            "Format wechseln", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    public ProductFamilyInfo SelectedProductFamilyInfo
    {
        get => ProductFamilyDefinitions.Get(_selectedProductFamily);
        set
        {
            if (value is null || value.Family == _selectedProductFamily) return;
            if (!ConfirmContentLoss())
            {
                // ComboBox-Auswahl asynchron zuruecksetzen (synchrones
                // PropertyChanged wird vom laufenden Binding-Update verschluckt)
                Application.Current?.Dispatcher.BeginInvoke(
                    new Action(() => OnPropertyChanged(nameof(SelectedProductFamilyInfo))));
                return;
            }
            if (SetProperty(ref _selectedProductFamily, value.Family))
            {
                // Ränder auf Family-Defaults setzen. Guard verhindert, dass der erste
                // Input-Setter via Live-Apply die noch alten Input-Werte der vorherigen
                // Familie in _settings zurueckschreibt (Regression aus 06d52ad).
                _settings.ResetForFamily(_selectedProductFamily);
                _suspendLiveApply = true;
                try
                {
                    InputMarginTop = _settings.MarginTop;
                    InputMarginLeft = _settings.MarginLeft;
                    InputMarginBottom = _settings.MarginBottom;
                    InputMarginRight = _settings.MarginRight;
                }
                finally { _suspendLiveApply = false; }
                OnPropertyChanged(nameof(Settings));
                OnPropertyChanged(nameof(PreviewMargin));
                NotifyMpPreviewChanged();

                // Formate filtern
                AvailableFormats.Clear();
                foreach (var fmt in FormatDefinitions.GetFormatsForFamily(_selectedProductFamily))
                    AvailableFormats.Add(fmt);

                // Modul-Varianten familiengerecht filtern (25mm vs 35mm)
                AvailableMpVariants.Clear();
                foreach (var v in MpModuleLayoutFactory.VariantsForFamily(_selectedProductFamily))
                    AvailableMpVariants.Add(v);
                OnPropertyChanged(nameof(AvailableMpArticles));

                // Erstes Format der neuen Familie waehlen — Inhaltsverlust wurde
                // hier schon bestaetigt, daher kein zweiter Format-Prompt.
                _suppressContentLossConfirm = true;
                try { SelectedFormat = FormatDefinitions.GetDefaultFormat(_selectedProductFamily); }
                finally { _suppressContentLossConfirm = false; }
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

    // True wenn im aktuellen Modus eine Auswahl (Label oder MP-Modul) existiert.
    // Steuert IsEnabled der Eingabe-Tabs (Generator, Manuell, MP Modul).
    public bool HasSelection => SelectedLabel is not null || SelectedMpModule is not null;

    // Incrementaler Token fuer MP-Preview-Refresh. MpPreviewControl hoert darauf
    // und rendert neu, wenn der Generator o.ae. Daten innerhalb von Modulen aendert
    // (Zell-PropertyChanged allein triggert kein Re-Render des Canvas-Controls).
    private int _mpPreviewRefreshToken;
    public int MpPreviewRefreshToken
    {
        get => _mpPreviewRefreshToken;
        private set => SetProperty(ref _mpPreviewRefreshToken, value);
    }
    public void NotifyMpPreviewChanged() => MpPreviewRefreshToken++;

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
                OnPropertyChanged(nameof(SelectedMpVariant));
                OnPropertyChanged(nameof(SelectedMpArticle));
                OnPropertyChanged(nameof(HasSelection));
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

    public MpModuleLayout? SelectedMpVariant
    {
        get => _selectedMpModule is not null
            ? MpModuleLayoutFactory.GetLayout(_selectedMpModule.Variant)
            : null;
        set
        {
            if (value is not null && _selectedMpModule is not null)
            {
                // Manuelle Variantenwahl = benutzerdefiniert (Katalog-Artikel abwaehlen)
                _selectedMpModule.ArticleNumber = null;
                _selectedMpModule.Variant = value.Variant;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedMpModuleInfo));
                OnPropertyChanged(nameof(SelectedMpArticle));
            }
        }
    }

    // === Modul-Katalog (konkrete Siemens-Module mit exakter Klemmenbelegung) ===

    public IReadOnlyList<MpCatalogEntry> AvailableMpArticles => MpModuleCatalog.EntriesForFamily(_selectedProductFamily);

    public MpCatalogEntry? SelectedMpArticle
    {
        get => _selectedMpModule is null
            ? null
            : MpModuleCatalog.Find(_selectedMpModule.ArticleNumber) ?? MpModuleCatalog.CustomEntry;
        set
        {
            if (value is null || _selectedMpModule is null) return;

            _selectedMpModule.ArticleNumber =
                string.IsNullOrEmpty(value.ArticleNo) ? null : value.ArticleNo;

            // Generator-Modultyp am Katalogeintrag vorbelegen (DI/DO/AI/AO)
            if (!string.IsNullOrEmpty(value.ArticleNo))
            {
                var typeInfo = AddressGenerator.ModuleTypes.FirstOrDefault(t => t.Type == value.IoType);
                if (typeInfo is not null)
                    GenModuleType = typeInfo;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMpVariant));
            OnPropertyChanged(nameof(SelectedMpModuleInfo));
        }
    }

    public string SelectedMpModuleInfo
    {
        get
        {
            if (_selectedMpModule is null) return "Kein Modul ausgewaehlt";
            var familyInfo = ProductFamilyDefinitions.Get(_selectedFormat.Family);
            int band = familyInfo.BandOf(_selectedMpModule.ModuleIndex);
            int col = familyInfo.ColumnOf(_selectedMpModule.ModuleIndex);
            return $"Modul {_selectedMpModule.ModuleIndex + 1} / {MpModules.Count} (Spalte {col + 1}, Band {band + 1})";
        }
    }

    public FormatInfo SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (value is null || value.Format == _selectedFormat.Format) return;
            if (!ConfirmContentLoss())
            {
                Application.Current?.Dispatcher.BeginInvoke(
                    new Action(() => OnPropertyChanged(nameof(SelectedFormat))));
                return;
            }
            if (SetProperty(ref _selectedFormat, value))
            {
                InitializeLabels();
                OnPropertyChanged(nameof(HasHeader));
                OnPropertyChanged(nameof(IsDoubleLine));
                OnPropertyChanged(nameof(Line2RowHeight));
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
                    // Schrift-Einstellungen des Etiketts laden — Live-Apply aussetzen,
                    // sonst wuerde das Label sofort auf seine eigenen Werte "ueberschrieben".
                    _suspendLiveApply = true;
                    try
                    {
                        InputFontSize = value.CellFontSize;
                        InputIsBold = value.CellIsBold;
                        InputIsItalic = value.CellIsItalic;
                        InputFontFamily = value.CellFontFamily;
                    }
                    finally { _suspendLiveApply = false; }
                    StatusMessage = $"Etikett {value.DisplayPosition}/{Labels.Count} (Seite {_currentPageIndex + 1}/{PageCount})";
                }
                OnPropertyChanged(nameof(SelectedLabelInfo));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string SelectedLabelInfo => SelectedLabel is not null
        ? $"Etikett {SelectedLabel.DisplayPosition} von {Labels.Count} (Seite {_currentPageIndex + 1})"
        : "Kein Etikett ausgewaehlt";

    public bool HasHeader => _selectedFormat.HasHeader;
    public bool IsDoubleLine => _selectedFormat.RowsPerLabel == 2;
    public bool IsVertical => _selectedFormat.IsVertical;

    // Hoehe der zweiten Adressreihe in der Vertikal-Preview: einzeilige Formate
    // drucken keine Line2, also auch keine untere Reihe anzeigen (Preview = Druck).
    public GridLength Line2RowHeight => IsDoubleLine
        ? new GridLength(1, GridUnitType.Star)
        : new GridLength(0);
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
    // Alle Input-Setter triggern Live-Preview: Aenderungen werden sofort angewendet,
    // ohne dass "Uebernehmen" geklickt werden muss.
    public int InputFontSize
    {
        get => _inputFontSize;
        set
        {
            if (SetProperty(ref _inputFontSize, value))
                ApplyInputFontToSelected();
        }
    }

    public bool InputIsBold
    {
        get => _inputIsBold;
        set
        {
            if (SetProperty(ref _inputIsBold, value))
                ApplyInputFontToSelected();
        }
    }

    public bool InputIsItalic
    {
        get => _inputIsItalic;
        set
        {
            if (SetProperty(ref _inputIsItalic, value))
                ApplyInputFontToSelected();
        }
    }

    public string InputFontFamily
    {
        get => _inputFontFamily;
        set
        {
            if (SetProperty(ref _inputFontFamily, value))
                ApplyInputFontToSelected();
        }
    }

    public int InputHeaderFontSize
    {
        get => _inputHeaderFontSize;
        set
        {
            if (SetProperty(ref _inputHeaderFontSize, value))
                ApplyHeaderStyleToSettings();
        }
    }

    public bool InputHeaderIsBold
    {
        get => _inputHeaderIsBold;
        set
        {
            if (SetProperty(ref _inputHeaderIsBold, value))
                ApplyHeaderStyleToSettings();
        }
    }

    public double InputMarginTop
    {
        get => _inputMarginTop;
        set
        {
            if (SetProperty(ref _inputMarginTop, value))
                ApplyMarginsToSettings();
        }
    }

    public double InputMarginLeft
    {
        get => _inputMarginLeft;
        set
        {
            if (SetProperty(ref _inputMarginLeft, value))
                ApplyMarginsToSettings();
        }
    }

    public double InputMarginBottom
    {
        get => _inputMarginBottom;
        set
        {
            if (SetProperty(ref _inputMarginBottom, value))
                ApplyMarginsToSettings();
        }
    }

    public double InputMarginRight
    {
        get => _inputMarginRight;
        set
        {
            if (SetProperty(ref _inputMarginRight, value))
                ApplyMarginsToSettings();
        }
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

    // Preview-Convenience fuer XAML-Bindings auf den globalen Header-Style
    public int HeaderPreviewFontSize => (int)Math.Round(_settings.HeaderFontSize * 1.06);
    public FontWeight HeaderPreviewFontWeight => _settings.HeaderIsBold ? FontWeights.Bold : FontWeights.Normal;

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
    public ICommand CopyCommand { get; }
    public ICommand PasteCommand { get; }

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
        // Default-Variante haengt an der Familie (25mm: MP25_16, sonst DI_DQ_16)
        var defaultVariant = MpModuleLayoutFactory.DefaultVariantFor(_selectedFormat.Family);
        var page = new List<MpModuleViewModel>(moduleCount);
        for (int i = 0; i < moduleCount; i++)
        {
            var module = new MpModule { ModuleIndex = i, Variant = defaultVariant };
            module.AddressCells = MpModuleLayoutFactory.CreateCells(module.Variant);
            page.Add(new MpModuleViewModel(module) { ContentChanged = OnMpContentChanged });
        }
        return page;
    }

    // Direktbindungen des MP-Tabs (Header, Netzadressen, Zelltexte, Variante)
    // mutieren das Model ohne Command — Dirty-Markierung und Preview-Refresh
    // haengen deshalb am ContentChanged-Callback der Modul-VMs.
    private void OnMpContentChanged()
    {
        if (_suspendLiveApply) return;
        IsDirty = true;
        NotifyMpPreviewChanged();
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
        // Multi-Selection ist seitenlokal: beim Seitenwechsel ALLE Markierungen
        // (auch auf unsichtbaren Seiten) aufheben — sonst kopiert Strg+C spaeter
        // unsichtbar markierte Etiketten einer anderen Seite.
        ClearChecksOnAllPages();

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
            NotifyMpPreviewChanged();
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

            // Fuer MP: Anzahl Bytes/Kanaele automatisch aus Modulvariante ableiten
            var info = AddressGenerator.ModuleTypes.First(m => m.Type == GenModuleType.Type);
            var editableCells = SelectedMpModule.AddressCells.Where(c => c.IsEditable).ToList();
            int editableCount = editableCells.Count;

            if (info.IsBitAddressed)
            {
                // Byte-Anzahl: 35mm-Varianten haben ein festes Kanalraster -> aus den
                // editierbaren Zellen ableiten. 25mm-Varianten haben generische 20/40
                // Slots (mehr als Kanaele) -> GenCount (Bytes) entscheidet, Rest leer.
                int maxBytes = editableCount / 8;
                bool is25 = SelectedMpModule.Variant
                    is MpModuleVariant.MP25_16 or MpModuleVariant.MP25_32;
                int byteCount = is25
                    ? Math.Clamp(GenCount, 1, maxBytes)
                    : Math.Max(1, maxBytes);

                var addresses = new List<string>();
                for (int b = 0; b < byteCount; b++)
                {
                    int byteNum = GenStartByte + b;
                    for (int bit = 0; bit < 8; bit++)
                        addresses.Add($"{info.Prefix} {byteNum}.{bit}");
                }
                for (int i = 0; i < editableCells.Count; i++)
                    editableCells[i].Text = i < addresses.Count ? addresses[i] : string.Empty;

                // Auto-Advance: um die tatsaechliche Byte-Anzahl weiterschalten
                if (GenAutoAdvanceAddress)
                    GenStartByte += byteCount;
            }
            else
            {
                // Analog: GenCount Kanaele, SPALTENAUSGEGLICHEN verteilen. Das Excel-
                // Layout hat 5 generische Bloecke pro Spalte (inkl. MANA/Reserve);
                // ein AI 8 hat physisch CH0-3 links + CH4-7 rechts, also 4+4 statt 5+3.
                // Restbloecke bleiben leer (fuer manuelle MANA-Beschriftung).
                int channelCount = Math.Min(Math.Max(GenCount, 0), editableCount);
                var byColumn = editableCells
                    .GroupBy(c => c.StartCol)
                    .OrderBy(g => g.Key)
                    .Select(g => g.ToList())
                    .ToList();
                int numCols = byColumn.Count;

                // Alle Zellen leeren, dann kanalweise spaltenausgeglichen befuellen
                foreach (var c in editableCells) c.Text = string.Empty;
                int ch = 0;
                for (int colIdx = 0; colIdx < numCols && ch < channelCount; colIdx++)
                {
                    // Diese Spalte bekommt ceil(rest / verbleibende Spalten) Kanaele
                    int remainingCols = numCols - colIdx;
                    int perCol = (int)Math.Ceiling((channelCount - ch) / (double)remainingCols);
                    var colCells = byColumn[colIdx];
                    for (int k = 0; k < perCol && k < colCells.Count; k++)
                        colCells[k].Text = $"{info.Prefix} {GenStartByte + ch++ * 2}";
                }

                if (GenAutoAdvanceAddress)
                    GenStartByte += channelCount * 2;
            }

            // GenCount fuer UI-Anzeige aktualisieren (damit Preview stimmt)
            // NICHT GenAutoAdvanceAddress nochmal ausfuehren — wurde oben schon gemacht

            IsDirty = true;
            int filledCount = editableCells.Count(c => c.HasText);
            StatusMessage = $"Generiert: {GenModuleName} ({GenModuleType.DisplayName}) → {filledCount} Adressen auf Modul {SelectedMpModule.ModuleIndex + 1}";

            NotifyMpPreviewChanged();

            // Zum naechsten Modul springen (analog AdvanceToNextLabel bei ET200SP).
            // Adressen werden NICHT automatisch neu generiert — User muss Variante
            // und Start-Byte pruefen und erneut "Generieren + Uebertragen" druecken.
            AdvanceToNextMpModule();
        }
        else if (SelectedLabel is not null)
        {
            // ET200SP: bisherige Logik
            string line1 = result.Line1;
            string line2 = result.Line2;

            // Einzeilige Formate drucken Line2 nicht — Adressen kanal-aufsteigend
            // in Line1 zusammenfuehren statt sie unsichtbar in Line2 abzulegen.
            if (!IsDoubleLine && !string.IsNullOrWhiteSpace(line2))
            {
                line1 = MergeAddressLines(result.Line1, result.Line2);
                line2 = string.Empty;
            }

            InputHeader = result.Header;
            InputLine1 = line1;
            InputLine2 = line2;

            SelectedLabel.Header = result.Header;
            SelectedLabel.Line1 = line1;
            SelectedLabel.Line2 = line2;
            ApplyFontToLabel(SelectedLabel);

            IsDirty = true;
            StatusMessage = $"Generiert: {GenModuleName} ({GenModuleType.DisplayName}) ab Byte {GenStartByte}";

            if (GenAutoAdvanceAddress)
            {
                // Nur um die tatsaechlich aufs Etikett gepasste Anzahl weiterschalten
                // (Generator kappt Digital bei 2 Bytes) — sonst gehen Adressen verloren.
                int effective = AddressGenerator.GetEffectiveCount(GenModuleType.Type, GenCount);
                GenStartByte = AddressGenerator.GetNextStartByte(GenModuleType.Type, GenStartByte, effective);
            }

            AdvanceToNextLabel();
        }
    }

    /// <summary>Verschraenkt Zeile 1 (ungerade Bits, oben) und Zeile 2 (gerade Bits,
    /// unten) slotweise kanal-aufsteigend zu einer Zeile (fuer einzeilige Formate).</summary>
    private static string MergeAddressLines(string line1, string line2)
    {
        var odd = line1.Split("  ", StringSplitOptions.None);
        var even = line2.Split("  ", StringSplitOptions.None);
        var merged = new List<string>();
        for (int i = 0; i < Math.Max(odd.Length, even.Length); i++)
        {
            if (i < even.Length && !string.IsNullOrWhiteSpace(even[i])) merged.Add(even[i]);
            if (i < odd.Length && !string.IsNullOrWhiteSpace(odd[i])) merged.Add(odd[i]);
        }
        return string.Join("  ", merged);
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

    private void AdvanceToNextMpModule()
    {
        if (SelectedMpModule is null) return;

        int currentIndex = MpModules.IndexOf(SelectedMpModule);
        if (currentIndex < 0) return;

        if (currentIndex + 1 < MpModules.Count)
        {
            SelectedMpModule = MpModules[currentIndex + 1];
        }
        else if (_currentPageIndex < PageCount - 1)
        {
            NavigateToPage(_currentPageIndex + 1);
            if (MpModules.Count > 0)
                SelectedMpModule = MpModules[0];
        }
        // Am Ende der letzten Seite: keine neue Seite automatisch anlegen
        // (ET200MP-Seiten werden manuell hinzugefuegt, Varianten pro Seite)
    }

    // === Multi-Selection (Check-State fuer Copy) ===

    public void ClearAllLabelChecks()
    {
        foreach (var l in Labels) l.IsChecked = false;
    }

    private void ClearChecksOnAllPages()
    {
        foreach (var page in _allPages)
            foreach (var l in page) l.IsChecked = false;
        foreach (var page in _allMpPages)
            foreach (var m in page) m.IsChecked = false;
    }

    public void ClearAllMpModuleChecks()
    {
        foreach (var m in MpModules) m.IsChecked = false;
        NotifyMpPreviewChanged();
    }

    /// <summary>Shift+Klick: Range zwischen SelectedLabel (Anker) und target checken.</summary>
    public void CheckRangeToLabel(LabelViewModel target)
    {
        var anchor = SelectedLabel;
        int targetIndex = Labels.IndexOf(target);
        if (targetIndex < 0) return;
        int anchorIndex = anchor is not null ? Labels.IndexOf(anchor) : targetIndex;
        if (anchorIndex < 0) anchorIndex = targetIndex;

        int from = Math.Min(anchorIndex, targetIndex);
        int to = Math.Max(anchorIndex, targetIndex);
        for (int i = from; i <= to; i++)
            Labels[i].IsChecked = true;
        SelectedLabel = target;
    }

    /// <summary>Shift+Klick: Range zwischen SelectedMpModule (Anker) und target checken.</summary>
    public void CheckRangeToMpModule(MpModuleViewModel target)
    {
        var anchor = SelectedMpModule;
        int targetIndex = MpModules.IndexOf(target);
        if (targetIndex < 0) return;
        int anchorIndex = anchor is not null ? MpModules.IndexOf(anchor) : targetIndex;
        if (anchorIndex < 0) anchorIndex = targetIndex;

        int from = Math.Min(anchorIndex, targetIndex);
        int to = Math.Max(anchorIndex, targetIndex);
        for (int i = from; i <= to; i++)
            MpModules[i].IsChecked = true;
        SelectedMpModule = target;
        NotifyMpPreviewChanged();
    }

    // === Copy / Paste ===

    private bool CanCopy()
    {
        if (_selectedFormat.IsModuleBased)
            return GetCopySourceModules().Any();
        return GetCopySourceLabels().Any();
    }

    private bool CanPaste()
    {
        if (_selectedFormat.IsModuleBased)
            return ClipboardService.HasModules && SelectedMpModule is not null;
        return ClipboardService.HasLabels && SelectedLabel is not null;
    }

    private IReadOnlyList<LabelViewModel> GetCopySourceLabels()
    {
        // Phase B: Multi-Select via IsChecked wird hier beruecksichtigt.
        // Phase A: nur das Single-Selected Label.
        var checkedLabels = Labels.Where(l => l.IsChecked).ToList();
        if (checkedLabels.Count > 0) return checkedLabels;
        return SelectedLabel is not null ? [SelectedLabel] : [];
    }

    private IReadOnlyList<MpModuleViewModel> GetCopySourceModules()
    {
        var checkedModules = MpModules.Where(m => m.IsChecked).ToList();
        if (checkedModules.Count > 0) return checkedModules;
        return SelectedMpModule is not null ? [SelectedMpModule] : [];
    }

    private void CopySelection()
    {
        if (_selectedFormat.IsModuleBased)
        {
            var source = GetCopySourceModules();
            if (source.Count == 0) return;
            ClipboardService.SetModules(source.Select(vm => vm.GetModule()));
            StatusMessage = $"{source.Count} Modul(e) kopiert";
        }
        else
        {
            var source = GetCopySourceLabels();
            if (source.Count == 0) return;
            ClipboardService.SetLabels(source.Select(vm => vm.GetCell()));
            StatusMessage = $"{source.Count} Etikett(en) kopiert";
        }
        CommandManager.InvalidateRequerySuggested();
    }

    private void PasteFromClipboard()
    {
        if (_selectedFormat.IsModuleBased)
            PasteModules();
        else
            PasteLabels();
        IsDirty = true;
        CommandManager.InvalidateRequerySuggested();
    }

    private void PasteLabels()
    {
        if (SelectedLabel is null) return;
        var source = ClipboardService.GetLabels();
        if (source.Count == 0) return;

        int startIndex = SelectedLabel.Index;
        int pasted = 0;
        for (int i = 0; i < source.Count; i++)
        {
            int targetIndex = startIndex + i;
            if (targetIndex >= Labels.Count) break; // Keine neue Seite automatisch
            Labels[targetIndex].SetCell(source[i]);
            pasted++;
        }
        StatusMessage = $"{pasted} Etikett(en) eingefuegt ab Position {startIndex + 1}";
    }

    private void PasteModules()
    {
        if (SelectedMpModule is null) return;
        var source = ClipboardService.GetModules();
        if (source.Count == 0) return;

        int startIndex = MpModules.IndexOf(SelectedMpModule);
        if (startIndex < 0) return;
        int pasted = 0;
        for (int i = 0; i < source.Count; i++)
        {
            int targetIndex = startIndex + i;
            if (targetIndex >= MpModules.Count) break;
            MpModules[targetIndex].SetModule(source[i]);
            pasted++;
        }
        NotifyMpPreviewChanged();
        StatusMessage = $"{pasted} Modul(e) eingefuegt ab Position {startIndex + 1}";
    }

    private void ClearAllLabels()
    {
        if (_selectedFormat.IsModuleBased)
        {
            // ET200MP: alle Modul-Seiten auf eine leere Seite zuruecksetzen
            _allMpPages.Clear();
            MpModules.Clear();
            SelectedMpModule = null;
            SelectedMpCell = null;

            var familyInfo = ProductFamilyDefinitions.Get(_selectedFormat.Family);
            var firstMpPage = CreateEmptyMpPage(familyInfo.ModulesPerPage);
            _allMpPages.Add(firstMpPage);
            _currentPageIndex = 0;

            foreach (var vm in firstMpPage)
                MpModules.Add(vm);

            NotifyPageProperties();
            NotifyMpPreviewChanged();

            if (MpModules.Count > 0)
                SelectedMpModule = MpModules[0];

            IsDirty = true;
            StatusMessage = "Alle Module geloescht";
            return;
        }

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

    // === Live-Preview Apply-Helfer (aus Input-Settern aufgerufen) ===

    private void ApplyInputFontToSelected()
    {
        if (_suspendLiveApply) return;
        if (SelectedLabel is null) return;
        SelectedLabel.CellFontSize = _inputFontSize;
        SelectedLabel.CellIsBold = _inputIsBold;
        SelectedLabel.CellIsItalic = _inputIsItalic;
        SelectedLabel.CellFontFamily = _inputFontFamily;
        IsDirty = true;
    }

    private void ApplyHeaderStyleToSettings()
    {
        if (_suspendLiveApply) return;
        _settings.HeaderFontSize = _inputHeaderFontSize;
        _settings.HeaderIsBold = _inputHeaderIsBold;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(HeaderPreviewFontSize));
        OnPropertyChanged(nameof(HeaderPreviewFontWeight));
        NotifyMpPreviewChanged();
        IsDirty = true;
    }

    private void ApplyMarginsToSettings()
    {
        if (_suspendLiveApply) return;
        _settings.MarginTop = _inputMarginTop;
        _settings.MarginLeft = _inputMarginLeft;
        _settings.MarginBottom = _inputMarginBottom;
        _settings.MarginRight = _inputMarginRight;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(PreviewMargin));
        NotifyMpPreviewChanged();
        IsDirty = true;
    }

    private void ResetSettings()
    {
        _settings.ResetForFamily(_selectedProductFamily);
        _suspendLiveApply = true;
        try
        {
            InputFontSize = _settings.FontSize;
            InputIsBold = _settings.IsBold;
            InputIsItalic = _settings.IsItalic;
            InputFontFamily = _settings.FontFamily;
            InputHeaderFontSize = _settings.HeaderFontSize;
            InputHeaderIsBold = _settings.HeaderIsBold;
            InputMarginTop = _settings.MarginTop;
            InputMarginLeft = _settings.MarginLeft;
            InputMarginBottom = _settings.MarginBottom;
            InputMarginRight = _settings.MarginRight;
        }
        finally { _suspendLiveApply = false; }
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(PreviewMargin));
        OnPropertyChanged(nameof(HeaderPreviewFontSize));
        OnPropertyChanged(nameof(HeaderPreviewFontWeight));
        NotifyMpPreviewChanged();
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
        _settings.HeaderFontSize = InputHeaderFontSize;
        _settings.HeaderIsBold = InputHeaderIsBold;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(PreviewMargin));
        OnPropertyChanged(nameof(HeaderPreviewFontSize));
        OnPropertyChanged(nameof(HeaderPreviewFontWeight));
        NotifyMpPreviewChanged();

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
            NotifyMpPreviewChanged();
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
        // Dirty-Check hat den User schon gefragt — kein zweiter Inhaltsverlust-Prompt
        if (!ConfirmDiscardChanges()) return;
        _suppressContentLossConfirm = true;
        try
        {
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
        finally { _suppressContentLossConfirm = false; }
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

    /// <summary>Serialisiert den KOMPLETTEN Projektzustand (alle Seiten aus
    /// _allPages/_allMpPages). Gemeinsame Quelle fuer DoSave und Test-Automation —
    /// der Automation-Pfad speicherte frueher nur die sichtbare Seite.</summary>
    internal LabelProject BuildProject()
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

        return new LabelProject
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
    }

    private bool DoSave(string filePath)
    {
        try
        {
            ProjectService.Save(BuildProject(), filePath);
            _currentFilePath = filePath;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshRecentFiles();
            StatusMessage = $"Gespeichert: {Path.GetFileName(filePath)} ({PageCount} Seiten)";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Speicherfehler: {ex.Message}";
            // Modal melden: beim Schliessen/Neu/Oeffnen ist die Statusleiste nicht
            // (mehr) sichtbar — ohne Dialog wuerden Daten kommentarlos verworfen.
            MessageBox.Show(
                $"Das Projekt konnte nicht gespeichert werden:\n{ex.Message}",
                "Speicherfehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
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
            ApplyLoadedProject(project, filePath);
            StatusMessage = $"Geladen: {Path.GetFileName(filePath)} ({PageCount} Seiten)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ladefehler: {ex.Message}";
        }
    }

    /// <summary>Uebertraegt ein geladenes Projekt vollstaendig in den ViewModel-Zustand
    /// (Familie, Format, alle Seiten/Module, Einstellungen, Kalibrierung). Gemeinsame
    /// Quelle fuer DoOpen und Test-Automation — Gegenstueck zu <see cref="BuildProject"/>.</summary>
    internal void ApplyLoadedProject(LabelProject project, string? filePath)
    {
        // Laden ersetzt den gesamten Zustand — der Aufrufer hat via
        // ConfirmDiscardChanges bereits gefragt; kein Inhaltsverlust-Prompt.
        _suppressContentLossConfirm = true;
        try
        {
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
                                return new MpModuleViewModel(mod) { ContentChanged = OnMpContentChanged };
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
            _settings.HeaderFontSize = project.Settings.HeaderFontSize;
            _settings.HeaderIsBold = project.Settings.HeaderIsBold;
            _suspendLiveApply = true;
            try
            {
                InputMarginTop = project.Settings.MarginTop;
                InputMarginLeft = project.Settings.MarginLeft;
                InputMarginBottom = project.Settings.MarginBottom;
                InputMarginRight = project.Settings.MarginRight;
                InputFontSize = project.Settings.FontSize;
                InputIsBold = project.Settings.IsBold;
                InputIsItalic = project.Settings.IsItalic;
                InputFontFamily = project.Settings.FontFamily;
                InputHeaderFontSize = project.Settings.HeaderFontSize;
                InputHeaderIsBold = project.Settings.HeaderIsBold;
            }
            finally { _suspendLiveApply = false; }
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(PreviewMargin));
            OnPropertyChanged(nameof(HeaderPreviewFontSize));
            OnPropertyChanged(nameof(HeaderPreviewFontWeight));
            NotifyMpPreviewChanged();

            // Druckoptionen uebernehmen; Kalibrierung ist MASCHINENspezifisch:
            // die lokale calibration.json hat Vorrang vor den Projektwerten,
            // sonst verstellt ein fremdes Projekt den eigenen Druckversatz
            // (und der naechste Druck persistiert die fremden Werte).
            if (!CalibrationService.Exists)
            {
                CalibrationOffsetX = project.CalibrationOffsetX;
                CalibrationOffsetY = project.CalibrationOffsetY;
            }
            PrintGridLines = project.PrintGridLines;

            _currentFilePath = filePath;
            IsDirty = false;
            OnPropertyChanged(nameof(WindowTitle));
            RefreshRecentFiles();
            if (Labels.Count > 0) SelectedLabel = Labels[0];
        }
        finally { _suppressContentLossConfirm = false; }
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
        var ok = CalibrationService.Save(new CalibrationData
        {
            OffsetX = CalibrationOffsetX,
            OffsetY = CalibrationOffsetY
        });
        if (!ok)
            StatusMessage = "Warnung: Kalibrierung konnte nicht gespeichert werden";
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
            return DoSave(dialog.FileName);
        }

        return DoSave(_currentFilePath);
    }
}
