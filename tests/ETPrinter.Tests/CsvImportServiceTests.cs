using System.Text;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

public class CsvImportServiceTests
{
    private static string WriteTemp(string content, Encoding encoding)
    {
        string path = Path.Combine(Path.GetTempPath(), $"etprinter_csv_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content, encoding);
        return path;
    }

    // Separator-Erkennung darf Kommas INNERHALB von Anfuehrungszeichen nicht
    // mitzaehlen — sonst kippt eine Semikolon-CSV mit quoted Kommas auf ','.
    [Fact]
    public void Import_SemicolonCsv_WithQuotedCommas_UsesSemicolon()
    {
        var path = WriteTemp(
            "Kopfzeile;Zeile1;Zeile2\n\"A, B, C\";\"D, E\";F\n",
            new UTF8Encoding(false));
        try
        {
            var cells = CsvImportService.Import(path);
            Assert.Single(cells);
            Assert.Equal("A, B, C", cells[0].Header);
            Assert.Equal("D, E", cells[0].Line1);
            Assert.Equal("F", cells[0].Line2);
        }
        finally { File.Delete(path); }
    }

    // ANSI-Datei mit reiner ASCII-Kopfzeile, aber Umlauten in Datenzeilen:
    // die Encoding-Erkennung muss die ganze Datei pruefen, nicht nur Zeile 1.
    [Fact]
    public void Import_Ansi1252_WithUmlautsInDataRows_DecodesCorrectly()
    {
        var cp1252 = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        var path = WriteTemp(
            "Kopfzeile;Zeile1;Zeile2\nStörmelder;Störung Lüfter;E0.1\n",
            cp1252);
        try
        {
            var cells = CsvImportService.Import(path);
            Assert.Single(cells);
            Assert.Equal("Störung Lüfter", cells[0].Line1);
            Assert.DoesNotContain("�", cells[0].Line1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Import_CommaCsv_UsesComma()
    {
        var path = WriteTemp(
            "Kopfzeile,Zeile1,Zeile2\nH,L1,L2\n",
            new UTF8Encoding(false));
        try
        {
            var cells = CsvImportService.Import(path);
            Assert.Single(cells);
            Assert.Equal("H", cells[0].Header);
            Assert.Equal("L1", cells[0].Line1);
            Assert.Equal("L2", cells[0].Line2);
        }
        finally { File.Delete(path); }
    }
}
