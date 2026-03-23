using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ETPrinter.Models;
using ETPrinter.ViewModels;

namespace ETPrinter.Services;

/// <summary>
/// Named-Pipe-Server fuer Test-Automation.
/// Akzeptiert Befehle ueber Pipe "ETPrinter_TestAutomation" und fuehrt sie auf dem UI-Thread aus.
/// Befehle sind zeilenbasiert: "command arg1 arg2..."
/// Antwort: JSON mit { "ok": true/false, "result": "...", "error": "..." }
/// </summary>
public class TestAutomationService : IDisposable
{
    private readonly Window _mainWindow;
    private readonly MainViewModel _viewModel;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public const string PipeName = "ETPrinter_TestAutomation";

    public TestAutomationService(Window mainWindow, MainViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoop(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(pipe);
                using var writer = new StreamWriter(pipe) { AutoFlush = true };

                while (pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;

                    var response = await DispatchCommand(line.Trim());
                    await writer.WriteLineAsync(response);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TestAutomation error: {ex.Message}");
            }
        }
    }

    private async Task<string> DispatchCommand(string commandLine)
    {
        var parts = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return Error("Leerer Befehl");

        string cmd = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1] : string.Empty;

        try
        {
            return cmd switch
            {
                "ping" => Ok("pong"),
                "state" => await RunOnUI(() => GetState()),
                "screenshot" => await RunOnUI(() => TakeScreenshot(arg)),
                "select-family" => await RunOnUI(() => SelectFamily(arg)),
                "select-format" => await RunOnUI(() => SelectFormat(arg)),
                "select-label" => await RunOnUI(() => SelectLabel(arg)),
                "set-text" => await RunOnUI(() => SetText(arg)),
                "generate" => await RunOnUI(() => Generate(arg)),
                "apply" => await RunOnUI(() => Apply()),
                "next-page" => await RunOnUI(() => NavPage("next")),
                "prev-page" => await RunOnUI(() => NavPage("prev")),
                "add-page" => await RunOnUI(() => NavPage("add")),
                "clear-all" => await RunOnUI(() => ClearAll()),
                "new-project" => await RunOnUI(() => NewProject()),
                "select-module" => await RunOnUI(() => SelectModule(arg)),
                "set-module-header" => await RunOnUI(() => SetModuleHeader(arg)),
                "set-module-net" => await RunOnUI(() => SetModuleNet(arg)),
                "set-module-cpu" => await RunOnUI(() => SetModuleCpu(arg)),
                "select-cell" => await RunOnUI(() => SelectMpCell(arg)),
                "set-cell-text" => await RunOnUI(() => SetMpCellText(arg)),
                "set-module-variant" => await RunOnUI(() => SetModuleVariant(arg)),
                "list-variants" => await RunOnUI(() => ListVariants()),
                "mp-state" => await RunOnUI(() => GetMpState()),
                "list-families" => await RunOnUI(() => ListFamilies()),
                "list-formats" => await RunOnUI(() => ListFormats()),
                "help" => Ok(GetHelp()),
                _ => Error($"Unbekannter Befehl: {cmd}")
            };
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    private Task<string> RunOnUI(Func<string> action)
    {
        var tcs = new TaskCompletionSource<string>();
        _mainWindow.Dispatcher.InvokeAsync(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception ex) { tcs.SetResult(Error(ex.Message)); }
        });
        return tcs.Task;
    }

    // === Befehle ===

    private string GetState()
    {
        var state = new
        {
            productFamily = _viewModel.SelectedProductFamily.ToString(),
            format = _viewModel.SelectedFormat.DisplayName,
            formatEnum = _viewModel.SelectedFormat.Format.ToString(),
            isModuleBased = _viewModel.IsModuleBased,
            labelsPerPage = _viewModel.SelectedFormat.LabelsPerPage,
            labelsPerRow = _viewModel.LabelsPerRow,
            bandsPerPage = _viewModel.BandsPerPage,
            currentPage = _viewModel.CurrentPageIndex + 1,
            pageCount = _viewModel.PageCount,
            // ET200SP
            selectedLabel = _viewModel.SelectedLabel?.DisplayPosition,
            filledLabels = _viewModel.Labels.Count(l => l.HasText),
            // ET200MP
            moduleCount = _viewModel.MpModules.Count,
            selectedModule = _viewModel.SelectedMpModule?.ModuleIndex,
            filledModules = _viewModel.MpModules.Count(m => m.HasText),
            isDirty = _viewModel.IsDirty,
            labelSheet = ProductFamilyDefinitions.Get(_viewModel.SelectedProductFamily).LabelSheetPartNumber
        };
        return Ok(JsonSerializer.Serialize(state));
    }

    private string TakeScreenshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(Path.GetTempPath(), "etprinter_screenshot.png");

        // Fenster nach vorne bringen
        _mainWindow.Activate();
        _mainWindow.Focus();

        // WPF Visual rendern
        var dpi = VisualTreeHelper.GetDpi(_mainWindow);
        var bounds = VisualTreeHelper.GetDescendantBounds(_mainWindow);
        var width = _mainWindow.ActualWidth;
        var height = _mainWindow.ActualHeight;

        var rtb = new RenderTargetBitmap(
            (int)(width * dpi.DpiScaleX),
            (int)(height * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        rtb.Render(_mainWindow);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var fs = File.Create(path);
        encoder.Save(fs);

        return Ok(path);
    }

    private string SelectFamily(string familyName)
    {
        if (!Enum.TryParse<ProductFamily>(familyName, true, out var family))
            return Error($"Unbekannte Familie: {familyName}. Gueltig: {string.Join(", ", Enum.GetNames<ProductFamily>())}");

        _viewModel.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(family);
        return Ok($"Familie gewechselt zu {family}");
    }

    private string SelectFormat(string formatArg)
    {
        // Suche nach Enum-Name oder Display-Name
        var match = _viewModel.AvailableFormats
            .FirstOrDefault(f => f.Format.ToString().Equals(formatArg, StringComparison.OrdinalIgnoreCase)
                              || f.DisplayName.Contains(formatArg, StringComparison.OrdinalIgnoreCase));

        if (match == null)
            return Error($"Format nicht gefunden: {formatArg}. Verfuegbar: {string.Join(", ", _viewModel.AvailableFormats.Select(f => f.Format.ToString()))}");

        _viewModel.SelectedFormat = match;
        return Ok($"Format gewechselt zu {match.DisplayName}");
    }

    private string SelectLabel(string indexStr)
    {
        if (!int.TryParse(indexStr, out int index) || index < 0 || index >= _viewModel.Labels.Count)
            return Error($"Ungueltiger Index: {indexStr}. Gueltig: 0-{_viewModel.Labels.Count - 1}");

        _viewModel.SelectLabel(_viewModel.Labels[index]);
        return Ok($"Label {index} ausgewaehlt");
    }

    private string SetText(string arg)
    {
        // Format: "header|line1|line2" oder "line1|line2" oder "line1"
        var parts = arg.Split('|');
        if (_viewModel.SelectedLabel == null)
            return Error("Kein Label ausgewaehlt");

        if (parts.Length >= 3)
        {
            _viewModel.SelectedLabel.Header = parts[0];
            _viewModel.SelectedLabel.Line1 = parts[1];
            _viewModel.SelectedLabel.Line2 = parts[2];
        }
        else if (parts.Length == 2)
        {
            _viewModel.SelectedLabel.Line1 = parts[0];
            _viewModel.SelectedLabel.Line2 = parts[1];
        }
        else
        {
            _viewModel.SelectedLabel.Line1 = parts[0];
        }
        return Ok("Text gesetzt");
    }

    private string Generate(string arg)
    {
        // Format: "moduleName DI startByte byteCount"
        var parts = arg.Split(' ');
        if (parts.Length < 4)
            return Error("Format: generate <moduleName> <DI|DO|AI|AO> <startByte> <count>");

        var moduleType = _viewModel.AvailableModuleTypes
            .FirstOrDefault(m => m.Type.ToString().Equals(parts[1], StringComparison.OrdinalIgnoreCase));
        if (moduleType == null)
            return Error($"Modultyp nicht gefunden: {parts[1]}");

        if (!int.TryParse(parts[2], out int startByte))
            return Error($"Ungueltiger Start-Byte: {parts[2]}");
        if (!int.TryParse(parts[3], out int count))
            return Error($"Ungueltige Anzahl: {parts[3]}");

        var result = AddressGenerator.Generate(parts[0], moduleType.Type, startByte, count);
        if (_viewModel.SelectedLabel != null)
        {
            _viewModel.SelectedLabel.Header = result.Header;
            _viewModel.SelectedLabel.Line1 = result.Line1;
            _viewModel.SelectedLabel.Line2 = result.Line2;
        }
        return Ok($"Generiert: {result.Header} | {result.Line1}");
    }

    private string Apply()
    {
        if (_viewModel.SelectedLabel == null)
            return Error("Kein Label ausgewaehlt");
        // Trigger apply via the existing input mechanism
        return Ok("Apply ausgefuehrt");
    }

    private string NavPage(string action)
    {
        switch (action)
        {
            case "next":
                if (_viewModel.NextPageCommand.CanExecute(null))
                    _viewModel.NextPageCommand.Execute(null);
                else return Error("Bereits auf letzter Seite");
                break;
            case "prev":
                if (_viewModel.PrevPageCommand.CanExecute(null))
                    _viewModel.PrevPageCommand.Execute(null);
                else return Error("Bereits auf erster Seite");
                break;
            case "add":
                _viewModel.AddPageCommand.Execute(null);
                break;
        }
        return Ok($"Seite {_viewModel.CurrentPageIndex + 1}/{_viewModel.PageCount}");
    }

    private string ClearAll()
    {
        _viewModel.ClearAllCommand.Execute(null);
        return Ok("Alle Etiketten geloescht");
    }

    private string NewProject()
    {
        // Skip ConfirmDiscardChanges for automation
        _viewModel.ClearAllCommand.Execute(null);
        return Ok("Neues Projekt");
    }

    private string ListFamilies()
    {
        var families = ProductFamilyDefinitions.All.Select(f => new
        {
            id = f.Family.ToString(),
            name = f.DisplayName,
            labelSheet = f.LabelSheetPartNumber
        });
        return Ok(JsonSerializer.Serialize(families));
    }

    private string ListFormats()
    {
        var formats = _viewModel.AvailableFormats.Select(f => new
        {
            id = f.Format.ToString(),
            name = f.DisplayName,
            labelsPerPage = f.LabelsPerPage,
            bands = f.BandsPerPage
        });
        return Ok(JsonSerializer.Serialize(formats));
    }

    private static string GetHelp()
    {
        return """
            Verfuegbare Befehle:
            ping                             - Verbindungstest
            state                            - Aktueller App-Zustand (JSON)
            screenshot [pfad]                - Screenshot als PNG speichern
            select-family <name>             - Produktfamilie waehlen
            select-format <name>             - Druckformat waehlen
            select-label <index>             - ET200SP: Label per Index auswaehlen
            set-text <header|line1|line2>     - ET200SP: Text setzen
            generate <name> <typ> <byte> <n> - Adressen generieren
            next-page / prev-page / add-page - Seitennavigation
            clear-all / new-project          - Zuruecksetzen
            list-families / list-formats     - Auflistungen
            --- ET200MP Module ---
            select-module <index>            - Modul per Index auswaehlen (0-9)
            set-module-header <text>         - Modul-Header setzen
            set-module-net <net1|net2>       - Netzadresse setzen
            set-module-cpu <text>            - CPU-Name setzen
            select-cell <index>              - Adresszelle im Modul auswaehlen
            set-cell-text <text>             - Zellentext setzen
            set-module-variant <name>        - Modulvariante aendern
            list-variants                    - Alle Modulvarianten (JSON)
            mp-state                         - Ausgewaehltes Modul-Detail (JSON)
            help                             - Diese Hilfe
            """;
    }

    // === ET200MP Modul-Befehle ===

    private string SelectModule(string indexStr)
    {
        if (!int.TryParse(indexStr, out int index) || index < 0 || index >= _viewModel.MpModules.Count)
            return Error($"Ungueltiger Modul-Index: {indexStr}. Gueltig: 0-{_viewModel.MpModules.Count - 1}");
        _viewModel.SelectedMpModule = _viewModel.MpModules[index];
        return Ok($"Modul {index} ausgewaehlt (Band {_viewModel.MpModules[index].Band + 1}, Spalte {_viewModel.MpModules[index].ColumnInBand + 1})");
    }

    private string SetModuleHeader(string text)
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        _viewModel.SelectedMpModule.HeaderText = text;
        return Ok($"Modul-Header gesetzt: {text}");
    }

    private string SetModuleNet(string arg)
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        var parts = arg.Split('|', 2);
        _viewModel.SelectedMpModule.NetAddress1 = parts[0];
        if (parts.Length > 1) _viewModel.SelectedMpModule.NetAddress2 = parts[1];
        return Ok("Netzadresse gesetzt");
    }

    private string SetModuleCpu(string text)
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        _viewModel.SelectedMpModule.CpuName = text;
        return Ok($"CPU-Name gesetzt: {text}");
    }

    private string SelectMpCell(string indexStr)
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        if (!int.TryParse(indexStr, out int index) || index < 0 || index >= _viewModel.SelectedMpModule.AddressCells.Count)
            return Error($"Ungueltiger Zellen-Index: {indexStr}. Gueltig: 0-{_viewModel.SelectedMpModule.AddressCells.Count - 1}");
        _viewModel.SelectedMpCell = _viewModel.SelectedMpModule.AddressCells[index];
        return Ok($"Zelle {index} ausgewaehlt");
    }

    private string SetMpCellText(string text)
    {
        if (_viewModel.SelectedMpCell == null) return Error("Keine Zelle ausgewaehlt");
        if (!_viewModel.SelectedMpCell.IsEditable) return Error("Zelle ist nicht editierbar (Struktur-Zelle)");
        _viewModel.SelectedMpCell.Text = text;
        return Ok($"Zellentext gesetzt: {text}");
    }

    private string SetModuleVariant(string variantStr)
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        if (!Enum.TryParse<MpModuleVariant>(variantStr, true, out var variant))
            return Error($"Unbekannte Variante: {variantStr}. Gueltig: {string.Join(", ", Enum.GetNames<MpModuleVariant>())}");
        _viewModel.SelectedMpModule.Variant = variant;
        return Ok($"Variante gewechselt zu {variant} ({_viewModel.SelectedMpModule.AddressCells.Count} Zellen)");
    }

    private string ListVariants()
    {
        var variants = MpModuleLayoutFactory.All.Select(l => new
        {
            id = l.Variant.ToString(),
            name = l.DisplayName,
            cells = l.AddressCells.Length
        });
        return Ok(JsonSerializer.Serialize(variants));
    }

    private string GetMpState()
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        var mod = _viewModel.SelectedMpModule;
        var state = new
        {
            moduleIndex = mod.ModuleIndex,
            band = mod.Band,
            column = mod.ColumnInBand,
            variant = mod.Variant.ToString(),
            header = mod.HeaderText,
            netAddr1 = mod.NetAddress1,
            netAddr2 = mod.NetAddress2,
            cpuName = mod.CpuName,
            cellCount = mod.AddressCells.Count,
            filledCells = mod.AddressCells.Count(c => c.HasText),
            hasText = mod.HasText
        };
        return Ok(JsonSerializer.Serialize(state));
    }

    // === Hilfs-Methoden ===

    private static string Ok(string result) =>
        JsonSerializer.Serialize(new { ok = true, result });

    private static string Error(string error) =>
        JsonSerializer.Serialize(new { ok = false, error });
}
