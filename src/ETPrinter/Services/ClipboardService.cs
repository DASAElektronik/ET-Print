using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>
/// App-interner Copy-Puffer fuer Label-Kopien (ET200SP) und Modul-Kopien (ET200MP).
/// Modus-exklusiv: Bei Wechsel der Etikettenart wird der andere Buffer geleert.
/// Kein Austausch mit dem Windows-Zwischenablage-Clipboard (app-intern).
/// </summary>
public static class ClipboardService
{
    private static List<LabelCell>? _labelCells;
    private static List<MpModule>? _mpModules;

    public static bool HasLabels => _labelCells is { Count: > 0 };
    public static bool HasModules => _mpModules is { Count: > 0 };

    public static int LabelCount => _labelCells?.Count ?? 0;
    public static int ModuleCount => _mpModules?.Count ?? 0;

    public static void SetLabels(IEnumerable<LabelCell> cells)
    {
        _labelCells = cells.Select(c => c.CloneContent()).ToList();
        _mpModules = null; // Mode-exklusiv
    }

    public static IReadOnlyList<LabelCell> GetLabels() =>
        _labelCells is null ? [] : _labelCells.Select(c => c.CloneContent()).ToList();

    public static void SetModules(IEnumerable<MpModule> modules)
    {
        _mpModules = modules.Select(m => m.CloneContent()).ToList();
        _labelCells = null; // Mode-exklusiv
    }

    public static IReadOnlyList<MpModule> GetModules() =>
        _mpModules is null ? [] : _mpModules.Select(m => m.CloneContent()).ToList();

    public static void Clear()
    {
        _labelCells = null;
        _mpModules = null;
    }
}
