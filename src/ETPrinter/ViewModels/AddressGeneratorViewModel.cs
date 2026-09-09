using ETPrinter.Services;

namespace ETPrinter.ViewModels;

/// <summary>
/// Eingabefelder des Adress-Generators (Tab "Adress-Generator") mit Live-Vorschau.
/// Aus MainViewModel herausgeloest (ABSCHLUSSPLAN AP9). Das Uebertragen auf
/// Etikett/Modul bleibt im MainViewModel (braucht Auswahl und Seiten).
/// </summary>
public class AddressGeneratorViewModel : ViewModelBase
{
    private string _moduleName = string.Empty;
    private ModuleTypeInfo _moduleType = AddressGenerator.ModuleTypes[0]; // DI
    private int _startByte;
    private int _count = 2;
    private bool _autoAdvance = true;
    private string _previewLine1 = string.Empty;
    private string _previewLine2 = string.Empty;

    /// <summary>Modultyp gewechselt (MainViewModel uebertraegt ihn auf ein benutzerdefiniertes MP-Modul).</summary>
    public event Action? ModuleTypeChanged;

    /// <summary>Statusmeldung an die Statusleiste (z.B. Wertebereich).</summary>
    public event Action<string>? StatusRequested;

    public ModuleTypeInfo[] AvailableModuleTypes => AddressGenerator.ModuleTypes;

    public string ModuleName
    {
        get => _moduleName;
        set { if (SetProperty(ref _moduleName, value)) UpdatePreview(); }
    }

    public ModuleTypeInfo ModuleType
    {
        get => _moduleType;
        set
        {
            if (SetProperty(ref _moduleType, value))
            {
                OnPropertyChanged(nameof(CountLabel));
                OnPropertyChanged(nameof(TypicalCounts));
                // DI (1/2/4 Bytes) -> AI (2/4/8 Kanaele): ein nicht mehr gueltiger
                // Wert liess die ComboBox leer, der Generator rechnete still weiter.
                if (!TypicalCounts.Contains(Count))
                    Count = TypicalCounts[0];
                ModuleTypeChanged?.Invoke();
                UpdatePreview();
            }
        }
    }

    public int StartByte
    {
        get => _startByte;
        set
        {
            if (value < 0) { StatusRequested?.Invoke("Start-Byte darf nicht negativ sein"); value = 0; }
            if (SetProperty(ref _startByte, value)) UpdatePreview();
        }
    }

    public int Count
    {
        get => _count;
        set { if (SetProperty(ref _count, value)) UpdatePreview(); }
    }

    public bool AutoAdvance
    {
        get => _autoAdvance;
        set => SetProperty(ref _autoAdvance, value);
    }

    public string PreviewLine1
    {
        get => _previewLine1;
        private set => SetProperty(ref _previewLine1, value);
    }

    public string PreviewLine2
    {
        get => _previewLine2;
        private set => SetProperty(ref _previewLine2, value);
    }

    public string CountLabel => AddressGenerator.GetCountLabel(_moduleType.Type);
    public int[] TypicalCounts => AddressGenerator.GetTypicalCounts(_moduleType.Type);

    /// <summary>Modultyp-Info (Praefix, bit-adressiert) des aktuellen Typs.</summary>
    public ModuleTypeInfo Info => _moduleType;

    /// <summary>Typ per Enum setzen (Katalogwahl).</summary>
    public void SelectType(ModuleType type)
    {
        var info = AddressGenerator.ModuleTypes.FirstOrDefault(t => t.Type == type);
        if (info is not null) ModuleType = info;
    }

    /// <summary>Standardwerte (Neues Projekt).</summary>
    public void Reset()
    {
        ModuleName = string.Empty;
        StartByte = 0;
        Count = 2;
    }

    /// <summary>Adressen fuer die aktuellen Felder erzeugen (ET200SP-Etikett).</summary>
    public GeneratedLabel Generate() =>
        AddressGenerator.Generate(ModuleName, ModuleType.Type, StartByte, Count);

    /// <summary>Auto-Advance nach einem SP-Etikett: nur um die tatsaechlich aufs
    /// Etikett gepasste Anzahl (Digital max. 2 Bytes) weiterschalten.</summary>
    public void AdvanceAfterLabel()
    {
        if (!AutoAdvance) return;
        int effective = AddressGenerator.GetEffectiveCount(ModuleType.Type, Count);
        StartByte = AddressGenerator.GetNextStartByte(ModuleType.Type, StartByte, effective);
    }

    /// <summary>Auto-Advance nach einem MP-Modul um die belegten Bytes bzw. Kanaele.</summary>
    public void AdvanceAfterModule(int consumed)
    {
        if (!AutoAdvance) return;
        StartByte += ModuleType.IsBitAddressed ? consumed : consumed * 2;
    }

    public void UpdatePreview()
    {
        if (Count <= 0) return;
        try
        {
            var result = AddressGenerator.Generate(
                string.IsNullOrWhiteSpace(ModuleName) ? "..." : ModuleName,
                ModuleType.Type, StartByte, Count);
            PreviewLine1 = result.Line1;
            PreviewLine2 = result.Line2;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            PreviewLine1 = string.Empty;
            PreviewLine2 = string.Empty;
        }
    }
}
