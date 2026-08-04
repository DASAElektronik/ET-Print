using System.Collections.ObjectModel;
using ETPrinter.Models;
using ETPrinter.Services;

namespace ETPrinter.ViewModels;

public class MpAddressCellViewModel : ViewModelBase
{
    private readonly MpAddressCell _cell;
    private readonly MpCellDefinition _definition;
    private readonly Action? _contentChanged;
    private bool _isSelected;

    public MpAddressCellViewModel(MpAddressCell cell, MpCellDefinition definition,
        Action? contentChanged = null)
    {
        _cell = cell;
        _definition = definition;
        _contentChanged = contentChanged;
    }

    public int CellIndex => _cell.CellIndex;
    public int StartRow => _definition.StartRow;
    public int RowSpan => _definition.RowSpan;
    public int StartCol => _definition.StartCol;
    public int ColSpan => _definition.ColSpan;
    public bool IsEditable => _definition.IsEditable;

    /// <summary>Struktur-Label (M/L+/leer) — nur fuer nicht editierbare Zellen belegt.</summary>
    public string Label => _definition.Label;

    public string Text
    {
        get => _cell.Text;
        set { _cell.Text = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); _contentChanged?.Invoke(); }
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
    private bool _isChecked;

    public MpModuleViewModel(MpModule module)
    {
        _module = module;
        RebuildCellViewModels();
    }

    /// <summary>Wird bei jeder Inhaltsaenderung (Text, Variante, Schrift, Druckflag)
    /// aufgerufen — MainViewModel haengt hier Dirty-Tracking + Preview-Refresh an.
    /// IsSelected/IsChecked loesen den Callback bewusst NICHT aus.</summary>
    public Action? ContentChanged { get; set; }

    private void NotifyContentChanged() => ContentChanged?.Invoke();

