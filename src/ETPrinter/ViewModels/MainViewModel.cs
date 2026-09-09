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

    /// <summary>Adress-Generator (Tab "Adress-Generator").</summary>
    public AddressGeneratorViewModel Generator { get; }

    /// <summary>Panels "Seite" und "Schrift" (Live-Apply).</summary>
    public SettingsPanelViewModel Panel { get; }

    private bool _printGridLines;
    private double _calibrationOffsetX;
    private double _calibrationOffsetY;
    private string _statusMessage = "Bereit";

    // Seiten je Modus (alle Seiten + sichtbare Seite); nur eines ist aktiv
    private readonly PageDocument<LabelViewModel> _spDoc = new();
    private readonly PageDocument<MpModuleViewModel> _mpDoc = new();

    /// <summary>Projektdatei: Pfad, Dirty-Zustand, Zuletzt geoeffnet, Neu/Oeffnen/Speichern.</summary>
    public ProjectSession Session { get; }

    // ET200MP Module-based support
    private MpModuleViewModel? _selectedMpModule;
    private MpAddressCellViewModel? _selectedMpCell;

    private readonly IDialogService _dialogs;

    public MainViewModel() : this(null) { }

    /// <summary>Alle Dialoge laufen ueber <paramref name="dialogs"/> (Tests: SilentDialogService).</summary>
    public MainViewModel(IDialogService? dialogs)
    {
        _dialogs = dialogs ?? new WpfDialogService();
        _settings = new LabelSettings();
        _selectedFormat = FormatDefinitions.GetDefaultFormat(ProductFamily.ET200SP);

        Generator = new AddressGeneratorViewModel();
        Generator.ModuleTypeChanged += ApplyIoTypeToSelectedMpModule;
        Generator.StatusRequested += msg => StatusMessage = msg;

        Panel = new SettingsPanelViewModel(_settings);
        Panel.FontChanged += ApplyInputFontToSelected;
        Panel.HeaderChanged += OnPanelHeaderChanged;
        Panel.MarginsChanged += OnPanelMarginsChanged;
        Panel.StatusRequested += msg => StatusMessage = msg;

        Session = new ProjectSession(_dialogs)
        {
            Snapshot = BuildProject,
            Restore = ApplyLoadedProject,
            ResetToEmpty = ResetWorkspace,
            PageCountProvider = () => PageCount
        };
        Session.StatusRequested += msg => StatusMessage = msg;
        Session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ProjectSession.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
            if (e.PropertyName is nameof(ProjectSession.IsDirty) or nameof(ProjectSession.CurrentFilePath))
                OnPropertyChanged(nameof(WindowTitle));
        };

        AvailableProductFamilies = new ObservableCollection<ProductFamilyInfo>(ProductFamilyDefinitions.All);
        AvailableFormats = new ObservableCollection<FormatInfo>(FormatDefinitions.GetFormatsForFamily(ProductFamily.ET200SP));
        AvailableMpVariants = new ObservableCollection<MpModuleLayout>(
            Services.MpModuleLayoutFactory.VariantsForFamily(_selectedProductFamily));
        FontSizes = [4, 5, 6, 7, 8, 9, 10];
        AvailableFonts = new ObservableCollection<string> { "Arial" };
        LoadFontsInBackground();

        ApplyCommand = new RelayCommand(ApplyToLabel, () => SelectedLabel is not null || SelectedMpModule is not null);
        GenerateAndApplyCommand = new RelayCommand(GenerateAndApply, () => SelectedLabel is not null || SelectedMpModule is not null);
        GeneratePreviewCommand = new RelayCommand(Generator.UpdatePreview);
        ClearAllCommand = new RelayCommand(ClearAllLabels);
        ClearSelectedCommand = new RelayCommand(ClearSelected, () => HasSelection && !TextBoxHasFocus());
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        ApplyFontToAllCommand = new RelayCommand(ApplyFontToAll);
        FitZoomCommand = new RelayCommand(() => FitZoomRequested?.Invoke());
        PrintCommand = new RelayCommand(PrintLabels);
        PrintCurrentPageCommand = new RelayCommand(PrintCurrentPage);
        PrintCalibrationCommand = new RelayCommand(PrintCalibration);
        NewProjectCommand = new RelayCommand(Session.NewProject);
        SaveCommand = new RelayCommand(Session.Save);
        SaveAsCommand = new RelayCommand(Session.SaveAs);
        OpenCommand = new RelayCommand(Session.Open);
        OpenRecentCommand = new RelayCommand<string>(Session.OpenRecent);
        UpdateHeaderCommand = new RelayCommand(UpdateHeader, () => SelectedLabel is not null || SelectedMpModule is not null);

        // Page navigation commands
        NextPageCommand = new RelayCommand(NextPage, () => CurrentPageIndex < PageCount - 1);
        PrevPageCommand = new RelayCommand(PrevPage, () => CurrentPageIndex > 0);
        AddPageCommand = new RelayCommand(AddPage);
        RemovePageCommand = new RelayCommand(RemovePage, () => PageCount > 1);

        // Import commands — CSV/Excel liefern Etikettenzeilen (Header/Zeile1/Zeile2),
        // dafuer gibt es im modulbasierten MP-Modus kein Ziel. PDF-Import fuellt
        // dort Module ueber den Generator.
        ImportCsvCommand = new RelayCommand(ImportCsv, () => !IsModuleBased);
        ImportExcelCommand = new RelayCommand(ImportExcel, () => !IsModuleBased);
        ImportSchematicCommand = new RelayCommand(ImportSchematic);

        // Selective print commands
        SelectAllForPrintCommand = new RelayCommand(SelectAllForPrint);
        DeselectAllForPrintCommand = new RelayCommand(DeselectAllForPrint);
        SelectFilledForPrintCommand = new RelayCommand(SelectFilledForPrint);
        TogglePrintCommand = new RelayCommand(TogglePrint, () => HasSelection);

        // Copy / Paste
        CopyCommand = new RelayCommand(CopySelection, CanCopy);
        PasteCommand = new RelayCommand(PasteFromClipboard, CanPaste);

        LoadCalibration();
        InitializeLabels();
    }

    public ObservableCollection<ProductFamilyInfo> AvailableProductFamilies { get; }
    public ObservableCollection<FormatInfo> AvailableFormats { get; }
    public ObservableCollection<MpModuleViewModel> MpModules => _mpDoc.Visible;
    public ObservableCollection<MpModuleLayout> AvailableMpVariants { get; }
    public ObservableCollection<LabelViewModel> Labels => _spDoc.Visible;
    public ObservableCollection<RecentFileItem> RecentFiles => Session.RecentFiles;
    public ObservableCollection<string> AvailableFonts { get; }
    public int[] FontSizes { get; }

    // Unterdrueckt die Inhaltsverlust-Rueckfrage bei programmatischen Wechseln
    // (Projekt laden, Neues Projekt, Test-Automation).
    private bool _suppressContentLossConfirm;
    internal bool SuppressContentLossConfirm
    {
        get => _suppressContentLossConfirm;
        set => _suppressContentLossConfirm = value;
    }

    private bool HasAnyContent() =>
        _spDoc.AllItems.Any(l => l.HasText)
        || _mpDoc.AllItems.Any(m => m.HasPrintableContent);

    private int ModulesPerPage => ProductFamilyDefinitions.Get(_selectedFormat.Family).ModulesPerPage;

    /// <summary>Fuer die Notfall-Sicherung im globalen Exception-Handler.</summary>
    internal bool HasAnyContentForRecovery => HasAnyContent();

    /// <summary>Projekt als geaendert markieren (z.B. nach Wiederherstellung).</summary>
    internal void MarkDirty(string status)
    {
        IsDirty = true;
        StatusMessage = status;
    }

    /// <summary>
    /// Gemeinsamer Familienwechsel fuer Setter, NewProject und ApplyLoadedProject:
    /// Formate, Modul-Varianten und Katalog-Artikel der Familie nachziehen.
    /// Frueher aktualisierte ApplyLoadedProject nur die Formate — nach dem Laden
    /// eines 25mm-Projekts zeigten Varianten-/Artikel-ComboBox die 35mm-Familie.
    /// </summary>
    private void ApplyFamilyCore(ProductFamily family)
    {
        _selectedProductFamily = family;

        AvailableFormats.Clear();
        foreach (var fmt in FormatDefinitions.GetFormatsForFamily(family))
            AvailableFormats.Add(fmt);

        AvailableMpVariants.Clear();
        foreach (var v in MpModuleLayoutFactory.VariantsForFamily(family))
            AvailableMpVariants.Add(v);

        OnPropertyChanged(nameof(AvailableMpArticles));
        OnPropertyChanged(nameof(SelectedProductFamilyInfo));
        OnPropertyChanged(nameof(SelectedProductFamily));
    }

    /// <summary>Warnt vor Format-/Familienwechsel, wenn befuellte Etiketten/Module
    /// verworfen wuerden. True = fortfahren.</summary>
    private bool ConfirmContentLoss()
    {
        if (_suppressContentLossConfirm || !HasAnyContent()) return true;
        return _dialogs.Confirm(
            "Beim Wechsel von Druckformat oder Produktfamilie werden alle\n" +
            "befuellten Etiketten/Module verworfen.\n\nFortfahren?",
            "Format wechseln");
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
            ApplyFamilyCore(value.Family);

            // Nur die Raender auf Family-Defaults setzen — Schrift-Einstellungen
            // bleiben (frueher setzte ResetForFamily auch Header-Font zurueck, ohne
            // die Input-Felder zu benachrichtigen: ComboBox zeigte 6, Druck nutzte 9).
            // Guard verhindert, dass der erste Input-Setter via Live-Apply die noch
            // alten Input-Werte der vorherigen Familie zurueckschreibt.
            _settings.ResetMarginsForFamily(_selectedProductFamily);
            Panel.LoadMargins();
            OnPropertyChanged(nameof(Settings));
            NotifyPreviewGeometry();
            NotifyMpPreviewChanged();

            // Erstes Format der neuen Familie waehlen — Inhaltsverlust wurde
            // hier schon bestaetigt, daher kein zweiter Format-Prompt.
            bool hadContent = HasAnyContent();
            _suppressContentLossConfirm = true;
            try { SelectedFormat = FormatDefinitions.GetDefaultFormat(_selectedProductFamily); }
            finally { _suppressContentLossConfirm = false; }
            OnPropertyChanged(nameof(BandsPerPage));
            OnPropertyChanged(nameof(IsMultiBand));
            OnPropertyChanged(nameof(WindowTitle));
            // Verworfener Inhalt oder geaenderte Raender = ungespeicherte Aenderung
            if (hadContent || Session.CurrentFilePath is not null) IsDirty = true;
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
            // Erneute Auswahl desselben Moduls darf die Markierung nicht loeschen
            if (ReferenceEquals(_selectedMpModule, value))
            {
                if (value is not null) value.IsSelected = true;
                return;
            }
            if (_selectedMpModule is not null)
                _selectedMpModule.IsSelected = false;

            // Die ausgewaehlte Zelle gehoert zum ALTEN Modul — ohne Reset schrieben
            // "Ausgewaehlte Adresszelle" und "Uebertragen" nach Auto-Advance oder
            // Header-Klick unsichtbar ins vorherige Modul.
            SelectedMpCell = null;

            _selectedMpModule = value;
            OnPropertyChanged();
            if (value is not null)
            {
                value.IsSelected = true;
                LoadFontInputsFromMpModule(value);
            }
            OnPropertyChanged(nameof(SelectedMpModuleInfo));
            OnPropertyChanged(nameof(EditTargetInfo));
            OnPropertyChanged(nameof(StatusSelectionInfo));
            OnPropertyChanged(nameof(SelectedMpVariant));
            OnPropertyChanged(nameof(SelectedMpArticle));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    /// <summary>Schrift-Eingabefelder aus dem Modul laden (wie bei SP-Etiketten),
    /// ohne dass Live-Apply das Modul sofort mit seinen eigenen Werten ueberschreibt.</summary>
    private void LoadFontInputsFromMpModule(MpModuleViewModel module) =>
        Panel.LoadFont(module.FontSize, module.IsBold, module.IsItalic, module.FontFamily);

    /// <summary>Nach Zell-Neuaufbau (Variante/Artikel/Modultyp/Paste) die ausgewaehlte
    /// Zelle per CellIndex neu aufloesen — die alte VM ist abgehaengt, Eingaben
    /// darin verschwanden ohne Markierung.</summary>
    private void OnMpCellsRebuilt(MpModuleViewModel module)
    {
        if (!ReferenceEquals(module, _selectedMpModule)) return;
        var old = _selectedMpCell;
        if (old is null) return;
        var replacement = module.AddressCells
            .FirstOrDefault(c => c.CellIndex == old.CellIndex && c.IsEditable);
        SelectedMpCell = replacement;
    }

    private MpModuleViewModel CreateMpModuleViewModel(MpModule module)
    {
        var vm = new MpModuleViewModel(module) { ContentChanged = OnMpContentChanged };
        vm.CellsRebuilt = () => OnMpCellsRebuilt(vm);
        return vm;
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

            bool isCustom = string.IsNullOrEmpty(value.ArticleNo);

            // Modultyp vor dem Artikel setzen — bei "Benutzerdefiniert" bestimmt er
            // die generischen Struktur-Labels, die der Zellen-Neuaufbau liest.
            _selectedMpModule.IoType = isCustom ? Generator.ModuleType.Type : value.IoType;
            _selectedMpModule.ArticleNumber = isCustom ? null : value.ArticleNo;

            // Generator-Modultyp am Katalogeintrag vorbelegen (DI/DO/AI/AO) und die
            // Anzahl auf die volle Kanalzahl des Moduls setzen (digital: Bytes = editierbare
            // Zellen / 8, gemischt: je Spalte), damit "Generieren" das Modul komplett fuellt.
            if (!isCustom)
            {
                Generator.SelectType(value.IoType);
                if (Generator.ModuleType.IsBitAddressed)
                {
                    int editable = _selectedMpModule.AddressCells.Count(c => c.IsEditable);
                    Generator.Count = Math.Max(1, (value.MixedOutputRightColumn ? editable / 2 : editable) / 8);
                }
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMpVariant));
            OnPropertyChanged(nameof(SelectedMpModuleInfo));
        }
    }

    /// <summary>Uebertraegt den Generator-Modultyp auf das ausgewaehlte MP-Modul.
    /// Nur fuer benutzerdefinierte Module — bei Katalog-Artikeln kommt die
    /// Klemmenbelegung aus dem Datenblatt und darf nicht ueberschrieben werden.</summary>
    private void ApplyIoTypeToSelectedMpModule()
    {
        if (_selectedMpModule is null) return;
        if (!string.IsNullOrEmpty(_selectedMpModule.ArticleNumber)) return;
        _selectedMpModule.IoType = Generator.ModuleType.Type;
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
            bool hadContent = HasAnyContent();
            if (SetProperty(ref _selectedFormat, value))
            {
                InitializeLabels();
                // Modul-Editor bei MP-Formaten aktivieren, Generator bei SP
                InputTabIndex = value.IsModuleBased ? 2 : 0;
                // Verworfener Inhalt = ungespeicherte Aenderung (Laden/Neu setzen danach
                // selbst IsDirty=false)
                if (hadContent || Session.CurrentFilePath is not null) IsDirty = true;
                OnPropertyChanged(nameof(IsMarginBottomEditable));
                OnPropertyChanged(nameof(EditTargetInfo));
                OnPropertyChanged(nameof(LayoutInfo));
                NotifyPreviewGeometry();
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
            // Klick auf das bereits markierte Etikett darf den Rahmen nicht entfernen
            if (ReferenceEquals(_selectedLabel, value))
            {
                if (value is not null) value.IsSelected = true;
                return;
            }
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
                    Generator.ModuleName = value.Header; // Kopfzeile auch im Generator laden
                    // Schrift-Einstellungen des Etiketts laden — Live-Apply aussetzen,
                    // sonst wuerde das Label sofort auf seine eigenen Werte "ueberschrieben".
                    Panel.LoadFont(value.CellFontSize, value.CellIsBold, value.CellIsItalic, value.CellFontFamily);
                    StatusMessage = $"Etikett {value.DisplayPosition}/{Labels.Count} (Seite {CurrentPageIndex + 1}/{PageCount})";
                }
                OnPropertyChanged(nameof(SelectedLabelInfo));
                OnPropertyChanged(nameof(EditTargetInfo));
                OnPropertyChanged(nameof(StatusSelectionInfo));
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string SelectedLabelInfo => SelectedLabel is not null
        ? $"Etikett {SelectedLabel.DisplayPosition} von {Labels.Count} (Seite {CurrentPageIndex + 1})"
        : "Kein Etikett ausgewaehlt";

    /// <summary>"Bearbeite:"-Zeile, familienbewusst (MP: Modul statt Etikett).</summary>
    public string EditTargetInfo => _selectedFormat.IsModuleBased ? SelectedMpModuleInfo : SelectedLabelInfo;

    /// <summary>Raster-Zeile, familienbewusst.</summary>
    public string LayoutInfo
    {
        get
        {
            if (!_selectedFormat.IsModuleBased)
                return $"Raster: {LabelsPerRow} x {LabelRows} = {_selectedFormat.LabelsPerPage} Etiketten";
            var fam = ProductFamilyDefinitions.Get(_selectedFormat.Family);
            return $"Bogen: {fam.BandsPerPageText} = {fam.ModulesPerPage} Streifen";
        }
    }

    /// <summary>Statusleiste: "Etikett 3 / 100" bzw. "Modul 2 / 10".</summary>
    public string StatusSelectionInfo => _selectedFormat.IsModuleBased
        ? $"Modul: {(SelectedMpModule is null ? "-" : (SelectedMpModule.ModuleIndex + 1).ToString())} / {MpModules.Count}"
        : $"Etikett: {(SelectedLabel is null ? "-" : SelectedLabel.DisplayPosition.ToString())} / {_selectedFormat.LabelsPerPage}";

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

    /// <summary>Ungespeicherte Aenderungen (Zustand liegt in <see cref="Session"/>).</summary>
    public bool IsDirty
    {
        get => Session.IsDirty;
        private set => Session.IsDirty = value;
    }

    public string WindowTitle
    {
        get
        {
            var dirty = Session.IsDirty ? " *" : "";
            return Session.FileName is { } name
                ? $"ET-Printer - {name}{dirty}"
                : $"ET-Printer - {_selectedFormat.DisplayName}{dirty}";
        }
    }

    // === Multi-page properties ===
    private IPageDocument ActiveDocument => _selectedFormat.IsModuleBased ? _mpDoc : _spDoc;

    public int CurrentPageIndex => ActiveDocument.CurrentIndex;

    public int PageCount => ActiveDocument.PageCount;

    public string PageIndicator => $"Seite {CurrentPageIndex + 1} / {PageCount}";

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

    public bool PrintGridLines
    {
        get => _printGridLines;
        set
        {
            if (SetProperty(ref _printGridLines, value))
                IsDirty = true; // wird mitgespeichert -> Aenderung
        }
    }

    /// <summary>Der MP-Druck rastert von oben (Header + 2 x 20 Zeilen mit festen Hoehen);
    /// "Rand unten" hat dort keine Wirkung und wird in der UI gesperrt.</summary>
    public bool IsMarginBottomEditable => !IsModuleBased;

    // Kalibrierung +/-10 mm; ausserhalb liegende Eingaben werden begrenzt und gemeldet.
    public const double CalibrationMaxMm = 10;

    private double ClampCalibration(double v)
    {
        double c = Math.Clamp(double.IsNaN(v) ? 0 : v, -CalibrationMaxMm, CalibrationMaxMm);
        if (c != v) StatusMessage = $"Kalibrierung: Wert auf +/-{CalibrationMaxMm:0} mm begrenzt";
        return c;
    }

    public double CalibrationOffsetX
    {
        get => _calibrationOffsetX;
        set => SetProperty(ref _calibrationOffsetX, ClampCalibration(value));
    }

    public double CalibrationOffsetY
    {
        get => _calibrationOffsetY;
        set => SetProperty(ref _calibrationOffsetY, ClampCalibration(value));
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

    // Vorschau-Massstab: 3 px je mm (A4 = 630 x 891 px)
    public const double PreviewPxPerMm = 3.0;

    // Seitenraender fuer A4-Preview — das Etikettenraster fuellt exakt den Druckbereich
    public Thickness PreviewMargin => new(
        _settings.MarginLeft * PreviewPxPerMm, _settings.MarginTop * PreviewPxPerMm,
        _settings.MarginRight * PreviewPxPerMm, _settings.MarginBottom * PreviewPxPerMm);

    /// <summary>Breite der Kopfspalte in der SP-Vorschau aus derselben Geometrie wie
    /// der Druck (20 % der Gruppenbreite) — frueher fest 18 px (~2 % Abweichung).</summary>
    public GridLength SpHeaderPreviewWidth =>
        new(SheetGeometry.For(_selectedFormat, _settings).SpHeaderWidth * PreviewPxPerMm);

    /// <summary>Zeilennummern 20..1 im RECHTEN Seitenrand (neben dem Raster), damit sie
    /// die Rasterbreite nicht mehr verkleinern.</summary>
    public Thickness RowNumbersMargin => new(
        (FormatDefinitions.PageWidth - _settings.MarginRight) * PreviewPxPerMm + 2,
        _settings.MarginTop * PreviewPxPerMm, 0, _settings.MarginBottom * PreviewPxPerMm);

    private void NotifyPreviewGeometry()
    {
        OnPropertyChanged(nameof(PreviewMargin));
        OnPropertyChanged(nameof(SpHeaderPreviewWidth));
        OnPropertyChanged(nameof(RowNumbersMargin));
    }

    // === Commands ===
    public ICommand ApplyCommand { get; }
    public ICommand GenerateAndApplyCommand { get; }
    public ICommand GeneratePreviewCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ClearSelectedCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand ApplyFontToAllCommand { get; }
    public ICommand FitZoomCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand PrintCurrentPageCommand { get; }
    public ICommand PrintCalibrationCommand { get; }

    /// <summary>Die View setzt den Zoom so, dass die ganze Seite sichtbar ist (braucht
    /// die Viewport-Groesse, die das ViewModel nicht kennt).</summary>
    public event Action? FitZoomRequested;

    /// <summary>
    /// Systemschriften im Hintergrund laden: Fonts.SystemFontFamilies dauert je nach
    /// Rechner mehrere hundert Millisekunden und verzoegerte frueher den Start.
    /// </summary>
    private void LoadFontsInBackground()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // Tests/Headless: synchron
            foreach (var f in SystemFontNames().Where(f => f != "Arial")) AvailableFonts.Add(f);
            return;
        }
        Task.Run(() =>
        {
            var names = SystemFontNames();
            dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var f in names)
                    if (!AvailableFonts.Contains(f)) AvailableFonts.Add(f);
                // Sortiert nachziehen (Arial war Platzhalter an Position 0)
                var sorted = AvailableFonts.OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    int cur = AvailableFonts.IndexOf(sorted[i]);
                    if (cur != i) AvailableFonts.Move(cur, i);
                }
            }));
        });
    }

    private static List<string> SystemFontNames() =>
        Fonts.SystemFontFamilies.Select(f => f.Source)
            .OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToList();

    // Eingabe-Tabs: 0 = Adress-Generator, 1 = Manuell (nur SP), 2 = MP Modul.
    // Beim Wechsel in ein modulbasiertes Format wird der Modul-Editor aktiviert.
    private int _inputTabIndex;
    public int InputTabIndex
    {
        get => _inputTabIndex;
        set => SetProperty(ref _inputTabIndex, value);
    }

    private void ClearSelected()
    {
        if (_selectedFormat.IsModuleBased)
        {
            if (SelectedMpModule is null) return;
            var empty = new MpModule { Variant = SelectedMpModule.Variant, IoType = SelectedMpModule.IoType, ArticleNumber = SelectedMpModule.ArticleNumber };
            empty.AddressCells = MpModuleLayoutFactory.CreateCells(empty.Variant);
            SelectedMpModule.SetModule(empty);
            NotifyMpPreviewChanged();
            IsDirty = true;
            StatusMessage = $"Modul {SelectedMpModule.ModuleIndex + 1} geleert";
            return;
        }
        if (SelectedLabel is null) return;
        SelectedLabel.Clear();
        InputHeader = string.Empty;
        InputLine1 = string.Empty;
        InputLine2 = string.Empty;
        IsDirty = true;
        StatusMessage = $"Etikett {SelectedLabel.DisplayPosition} geleert";
    }

    /// <summary>Schrift des Eingabepanels auf ALLE Etiketten/Module aller Seiten.</summary>
    private void ApplyFontToAll()
    {
        int count = 0;
        foreach (var l in _spDoc.AllItems) { ApplyFontToLabel(l); count++; }
        foreach (var m in _mpDoc.AllItems)
        {
            m.FontSize = Panel.FontSize; m.IsBold = Panel.IsBold; m.IsItalic = Panel.IsItalic; m.FontFamily = Panel.FontFamily;
            count++;
        }
        Panel.StoreFontInSettings();
        IsDirty = true;
        NotifyMpPreviewChanged();
        StatusMessage = $"Schrift auf {count} {(IsModuleBased ? "Module" : "Etiketten")} angewendet";
    }

    /// <summary>Oeffnet eine Projektdatei (Drag&Drop, Kommandozeile) mit Rueckfrage bei
    /// ungespeicherten Aenderungen.</summary>
    public void OpenFile(string path) => Session.OpenFile(path);
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
        SelectedLabel = null;
        SelectedMpModule = null;
        SelectedMpCell = null;
        _spDoc.Clear();
        _mpDoc.Clear();

        if (_selectedFormat.IsModuleBased)
            _mpDoc.Reset(CreateEmptyMpPage(ModulesPerPage));
        else
            _spDoc.Reset(CreateEmptyPage(_selectedFormat.LabelsPerPage));

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
            page.Add(CreateMpModuleViewModel(module));
        }
        return page;
    }

    // Direktbindungen des MP-Tabs (Header, Netzadressen, Zelltexte, Variante)
    // mutieren das Model ohne Command — Dirty-Markierung und Preview-Refresh
    // haengen deshalb am ContentChanged-Callback der Modul-VMs.
    private void OnMpContentChanged()
    {
        if (Panel.SuspendLiveApply) return;
        IsDirty = true;
        NotifyMpPreviewChanged();
    }

    private List<LabelViewModel> CreateEmptyPage(int count)
    {
        var page = new List<LabelViewModel>(count);
        for (int i = 0; i < count; i++)
            page.Add(CreateLabelViewModel(new LabelCell { Index = i }));
        return page;
    }

    private LabelViewModel CreateLabelViewModel(LabelCell cell) =>
        new(cell) { PrintFlagChanged = () => IsDirty = true };

    private void NavigateToPage(int index)
    {
        // Multi-Selection ist seitenlokal: beim Seitenwechsel ALLE Markierungen
        // (auch auf unsichtbaren Seiten) aufheben — sonst kopiert Strg+C spaeter
        // unsichtbar markierte Etiketten einer anderen Seite.
        ClearChecksOnAllPages();

        if (index < 0 || index >= PageCount) return;
        if (_selectedFormat.IsModuleBased)
        {
            SelectedMpModule = null;
            SelectedMpCell = null;
            _mpDoc.Show(index);
            NotifyPageProperties();
            if (MpModules.Count > 0)
                SelectedMpModule = MpModules[0];
        }
        else
        {
            SelectedLabel = null;
            _spDoc.Show(index);
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
        if (CurrentPageIndex < PageCount - 1)
            NavigateToPage(CurrentPageIndex + 1);
    }

    private void PrevPage()
    {
        if (CurrentPageIndex > 0)
            NavigateToPage(CurrentPageIndex - 1);
    }

    private void AddPage()
    {
        if (_selectedFormat.IsModuleBased)
            _mpDoc.AddPage(CreateEmptyMpPage(ModulesPerPage));
        else
            _spDoc.AddPage(CreateEmptyPage(_selectedFormat.LabelsPerPage));
        NavigateToPage(PageCount - 1);
        IsDirty = true;
        StatusMessage = $"Seite {PageCount} hinzugefuegt";
    }

    private void RemovePage()
    {
        bool pageHasContent = _selectedFormat.IsModuleBased
            ? MpModules.Any(m => m.HasPrintableContent)
            : Labels.Any(l => l.HasText);
        if (pageHasContent && !ConfirmDestructive(
                $"Seite {CurrentPageIndex + 1} enthaelt befuellte Etiketten/Module.\n\nSeite wirklich entfernen?",
                "Seite entfernen"))
            return;

        int removedIndex = CurrentPageIndex;
        bool removed = _selectedFormat.IsModuleBased
            ? _mpDoc.RemovePage(removedIndex)
            : _spDoc.RemovePage(removedIndex);
        if (!removed) return;
        NavigateToPage(CurrentPageIndex); // Auswahl/Markierungen der neuen Seite setzen
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
        var result = Generator.Generate();

        if (_selectedFormat.IsModuleBased && SelectedMpModule is not null)
        {
            // ET200MP: Adressen sequenziell pro Byte erzeugen und klemmengerecht verteilen
            SelectedMpModule.HeaderText = result.Header;

            // Modultyp uebernehmen, BEVOR die Zellen gelesen werden: bei
            // benutzerdefinierten Modulen aendert er die Struktur-Klemmen und
            // damit die Menge der editierbaren Zellen.
            ApplyIoTypeToSelectedMpModule();

            int consumed = FillMpModuleAddresses(SelectedMpModule, Generator.ModuleType.Type, Generator.StartByte, Generator.Count);

            // Auto-Advance: um die tatsaechlich belegten Bytes/Kanaele weiterschalten
            Generator.AdvanceAfterModule(consumed);

            IsDirty = true;
            int filledCount = SelectedMpModule.AddressCells.Count(c => c.IsEditable && c.HasText);
            string status = $"Generiert: {Generator.ModuleName} ({Generator.ModuleType.DisplayName}) → {filledCount} Adressen auf Modul {SelectedMpModule.ModuleIndex + 1}";

            NotifyMpPreviewChanged();

            // Zum naechsten Modul springen (analog AdvanceToNextLabel bei ET200SP).
            // Adressen werden NICHT automatisch neu generiert — User muss Variante
            // und Start-Byte pruefen und erneut "Generieren + Uebertragen" druecken.
            AdvanceToNextMpModule();
            StatusMessage = status; // nach dem Advance, sonst ueberschreibt der Setter die Meldung
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
            string status = $"Generiert: {Generator.ModuleName} ({Generator.ModuleType.DisplayName}) ab Byte {Generator.StartByte}";

            // Nur um die tatsaechlich aufs Etikett gepasste Anzahl weiterschalten
            // (Generator kappt Digital bei 2 Bytes) — sonst gehen Adressen verloren.
            Generator.AdvanceAfterLabel();

            AdvanceToNextLabel();
            StatusMessage = status; // nach dem Advance, sonst ueberschreibt der Setter die Meldung
        }
    }

    /// <summary>
    /// Verteilt Adressen klemmengerecht auf die editierbaren Zellen eines MP-Moduls.
    /// Digital: Byte-Anzahl aus dem Kanalraster der Variante (35mm) bzw. aus genCount
    /// (25mm, generische 20/40 Slots). Analog: genCount Kanaele spaltenausgeglichen.
    /// Liefert die belegte Anzahl (Bytes bei digital, Kanaele bei analog) fuer den
    /// Auto-Advance. Gemeinsame Quelle fuer Generator und PDF-Import.
    /// </summary>
    internal static int FillMpModuleAddresses(MpModuleViewModel module, ModuleType type, int startByte, int genCount)
    {
        var info = AddressGenerator.ModuleTypes.First(m => m.Type == type);
        var editableCells = module.AddressCells.Where(c => c.IsEditable).ToList();
        int editableCount = editableCells.Count;

        if (info.IsBitAddressed)
        {
            // Byte-Anzahl: das Kanalraster der Variante/Belegung begrenzt (editierbare
            // Zellen / 8), genCount waehlt darunter (z.B. 8-Kanal-Modul in einem
            // 16er-Streifen). Katalogwahl setzt genCount auf die volle Bytezahl.
            int maxBytes = Math.Max(1, editableCount / 8);
            int byteCount = Math.Clamp(genCount, 1, maxBytes);

            // Gemischtes DI/DQ-Modul (DI16/DQ16 BA): rechte Spalte = Ausgaenge mit
            // A-Praefix, Bytezaehlung beginnt rechts wieder bei startByte.
            var entry = MpModuleCatalog.Find(module.ArticleNumber);
            bool mixed = entry is { MixedOutputRightColumn: true } && entry.Variant == module.Variant;
            string outPrefix = AddressGenerator.ModuleTypes.First(m => m.Type == ModuleType.DO).Prefix;

            var left = mixed ? editableCells.Where(c => c.StartCol == 0).ToList() : editableCells;
            var right = mixed ? editableCells.Where(c => c.StartCol == 1).ToList() : [];

            static void Fill(List<MpAddressCellViewModel> cells, string prefix, int start, int bytes)
            {
                var addresses = new List<string>();
                for (int b = 0; b < bytes; b++)
                    for (int bit = 0; bit < 8; bit++)
                        addresses.Add($"{prefix} {start + b}.{bit}");
                for (int i = 0; i < cells.Count; i++)
                    cells[i].Text = i < addresses.Count ? addresses[i] : string.Empty;
            }

            if (mixed)
            {
                int perSide = Math.Clamp(genCount, 1, Math.Max(1, left.Count / 8));
                Fill(left, info.Prefix, startByte, perSide);
                Fill(right, outPrefix, startByte, perSide);
                return perSide;
            }

            Fill(left, info.Prefix, startByte, byteCount);
            return byteCount;
        }
        else
        {
            // Analog: genCount Kanaele, SPALTENAUSGEGLICHEN verteilen. Das Excel-
            // Layout hat 5 generische Bloecke pro Spalte (inkl. MANA/Reserve);
            // ein AI 8 hat physisch CH0-3 links + CH4-7 rechts, also 4+4 statt 5+3.
            // Restbloecke bleiben leer (fuer manuelle MANA-Beschriftung).
            int channelCount = Math.Min(Math.Max(genCount, 0), editableCount);
            var byColumn = editableCells
                .GroupBy(c => c.StartCol)
                .OrderBy(g => g.Key)
                .Select(g => g.ToList())
                .ToList();
            int numCols = byColumn.Count;

            foreach (var c in editableCells) c.Text = string.Empty;
            int ch = 0;
            for (int colIdx = 0; colIdx < numCols && ch < channelCount; colIdx++)
            {
                int remainingCols = numCols - colIdx;
                int perCol = (int)Math.Ceiling((channelCount - ch) / (double)remainingCols);
                var colCells = byColumn[colIdx];
                for (int k = 0; k < perCol && k < colCells.Count; k++)
                    colCells[k].Text = $"{info.Prefix} {startByte + ch++ * 2}";
            }

            return channelCount;
        }
    }

    /// <summary>Verschraenkt Zeile 1 (ungerade Bits, oben) und Zeile 2 (gerade Bits,
    /// unten) slotweise kanal-aufsteigend zu einer Zeile (fuer einzeilige Formate).</summary>
    internal static string MergeAddressLines(string line1, string line2)
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

    private void AdvanceToNextLabel()
    {
        if (SelectedLabel is null) return;

        int nextIndex = SelectedLabel.Index + 1;
        if (nextIndex < Labels.Count)
        {
            SelectedLabel = Labels[nextIndex];
        }
        else if (CurrentPageIndex < PageCount - 1)
        {
            // At last label of current page, advance to first label of next page
            NavigateToPage(CurrentPageIndex + 1);
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
        else if (CurrentPageIndex < PageCount - 1)
        {
            NavigateToPage(CurrentPageIndex + 1);
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
        foreach (var l in _spDoc.AllItems) l.IsChecked = false;
        foreach (var m in _mpDoc.AllItems) m.IsChecked = false;
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

    /// <summary>Strg+C/V sind Window-KeyBindings und wuerden der TextBox das
    /// Kopieren/Einfuegen von Text wegnehmen. Hat ein Textfeld den Fokus, sind die
    /// Etiketten-Commands nicht ausfuehrbar — die Taste geht dann an die TextBox.</summary>
    private static bool TextBoxHasFocus() =>
        Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;

    private bool CanCopy()
    {
        if (TextBoxHasFocus()) return false;
        if (_selectedFormat.IsModuleBased)
            return GetCopySourceModules().Any();
        return GetCopySourceLabels().Any();
    }

    private bool CanPaste()
    {
        if (TextBoxHasFocus()) return false;
        if (_selectedFormat.IsModuleBased)
            return ClipboardService.HasModules && SelectedMpModule is not null;
        return ClipboardService.HasLabels && SelectedLabel is not null;
    }

    /// <summary>Strg+Klick: Markierung umschalten. Beim ersten Strg+Klick wird das
    /// bisher ausgewaehlte Etikett (Anker) mit markiert — sonst kopierte "3 markiert,
    /// Strg+Klick auf 5" nur Etikett 5.</summary>
    public void ToggleCheck(LabelViewModel label)
    {
        bool anyChecked = Labels.Any(l => l.IsChecked);
        if (!anyChecked && SelectedLabel is not null && !ReferenceEquals(SelectedLabel, label))
            SelectedLabel.IsChecked = true;
        label.IsChecked = !label.IsChecked;
        SelectedLabel = label;
    }

    public void ToggleCheck(MpModuleViewModel module)
    {
        bool anyChecked = MpModules.Any(m => m.IsChecked);
        if (!anyChecked && SelectedMpModule is not null && !ReferenceEquals(SelectedMpModule, module))
            SelectedMpModule.IsChecked = true;
        module.IsChecked = !module.IsChecked;
        SelectedMpModule = module;
        NotifyMpPreviewChanged();
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
        StatusMessage = pasted < source.Count
            ? $"{pasted} von {source.Count} Etikett(en) eingefuegt ab Position {startIndex + 1} (Seitenende erreicht)"
            : $"{pasted} Etikett(en) eingefuegt ab Position {startIndex + 1}";
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
        StatusMessage = pasted < source.Count
            ? $"{pasted} von {source.Count} Modul(en) eingefuegt ab Position {startIndex + 1} (Seitenende erreicht)"
            : $"{pasted} Modul(e) eingefuegt ab Position {startIndex + 1}";
    }

    /// <summary>Rueckfrage vor destruktiven Aktionen (Alle loeschen, Seite entfernen).
    /// Automation unterdrueckt sie ueber SuppressContentLossConfirm.</summary>
    private bool ConfirmDestructive(string message, string title)
    {
        if (_suppressContentLossConfirm) return true;
        return _dialogs.Confirm(message, title);
    }

    private void ClearAllLabels()
    {
        if (HasAnyContent() && !ConfirmDestructive(
                "Alle Etiketten bzw. Module auf ALLEN Seiten werden geloescht.\n\nFortfahren?",
                "Alle loeschen"))
            return;

        // Alle Seiten auf eine leere Seite zuruecksetzen (SP wie MP)
        InitializeLabels();
        if (!_selectedFormat.IsModuleBased)
        {
            InputHeader = string.Empty;
            InputLine1 = string.Empty;
            InputLine2 = string.Empty;
        }
        NotifyMpPreviewChanged();
        IsDirty = true;
        StatusMessage = _selectedFormat.IsModuleBased ? "Alle Module geloescht" : "Alle Etiketten geloescht";
    }

    // === Live-Preview Apply-Helfer (aus Input-Settern aufgerufen) ===

    private void ApplyInputFontToSelected()
    {
        // ET200MP: Schrift wirkt auf das ausgewaehlte Modul (Setter melden
        // ContentChanged -> Dirty + Preview-Refresh). Frueher waren die vier
        // Schrift-Bedienelemente im MP-Modus komplett wirkungslos.
        if (_selectedFormat.IsModuleBased)
        {
            if (SelectedMpModule is null) return;
            SelectedMpModule.FontSize = Panel.FontSize;
            SelectedMpModule.IsBold = Panel.IsBold;
            SelectedMpModule.IsItalic = Panel.IsItalic;
            SelectedMpModule.FontFamily = Panel.FontFamily;
            return;
        }

        if (SelectedLabel is null) return;
        ApplyFontToLabel(SelectedLabel);
        IsDirty = true;
    }

    private void OnPanelHeaderChanged()
    {
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(HeaderPreviewFontSize));
        OnPropertyChanged(nameof(HeaderPreviewFontWeight));
        NotifyMpPreviewChanged();
        IsDirty = true;
    }

    private void OnPanelMarginsChanged()
    {
        OnPropertyChanged(nameof(Settings));
        NotifyPreviewGeometry();
        NotifyMpPreviewChanged();
        IsDirty = true;
    }

    private void ResetSettings()
    {
        _settings.ResetForFamily(_selectedProductFamily);
        Panel.LoadFromSettings();
        OnPropertyChanged(nameof(Settings));
        NotifyPreviewGeometry();
        OnPropertyChanged(nameof(HeaderPreviewFontSize));
        OnPropertyChanged(nameof(HeaderPreviewFontWeight));
        NotifyMpPreviewChanged();
        StatusMessage = "Einstellungen zurueckgesetzt";
    }

    private void UpdateHeader()
    {
        if (_selectedFormat.IsModuleBased && SelectedMpModule is not null)
        {
            SelectedMpModule.HeaderText = Generator.ModuleName;
            IsDirty = true;
            NotifyMpPreviewChanged();
            StatusMessage = $"Kopfzeile von Modul {SelectedMpModule.ModuleIndex + 1} geaendert";
            return;
        }
        if (SelectedLabel is null) return;
        SelectedLabel.Header = Generator.ModuleName;
        InputHeader = Generator.ModuleName;
        IsDirty = true;
        StatusMessage = $"Kopfzeile von Etikett {SelectedLabel.DisplayPosition} geaendert";
    }

    private void ApplyFontToLabel(LabelViewModel label)
    {
        label.CellFontSize = Panel.FontSize;
        label.CellIsBold = Panel.IsBold;
        label.CellIsItalic = Panel.IsItalic;
        label.CellFontFamily = Panel.FontFamily;
    }

    /// <summary>Arbeitsbereich auf ein leeres SP-Projekt zuruecksetzen (fuer
    /// <see cref="ProjectSession.NewProject"/>; Rueckfrage, Pfad und Dirty-Reset macht
    /// die Session, daher hier kein zweiter Inhaltsverlust-Prompt).</summary>
    private void ResetWorkspace()
    {
        _suppressContentLossConfirm = true;
        try
        {
            ApplyFamilyCore(ProductFamily.ET200SP);
            _settings.Reset();
            ResetSettings();
            Generator.Reset();
            PrintGridLines = false;

            // Etiketten IMMER neu anlegen: der Format-Setter kehrt bei unveraendertem
            // Format sofort zurueck — dann blieben alle Etiketten stehen und galten
            // wegen IsDirty=false als gespeichert ("Neu" war ein No-op mit Datenverlust-
            // Risiko beim naechsten "Speichern unter").
            var defaultFormat = FormatDefinitions.GetDefaultFormat(ProductFamily.ET200SP);
            if (_selectedFormat.Format != defaultFormat.Format)
                SelectedFormat = defaultFormat;
            else
                InitializeLabels();

            OnPropertyChanged(nameof(BandsPerPage));
            OnPropertyChanged(nameof(IsMultiBand));
        }
        finally { _suppressContentLossConfirm = false; }
    }

    /// <summary>Serialisiert den KOMPLETTEN Projektzustand (alle Seiten aus
    /// _spDoc/_mpDoc). Gemeinsame Quelle fuer ProjectSession.DoSave und Test-Automation —
    /// der Automation-Pfad speicherte frueher nur die sichtbare Seite.</summary>
    internal LabelProject BuildProject()
    {
        // Seiten serialisieren (LabelViewModels -> LabelCells)
        var pages = _spDoc.Pages.Select(pageVms => new LabelPage
        {
            Labels = pageVms.Select(vm => vm.GetCell()).ToList()
        }).ToList();

        // MP-Module serialisieren
        List<MpModulePage>? mpPages = null;
        if (_selectedFormat.IsModuleBased)
        {
            mpPages = _mpDoc.Pages.Select(pageMods => new MpModulePage
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
            // ProductFamily setzen (filtert Formate, Varianten, Katalog)
            ApplyFamilyCore(project.ProductFamily);
            OnPropertyChanged(nameof(BandsPerPage));
            OnPropertyChanged(nameof(IsMultiBand));

            // Format setzen — sicherstellen dass Format zur Family passt
            var formatInfo = FormatDefinitions.Get(project.Format);
            if (formatInfo.Family != _selectedProductFamily)
                formatInfo = FormatDefinitions.GetDefaultFormat(_selectedProductFamily);
            SelectedFormat = formatInfo;

            if (_selectedFormat.IsModuleBased)
            {
                SelectedMpModule = null;
                SelectedMpCell = null;
                _mpDoc.Clear();

                if (project.MpPages != null && project.MpPages.Count > 0)
                {
                    var loadedFamily = ProductFamilyDefinitions.Get(_selectedFormat.Family);
                    var defaultVariant = MpModuleLayoutFactory.DefaultVariantFor(_selectedFormat.Family);
                    foreach (var mpPage in project.MpPages)
                    {
                        var pageVms = mpPage.Modules
                            .Take(loadedFamily.ModulesPerPage)
                            .Select(mod =>
                            {
                                // Zellen neu generieren falls noetig
                                if (mod.AddressCells.Count == 0)
                                    mod.AddressCells = MpModuleLayoutFactory.CreateCells(mod.Variant);
                                // Artikel, der nicht zur Familie passt (z.B. DI 16 BA war bis
                                // v3.0 als 35mm gefuehrt, ist aber ein 25mm-Modul): abwaehlen,
                                // Texte bleiben, Belegung wird generisch.
                                var entry = MpModuleCatalog.Find(mod.ArticleNumber);
                                if (entry is not null && !MpModuleCatalog.FitsFamily(entry, _selectedFormat.Family))
                                    mod.ArticleNumber = null;
                                return CreateMpModuleViewModel(mod);
                            })
                            .ToList();
                        // Seite auf alle Streifen-Positionen auffuellen (handeditierte
                        // oder unvollstaendige Dateien) — sonst fehlen Positionen in
                        // Vorschau und Auswahl.
                        while (pageVms.Count < loadedFamily.ModulesPerPage)
                        {
                            var module = new MpModule { ModuleIndex = pageVms.Count, Variant = defaultVariant };
                            module.AddressCells = MpModuleLayoutFactory.CreateCells(module.Variant);
                            pageVms.Add(CreateMpModuleViewModel(module));
                        }
                        _mpDoc.AddPage(pageVms);
                    }
                }

                if (_mpDoc.PageCount == 0)
                    _mpDoc.AddPage(CreateEmptyMpPage(ModulesPerPage));

                _mpDoc.Show(0);
                NotifyPageProperties();
                OnPropertyChanged(nameof(IsModuleBased));
                if (MpModules.Count > 0)
                    SelectedMpModule = MpModules[0];
            }
            else
            {
                // Seiten aus project.Pages aufbauen (ET200SP)
                _spDoc.Clear();
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
                        pageVms.Add(CreateLabelViewModel(cell));
                    }
                    _spDoc.AddPage(pageVms);
                }

                if (_spDoc.PageCount == 0)
                    _spDoc.AddPage(CreateEmptyPage(labelsPerPage));

                SelectedLabel = null;
                _spDoc.Show(0);
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
            Panel.LoadFromSettings();
            OnPropertyChanged(nameof(Settings));
            NotifyPreviewGeometry();
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

            Session.MarkLoaded(filePath);
            if (Labels.Count > 0) SelectedLabel = Labels[0];
        }
        finally { _suppressContentLossConfirm = false; }
    }

    /// <summary>Baut das Druckdokument des gesamten Projekts (alle Seiten) — ohne
    /// Dialog. Gemeinsame Quelle fuer den echten Druck und fuer die Test-Automation
    /// (render-print), damit beide exakt dieselben Seiten erzeugen.</summary>
    internal System.Windows.Documents.FixedDocument BuildPrintDocument()
    {
        if (_selectedFormat.IsModuleBased)
        {
            return PrintService.BuildMpDocument(_mpDoc.Pages, _selectedFormat, _settings, PrintGridLines,
                CalibrationOffsetX, CalibrationOffsetY);
        }

        return PrintService.BuildDocument(_spDoc.Pages, _selectedFormat, _settings, PrintGridLines,
            CalibrationOffsetX, CalibrationOffsetY);
    }

    internal System.Windows.Documents.FixedDocument BuildCalibrationDocument() =>
        PrintService.BuildCalibrationDocument(_selectedFormat, _settings,
            CalibrationOffsetX, CalibrationOffsetY);

    /// <summary>Druckdokument nur fuer die aktuell sichtbare Seite.</summary>
    internal System.Windows.Documents.FixedDocument BuildCurrentPageDocument()
    {
        if (_selectedFormat.IsModuleBased)
        {
            IReadOnlyList<IReadOnlyList<MpModuleViewModel>> pages = [_mpDoc.PageAt(CurrentPageIndex)];
            return PrintService.BuildMpDocument(pages, _selectedFormat, _settings, PrintGridLines,
                CalibrationOffsetX, CalibrationOffsetY);
        }
        IReadOnlyList<IReadOnlyList<LabelViewModel>> spPages = [_spDoc.PageAt(CurrentPageIndex)];
        return PrintService.BuildDocument(spPages, _selectedFormat, _settings, PrintGridLines,
            CalibrationOffsetX, CalibrationOffsetY);
    }

    private void PrintLabels() => PrintDocument(BuildPrintDocument, PageCount);

    private void PrintCurrentPage() => PrintDocument(BuildCurrentPageDocument, 1);

    private void PrintDocument(Func<System.Windows.Documents.FixedDocument> build, int requestedPages)
    {
        try
        {
            var document = build();
            if (document.Pages.Count == 0)
            {
                StatusMessage = "Nichts zu drucken";
                _dialogs.ShowInfo(
                    "Es gibt keine befuellten, druckaktiven Etiketten bzw. Module.\n" +
                    "Leere Seiten werden nicht gedruckt.", "Drucken");
                return;
            }
            if (!PrintService.Print(document, PrintService.JobTitleFor(_selectedFormat)))
            {
                StatusMessage = "Druck abgebrochen";
                return;
            }
            // Kalibrierung erst nach tatsaechlichem Druck persistieren — ein
            // abgebrochener Dialog darf keine lokale calibration.json anlegen.
            SaveCalibration();
            int skipped = requestedPages - document.Pages.Count;
            StatusMessage = skipped > 0
                ? $"Druckauftrag gesendet ({document.Pages.Count} von {requestedPages} Seiten, {skipped} leere uebersprungen)"
                : $"Druckauftrag gesendet ({document.Pages.Count} Seiten)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Druckfehler: {ex.Message}";
            _dialogs.ShowError($"Fehler beim Drucken:\n{ex.Message}", "Druckfehler");
        }
    }

    private void PrintCalibration()
    {
        try
        {
            var document = BuildCalibrationDocument();
            if (!PrintService.Print(document, PrintService.CalibrationJobTitleFor(_selectedFormat)))
            {
                StatusMessage = "Druck abgebrochen";
                return;
            }
            SaveCalibration();
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

    private void ImportCsv() => ImportCellsAsync("CSV-Dateien|*.csv|Alle Dateien|*.*",
        "CSV-Datei importieren", "CSV", CsvImportService.Import);

    private void ImportExcel() => ImportCellsAsync("Excel-Dateien|*.xlsx|Alle Dateien|*.*",
        "Excel-Datei importieren", "Excel", ExcelImportService.Import);

    /// <summary>Gemeinsamer CSV-/Excel-Import: Datei waehlen, Parser im Hintergrund
    /// (Wartecursor statt eingefrorener UI), Ergebnis auf dem UI-Thread uebernehmen.</summary>
    private async void ImportCellsAsync(string filter, string title, string kind, Func<string, List<LabelCell>> parser)
    {
        var file = _dialogs.OpenFile(filter, title);
        if (file is null) return;

        try
        {
            List<LabelCell> cells;
            using (new WaitCursorScope())
                cells = await Task.Run(() => parser(file));

            if (cells.Count == 0)
            {
                StatusMessage = $"{kind}-Datei enthaelt keine Daten";
                return;
            }
            PopulateFromImportedCells(cells);
            StatusMessage = $"{cells.Count} Etiketten aus {kind} importiert";
        }
        catch (Exception ex)
        {
            Log.Error($"{kind}-Import", ex);
            StatusMessage = $"{kind}-Importfehler: {ex.Message}";
            _dialogs.ShowError($"Fehler beim {kind}-Import:\n{ex.Message}", "Importfehler");
        }
    }

    private async void ImportSchematic()
    {
        var file = _dialogs.OpenFile("PDF-Dateien|*.pdf|Alle Dateien|*.*", "PDF-Schaltplan importieren");
        if (file is null) return;

        try
        {
            SchematicParseResult result;
            using (new WaitCursorScope())
                result = await Task.Run(() => SchematicParserService.Parse(file));

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
            Log.Error("PDF-Import", ex);
            StatusMessage = $"PDF-Importfehler: {ex.Message}";
            _dialogs.ShowError($"Fehler beim PDF-Import:\n{ex.Message}", "Importfehler");
        }
    }

    /// <summary>Wartecursor fuer die Dauer einer Hintergrundoperation.</summary>
    private sealed class WaitCursorScope : IDisposable
    {
        private readonly Cursor? _previous = Mouse.OverrideCursor;
        public WaitCursorScope() => Mouse.OverrideCursor = Cursors.Wait;
        public void Dispose() => Mouse.OverrideCursor = _previous;
    }

    private void PopulateFromImportedCells(List<LabelCell> cells)
    {
        int labelsPerPage = _selectedFormat.LabelsPerPage;
        int startIndex = SelectedLabel?.Index ?? 0;
        int startPage = CurrentPageIndex;
        int cellIndex = 0;

        int pageIdx = startPage;
        int labelIdx = startIndex;

        while (cellIndex < cells.Count)
        {
            _spDoc.EnsurePage(pageIdx, () => CreateEmptyPage(_selectedFormat.LabelsPerPage));
            var pageVms = _spDoc.PageAt(pageIdx);
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

        if (!_suppressContentLossConfirm) // Automation: keine modale Box
            _dialogs.ShowInfo(
                $"{cells.Count} Etiketten importiert.\nVerteilt auf {_spDoc.PageCount} Seite(n).",
                "Import abgeschlossen");
    }

    /// <summary>Test-Automation: CSV/Excel-Datei ohne Dialog importieren.</summary>
    internal int ImportCellsFromFile(string path)
    {
        var cells = string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ExcelImportService.Import(path)
            : CsvImportService.Import(path);
        if (cells.Count > 0) PopulateFromImportedCells(cells);
        return cells.Count;
    }

    internal static ModuleType? MapParsedModuleType(string moduleType) => moduleType.ToUpperInvariant() switch
    {
        "DI" => ModuleType.DI,
        "DO" => ModuleType.DO,
        "AI" => ModuleType.AI,
        "AO" => ModuleType.AO,
        _ => null
    };

    /// <summary>Layout-Variante fuer ein importiertes Modul ohne Katalog-Artikel:
    /// digital bis 16 Kanaele = 1 Spalte, darueber 2 Spalten; analog = Analogblock.</summary>
    internal static MpModuleVariant SuggestVariant(ProductFamily family, ModuleType type, int channelCount)
    {
        bool is25 = family == ProductFamily.S71500_ET200MP_25mm;
        var info = AddressGenerator.ModuleTypes.First(m => m.Type == type);
        if (info.IsBitAddressed)
        {
            if (is25) return channelCount > 16 ? MpModuleVariant.MP25_32 : MpModuleVariant.MP25_16;
            return channelCount > 16 ? MpModuleVariant.DI_DQ_32 : MpModuleVariant.DI_DQ_16;
        }
        if (is25) return MpModuleVariant.MP25_16;
        return channelCount <= 4 && type == ModuleType.AO ? MpModuleVariant.AQ_4 : MpModuleVariant.AI_AQ_8;
    }

    /// <summary>Anzahl der Generator-Einheiten eines geparsten Moduls: Bytes bei
    /// digital (aufgerundet), Kanaele bei analog. Mindestens 1.</summary>
    internal static int ParsedModuleUnits(ModuleType type, int channelCount)
    {
        var info = AddressGenerator.ModuleTypes.First(m => m.Type == type);
        return info.IsBitAddressed
            ? Math.Max(1, (channelCount + 7) / 8)
            : Math.Max(1, channelCount);
    }

    /// <summary>
    /// ET200SP: Etikettenzellen fuer ein geparstes Modul. Ein Etikett fasst maximal
    /// 2 Bytes bzw. 16 Kanaele (AddressGenerator.GetEffectiveCount) — groessere Module
    /// werden auf mehrere Etiketten verteilt, statt still gekappt (32-Kanal-Modul
    /// verlor frueher Byte 2+3). Einzeilige Formate bekommen die Adressen gemerged.
    /// </summary>
    internal static List<LabelCell> BuildImportCells(string moduleName, ModuleType? type,
        int startByte, int channelCount, IReadOnlyList<string> rawAddresses, bool isDoubleLine)
    {
        var cells = new List<LabelCell>();

        if (type is null)
        {
            // Unbekannter Typ: Modulname + Rohadressen auf zwei Zeilen
            int half = (rawAddresses.Count + 1) / 2;
            string l1 = string.Join("  ", rawAddresses.Take(half));
            string l2 = rawAddresses.Count > half ? string.Join("  ", rawAddresses.Skip(half)) : string.Empty;
            if (!isDoubleLine && l2.Length > 0) { l1 = string.Join("  ", rawAddresses); l2 = string.Empty; }
            cells.Add(new LabelCell { Header = moduleName, Line1 = l1, Line2 = l2 });
            return cells;
        }

        int remaining = ParsedModuleUnits(type.Value, channelCount);
        int cursor = startByte;
        while (remaining > 0)
        {
            int chunk = AddressGenerator.GetEffectiveCount(type.Value, remaining);
            var generated = AddressGenerator.Generate(moduleName, type.Value, cursor, chunk);
            string line1 = generated.Line1;
            string line2 = generated.Line2;
            if (!isDoubleLine && !string.IsNullOrWhiteSpace(line2))
            {
                line1 = MergeAddressLines(line1, line2);
                line2 = string.Empty;
            }
            cells.Add(new LabelCell { Header = generated.Header, Line1 = line1, Line2 = line2 });
            cursor = AddressGenerator.GetNextStartByte(type.Value, cursor, chunk);
            remaining -= chunk;
        }
        return cells;
    }

    private void PopulateFromParsedModules(List<ParsedModule> modules)
    {
        if (_selectedFormat.IsModuleBased)
        {
            PopulateMpFromParsedModules(modules);
            return;
        }

        var cells = new List<LabelCell>();
        foreach (var module in modules)
        {
            cells.AddRange(BuildImportCells(module.ModuleName, MapParsedModuleType(module.ModuleType),
                module.StartByte, module.ChannelCount,
                module.Channels.Select(c => c.Address).ToList(), IsDoubleLine));
        }

        if (cells.Count > 0)
            PopulateFromImportedCells(cells);
    }

    /// <summary>ET200MP: je geparstem Modul ein Streifen ab dem ausgewaehlten Modul
    /// (Header = Modulname, Adressen ueber den Generator), Seiten bei Bedarf anlegen.
    /// Frueher landete der Import hier in unsichtbaren SP-Seiten.</summary>
    private void PopulateMpFromParsedModules(List<ParsedModule> modules)
    {
        var familyInfo = ProductFamilyDefinitions.Get(_selectedFormat.Family);
        int startPage = CurrentPageIndex;
        int pageIdx = startPage;
        int modIdx = SelectedMpModule is not null ? MpModules.IndexOf(SelectedMpModule) : 0;
        if (modIdx < 0) modIdx = 0;

        int imported = 0;
        foreach (var parsed in modules)
        {
            _mpDoc.EnsurePage(pageIdx, () => CreateEmptyMpPage(familyInfo.ModulesPerPage));

            var target = _mpDoc.PageAt(pageIdx)[modIdx];
            var type = MapParsedModuleType(parsed.ModuleType);
            target.HeaderText = parsed.ModuleName;
            if (type is not null)
            {
                if (string.IsNullOrEmpty(target.ArticleNumber))
                {
                    // Benutzerdefiniertes Modul: Layout-Variante zur Kanalzahl waehlen
                    // (32 Kanaele passen nicht in einen 16-Kanal-Streifen)
                    target.Variant = SuggestVariant(_selectedFormat.Family, type.Value, parsed.ChannelCount);
                    target.IoType = type.Value;
                }
                FillMpModuleAddresses(target, type.Value, parsed.StartByte,
                    ParsedModuleUnits(type.Value, parsed.ChannelCount));
            }
            else
            {
                var editable = target.AddressCells.Where(c => c.IsEditable).ToList();
                var addresses = parsed.Channels.Select(c => c.Address).ToList();
                for (int i = 0; i < editable.Count; i++)
                    editable[i].Text = i < addresses.Count ? addresses[i] : string.Empty;
            }
            imported++;

            modIdx++;
            if (modIdx >= familyInfo.ModulesPerPage) { modIdx = 0; pageIdx++; }
        }

        NavigateToPage(startPage);
        NotifyPageProperties();
        NotifyMpPreviewChanged();
        IsDirty = true;

        if (!_suppressContentLossConfirm)
            _dialogs.ShowInfo(
                $"{imported} Module importiert.\nVerteilt auf {_mpDoc.PageCount} Seite(n).",
                "Import abgeschlossen");
    }

    /// <summary>Test-Automation: geparste Module (Textzeilen eines Schaltplans) ohne
    /// Dialog importieren — SP: Etiketten, MP: Module.</summary>
    internal int ImportParsedLines(IEnumerable<string> lines)
    {
        var modules = SchematicParserService.ParseLines(lines);
        if (modules.Count > 0) PopulateFromParsedModules(modules);
        return modules.Count;
    }

    // === Selective Print Methods ===

    private void SetPrintFlagForAll(Func<LabelViewModel, bool> labelRule, Func<MpModuleViewModel, bool> moduleRule)
    {
        foreach (var label in _spDoc.AllItems)
            label.IsPrintEnabled = labelRule(label);
        foreach (var mod in _mpDoc.AllItems)
            mod.IsPrintEnabled = moduleRule(mod);
        IsDirty = true;
        NotifyMpPreviewChanged();
    }

    private void SelectAllForPrint()
    {
        SetPrintFlagForAll(_ => true, _ => true);
        StatusMessage = IsModuleBased ? "Alle Module zum Drucken aktiviert" : "Alle Etiketten zum Drucken aktiviert";
    }

    private void DeselectAllForPrint()
    {
        SetPrintFlagForAll(_ => false, _ => false);
        StatusMessage = IsModuleBased ? "Alle Module vom Drucken ausgeschlossen" : "Alle Etiketten vom Drucken ausgeschlossen";
    }

    private void SelectFilledForPrint()
    {
        SetPrintFlagForAll(l => l.HasText, m => m.HasPrintableContent);
        StatusMessage = IsModuleBased ? "Nur befuellte Module zum Drucken aktiviert" : "Nur befuellte Etiketten zum Drucken aktiviert";
    }

    private void TogglePrint()
    {
        if (_selectedFormat.IsModuleBased)
        {
            if (SelectedMpModule is null) return;
            SelectedMpModule.IsPrintEnabled = !SelectedMpModule.IsPrintEnabled; // ContentChanged -> dirty + preview
            StatusMessage = SelectedMpModule.IsPrintEnabled
                ? $"Modul {SelectedMpModule.ModuleIndex + 1}: Druck aktiviert"
                : $"Modul {SelectedMpModule.ModuleIndex + 1}: Druck deaktiviert";
            return;
        }
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

    /// <summary>Test-Automation: Aenderungen verwerfen, damit das Fenster ohne
    /// modale Rueckfrage geschlossen werden kann (headless haengt sonst).</summary>
    internal void DiscardChangesForShutdown() => IsDirty = false;

    internal string? CurrentFilePath => Session.CurrentFilePath;

    /// <summary>Test-Automation: druckbare Etiketten/Module je Seite (dieselbe
    /// Entscheidung wie der Druck).</summary>
    internal int[] PrintablePerPage() => _selectedFormat.IsModuleBased
        ? _mpDoc.Pages.Select(p => p.Count(PrintService.IsPrintable)).ToArray()
        : _spDoc.Pages.Select(p => p.Count(PrintService.IsPrintable)).ToArray();

    /// <summary>Test-Automation: "Neues Projekt" ohne Rueckfrage.</summary>
    internal void NewProjectWithoutConfirm() => Session.NewProjectWithoutConfirm();

    /// <summary>Fragt, ob ungespeicherte Aenderungen verworfen werden sollen (Fenster
    /// schliessen). True = fortfahren.</summary>
    public bool ConfirmDiscardChanges() => Session.ConfirmDiscardChanges();
}
