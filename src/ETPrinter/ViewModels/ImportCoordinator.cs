using System.IO;
using System.Windows.Input;
using ETPrinter.Models;
using ETPrinter.Services;

namespace ETPrinter.ViewModels;

/// <summary>Ziel eines Imports: der Arbeitsbereich, der Etiketten (SP) bzw. Module
/// (MP) ab der aktuellen Auswahl befuellt.</summary>
public interface IImportTarget
{
    bool IsModuleBased { get; }
    bool IsDoubleLine { get; }

    /// <summary>ET200SP: Etikettenzellen ab dem ausgewaehlten Etikett verteilen.</summary>
    void PopulateLabels(List<LabelCell> cells);

    /// <summary>ET200MP: je geparstem Modul ein Streifen ab dem ausgewaehlten Modul.</summary>
    void PopulateModules(List<ParsedModule> modules);
}

/// <summary>
/// Import-Ablaeufe (ABSCHLUSSPLAN AP9, Schnitt "ImportCoordinator"): CSV, Excel und
/// PDF-Schaltplan — Datei waehlen, Parser im Hintergrund (Wartecursor statt
/// eingefrorener UI), Auswahl-Dialog, Ergebnis ins Ziel uebernehmen. Die reinen
/// Zuordnungsregeln (Modultyp, Layout-Variante, Etikettenaufteilung) sind statisch
/// und einzeln testbar.
/// </summary>
public sealed class ImportCoordinator
{
    private readonly IDialogService _dialogs;
    private readonly IImportTarget _target;

    public ImportCoordinator(IDialogService dialogs, IImportTarget target)
    {
        _dialogs = dialogs;
        _target = target;
    }

    public event Action<string>? StatusRequested;

    private void Status(string message) => StatusRequested?.Invoke(message);

    // ---- Dialog-Ablaeufe -----------------------------------------------------------

    public Task ImportCsvAsync() => ImportCellsAsync("CSV-Dateien|*.csv|Alle Dateien|*.*",
        "CSV-Datei importieren", "CSV", CsvImportService.Import);

    public Task ImportExcelAsync() => ImportCellsAsync("Excel-Dateien|*.xlsx|Alle Dateien|*.*",
        "Excel-Datei importieren", "Excel", ExcelImportService.Import);

    private async Task ImportCellsAsync(string filter, string title, string kind, Func<string, List<LabelCell>> parser)
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
                Status($"{kind}-Datei enthält keine Daten");
                return;
            }
            _target.PopulateLabels(cells);
            Status($"{cells.Count} Etiketten aus {kind} importiert");
        }
        catch (Exception ex)
        {
            Log.Error($"{kind}-Import", ex);
            Status($"{kind}-Importfehler: {ex.Message}");
            _dialogs.ShowError($"Fehler beim {kind}-Import:\n{ex.Message}", "Importfehler");
        }
    }

    public async Task ImportSchematicAsync()
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
            if (!_dialogs.ShowPdfImport(importVm)) return;

            var selectedModules = importVm.GetSelectedModules();
            if (selectedModules.Count == 0)
            {
                Status("Keine Module ausgewählt");
                return;
            }
            PopulateFromParsedModules(selectedModules);
            Status($"{selectedModules.Count} Module aus Schaltplan importiert");
        }
        catch (Exception ex)
        {
            Log.Error("PDF-Import", ex);
            Status($"PDF-Importfehler: {ex.Message}");
            _dialogs.ShowError($"Fehler beim PDF-Import:\n{ex.Message}", "Importfehler");
        }
    }

    /// <summary>Wartecursor fuer die Dauer einer Hintergrundoperation (nur mit
    /// laufender WPF-Anwendung; headless/Tests: ohne Wirkung).</summary>
    private sealed class WaitCursorScope : IDisposable
    {
        private readonly bool _active = System.Windows.Application.Current is not null;
        private readonly Cursor? _previous;
        public WaitCursorScope()
        {
            if (!_active) return;
            _previous = Mouse.OverrideCursor;
            Mouse.OverrideCursor = Cursors.Wait;
        }
        public void Dispose() { if (_active) Mouse.OverrideCursor = _previous; }
    }

    // ---- Dialogfreie Einstiege (Test-Automation) ---------------------------------------

    /// <summary>CSV/Excel-Datei ohne Dialog importieren; liefert die Zellenanzahl.</summary>
    public int ImportCellsFromFile(string path)
    {
        var cells = string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ExcelImportService.Import(path)
            : CsvImportService.Import(path);
        if (cells.Count > 0) _target.PopulateLabels(cells);
        return cells.Count;
    }

    /// <summary>Textzeilen wie einen Schaltplan parsen und ohne Dialog importieren —
    /// SP: Etiketten, MP: Module. Liefert die Modulanzahl.</summary>
    public int ImportParsedLines(IEnumerable<string> lines)
    {
        var modules = SchematicParserService.ParseLines(lines);
        if (modules.Count > 0) PopulateFromParsedModules(modules);
        return modules.Count;
    }

    /// <summary>Geparste Module ins Ziel: MP direkt als Module, SP als Etikettenzellen.</summary>
    public void PopulateFromParsedModules(List<ParsedModule> modules)
    {
        if (_target.IsModuleBased)
        {
            _target.PopulateModules(modules);
            return;
        }

        var cells = new List<LabelCell>();
        foreach (var module in modules)
        {
            cells.AddRange(BuildImportCells(module.ModuleName, MapParsedModuleType(module.ModuleType),
                module.StartByte, module.ChannelCount,
                module.Channels.Select(c => c.Address).ToList(), _target.IsDoubleLine));
        }

        if (cells.Count > 0)
            _target.PopulateLabels(cells);
    }

    // ---- Zuordnungsregeln (statisch, testbar) --------------------------------------------

    public static ModuleType? MapParsedModuleType(string moduleType) => moduleType.ToUpperInvariant() switch
    {
        "DI" => ModuleType.DI,
        "DO" => ModuleType.DO,
        "AI" => ModuleType.AI,
        "AO" => ModuleType.AO,
        _ => null
    };

    /// <summary>Layout-Variante fuer ein importiertes Modul ohne Katalog-Artikel:
    /// digital bis 16 Kanaele = 1 Spalte, darueber 2 Spalten; analog = Analogblock.</summary>
    public static MpModuleVariant SuggestVariant(ProductFamily family, ModuleType type, int channelCount)
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
    public static int ParsedModuleUnits(ModuleType type, int channelCount)
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
    public static List<LabelCell> BuildImportCells(string moduleName, ModuleType? type,
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
                line1 = AddressGenerator.MergeAddressLines(line1, line2);
                line2 = string.Empty;
            }
            cells.Add(new LabelCell { Header = generated.Header, Line1 = line1, Line2 = line2 });
            cursor = AddressGenerator.GetNextStartByte(type.Value, cursor, chunk);
            remaining -= chunk;
        }
        return cells;
    }
}
