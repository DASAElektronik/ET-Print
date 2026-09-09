using System.Text.RegularExpressions;
using ETPrinter.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ETPrinter.Services;

/// <summary>
/// Erkennt Module und SPS-Adressen in Schaltplan-PDFs (EPLAN/WSCAD-Export).
/// Zeilenweise: eine Zeile mit Modultyp (DI/DO/DQ/AI/AO/AQ + Kanalzahl) und BMK
/// (+/=...) eroeffnet ein Modul, alle folgenden Adressen gehoeren dazu.
/// Deutsche (E/A/EW/AW) und englische (I/Q/IW/QW) Mnemonics werden erkannt.
/// </summary>
public static class SchematicParserService
{
    // NonBacktracking + Timeout schuetzen gegen pathologische PDF-Inhalte.
    // NonBacktracking ist inkompatibel mit Compiled und mit Lookarounds — die
    // Wortgrenze vor dem Mnemonic ist deshalb als Capture-Gruppe modelliert.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    // Kein trailing \b: reale Typbezeichnungen wie "DI 8x24VDC ST" haben nach der
    // Kanalzahl direkt Buchstaben ("x24VDC"), Ziffer->Buchstabe ist keine Wortgrenze.
    private static readonly Regex ModuleTypePattern = new(
        @"(^|[^A-Za-z0-9])(DI|DO|DQ|AI|AO|AQ)\s*\d+",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking,
        RegexTimeout);