    public int ModuleIndex => _module.ModuleIndex;  // = Spalte auf der Seite (0-4)

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
                NotifyContentChanged();
            }
        }
    }

    /// <summary>Modultyp (DI/DO/AI/AO). Ohne Katalog-Artikel bestimmt er die
    /// generischen Struktur-Klemmen-Labels — Wechsel baut die Zellen neu auf.</summary>
    public ModuleType IoType
    {
        get => _module.IoType;
        set
        {
            if (_module.IoType == value) return;
            _module.IoType = value;
            RebuildCellViewModels();
            OnPropertyChanged();
            NotifyContentChanged();
        }
    }

    /// <summary>Konkretes Siemens-Modul (Artikelnummer aus MpModuleCatalog);
    /// null = benutzerdefiniert. Setzt Variante + Klemmenbelegung des Katalogs.</summary>
    public string? ArticleNumber
    {
        get => _module.ArticleNumber;
        set
        {
            if (_module.ArticleNumber == value) return;
            _module.ArticleNumber = value;

            var entry = MpModuleCatalog.Find(value);
            if (entry is not null && _module.Variant != entry.Variant)
            {
                // Variant-Setter baut die Zellen bereits mit dem neuen Artikel um
                Variant = entry.Variant;
            }
            else
            {
                // Gleiche Variante, aber andere Struktur-Labels -> neu aufbauen
                RebuildCellViewModels();
                NotifyContentChanged();
            }
            OnPropertyChanged();
        }
    }

    public string HeaderText
    {
        get => _module.HeaderText;
        set { _module.HeaderText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); NotifyContentChanged(); }
    }

    public string NetAddress1
    {
        get => _module.NetAddress1;
        set { _module.NetAddress1 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); NotifyContentChanged(); }
    }

    public string NetAddress2
    {
        get => _module.NetAddress2;
        set { _module.NetAddress2 = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); NotifyContentChanged(); }
    }

    public string CpuName
    {
        get => _module.CpuName;
        set { _module.CpuName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasText)); NotifyContentChanged(); }
    }

    public int FontSize
    {
        get => _module.FontSize;
        set { _module.FontSize = value; OnPropertyChanged(); NotifyContentChanged(); }
    }

    public string FontFamily
    {
        get => _module.FontFamily;
        set { _module.FontFamily = value; OnPropertyChanged(); NotifyContentChanged(); }
    }

    public bool IsBold
    {
        get => _module.IsBold;
        set { _module.IsBold = value; OnPropertyChanged(); NotifyContentChanged(); }
    }

    public bool IsPrintEnabled
    {
        get => _module.IsPrintEnabled;
        set { _module.IsPrintEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrintOpacity)); NotifyContentChanged(); }
    }

    public double PrintOpacity => IsPrintEnabled ? 1.0 : 0.4;

    public ObservableCollection<MpAddressCellViewModel> AddressCells { get; } = [];

    public bool HasText => _module.HasText;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>True wenn das Modul Teil einer Multi-Selection fuer Copy ist (Strg+Klick / Shift+Klick).</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    public MpModule GetModule() => _module;

    /// <summary>Uebernimmt Inhalt eines anderen Moduls (fuer Paste). ModuleIndex bleibt.</summary>
    public void SetModule(MpModule source)
    {
        // Artikel + Modultyp vor Variante setzen, damit der Zellen-Neuaufbau die
        // Belegung des Quellmoduls verwendet
        _module.ArticleNumber = source.ArticleNumber;
        _module.IoType = source.IoType;
        // Variante setzen ueber Property, damit AddressCells neu aufgebaut werden
        Variant = source.Variant;
        _module.HeaderText = source.HeaderText;
        _module.NetAddress1 = source.NetAddress1;
        _module.NetAddress2 = source.NetAddress2;
        _module.CpuName = source.CpuName;
        _module.FontSize = source.FontSize;
        _module.FontFamily = source.FontFamily;
        _module.IsBold = source.IsBold;
        _module.IsPrintEnabled = source.IsPrintEnabled;

        // Zelltexte uebertragen (AddressCells-Liste wurde durch Variant-Setter neu angelegt)
        for (int i = 0; i < Math.Min(source.AddressCells.Count, _module.AddressCells.Count); i++)
            _module.AddressCells[i].Text = source.AddressCells[i].Text;

        // UI informieren
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(NetAddress1));
        OnPropertyChanged(nameof(NetAddress2));
        OnPropertyChanged(nameof(CpuName));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(FontFamily));
        OnPropertyChanged(nameof(IsBold));
        OnPropertyChanged(nameof(IsPrintEnabled));
        OnPropertyChanged(nameof(PrintOpacity));
        OnPropertyChanged(nameof(HasText));
        OnPropertyChanged(nameof(ArticleNumber));
        OnPropertyChanged(nameof(IoType));
        // AddressCell-Texte via RebuildCellViewModels neu durchreichen
        RebuildCellViewModels();
    }

    private void RebuildCellViewModels()
    {
        AddressCells.Clear();
        // Katalog-Belegung (konkretes Modul) oder generische Varianten-Belegung
        var definitions = MpModuleLayoutFactory.GetDefinitions(_module);

        // Nur neue Zellen erstellen wenn keine vorhanden oder Anzahl nicht passt
        if (_module.AddressCells.Count != definitions.Length)
        {
            // Alte Texte soweit moeglich migrieren
            var oldTexts = _module.AddressCells.Select(c => c.Text).ToList();
            _module.AddressCells = MpModuleLayoutFactory.CreateCells(_module.Variant);
            for (int i = 0; i < Math.Min(oldTexts.Count, _module.AddressCells.Count); i++)
                _module.AddressCells[i].Text = oldTexts[i];
        }

        for (int i = 0; i < _module.AddressCells.Count; i++)
        {
            // Closure statt Direktreferenz: ContentChanged wird erst NACH dem
            // Konstruktor (und damit nach diesem Aufruf) vom MainViewModel gesetzt.
            AddressCells.Add(new MpAddressCellViewModel(
                _module.AddressCells[i], definitions[i],
                () => ContentChanged?.Invoke()));
        }
    }
}
