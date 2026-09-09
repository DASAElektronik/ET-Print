using ETPrinter.Models;

namespace ETPrinter.ViewModels;

/// <summary>
/// Eingabefelder der Panels "Seite" (Kopfzeilen-Schrift, Raender) und "Schrift"
/// (Adress-Schrift des ausgewaehlten Etiketts/Moduls) mit Live-Apply.
/// Aus MainViewModel herausgeloest (ABSCHLUSSPLAN AP9). Raender und Header-Schrift
/// werden direkt in <see cref="LabelSettings"/> geschrieben; die Adress-Schrift geht
/// per <see cref="FontChanged"/> an das MainViewModel, das sie auf die Auswahl anwendet.
/// </summary>
public class SettingsPanelViewModel : ViewModelBase
{
    // Wertebereiche: Raender 0-60 mm (darueber laeuft das Raster aus dem Blatt).
    public const double MarginMinMm = 0, MarginMaxMm = 60;

    private readonly LabelSettings _settings;

    private int _fontSize = 7;
    private bool _isBold;
    private bool _isItalic;
    private string _fontFamily = "Arial";
    private int _headerFontSize = 9;
    private bool _headerIsBold = true;
    // Startwerte = LabelSettings-Defaults (ET200SP), sonst verstellt der erste
    // Live-Apply drei nicht angefasste Raender auf die abweichenden Input-Werte.
    private double _marginTop = 20.5;
    private double _marginLeft = 27.5;
    private double _marginBottom = 20.5;
    private double _marginRight = 27.5;

    public SettingsPanelViewModel(LabelSettings settings)
    {
        _settings = settings;
    }

    /// <summary>Guard: waehrend die Felder programmatisch geladen werden (Etikett-/
    /// Modulwechsel, Projekt laden), loest kein Setter Live-Apply aus.</summary>
    public bool SuspendLiveApply { get; private set; }

    /// <summary>Adress-Schrift geaendert (Groesse/fett/kursiv/Schriftart) — auf die Auswahl anwenden.</summary>
    public event Action? FontChanged;
    /// <summary>Kopfzeilen-Schrift in den Settings geaendert.</summary>
    public event Action? HeaderChanged;
    /// <summary>Seitenraender in den Settings geaendert.</summary>
    public event Action? MarginsChanged;
    public event Action<string>? StatusRequested;

    // ---- Adress-Schrift (pro Etikett/Modul) ----

    public int FontSize
    {
        get => _fontSize;
        set { if (SetProperty(ref _fontSize, value)) RaiseFontChanged(); }
    }

    public bool IsBold
    {
        get => _isBold;
        set { if (SetProperty(ref _isBold, value)) RaiseFontChanged(); }
    }

    public bool IsItalic
    {
        get => _isItalic;
        set { if (SetProperty(ref _isItalic, value)) RaiseFontChanged(); }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { if (SetProperty(ref _fontFamily, value)) RaiseFontChanged(); }
    }

    // ---- Kopfzeilen-Schrift (global) ----

    public int HeaderFontSize
    {
        get => _headerFontSize;
        set { if (SetProperty(ref _headerFontSize, value)) ApplyHeaderToSettings(); }
    }

    public bool HeaderIsBold
    {
        get => _headerIsBold;
        set { if (SetProperty(ref _headerIsBold, value)) ApplyHeaderToSettings(); }
    }

    // ---- Raender (global, begrenzt) ----

    public double MarginTop
    {
        get => _marginTop;
        set { if (SetProperty(ref _marginTop, ClampMargin(value, "oben"))) ApplyMarginsToSettings(); }
    }

    public double MarginLeft
    {
        get => _marginLeft;
        set { if (SetProperty(ref _marginLeft, ClampMargin(value, "links"))) ApplyMarginsToSettings(); }
    }

    public double MarginBottom
    {
        get => _marginBottom;
        set { if (SetProperty(ref _marginBottom, ClampMargin(value, "unten"))) ApplyMarginsToSettings(); }
    }

    public double MarginRight
    {
        get => _marginRight;
        set { if (SetProperty(ref _marginRight, ClampMargin(value, "rechts"))) ApplyMarginsToSettings(); }
    }

    private double ClampMargin(double v, string name)
    {
        double c = Math.Clamp(double.IsNaN(v) ? 0 : v, MarginMinMm, MarginMaxMm);
        if (c != v) StatusRequested?.Invoke($"Rand {name}: Wert auf {MarginMinMm:0}-{MarginMaxMm:0} mm begrenzt");
        return c;
    }

    private void RaiseFontChanged()
    {
        if (!SuspendLiveApply) FontChanged?.Invoke();
    }

    private void ApplyHeaderToSettings()
    {
        if (SuspendLiveApply) return;
        _settings.HeaderFontSize = _headerFontSize;
        _settings.HeaderIsBold = _headerIsBold;
        HeaderChanged?.Invoke();
    }

    private void ApplyMarginsToSettings()
    {
        if (SuspendLiveApply) return;
        _settings.MarginTop = _marginTop;
        _settings.MarginLeft = _marginLeft;
        _settings.MarginBottom = _marginBottom;
        _settings.MarginRight = _marginRight;
        MarginsChanged?.Invoke();
    }

    // ---- Laden ohne Live-Apply ----

    /// <summary>Fuehrt <paramref name="load"/> aus, ohne dass Setter Live-Apply ausloesen.</summary>
    public void Suspended(Action load)
    {
        SuspendLiveApply = true;
        try { load(); }
        finally { SuspendLiveApply = false; }
    }

    /// <summary>Adress-Schrift eines Etiketts/Moduls in die Felder laden.</summary>
    public void LoadFont(int size, bool bold, bool italic, string family) => Suspended(() =>
    {
        FontSize = size;
        IsBold = bold;
        IsItalic = italic;
        FontFamily = family;
    });

    /// <summary>Raender aus den Settings in die Felder laden.</summary>
    public void LoadMargins() => Suspended(() =>
    {
        MarginTop = _settings.MarginTop;
        MarginLeft = _settings.MarginLeft;
        MarginBottom = _settings.MarginBottom;
        MarginRight = _settings.MarginRight;
    });

    /// <summary>Alle Felder aus den Settings laden (Projekt laden, Zuruecksetzen).</summary>
    public void LoadFromSettings() => Suspended(() =>
    {
        FontSize = _settings.FontSize;
        IsBold = _settings.IsBold;
        IsItalic = _settings.IsItalic;
        FontFamily = _settings.FontFamily;
        HeaderFontSize = _settings.HeaderFontSize;
        HeaderIsBold = _settings.HeaderIsBold;
        MarginTop = _settings.MarginTop;
        MarginLeft = _settings.MarginLeft;
        MarginBottom = _settings.MarginBottom;
        MarginRight = _settings.MarginRight;
    });

    /// <summary>Adress-Schrift der Felder in die globalen Settings uebernehmen ("Auf alle anwenden").</summary>
    public void StoreFontInSettings()
    {
        _settings.FontSize = _fontSize;
        _settings.IsBold = _isBold;
        _settings.IsItalic = _isItalic;
        _settings.FontFamily = _fontFamily;
    }
}