    // Gruppe 2 = Adresse. Vor dem Mnemonic darf kein Buchstabe/keine Ziffer stehen:
    // "SIZE 1.5", "TYPE 2.0" oder "NEW 12" lieferten frueher Phantomadressen.
    private static readonly Regex DigitalAddressPattern = new(
        @"(^|[^A-Za-z0-9])([EAIQ]\s*\d+\.\d)",
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex AnalogAddressPattern = new(
        @"(^|[^A-Za-z0-9])([EAIQ]W\s*\d+)",
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex ModuleBmkPattern = new(
        @"[+=][\w.\-]+",
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private static readonly Regex StartBytePattern = new(
        @"\d+",
        RegexOptions.NonBacktracking,
        RegexTimeout);

    private const double LineToleranceY = 3.0;
    private const double ColumnGapX = 50.0;

    public static SchematicParseResult Parse(string pdfFilePath)
    {
        var result = new SchematicParseResult();

        try
        {
            using var document = PdfDocument.Open(pdfFilePath);
            result.PagesScanned = document.NumberOfPages;

            for (int pageIndex = 1; pageIndex <= document.NumberOfPages; pageIndex++)
            {
                try
                {
                    var page = document.GetPage(pageIndex);
                    var textBlocks = ExtractTextBlocks(page);
                    var lines = GroupIntoLines(textBlocks).Select(l => l.GetFullText());
                    result.Modules.AddRange(ParseLines(lines));
                }
                catch (Exception ex)
                {
                    result.Warnings.Add($"Seite {pageIndex}: Fehler beim Lesen - {ex.Message}");
                }
            }

            if (result.Modules.Count == 0)
            {
                result.Warnings.Add("Keine Module im PDF erkannt. Ist dies ein Siemens ET200SP-Schaltplan?");
            }
        }
        catch (Exception ex)
        {
            result.Warnings.Add($"PDF konnte nicht geöffnet werden: {ex.Message}");
        }

        return result;
    }

    /// <summary>Kern des Parsers auf Textzeilen (testbar ohne PDF).</summary>
    public static List<ParsedModule> ParseLines(IEnumerable<string> lines)
    {
        var modules = new List<ParsedModule>();
        ParsedModule? currentModule = null;

        foreach (var lineText in lines)
        {
            var typeMatch = ModuleTypePattern.Match(lineText);
            var bmkMatch = ModuleBmkPattern.Match(lineText);

            if (typeMatch.Success && bmkMatch.Success)
            {
                if (currentModule != null)
                    modules.Add(currentModule);

                currentModule = new ParsedModule
                {
                    ModuleName = bmkMatch.Value,
                    ModuleType = NormalizeModuleType(typeMatch.Groups[2].Value)
                };
            }

            if (currentModule != null)
                ExtractAddresses(lineText, currentModule);
        }

        if (currentModule != null)
            modules.Add(currentModule);

        foreach (var module in modules)
            FinalizeModule(module);

        return modules;
    }

    private static List<TextBlock> ExtractTextBlocks(Page page)
    {
        var blocks = new List<TextBlock>();

        foreach (var word in page.GetWords())
        {
            blocks.Add(new TextBlock
            {
                Text = word.Text,
                X = word.BoundingBox.Left,
                Y = word.BoundingBox.Bottom
            });
        }

        return blocks;
    }

    private static List<TextLine> GroupIntoLines(List<TextBlock> blocks)
    {
        if (blocks.Count == 0)
            return [];

        var sorted = blocks.OrderByDescending(b => b.Y).ThenBy(b => b.X).ToList();
        var lines = new List<TextLine>();
        var currentLine = new TextLine { Y = sorted[0].Y };
        currentLine.Blocks.Add(sorted[0]);

        for (int i = 1; i < sorted.Count; i++)
        {
            if (Math.Abs(sorted[i].Y - currentLine.Y) <= LineToleranceY)
            {
                currentLine.Blocks.Add(sorted[i]);
            }
            else
            {
                lines.Add(currentLine);
                currentLine = new TextLine { Y = sorted[i].Y };
                currentLine.Blocks.Add(sorted[i]);
            }
        }

        lines.Add(currentLine);
        return lines;
    }

    private static void ExtractAddresses(string lineText, ParsedModule module)
    {
        foreach (Match match in DigitalAddressPattern.Matches(lineText))
            AddChannel(module, NormalizeAddress(match.Groups[2].Value));

        foreach (Match match in AnalogAddressPattern.Matches(lineText))
            AddChannel(module, NormalizeAddress(match.Groups[2].Value));
    }

    private static void AddChannel(ParsedModule module, string address)
    {
        if (module.Channels.Any(c => c.Address == address)) return;
        module.Channels.Add(new ParsedChannel
        {
            ChannelNumber = module.Channels.Count,
            Address = address
        });
    }

    /// <summary>Leerzeichen entfernen, englische Mnemonics (I/Q/IW/QW) auf die
    /// deutschen (E/A/EW/AW) abbilden — die App beschriftet einheitlich deutsch.</summary>
    internal static string NormalizeAddress(string raw)
    {
        var a = raw.Replace(" ", "").ToUpperInvariant();
        if (a.StartsWith("IW")) return "EW" + a[2..];
        if (a.StartsWith("QW")) return "AW" + a[2..];
        if (a.StartsWith('I')) return "E" + a[1..];
        if (a.StartsWith('Q')) return "A" + a[1..];
        return a;
    }

    private static void FinalizeModule(ParsedModule module)
    {
        module.ChannelCount = module.Channels.Count;

        // Start-Byte = KLEINSTE Byte-Adresse (nicht die zuerst gelesene): bei
        // Spaltenlayouts oder mehrbytigen Modulen kam sonst Byte 1 statt 0 heraus.
        int? min = null;
        foreach (var ch in module.Channels)
        {
            var byteMatch = StartBytePattern.Match(ch.Address);
            if (byteMatch.Success && int.TryParse(byteMatch.Value, out int b))
                min = min is null ? b : Math.Min(min.Value, b);
        }
        if (min is not null)
            module.StartByte = min.Value;
    }

    private static string NormalizeModuleType(string raw)
    {
        return raw.ToUpperInvariant() switch
        {
            "DI" => "DI",
            "DO" or "DQ" => "DO",
            "AI" => "AI",
            "AO" or "AQ" => "AO",
            _ => raw.ToUpperInvariant()
        };
    }

    private class TextBlock
    {
        public string Text { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
    }

    private class TextLine
    {
        public double Y { get; set; }
        public List<TextBlock> Blocks { get; set; } = [];

        public string GetFullText()
        {
            var sorted = Blocks.OrderBy(b => b.X).ToList();
            var parts = new List<string>();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0 && sorted[i].X - sorted[i - 1].X > ColumnGapX)
                    parts.Add("  ");

                parts.Add(sorted[i].Text);

                if (i < sorted.Count - 1)
                    parts.Add(" ");
            }

            return string.Join("", parts);
        }
    }
}
