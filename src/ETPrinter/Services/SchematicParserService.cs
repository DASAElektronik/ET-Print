using System.Text.RegularExpressions;
using ETPrinter.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ETPrinter.Services;

public static class SchematicParserService
{
    private static readonly Regex ModuleTypePattern = new(
        @"\b(DI|DO|DQ|AI|AO|AQ)\s*\d*x?\s*\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DigitalAddressPattern = new(
        @"[EA]\s*\d+\.\d+",
        RegexOptions.Compiled);

    private static readonly Regex AnalogAddressPattern = new(
        @"[EA]W\s*\d+",
        RegexOptions.Compiled);

    private static readonly Regex ModuleBmkPattern = new(
        @"[+=][\w.\-]+",
        RegexOptions.Compiled);

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
                    var lines = GroupIntoLines(textBlocks);
                    var modules = ExtractModulesFromLines(lines, pageIndex);
                    result.Modules.AddRange(modules);
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
            result.Warnings.Add($"PDF konnte nicht geoeffnet werden: {ex.Message}");
        }

        return result;
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

    private static List<ParsedModule> ExtractModulesFromLines(List<TextLine> lines, int pageNumber)
    {
        var modules = new List<ParsedModule>();
        ParsedModule? currentModule = null;

        foreach (var line in lines)
        {
            var lineText = line.GetFullText();

            var typeMatch = ModuleTypePattern.Match(lineText);
            var bmkMatch = ModuleBmkPattern.Match(lineText);

            if (typeMatch.Success && bmkMatch.Success)
            {
                if (currentModule != null)
                    modules.Add(currentModule);

                currentModule = new ParsedModule
                {
                    ModuleName = bmkMatch.Value,
                    ModuleType = NormalizeModuleType(typeMatch.Groups[1].Value)
                };
            }

            if (currentModule != null)
            {
                ExtractAddresses(lineText, currentModule);
            }
        }

        if (currentModule != null)
            modules.Add(currentModule);

        foreach (var module in modules)
        {
            FinalizeModule(module);
        }

        return modules;
    }

    private static void ExtractAddresses(string lineText, ParsedModule module)
    {
        var digitalMatches = DigitalAddressPattern.Matches(lineText);
        foreach (Match match in digitalMatches)
        {
            var address = match.Value.Replace(" ", "");
            if (!module.Channels.Any(c => c.Address == address))
            {
                module.Channels.Add(new ParsedChannel
                {
                    ChannelNumber = module.Channels.Count,
                    Address = address
                });
            }
        }

        var analogMatches = AnalogAddressPattern.Matches(lineText);
        foreach (Match match in analogMatches)
        {
            var address = match.Value.Replace(" ", "");
            if (!module.Channels.Any(c => c.Address == address))
            {
                module.Channels.Add(new ParsedChannel
                {
                    ChannelNumber = module.Channels.Count,
                    Address = address
                });
            }
        }
    }

    private static void FinalizeModule(ParsedModule module)
    {
        module.ChannelCount = module.Channels.Count;

        if (module.Channels.Count > 0)
        {
            var firstAddress = module.Channels[0].Address;
            var byteMatch = Regex.Match(firstAddress, @"\d+");
            if (byteMatch.Success && int.TryParse(byteMatch.Value, out int startByte))
            {
                module.StartByte = startByte;
            }
        }
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
