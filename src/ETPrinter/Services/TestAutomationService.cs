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
        // Hinweis: Dispose wird von App.OnExit auf dem UI-Thread aufgerufen.
        // Falls ListenLoop gerade einen RunOnUI-Dispatch wartet, kann Wait() bis zum Timeout
        // blockieren, weil der UI-Dispatcher durch diesen Aufruf nicht pumpt. Timeout = Obergrenze.
        try
        {
            _cts?.Cancel();
            try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { /* OperationCanceledException erwartet */ }
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
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
                Log.Error($"Pipe-Fehler: {ex.GetType().Name}", ex);
                // Backoff gegen Busy-Spin: schlaegt schon die Pipe-Erzeugung fehl
                // (z.B. Pipe-Name durch zweite Instanz belegt), wuerde die Schleife
                // sonst tausendfach pro Sekunde loggen und die Log-Rotation fluten.
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); }
                catch (OperationCanceledException) { break; }
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
            if (cmd == "ping") return Ok("pong");
            if (cmd == "help") return Ok(GetHelp());
            if (!Commands.TryGetValue(cmd, out var entry))
                return Error($"Unbekannter Befehl: {cmd}");
            return await RunOnUI(() => entry.Handler(this, arg));
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    private sealed record CommandInfo(string Usage, string Description,
        Func<TestAutomationService, string, string> Handler);

    // Eine Dispatch-Tabelle fuer Ausfuehrung UND Hilfe — frueher listete die
    // handgepflegte Hilfe 8 Befehle nicht.
    private static readonly Dictionary<string, CommandInfo> Commands = new()
    {
        ["state"] = new("state", "Aktueller App-Zustand (JSON)", (s, _) => s.GetState()),
        ["screenshot"] = new("screenshot [pfad]", "Screenshot des Fensters als PNG", (s, a) => s.TakeScreenshot(a)),
        ["render-print"] = new("render-print <ordner>", "Alle Druckseiten ohne Dialog als PNG rendern (page_NN.png)", (s, a) => s.RenderPrint(a)),
        ["render-calibration"] = new("render-calibration <ordner>", "Kalibrierseite ohne Dialog als PNG rendern", (s, a) => s.RenderCalibration(a)),
        ["zoom"] = new("zoom <faktor>", "Vorschau-Zoom 0.3-5.0", (s, a) => s.SetZoom(a)),
        ["maximize"] = new("maximize", "Fenster maximieren", (s, _) => s.MaximizeWindow()),
        ["resize"] = new("resize <b>x<h>", "Fenstergroesse setzen", (s, a) => s.ResizeWindow(a)),
        ["select-family"] = new("select-family <name>", "Produktfamilie waehlen", (s, a) => s.SelectFamily(a)),
        ["select-format"] = new("select-format <name>", "Druckformat waehlen", (s, a) => s.SelectFormat(a)),
        ["select-label"] = new("select-label <index>", "ET200SP: Etikett per Index (0-basiert)", (s, a) => s.SelectLabel(a)),
        ["set-text"] = new("set-text <header|z1|z2>", "ET200SP: Text des Etiketts setzen", (s, a) => s.SetText(a)),
        ["generate"] = new("generate <name> <typ> <byte> <n>", "ET200SP: Adressen direkt ins Etikett", (s, a) => s.Generate(a)),
        ["apply"] = new("apply", "Uebertragen-Button (Manuell-Tab)", (s, _) => s.Apply()),
        ["set-input"] = new("set-input <header|z1|z2>", "Manuell-Tab Eingabefelder setzen", (s, a) => s.SetInput(a)),
        ["next-page"] = new("next-page", "Naechste Seite", (s, _) => s.NavPage("next")),
        ["prev-page"] = new("prev-page", "Vorherige Seite", (s, _) => s.NavPage("prev")),
        ["add-page"] = new("add-page", "Seite hinzufuegen", (s, _) => s.NavPage("add")),
        ["remove-page"] = new("remove-page", "Aktuelle Seite entfernen", (s, _) => s.NavPage("remove")),
        ["clear-all"] = new("clear-all", "Alle Etiketten/Module loeschen", (s, _) => s.ClearAll()),
        ["new-project"] = new("new-project", "Neues Projekt (ohne Rueckfrage)", (s, _) => s.NewProject()),
        ["select-module"] = new("select-module <index>", "ET200MP: Modul per Index", (s, a) => s.SelectModule(a)),
        ["set-module-header"] = new("set-module-header <text>", "ET200MP: Header setzen", (s, a) => s.SetModuleHeader(a)),
        ["set-module-net"] = new("set-module-net <n1|n2>", "ET200MP: Netzadresse setzen", (s, a) => s.SetModuleNet(a)),
        ["set-module-cpu"] = new("set-module-cpu <text>", "ET200MP: CPU-Name setzen", (s, a) => s.SetModuleCpu(a)),
        ["select-cell"] = new("select-cell <index>", "ET200MP: Adresszelle waehlen", (s, a) => s.SelectMpCell(a)),
        ["set-cell-text"] = new("set-cell-text <text>", "ET200MP: Zellentext setzen", (s, a) => s.SetMpCellText(a)),
        ["set-module-variant"] = new("set-module-variant <name>", "ET200MP: Layout-Variante", (s, a) => s.SetModuleVariant(a)),
        ["set-module-article"] = new("set-module-article <artnr>", "ET200MP: Katalog-Artikel (leer/custom = benutzerdefiniert)", (s, a) => s.SetModuleArticle(a)),
        ["list-variants"] = new("list-variants", "Alle Modulvarianten (JSON)", (s, _) => s.ListVariants()),
        ["mp-state"] = new("mp-state", "Ausgewaehltes Modul (JSON)", (s, _) => s.GetMpState()),
        ["set-generator"] = new("set-generator <name> <typ> <byte> <n>", "Generator-Felder setzen", (s, a) => s.SetGenerator(a)),
        ["set-font"] = new("set-font <groesse> <fett 0/1> <kursiv 0/1> [schriftart]", "Schrift-Eingabefelder setzen (Live-Apply)", (s, a) => s.SetFont(a)),
        ["import-file"] = new("import-file <pfad.csv|.xlsx>", "CSV/Excel ohne Dialog importieren (ab ausgewaehltem Etikett)", (s, a) => s.ImportFile(a)),
        ["import-lines"] = new("import-lines <pfad.txt>", "Textzeilen wie ein Schaltplan-PDF parsen und importieren (SP: Etiketten, MP: Module)", (s, a) => s.ImportLines(a)),
        ["toggle-print"] = new("toggle-print", "Druckflag des ausgewaehlten Etiketts/Moduls umschalten", (s, _) => s.TogglePrint()),
        ["print-state"] = new("print-state", "Druckentscheidung: Seiten im Dokument + druckbare Etiketten/Module je Seite (JSON)", (s, _) => s.GetPrintState()),
        ["trigger-generate"] = new("trigger-generate", "Generieren + Uebertragen", (s, _) => s.TriggerGenerate()),
        ["save-project"] = new("save-project <pfad.etprint>", "Projekt speichern", (s, a) => s.SaveProject(a)),
        ["load-project"] = new("load-project <pfad.etprint>", "Projekt laden", (s, a) => s.LoadProject(a)),
        ["list-families"] = new("list-families", "Produktfamilien (JSON)", (s, _) => s.ListFamilies()),
        ["list-formats"] = new("list-formats", "Formate der Familie (JSON)", (s, _) => s.ListFormats()),
        ["quit"] = new("quit", "App beenden (verwirft Aenderungen)", (s, _) => s.Quit()),
    };

    private Task<string> RunOnUI(Func<string> action)
    {
        var tcs = new TaskCompletionSource<string>();
        var operation = _mainWindow.Dispatcher.InvokeAsync(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception ex)
            {
                Log.Error($"Dispatch-Exception: {ex.GetType().Name}", ex);
                tcs.SetResult(Error($"{ex.GetType().Name}: {ex.Message}"));
            }
        });
        // Beim App-Shutdown bricht der Dispatcher wartende Operationen ab —
        // ohne diesen Hook bliebe die TCS unerfuellt und Dispose() hinge
        // die vollen 2 Sekunden auf dem verwaisten ListenLoop-Task.
        operation.Aborted += (_, _) => tcs.TrySetResult(Error("Abgebrochen (App-Shutdown)"));
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
            printableModules = _viewModel.MpModules.Count(m => m.HasPrintableContent),
            isDirty = _viewModel.IsDirty,
            filePath = _viewModel.CurrentFilePath ?? "",
            printGridLines = _viewModel.PrintGridLines,
            availableVariants = _viewModel.AvailableMpVariants.Select(v => v.Variant.ToString()).ToArray(),
            availableArticles = _viewModel.AvailableMpArticles.Where(e => e.ArticleNo != "").Select(e => e.ArticleNo).ToArray(),
            labelSheet = ProductFamilyDefinitions.Get(_viewModel.SelectedProductFamily).LabelSheetPartNumber
        };
        return Ok(JsonSerializer.Serialize(state));
    }

    private string TakeScreenshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(Path.GetTempPath(), "etprinter_screenshot.png");

        // Fenster nach vorne bringen, entprellte Vorschau-Renders sofort ausfuehren
        _mainWindow.Activate();
        _mainWindow.Focus();
        (_mainWindow as MainWindow)?.FlushPreview();

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

        // Automation ist absichtlich — keine modale Inhaltsverlust-Rueckfrage
        // (wuerde headless haengen).
        _viewModel.SuppressContentLossConfirm = true;
        try { _viewModel.SelectedProductFamilyInfo = ProductFamilyDefinitions.Get(family); }
        finally { _viewModel.SuppressContentLossConfirm = false; }
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

        _viewModel.SuppressContentLossConfirm = true;
        try { _viewModel.SelectedFormat = match; }
        finally { _viewModel.SuppressContentLossConfirm = false; }
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
        if (!_viewModel.ApplyCommand.CanExecute(null))
            return Error("Kein Label/Modul ausgewaehlt");
        _viewModel.ApplyCommand.Execute(null);
        return Ok(_viewModel.StatusMessage);
    }

    private string SetInput(string arg)
    {
        // Format: "header|line1|line2" oder "line1|line2" oder "line1"
        var parts = arg.Split('|');
        if (parts.Length >= 3)
        {
            _viewModel.InputHeader = parts[0];
            _viewModel.InputLine1 = parts[1];
            _viewModel.InputLine2 = parts[2];
        }
        else if (parts.Length == 2)
        {
            _viewModel.InputLine1 = parts[0];
            _viewModel.InputLine2 = parts[1];
        }
        else
        {
            _viewModel.InputLine1 = parts[0];
        }
        return Ok("Eingabefelder gesetzt");
    }

    private string SetFont(string arg)
    {
        var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !int.TryParse(parts[0], out int size))
            return Error("Format: set-font <groesse> <fett 0/1> <kursiv 0/1> [schriftart]");
        _viewModel.InputFontSize = size;
        _viewModel.InputIsBold = parts[1] == "1";
        _viewModel.InputIsItalic = parts[2] == "1";
        if (parts.Length > 3) _viewModel.InputFontFamily = string.Join(' ', parts.Skip(3));
        return Ok($"Schrift: {size}pt fett={parts[1]} kursiv={parts[2]}");
    }

    private string GetPrintState()
    {
        var document = _viewModel.BuildPrintDocument();
        var state = new
        {
            pagesInDocument = document.Pages.Count,
            printablePerPage = _viewModel.PrintablePerPage()
        };
        return Ok(JsonSerializer.Serialize(state));
    }

    private string RenderPrint(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return Error("Ordner angeben");
        var document = _viewModel.BuildPrintDocument();
        int pages = PrintService.RenderToPng(document, dir);
        return Ok(JsonSerializer.Serialize(new { pages, dir = Path.GetFullPath(dir) }));
    }

    private string RenderCalibration(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return Error("Ordner angeben");
        var document = _viewModel.BuildCalibrationDocument();
        int pages = PrintService.RenderToPng(document, dir);
        return Ok(JsonSerializer.Serialize(new { pages, dir = Path.GetFullPath(dir) }));
    }

    private string Quit()
    {
        _viewModel.DiscardChangesForShutdown();
        // Antwort geht vor dem Schliessen raus — Close() erst im naechsten Dispatcher-Durchlauf
        _mainWindow.Dispatcher.BeginInvoke(new Action(() => _mainWindow.Close()));
        return Ok("Beende");
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
            case "remove":
                if (!_viewModel.RemovePageCommand.CanExecute(null))
                    return Error("Letzte Seite kann nicht entfernt werden");
                // Rueckfrage unterdruecken (headless)
                _viewModel.SuppressContentLossConfirm = true;
                try { _viewModel.RemovePageCommand.Execute(null); }
                finally { _viewModel.SuppressContentLossConfirm = false; }
                break;
        }
        return Ok($"Seite {_viewModel.CurrentPageIndex + 1}/{_viewModel.PageCount}");
    }

    private string ClearAll()
    {
        _viewModel.SuppressContentLossConfirm = true;
        try { _viewModel.ClearAllCommand.Execute(null); }
        finally { _viewModel.SuppressContentLossConfirm = false; }
        return Ok("Alle Etiketten geloescht");
    }

    private string ImportFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Error($"Datei nicht gefunden: {path}");
        _viewModel.SuppressContentLossConfirm = true;
        try
        {
            int n = _viewModel.ImportCellsFromFile(path);
            return Ok(JsonSerializer.Serialize(new { imported = n }));
        }
        catch (Exception ex) { return Error($"Importfehler: {ex.Message}"); }
        finally { _viewModel.SuppressContentLossConfirm = false; }
    }

    private string ImportLines(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Error($"Datei nicht gefunden: {path}");
        _viewModel.SuppressContentLossConfirm = true;
        try
        {
            int n = _viewModel.ImportParsedLines(File.ReadAllLines(path));
            return Ok(JsonSerializer.Serialize(new { modules = n }));
        }
        catch (Exception ex) { return Error($"Importfehler: {ex.Message}"); }
        finally { _viewModel.SuppressContentLossConfirm = false; }
    }

    private string TogglePrint()
    {
        if (!_viewModel.TogglePrintCommand.CanExecute(null)) return Error("Keine Auswahl");
        _viewModel.TogglePrintCommand.Execute(null);
        return Ok(_viewModel.StatusMessage);
    }

    private string NewProject()
    {
        // Echtes "Neu" (Familie/Format/Settings/Dateipfad zurueck), ohne Rueckfrage
        _viewModel.NewProjectWithoutConfirm();
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
        var sb = new System.Text.StringBuilder("Verfuegbare Befehle:\n");
        sb.Append("ping".PadRight(40)).Append("- Verbindungstest\n");
        foreach (var (_, info) in Commands.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            sb.Append(info.Usage.PadRight(40)).Append("- ").Append(info.Description).Append('\n');
        sb.Append("help".PadRight(40)).Append("- Diese Hilfe");
        return sb.ToString();
    }

    // === ET200MP Modul-Befehle ===

    private string SelectModule(string indexStr)
    {
        if (!int.TryParse(indexStr, out int index) || index < 0 || index >= _viewModel.MpModules.Count)
            return Error($"Ungueltiger Modul-Index: {indexStr}. Gueltig: 0-{_viewModel.MpModules.Count - 1}");
        _viewModel.SelectedMpModule = _viewModel.MpModules[index];
        var info = ProductFamilyDefinitions.Get(_viewModel.SelectedFormat.Family);
        return Ok($"Modul {index} ausgewaehlt (Spalte {info.ColumnOf(index) + 1}, Band {info.BandOf(index) + 1})");
    }

    private string SetModuleArticle(string articleNo)
    {
        if (_viewModel.SelectedMpModule == null) return Error("Kein Modul ausgewaehlt");
        // Nur Artikel der aktiven Familie — sonst bekaeme das Modul eine Variante,
        // die die Familie gar nicht anbietet (25mm-Artikel in einer 35mm-Seite).
        var available = _viewModel.AvailableMpArticles;
        var entry = available.FirstOrDefault(e => e.ArticleNo == articleNo && e.ArticleNo != "");
        if (entry is null && !string.IsNullOrEmpty(articleNo) && articleNo != "custom")
            return Error($"Unbekannter Artikel fuer diese Familie: {articleNo}. Verfuegbar: " +
                string.Join(", ", available.Where(e => e.ArticleNo != "").Select(e => e.ArticleNo)));
        _viewModel.SelectedMpArticle = entry ?? MpModuleCatalog.CustomEntry;
        return Ok($"Modul-Artikel gesetzt: {(entry?.DisplayName ?? "Benutzerdefiniert")} (Variante {_viewModel.SelectedMpModule.Variant})");
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
            column = mod.ModuleIndex,
            variant = mod.Variant.ToString(),
            article = mod.ArticleNumber ?? "",
            ioType = mod.IoType.ToString(),
            header = mod.HeaderText,
            netAddr1 = mod.NetAddress1,
            netAddr2 = mod.NetAddress2,
            cpuName = mod.CpuName,
            cellCount = mod.AddressCells.Count,
            editableCells = mod.AddressCells.Count(c => c.IsEditable),
            filledCells = mod.AddressCells.Count(c => c.HasText),
            selectedCell = _viewModel.SelectedMpCell?.CellIndex,
            fontSize = mod.FontSize,
            isBold = mod.IsBold,
            isItalic = mod.IsItalic,
            fontFamily = mod.FontFamily,
            isPrintEnabled = mod.IsPrintEnabled,
            hasPrintableContent = mod.HasPrintableContent,
            // Zelltexte in Zellreihenfolge (fuer Generator-Verifikation)
            cellTexts = mod.AddressCells.Select(c => c.IsEditable ? c.Text : "#" + c.Label).ToArray(),
            // Struktur-Klemmen als "row:label" (nur beschriftete), z.B. "8:1L+"
            structureLabels = mod.AddressCells
                .Where(c => !c.IsEditable && !string.IsNullOrEmpty(c.Label))
                .Select(c => $"{c.StartCol}/{c.StartRow}:{c.Label}")
                .ToArray(),
            hasText = mod.HasText
        };
        return Ok(JsonSerializer.Serialize(state));
    }

    // === Fenster-Steuerung ===

    private string SetZoom(string arg)
    {
        if (!double.TryParse(arg, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double zoom) || zoom < 0.3 || zoom > 5.0)
            return Error($"Ungueltiger Zoom: {arg}. Gueltig: 0.3-5.0");
        _viewModel.Zoom = zoom;
        return Ok($"Zoom: {zoom}");
    }

    private string MaximizeWindow()
    {
        _mainWindow.WindowState = WindowState.Maximized;
        return Ok("Fenster maximiert");
    }

    private string ResizeWindow(string arg)
    {
        var parts = arg.Split('x', 'X');
        if (parts.Length != 2
            || !double.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out double w)
            || !double.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out double h))
            return Error("Format: resize <breite>x<hoehe> (z.B. 1920x1080)");
        if (double.IsNaN(w) || double.IsNaN(h) || w < 400 || h < 300 || w > 10000 || h > 10000)
            return Error("Ungueltige Dimensionen (400..10000 x 300..10000)");
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Width = w;
        _mainWindow.Height = h;
        return Ok($"Fenster: {w}x{h}");
    }

    private static string? ValidateProjectPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "Pfad angeben";
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"Ungueltiger Pfad: {ex.Message}";
        }
        if (!string.Equals(Path.GetExtension(fullPath), ".etprint", StringComparison.OrdinalIgnoreCase))
            return "Nur .etprint-Dateien erlaubt";
        return null;
    }

    // === Generator + Save/Load ===

    private string SetGenerator(string arg)
    {
        // Format: "moduleName typ startByte count"
        var parts = arg.Split(' ');
        if (parts.Length < 4)
            return Error("Format: set-generator <moduleName> <DI|DO|AI|AO> <startByte> <count>");

        _viewModel.GenModuleName = parts[0];
        var moduleType = _viewModel.AvailableModuleTypes
            .FirstOrDefault(m => m.Type.ToString().Equals(parts[1], StringComparison.OrdinalIgnoreCase));
        if (moduleType == null)
            return Error($"Modultyp nicht gefunden: {parts[1]}");
        _viewModel.GenModuleType = moduleType;

        if (int.TryParse(parts[2], out int startByte))
            _viewModel.GenStartByte = startByte;
        if (int.TryParse(parts[3], out int count))
            _viewModel.GenCount = count;

        return Ok($"Generator: {parts[0]} {parts[1]} Byte={startByte} Count={count}");
    }

    private string TriggerGenerate()
    {
        if (!_viewModel.GenerateAndApplyCommand.CanExecute(null))
            return Error("GenerateAndApply nicht verfuegbar (kein Label/Modul ausgewaehlt?)");
        _viewModel.GenerateAndApplyCommand.Execute(null);
        return Ok(_viewModel.StatusMessage);
    }

    private string SaveProject(string path)
    {
        var validationError = ValidateProjectPath(path);
        if (validationError != null)
            return Error($"save-project: {validationError}");
        try
        {
            // BuildProject serialisiert ALLE Seiten (_allPages/_allMpPages) —
            // frueher wurde hier nur die sichtbare Seite geschrieben (Datenverlust).
            ProjectService.Save(_viewModel.BuildProject(), path);
            return Ok($"Gespeichert: {path}");
        }
        catch (Exception ex)
        {
            return Error($"Speicherfehler: {ex.Message}");
        }
    }

    private string LoadProject(string path)
    {
        var validationError = ValidateProjectPath(path);
        if (validationError != null)
            return Error($"load-project: {validationError}");
        if (!File.Exists(path))
            return Error($"Datei nicht gefunden: {path}");
        try
        {
            // Vollstaendige Ladelogik aus dem ViewModel wiederverwenden —
            // laedt alle Seiten, SP-Labels UND Einstellungen (frueher wurde
            // hier nur MpPages[0] geladen, SP-Projekte kamen leer an).
            var project = ProjectService.Load(path);
            _viewModel.ApplyLoadedProject(project, path);
            return Ok($"Geladen: {path} ({_viewModel.PageCount} Seiten)");
        }
        catch (Exception ex)
        {
            return Error($"Ladefehler: {ex.Message}");
        }
    }

    // === Hilfs-Methoden ===

    private static string Ok(string result) =>
        JsonSerializer.Serialize(new { ok = true, result });

    private static string Error(string error) =>
        JsonSerializer.Serialize(new { ok = false, error });
}
