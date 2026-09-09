using System.IO;
using System.Text.Json;

namespace ETPrinter.Services;

/// <summary>Fensterzustand zwischen zwei Starts (kein Neujustieren bei jedem Start).</summary>
public record UiState
{
    public double Left { get; init; } = double.NaN;
    public double Top { get; init; } = double.NaN;
    public double Width { get; init; } = 1400;
    public double Height { get; init; } = 900;
    public bool IsMaximized { get; init; }
    public double Zoom { get; init; } = double.NaN;      // NaN = beim Start "an Fenster anpassen"
    public double LeftPanelWidth { get; init; } = 300;   // GridSplitter-Position
}

public static class UiStateService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ETPrinter", "ui.json");

    public static UiState Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<UiState>(File.ReadAllText(FilePath)) ?? new UiState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warn($"{ex.GetType().Name}: {ex.Message}");
        }
        return new UiState();
    }

    public static void Save(UiState state)
    {
        try
        {
            ProjectService.WriteAtomic(FilePath,
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }),
                keepBackup: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Prueft, ob eine gespeicherte Fensterposition noch auf einen sichtbaren Bildschirm
    /// zeigt (abgesteckter Zweitmonitor). Liefert die bereinigten Werte oder NaN
    /// (= Windows waehlt die Position).
    /// </summary>
    public static (double left, double top, double width, double height) Sanitize(
        UiState s, double virtualLeft, double virtualTop, double virtualWidth, double virtualHeight,
        double minWidth, double minHeight)
    {
        double width = double.IsNaN(s.Width) || s.Width < minWidth ? minWidth : Math.Min(s.Width, virtualWidth);
        double height = double.IsNaN(s.Height) || s.Height < minHeight ? minHeight : Math.Min(s.Height, virtualHeight);

        double left = s.Left, top = s.Top;
        if (double.IsNaN(left) || double.IsNaN(top))
            return (double.NaN, double.NaN, width, height);

        // Mindestens 100 x 50 px des Fensters muessen im virtuellen Bildschirm liegen
        double right = virtualLeft + virtualWidth;
        double bottom = virtualTop + virtualHeight;
        bool visible = left + 100 <= right && left + width - 100 >= virtualLeft
                    && top + 50 <= bottom && top >= virtualTop - 10;
        return visible ? (left, top, width, height) : (double.NaN, double.NaN, width, height);
    }
}
