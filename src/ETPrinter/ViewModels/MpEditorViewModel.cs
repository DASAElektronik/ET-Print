using System.Collections.ObjectModel;
using ETPrinter.Models;
using ETPrinter.Services;
using MpModuleLayout = ETPrinter.Models.MpModuleLayout;

namespace ETPrinter.ViewModels;

/// <summary>
/// Modul-Editor des ET 200MP-Modus (ABSCHLUSSPLAN AP9, Schnitt "MpEditorVM"): Auswahl
/// von Modul und Adresszelle, Layout-Variante, Katalog-Artikel und die daraus
/// abgeleiteten Vorbelegungen des Adress-Generators. Arbeitet auf der sichtbaren
/// Modul-Seite (<see cref="Modules"/>), die das Haupt-ViewModel verwaltet.
/// </summary>
public sealed class MpEditorViewModel : ViewModelBase
{
    private readonly AddressGeneratorViewModel _generator;
    private readonly SettingsPanelViewModel _panel;
    private ProductFamily _family;
    private MpModuleViewModel? _selectedModule;
    private MpAddressCellViewModel? _selectedCell;

    public MpEditorViewModel(ObservableCollection<MpModuleViewModel> modules,
        AddressGeneratorViewModel generator, SettingsPanelViewModel panel, ProductFamily family)
    {
        Modules = modules;
        _generator = generator;
        _panel = panel;
        _family = family;
        AvailableVariants = new ObservableCollection<MpModuleLayout>(MpModuleLayoutFactory.VariantsForFamily(family));
        generator.ModuleTypeChanged += ApplyIoTypeToSelectedModule;
    }

    /// <summary>Module der sichtbaren Seite.</summary>
    public ObservableCollection<MpModuleViewModel> Modules { get; }

    /// <summary>Fasst Varianten-/Artikelwechsel zu einem Undo-Schritt zusammen
    /// (vom Haupt-ViewModel gesetzt; null = ohne Verlauf).</summary>
    public Func<string, IDisposable>? ChangeScope { get; set; }

    /// <summary>Layout-Varianten der aktuellen Familie (35mm bzw. 25mm).</summary>
    public ObservableCollection<MpModuleLayout> AvailableVariants { get; }

    /// <summary>Katalog-Artikel (konkrete Siemens-Module mit exakter Klemmenbelegung)
    /// der aktuellen Familie.</summary>
    public IReadOnlyList<MpCatalogEntry> AvailableArticles => MpModuleCatalog.EntriesForFamily(_family);

    /// <summary>Familienwechsel: Varianten und Katalog nachziehen.</summary>
    public void SetFamily(ProductFamily family)
    {
        _family = family;
        AvailableVariants.Clear();
        foreach (var v in MpModuleLayoutFactory.VariantsForFamily(family))
            AvailableVariants.Add(v);
        OnPropertyChanged(nameof(AvailableArticles));
    }

    public MpModuleViewModel? SelectedModule
    {
        get => _selectedModule;
        set
        {
            // Erneute Auswahl desselben Moduls darf die Markierung nicht loeschen
            if (ReferenceEquals(_selectedModule, value))
            {
                if (value is not null) value.IsSelected = true;
                return;
            }
            if (_selectedModule is not null)
                _selectedModule.IsSelected = false;

            // Die ausgewaehlte Zelle gehoert zum ALTEN Modul — ohne Reset schrieben
            // "Ausgewaehlte Adresszelle" und "Uebertragen" nach Auto-Advance oder
            // Header-Klick unsichtbar ins vorherige Modul.
            SelectedCell = null;

            _selectedModule = value;
            OnPropertyChanged();
            if (value is not null)
            {
                value.IsSelected = true;
                // Schrift-Eingabefelder aus dem Modul laden (wie bei SP-Etiketten),
                // ohne dass Live-Apply das Modul sofort mit seinen eigenen Werten ueberschreibt.
                _panel.LoadFont(value.FontSize, value.IsBold, value.IsItalic, value.FontFamily);
            }
            OnPropertyChanged(nameof(SelectionInfo));
            OnPropertyChanged(nameof(SelectedVariant));
            OnPropertyChanged(nameof(SelectedArticle));
        }
    }

