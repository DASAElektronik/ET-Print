using System.Text;
using ClosedXML.Excel;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP4: CSV/Excel/PDF-Import robust gegen reale Dateien.</summary>
public class ImportRobustnessTests
{
    // ---- CSV: Zeilenumbruch in Anfuehrungszeichen ---------------------------

    [Fact]
    public void Csv_QuotedNewline_StaysInField()
    {
        var cells = CsvImportService.Parse("Kopfzeile;Zeile1;Zeile2\r\n\"Störung\r\nLüfter\";E 0.1;E 0.2\r\nB;E 1.1;E 1.2\r\n");
        Assert.Equal(2, cells.Count);
        Assert.Equal("Störung\r\nLüfter", cells[0].Header);
        Assert.Equal("E 0.1", cells[0].Line1);
        Assert.Equal("B", cells[1].Header);
    }

    [Fact]
    public void Csv_EscapedQuotes_And_EmptyLines()
    {
        var cells = CsvImportService.Parse("Kopfzeile;Zeile1\n\"Sag \"\"Hallo\"\"\";E 0.0\n\n\n;\nC;E 0.1\n");
        Assert.Equal(2, cells.Count);
        Assert.Equal("Sag \"Hallo\"", cells[0].Header);
        Assert.Equal("C", cells[1].Header);
    }

    [Fact]
    public void Csv_TabSeparated_IsDetected()
    {
        var cells = CsvImportService.Parse("Kopfzeile\tZeile1\tZeile2\nH\tL1\tL2\n");
        Assert.Single(cells);
        Assert.Equal("L2", cells[0].Line2);
        Assert.Equal('\t', CsvImportService.DetectSeparator("a\tb\tc\n"));
    }

    [Fact]
    public void Csv_NoHeaderRow_UsesFirstThreeColumns()
    {
        var cells = CsvImportService.Parse("M1;E 0.0;E 0.1\nM2;E 1.0;E 1.1\n");
        Assert.Equal(2, cells.Count);
        Assert.Equal("M1", cells[0].Header);
        Assert.Equal("E 1.1", cells[1].Line2);
    }

    [Fact]
    public void Csv_LastLineWithoutNewline_IsImported()
    {
        var cells = CsvImportService.Parse("Kopfzeile;Zeile1\nA;1\nB;2");
        Assert.Equal(2, cells.Count);
        Assert.Equal("B", cells[1].Header);
    }