    public MpAddressCellViewModel? SelectedCell
    {
        get => _selectedCell;
        set
        {
            if (_selectedCell is not null)
                _selectedCell.IsSelected = false;
            if (SetProperty(ref _selectedCell, value))
            {
                if (value is not null)
                    value.IsSelected = true;
            }
        }
    }

    public MpModuleLayout? SelectedVariant
    {
        get => _selectedModule is not null
            ? MpModuleLayoutFactory.GetLayout(_selectedModule.Variant)
            : null;
        set
        {
            if (value is null || _selectedModule is null) return;
            using var change = ChangeScope?.Invoke("Modulvariante");
            // Manuelle Variantenwahl = benutzerdefiniert (Katalog-Artikel abwaehlen)
            _selectedModule.ArticleNumber = null;
            _selectedModule.Variant = value.Variant;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectionInfo));
            OnPropertyChanged(nameof(SelectedArticle));
        }
    }

    public MpCatalogEntry? SelectedArticle
    {
        get => _selectedModule is null
            ? null
            : MpModuleCatalog.Find(_selectedModule.ArticleNumber) ?? MpModuleCatalog.CustomEntry;
        set
        {
            if (value is null || _selectedModule is null) return;
            using var change = ChangeScope?.Invoke("Siemens-Modul");

            bool isCustom = string.IsNullOrEmpty(value.ArticleNo);

            // Modultyp vor dem Artikel setzen — bei "Benutzerdefiniert" bestimmt er
            // die generischen Struktur-Labels, die der Zellen-Neuaufbau liest.
            _selectedModule.IoType = isCustom ? _generator.ModuleType.Type : value.IoType;
            _selectedModule.ArticleNumber = isCustom ? null : value.ArticleNo;

            // Generator-Modultyp am Katalogeintrag vorbelegen (DI/DO/AI/AO) und die
            // Anzahl auf die volle Kanalzahl des Moduls setzen (digital: Bytes = editierbare
            // Zellen / 8, gemischt: je Spalte), damit "Generieren" das Modul komplett fuellt.
            if (!isCustom)
            {
                _generator.SelectType(value.IoType);
                if (_generator.ModuleType.IsBitAddressed)
                {
                    int editable = _selectedModule.AddressCells.Count(c => c.IsEditable);
                    _generator.Count = Math.Max(1, (value.MixedOutputRightColumn ? editable / 2 : editable) / 8);
                }
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedVariant));
            OnPropertyChanged(nameof(SelectionInfo));
        }
    }

    /// <summary>Uebertraegt den Generator-Modultyp auf das ausgewaehlte Modul.
    /// Nur fuer benutzerdefinierte Module — bei Katalog-Artikeln kommt die
    /// Klemmenbelegung aus dem Datenblatt und darf nicht ueberschrieben werden.</summary>
    public void ApplyIoTypeToSelectedModule()
    {
        if (_selectedModule is null) return;
        if (!string.IsNullOrEmpty(_selectedModule.ArticleNumber)) return;
        _selectedModule.IoType = _generator.ModuleType.Type;
    }

    /// <summary>"Modul 3 / 10 (Spalte 1, Band 2)".</summary>
    public string SelectionInfo
    {
        get
        {
            if (_selectedModule is null) return "Kein Modul ausgewaehlt";
            var familyInfo = ProductFamilyDefinitions.Get(_family);
            int band = familyInfo.BandOf(_selectedModule.ModuleIndex);
            int col = familyInfo.ColumnOf(_selectedModule.ModuleIndex);
            return $"Modul {_selectedModule.ModuleIndex + 1} / {Modules.Count} (Spalte {col + 1}, Band {band + 1})";
        }
    }

    /// <summary>Nach Zell-Neuaufbau (Variante/Artikel/Modultyp/Paste) die ausgewaehlte
    /// Zelle per CellIndex neu aufloesen — die alte VM ist abgehaengt, Eingaben
    /// darin verschwanden ohne Markierung.</summary>
    public void OnCellsRebuilt(MpModuleViewModel module)
    {
        if (!ReferenceEquals(module, _selectedModule)) return;
        var old = _selectedCell;
        if (old is null) return;
        SelectedCell = module.AddressCells
            .FirstOrDefault(c => c.CellIndex == old.CellIndex && c.IsEditable);
    }
}