    [Fact]
    public void Csv_Utf16LeWithBom_IsDecoded()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Kopfzeile;Zeile1\nStörung;E 0.0\n")).ToArray();
        var text = CsvImportService.DecodeText(bytes);
        var cells = CsvImportService.Parse(text);
        Assert.Single(cells);
        Assert.Equal("Störung", cells[0].Header);
    }

    [Fact]
    public void Csv_Utf16LeWithoutBom_IsDecoded()
    {
        var bytes = Encoding.Unicode.GetBytes("Kopfzeile;Zeile1\nA;B\n");
        var text = CsvImportService.DecodeText(bytes);
        Assert.StartsWith("Kopfzeile", text);
        Assert.DoesNotContain('\0', text);
    }

    [Fact]
    public void Csv_Utf8WithBom_StripsBom()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Kopfzeile;Zeile1\nÄ;B\n")).ToArray();
        var cells = CsvImportService.Parse(CsvImportService.DecodeText(bytes));
        Assert.Equal("Ä", cells[0].Header);
    }

    [Fact]
    public void Csv_Ansi_FallsBackTo1252()
    {
        var cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        var bytes = cp1252.GetBytes("Kopfzeile;Zeile1\nÜberdruck;E 0.0\n");
        var cells = CsvImportService.Parse(CsvImportService.DecodeText(bytes));
        Assert.Equal("Überdruck", cells[0].Header);
    }

    // ---- Excel ------------------------------------------------------------

    private static string TempXlsx() => Path.Combine(Path.GetTempPath(), $"etprinter_xlsx_{Guid.NewGuid():N}.xlsx");

    [Fact]
    public void Excel_HiddenCoverSheet_IsSkipped()
    {
        string path = TempXlsx();
        using (var wb = new XLWorkbook())
        {
            var cover = wb.Worksheets.Add("Deckblatt");
            cover.Cell(1, 1).Value = "Projekt XY";
            cover.Visibility = XLWorksheetVisibility.Hidden;
            var data = wb.Worksheets.Add("Daten");
            data.Cell(1, 1).Value = "Kopfzeile";
            data.Cell(1, 2).Value = "Zeile1";
            data.Cell(2, 1).Value = "M1";
            data.Cell(2, 2).Value = "E 0.0";
            wb.SaveAs(path);
        }
        try
        {
            var cells = ExcelImportService.Import(path);
            Assert.Single(cells);
            Assert.Equal("M1", cells[0].Header);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Excel_NumericAndDateCells_UseDisplayFormat()
    {
        string path = TempXlsx();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Daten");
            ws.Cell(1, 1).Value = "Kopfzeile";
            ws.Cell(1, 2).Value = "Zeile1";
            ws.Cell(1, 3).Value = "Zeile2";
            ws.Cell(2, 1).Value = 12;                  // Zahl
            ws.Cell(2, 2).Value = 1.5;                 // Dezimalzahl
            ws.Cell(2, 2).Style.NumberFormat.Format = "0.0";
            ws.Cell(2, 3).Value = new DateTime(2026, 1, 1);
            ws.Cell(2, 3).Style.DateFormat.Format = "dd.MM.yyyy";
            wb.SaveAs(path);
        }
        try
        {
            var cells = ExcelImportService.Import(path);
            Assert.Single(cells);
            Assert.Equal("12", cells[0].Header);
            Assert.Equal("1.5", cells[0].Line1.Replace(',', '.'));
            Assert.Equal("01.01.2026", cells[0].Line2);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Excel_DataStartingInColumnB_WithoutHeader()
    {
        string path = TempXlsx();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Daten");
            ws.Cell(3, 2).Value = "M1";
            ws.Cell(3, 3).Value = "E 0.0";
            ws.Cell(3, 4).Value = "E 0.1";
            wb.SaveAs(path);
        }
        try
        {
            var cells = ExcelImportService.Import(path);
            Assert.Single(cells);
            Assert.Equal("M1", cells[0].Header);
            Assert.Equal("E 0.1", cells[0].Line2);
        }
        finally { File.Delete(path); }
    }

    // ---- PDF-Parser (Textzeilen) --------------------------------------------

    [Fact]
    public void Pdf_ModuleWithAddresses_IsParsed()
    {
        var modules = SchematicParserService.ParseLines(
        [
            "=A1+S1-K3  DI 16x24VDC ST",
            "E 4.0  E 4.1  E 4.2  E 4.3",
            "E 5.0  E 5.1",
        ]);
        Assert.Single(modules);
        Assert.Equal("=A1", modules[0].ModuleName);
        Assert.Equal("DI", modules[0].ModuleType);
        Assert.Equal(6, modules[0].ChannelCount);
        Assert.Equal(4, modules[0].StartByte);
    }

    [Fact]
    public void Pdf_NoPhantomAddresses_FromWordsEndingInE()
    {
        var modules = SchematicParserService.ParseLines(
        [
            "+K1 DQ 8x24VDC",
            "SIZE 1.5  TYPE 2.0  NEW 12  CABLE 3.0",
            "A 2.0  A 2.1",
        ]);
        Assert.Single(modules);
        Assert.Equal(2, modules[0].ChannelCount);
        Assert.All(modules[0].Channels, c => Assert.StartsWith("A2.", c.Address));
    }

    [Fact]
    public void Pdf_StartByte_IsMinimumNotFirst()
    {
        var modules = SchematicParserService.ParseLines(
        [
            "+K2 DI 16",
            "E 11.0  E 11.1",   // erste gelesene Adresse ist Byte 11
            "E 10.0  E 10.1",
        ]);
        Assert.Equal(10, modules[0].StartByte);
    }

    [Fact]
    public void Pdf_EnglishMnemonics_AreNormalized()
    {
        var modules = SchematicParserService.ParseLines(
        [
            "+K5 AI 4",
            "IW 100  IW 102  I 3.0  Q 4.1  QW 20",
        ]);
        var addresses = modules[0].Channels.Select(c => c.Address).ToList();
        Assert.Contains("EW100", addresses);
        Assert.Contains("EW102", addresses);
        Assert.Contains("E3.0", addresses);
        Assert.Contains("A4.1", addresses);
        Assert.Contains("AW20", addresses);
        Assert.Equal("AI", modules[0].ModuleType);
    }

    [Fact]
    public void Pdf_TwoModules_SplitAtNextHeaderLine()
    {
        var modules = SchematicParserService.ParseLines(
        [
            "+K1 DI 8", "E 0.0", "E 0.1",
            "+K2 DQ 8", "A 0.0",
        ]);
        Assert.Equal(2, modules.Count);
        Assert.Equal("DO", modules[1].ModuleType);
        Assert.Single(modules[1].Channels);
    }

    [Fact]
    public void NormalizeAddress_MapsIQ()
    {
        Assert.Equal("E0.0", SchematicParserService.NormalizeAddress("I 0.0"));
        Assert.Equal("AW4", SchematicParserService.NormalizeAddress("QW 4"));
        Assert.Equal("E1.1", SchematicParserService.NormalizeAddress("E 1.1"));
    }

    // ---- Auto-Variante beim MP-Import ----------------------------------------

    [Theory]
    [InlineData(ETPrinter.Models.ProductFamily.S71500_ET200MP, ETPrinter.Services.ModuleType.DI, 8, ETPrinter.Models.MpModuleVariant.DI_DQ_16)]
    [InlineData(ETPrinter.Models.ProductFamily.S71500_ET200MP, ETPrinter.Services.ModuleType.DO, 32, ETPrinter.Models.MpModuleVariant.DI_DQ_32)]
    [InlineData(ETPrinter.Models.ProductFamily.S71500_ET200MP, ETPrinter.Services.ModuleType.AI, 8, ETPrinter.Models.MpModuleVariant.AI_AQ_8)]
    [InlineData(ETPrinter.Models.ProductFamily.S71500_ET200MP, ETPrinter.Services.ModuleType.AO, 4, ETPrinter.Models.MpModuleVariant.AQ_4)]
    [InlineData(ETPrinter.Models.ProductFamily.S71500_ET200MP_25mm, ETPrinter.Services.ModuleType.DI, 32, ETPrinter.Models.MpModuleVariant.MP25_32)]
    [InlineData(ETPrinter.Models.ProductFamily.S71500_ET200MP_25mm, ETPrinter.Services.ModuleType.AI, 8, ETPrinter.Models.MpModuleVariant.MP25_16)]
    public void SuggestVariant_MatchesChannelCount(ETPrinter.Models.ProductFamily family, ETPrinter.Services.ModuleType type, int channels, ETPrinter.Models.MpModuleVariant expected)
    {
        Assert.Equal(expected, ETPrinter.ViewModels.ImportCoordinator.SuggestVariant(family, type, channels));
    }
}
