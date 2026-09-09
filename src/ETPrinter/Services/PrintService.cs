using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ETPrinter.Models;
using ETPrinter.ViewModels;

namespace ETPrinter.Services;

public static class PrintService
{
    private const double MmToWpf = 96.0 / 25.4;
    private const double PageWidthWpf = 210.0 * MmToWpf;
    private const double PageHeightWpf = 297.0 * MmToWpf;

    private static readonly FontFamily DefaultFont = new("Arial");

    /// <summary>
    /// Baut das Druckdokument fuer ET200SP-Seiten (jede innere Liste = eine
    /// physische A4-Seite). Kein Dialog — dieselbe Quelle fuer den echten Druck
    /// (<see cref="Print"/>) und fuer die dialogfreie PNG-Ausgabe der
    /// Test-Automation (<see cref="RenderToPng"/>), damit beide identisch sind.
    /// </summary>
    public static FixedDocument BuildDocument(
        IReadOnlyList<IReadOnlyList<LabelViewModel>> pages,
        FormatInfo format,
        LabelSettings settings,
        bool printGridLines = false,
        double calibrationOffsetX = 0,
        double calibrationOffsetY = 0)
    {
        var document = NewDocument();

        foreach (var pageLabels in pages)
        {
            var page = CreatePage(pageLabels, format, settings, printGridLines,
                calibrationOffsetX, calibrationOffsetY);
            AddPage(document, page);
        }

        return document;
    }

    private static FixedDocument NewDocument()
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PageWidthWpf, PageHeightWpf);
        return document;
    }

    private static void AddPage(FixedDocument document, FixedPage page)
    {
        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);
    }

    /// <summary>
    /// Zeigt den Windows-Druckdialog und druckt das Dokument.
    /// Liefert false, wenn der Benutzer abgebrochen hat — der Aufrufer darf dann
    /// weder "gesendet" melden noch die Kalibrierung persistieren.
    /// </summary>
    public static bool Print(FixedDocument document, string jobTitle)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return false;

        printDialog.PrintDocument(document.DocumentPaginator, jobTitle);
        return true;
    }

    public static string JobTitleFor(FormatInfo format) =>
        format.Family == ProductFamily.ET200SP ? "ET200SP Etiketten" : "ET200MP Etiketten";

    /// <summary>
    /// Rendert jede Seite des Dokuments als PNG (page_01.png, ...) in den Ordner.
    /// Fuer Test-Automation: der Druckpfad wird ohne Drucker und ohne Dialog
    /// prueffbar, pixelgenau mit dem Preview-Screenshot vergleichbar.
    /// </summary>
    public static int RenderToPng(FixedDocument document, string directory, double dpi = 150)
    {
        Directory.CreateDirectory(directory);
        int index = 0;
        foreach (var pageContent in document.Pages)
        {
            var page = pageContent.Child ?? pageContent.GetPageRoot(false);
            if (page is null) continue;
            index++;

            page.Measure(new Size(PageWidthWpf, PageHeightWpf));
            page.Arrange(new Rect(0, 0, PageWidthWpf, PageHeightWpf));
            page.UpdateLayout();

            var rtb = new RenderTargetBitmap(
                (int)Math.Round(PageWidthWpf * dpi / 96.0),
                (int)Math.Round(PageHeightWpf * dpi / 96.0),
                dpi, dpi, PixelFormats.Pbgra32);

            // Weisser Hintergrund wie Papier — FixedPage selbst ist transparent
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, PageWidthWpf, PageHeightWpf));
            rtb.Render(dv);
            rtb.Render(page);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(System.IO.Path.Combine(directory, $"page_{index:00}.png"));
            encoder.Save(fs);
        }
        return index;
    }

    private static FixedPage CreatePage(
        IReadOnlyList<LabelViewModel> labels,
        FormatInfo format,
        LabelSettings settings,
        bool printGridLines,
        double calOffsetX,
        double calOffsetY)
    {
        var page = new FixedPage
        {
            Width = PageWidthWpf,
            Height = PageHeightWpf
        };

        var canvas = new Canvas
        {
            Width = PageWidthWpf,
            Height = PageHeightWpf
        };

        var (cellWidthMm, cellHeightMm, headerWidthMm) = FormatDefinitions.GetCellSize(format, settings);
        double cellW = cellWidthMm * MmToWpf;
        double cellH = cellHeightMm * MmToWpf;
        double headerW = headerWidthMm * MmToWpf;
        double groupW = cellW + headerW;

        double marginLeft = (settings.MarginLeft + calOffsetX) * MmToWpf;
        double marginTop = (settings.MarginTop + calOffsetY) * MmToWpf;

        // ET200MP: Band-Layout berechnen (Band 1 und Band 2 haben eigene Header-Hoehen)
        var familyInfo = ProductFamilyDefinitions.Get(format.Family);
        int labelsPerBand = format.LabelsPerBand;
        double bandHeaderH = familyInfo.EstimatedHeaderHeight * MmToWpf;
        double band2HeaderH = familyInfo.EstimatedBand2HeaderHeight * MmToWpf;

        foreach (var label in labels)
        {
            int i = label.Index;

            // Band-Zuordnung (0 = oben, 1 = unten)
            int band = format.BandsPerPage > 1 ? i / labelsPerBand : 0;
            int indexInBand = format.BandsPerPage > 1 ? i % labelsPerBand : i;

            int col = indexInBand % format.LabelsPerRow;
            int row = indexInBand / format.LabelsPerRow;

            // Spiegeln: Index 0 = unten rechts auf dem physischen Blatt
            int physCol = (format.LabelsPerRow - 1) - col;
            int physRow = (format.ChannelRowsPerBand - 1) - row;

            double x = marginLeft + physCol * groupW;
            double y;

            if (format.BandsPerPage > 1)
            {
                // ET200MP: Y-Position — Band 1: hoher Header, Band 2: flacher Header
                double bandStartY = marginTop + band * (bandHeaderH + format.ChannelRowsPerBand * cellH);
                y = bandStartY + (band == 0 ? bandHeaderH : band2HeaderH) + physRow * cellH;
            }
            else
            {
                // ET200SP: Einfache Y-Berechnung
                y = marginTop + physRow * cellH;
            }

            // Schnittkanten + Inhalt nur bei befuellten und druckaktiven Etiketten
            if (label.HasText && label.IsPrintEnabled)
            {
                if (printGridLines)
                    DrawCellBorder(canvas, x, y, groupW, cellH);
                RenderLabel(canvas, label, format, settings, x, y, cellW, cellH, headerW, printGridLines);
            }
        }

        page.Children.Add(canvas);
        page.Measure(new Size(PageWidthWpf, PageHeightWpf));
        page.Arrange(new Rect(0, 0, PageWidthWpf, PageHeightWpf));
        page.UpdateLayout();

        return page;
    }

    private static void DrawCellBorder(Canvas canvas, double x, double y, double w, double h)
    {
        // Schnittkanten schwarz, 0.5 pt — sichtbar zum Ausschneiden auf Blanko-A4
        var rect = new Rectangle
        {
            Width = w,
            Height = h,
            Stroke = Brushes.Black,
            StrokeThickness = 0.5,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }

    private static void RenderLabel(
        Canvas canvas, LabelViewModel label, FormatInfo format, LabelSettings settings,
        double x, double y, double cellW, double cellH, double headerW,
        bool printGridLines)
    {
        double contentX = x;

        if (format.HasHeader)
        {
            if (!string.IsNullOrWhiteSpace(label.Header))
                RenderHeader(canvas, label.Header, x, y, headerW, cellH,
                    settings.HeaderFontSize, settings.HeaderIsBold, label.CellFontFamily);

            if (printGridLines)
            {
                var line = new Line
                {
                    X1 = x + headerW, Y1 = y,
                    X2 = x + headerW, Y2 = y + cellH,
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.3
                };
                canvas.Children.Add(line);
            }

            contentX = x + headerW;
        }

        if (format.IsVertical)
            RenderVerticalAddresses(canvas, label, contentX, y, cellW, cellH, format.RowsPerLabel, printGridLines);
        else
            RenderHorizontalText(canvas, label, contentX, y, cellW, cellH, format.RowsPerLabel);
    }

    private static void RenderHeader(
        Canvas canvas, string text,
        double x, double y, double headerW, double cellH, int fontSize, bool isBold, string fontFamily = "Arial")
    {
        var container = new Border
        {
            Width = headerW,
            Height = cellH
        };

        var tb = CreateTextBlock(text, fontSize, isBold: isBold, isItalic: false, fontFamily: fontFamily);
        tb.LayoutTransform = new RotateTransform(-90);
        tb.HorizontalAlignment = HorizontalAlignment.Center;
        tb.VerticalAlignment = VerticalAlignment.Center;

        container.Child = tb;
        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);
        canvas.Children.Add(container);
    }

    private static void RenderHorizontalText(
        Canvas canvas, LabelViewModel label,
        double x, double y, double cellW, double cellH, int rowsPerLabel)
    {
        double lineH = cellH / Math.Max(rowsPerLabel, 1);

        if (!string.IsNullOrWhiteSpace(label.Line1))
        {
            var tb = CreateTextBlock(label.Line1, label.CellFontSize, label.CellIsBold, label.CellIsItalic, label.CellFontFamily);
            tb.TextAlignment = TextAlignment.Center;
            tb.Width = cellW;
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;

            var container = new Border { Width = cellW, Height = lineH };
            container.Child = tb;
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);
            canvas.Children.Add(container);
        }

        if (rowsPerLabel >= 2 && !string.IsNullOrWhiteSpace(label.Line2))
        {
            var tb = CreateTextBlock(label.Line2, label.CellFontSize, label.CellIsBold, label.CellIsItalic, label.CellFontFamily);
            tb.TextAlignment = TextAlignment.Center;
            tb.Width = cellW;
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;

            var container = new Border { Width = cellW, Height = lineH };
            container.Child = tb;
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y + lineH);
            canvas.Children.Add(container);
        }
    }

    private static void RenderVerticalAddresses(
        Canvas canvas, LabelViewModel label,
        double x, double y, double cellW, double cellH, int rowsPerLabel,
        bool printGridLines)
    {
        var line1Parts = label.Line1Parts;
        var line2Parts = label.Line2Parts;

        if (rowsPerLabel >= 2)
        {
            // Zweizeilig: Digital nutzt beide Reihen (odd/even), Analog nur oben.
            // Bei leerer Line2 werden leere Platzhalter gerendert — bei aktiven
            // Gitterlinien werden so die unbenutzten Klemmenplaetze sichtbar.
            double rowH = cellH / 2.0;
            RenderAddressRow(canvas, line1Parts, x, y, cellW, rowH,
                label.CellFontSize, label.CellIsBold, label.CellIsItalic, printGridLines, label.CellFontFamily);
            var line2Effective = line2Parts.Length > 0 ? line2Parts : EmptyPlaceholders(line1Parts.Length);
            RenderAddressRow(canvas, line2Effective, x, y + rowH, cellW, rowH,
                label.CellFontSize, label.CellIsBold, label.CellIsItalic, printGridLines, label.CellFontFamily);
        }
        else
        {
            RenderAddressRow(canvas, line1Parts, x, y, cellW, cellH,
                label.CellFontSize, label.CellIsBold, label.CellIsItalic, printGridLines, label.CellFontFamily);
        }
    }

    private static string[] EmptyPlaceholders(int count)
    {
        if (count <= 0) return [];
        var arr = new string[count];
        for (int i = 0; i < count; i++) arr[i] = string.Empty;
        return arr;
    }

    private static void RenderAddressRow(
        Canvas canvas, string[] parts,
        double x, double y, double totalWidth, double rowHeight,
        int fontSize, bool isBold, bool isItalic, bool printGridLines, string fontFamily = "Arial")
    {
        if (parts.Length == 0) return;

        double partW = totalWidth / parts.Length;

        for (int i = 0; i < parts.Length; i++)
        {
            double px = x + i * partW;

            if (printGridLines)
            {
                var rect = new Rectangle
                {
                    Width = partW,
                    Height = rowHeight,
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.3
                };
                Canvas.SetLeft(rect, px);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }

            // Text 90 Grad gedreht
            var tb = CreateTextBlock(parts[i], fontSize, isBold, isItalic, fontFamily);
            tb.LayoutTransform = new RotateTransform(-90);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;

            var container = new Border
            {
                Width = partW,
                Height = rowHeight
            };
            container.Child = tb;
            Canvas.SetLeft(container, px);
            Canvas.SetTop(container, y);
            canvas.Children.Add(container);
        }
    }

    /// <summary>Kalibrierseite (Fadenkreuze an den Rasterecken) als Dokument — ohne Dialog.</summary>
    public static FixedDocument BuildCalibrationDocument(
        FormatInfo format,
        LabelSettings settings,
        double calOffsetX,
        double calOffsetY)
    {
        var document = NewDocument();

        var page = new FixedPage { Width = PageWidthWpf, Height = PageHeightWpf };
        var canvas = new Canvas { Width = PageWidthWpf, Height = PageHeightWpf };

        var (cellWidthMm, cellHeightMm, headerWidthMm) = FormatDefinitions.GetCellSize(format, settings);
        double cellW = cellWidthMm * MmToWpf;
        double cellH = cellHeightMm * MmToWpf;
        double headerW = headerWidthMm * MmToWpf;
        double groupW = cellW + headerW;

        double marginLeft = (settings.MarginLeft + calOffsetX) * MmToWpf;
        double marginTop = (settings.MarginTop + calOffsetY) * MmToWpf;

        double gridWidth = format.LabelsPerRow * groupW;
        var familyInfo = ProductFamilyDefinitions.Get(format.Family);
        double totalGridHeight;
        if (format.IsModuleBased)
        {
            // Exakt dieselbe Geometrie wie CreateMpPage/MpPreviewControl (feste
            // Estimated-Masse), NICHT GetCellSize — sonst beschreibt die Kalibrier-
            // seite ein Raster, das der echte MP-Druck nie erzeugt (~6mm Abweichung).
            double printWidthMm = FormatDefinitions.PageWidth - settings.MarginLeft - settings.MarginRight;
            gridWidth = printWidthMm * MmToWpf;
            double dataRowH = familyInfo.EstimatedChannelRowHeight * MmToWpf;
            double bandDataH = MpModuleLayoutFactory.RowsPerHalf * dataRowH;
            totalGridHeight = familyInfo.EstimatedHeaderHeight * MmToWpf + bandDataH
                + familyInfo.EstimatedBand2HeaderHeight * MmToWpf + bandDataH;
        }
        else if (format.BandsPerPage > 1)
        {
            // Band 1: hoher Header, Band 2: flacher Header — je 20 Datenzeilen
            totalGridHeight = familyInfo.EstimatedHeaderHeight * MmToWpf
                + familyInfo.EstimatedBand2HeaderHeight * MmToWpf
                + format.BandsPerPage * format.ChannelRowsPerBand * cellH;
        }
        else
        {
            totalGridHeight = format.LabelRows * cellH;
        }

        // Fadenkreuz 1: Ecke unten rechts
        double x1 = marginLeft + gridWidth;
        double y1 = marginTop + totalGridHeight;
        DrawCrosshair(canvas, x1, y1, "unten rechts");

        // Fadenkreuz 2: Ecke oben links
        double x2 = marginLeft;
        double y2 = marginTop;
        DrawCrosshair(canvas, x2, y2, "oben links");

        // Info-Text in der Mitte
        var info = new TextBlock
        {
            Text = $"ET-Printer Kalibrierung\n\n" +
                   $"Format: {format.DisplayName}\n" +
                   $"Raender: L={settings.MarginLeft:0.0} O={settings.MarginTop:0.0} " +
                   $"R={settings.MarginRight:0.0} U={settings.MarginBottom:0.0} mm\n" +
                   $"Kalibrierung: X={calOffsetX:+0.0;-0.0;0.0} Y={calOffsetY:+0.0;-0.0;0.0} mm\n\n" +
                   $"Auf Normalpapier drucken, mit Siemens-Blatt\n" +
                   $"uebereinanderlegen und gegen Licht halten.\n" +
                   $"Fadenkreuze muessen auf die perforierten\n" +
                   $"Ecken des Etikettenbogens treffen.",
            FontFamily = DefaultFont,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
        };
        info.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(info, (PageWidthWpf - info.DesiredSize.Width) / 2);
        Canvas.SetTop(info, PageHeightWpf / 2 - 40);
        canvas.Children.Add(info);

        page.Children.Add(canvas);
        page.Measure(new Size(PageWidthWpf, PageHeightWpf));
        page.Arrange(new Rect(0, 0, PageWidthWpf, PageHeightWpf));
        page.UpdateLayout();

        AddPage(document, page);
        return document;
    }

    public static string CalibrationJobTitleFor(FormatInfo format) =>
        format.Family == ProductFamily.ET200SP ? "ET200SP Kalibrierung" : "ET200MP Kalibrierung";

    private static void DrawCrosshair(Canvas canvas, double cx, double cy, string label)
    {
        double armLength = 15 * MmToWpf; // 15mm Arme
        var stroke = Brushes.Black;
        double thickness = 0.8;

        // Horizontale Linie
        var hLine = new Line
        {
            X1 = cx - armLength, Y1 = cy,
            X2 = cx + armLength, Y2 = cy,
            Stroke = stroke, StrokeThickness = thickness
        };
        canvas.Children.Add(hLine);

        // Vertikale Linie
        var vLine = new Line
        {
            X1 = cx, Y1 = cy - armLength,
            X2 = cx, Y2 = cy + armLength,
            Stroke = stroke, StrokeThickness = thickness
        };
        canvas.Children.Add(vLine);

        // Kleiner Kreis am Zentrum
        var circle = new Ellipse
        {
            Width = 3 * MmToWpf,
            Height = 3 * MmToWpf,
            Stroke = stroke,
            StrokeThickness = 0.5,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(circle, cx - 1.5 * MmToWpf);
        Canvas.SetTop(circle, cy - 1.5 * MmToWpf);
        canvas.Children.Add(circle);

        // Beschriftung
        var tb = new TextBlock
        {
            Text = label,
            FontFamily = DefaultFont,
            FontSize = 7,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
        };
        Canvas.SetLeft(tb, cx + 4 * MmToWpf);
        Canvas.SetTop(tb, cy + 2 * MmToWpf);
        canvas.Children.Add(tb);
    }

    // FontSize ist in Punkt (docs/PRINT-FORMATS: 7pt Standard aus dem Siemens-Excel);
    // WPF-FontSize ist in DIP (1/96"), daher Umrechnung 1pt = 96/72 DIP. Ohne diese
    // Umrechnung druckte die App ~25% kleiner als das Original-Template.
    private const double PtToDip = 96.0 / 72.0;

    private static TextBlock CreateTextBlock(string text, int fontSize, bool isBold, bool isItalic, string fontFamily = "Arial")
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(fontFamily),
            FontSize = fontSize * PtToDip,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    // === ET200MP Modulbasierter Druck ===

    /// <summary>Baut das Druckdokument fuer ET200MP-Modulseiten (ohne Dialog).</summary>
    public static FixedDocument BuildMpDocument(
        IReadOnlyList<IReadOnlyList<MpModuleViewModel>> pages,
        FormatInfo format,
        LabelSettings settings,
        bool printGridLines,
        double calibrationOffsetX,
        double calibrationOffsetY)
    {
        var document = NewDocument();

        foreach (var pageModules in pages)
        {
            var page = CreateMpPage(pageModules, format, settings, printGridLines,
                calibrationOffsetX, calibrationOffsetY);
            AddPage(document, page);
        }

        return document;
    }

    private static FixedPage CreateMpPage(
        IReadOnlyList<MpModuleViewModel> modules,
        FormatInfo format,
        LabelSettings settings,
        bool printGridLines,
        double calOffsetX, double calOffsetY)
    {
        var page = new FixedPage { Width = PageWidthWpf, Height = PageHeightWpf };
        var canvas = new Canvas { Width = PageWidthWpf, Height = PageHeightWpf };

        var familyInfo = ProductFamilyDefinitions.Get(format.Family);
        double marginLeft = (settings.MarginLeft + calOffsetX) * MmToWpf;
        double marginTop = (settings.MarginTop + calOffsetY) * MmToWpf;

        double printWidthMm = FormatDefinitions.PageWidth - settings.MarginLeft - settings.MarginRight;
        double moduleWidthMm = printWidthMm / familyInfo.ColumnsPerPage;
        double moduleW = moduleWidthMm * MmToWpf;

        // Spalten-Anteile familienabhaengig (25mm hat keine Net-Address-Spalte)
        double col0W = moduleW * familyInfo.Col0Ratio;
        double col1W = moduleW * familyInfo.Col1Ratio;
        double col2W = moduleW * familyInfo.Col2Ratio;
        double col3W = moduleW * familyInfo.Col3Ratio;
        double addrW = col0W + col1W;

        double headerH = familyInfo.EstimatedHeaderHeight * MmToWpf;
        double band2HeaderH = familyInfo.EstimatedBand2HeaderHeight * MmToWpf;
        double dataRowH = familyInfo.EstimatedChannelRowHeight * MmToWpf;
        double bandDataH = familyInfo.RowsPerHalf * dataRowH;

        foreach (var mod in modules)
        {
            if (!mod.IsPrintEnabled) continue;
            if (!mod.HasText) continue; // leere Module komplett ueberspringen (keine Schnittkanten)

            // 10 Streifen-Positionen pro A4: Band 0 (oben, hoher Header) und
            // Band 1 (unten, flacher Header) mit je ColumnsPerPage Spalten.
            int band = familyInfo.BandOf(mod.ModuleIndex);
            int col = familyInfo.ColumnOf(mod.ModuleIndex);
            double modX = marginLeft + col * moduleW;
            // Katalog-Belegung (konkretes Siemens-Modul) oder Varianten-Default
            var definitions = MpModuleLayoutFactory.GetDefinitions(mod.GetModule());

            double modHeaderH = band == 0 ? headerH : band2HeaderH;
            double headerY = band == 0 ? marginTop : marginTop + headerH + bandDataH;

            // Header oben im Streifen — globaler Header-Style aus settings
            if (!string.IsNullOrWhiteSpace(mod.HeaderText))
            {
                var tb = CreateTextBlock(mod.HeaderText.Replace("\n", " / "),
                    settings.HeaderFontSize, settings.HeaderIsBold, false, mod.FontFamily);
                tb.Width = moduleW - 2;
                tb.TextAlignment = TextAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                var container = new Border { Width = moduleW, Height = modHeaderH };
                container.Child = tb;
                Canvas.SetLeft(container, modX);
                Canvas.SetTop(container, headerY);
                canvas.Children.Add(container);
            }
            if (printGridLines)
                DrawCellBorder(canvas, modX, headerY, moduleW, modHeaderH);

            double dataStartY = headerY + modHeaderH;

            // Adresszellen (alle Layouts definieren nur Half 0 = ein Band)
            for (int i = 0; i < mod.AddressCells.Count && i < definitions.Length; i++)
            {
                var def = definitions[i];
                if (def.Half != 0) continue;

                var cellVm = mod.AddressCells[i];
                double cellX = modX + (def.StartCol == 0 ? 0 : col0W);
                double cellW = def.ColSpan == 2 ? addrW : (def.StartCol == 0 ? col0W : col1W);
                double cellY = dataStartY + def.StartRow * dataRowH;
                double cellH = def.RowSpan * dataRowH;

                if (printGridLines)
                    DrawCellBorder(canvas, cellX, cellY, cellW, cellH);

                string text = def.IsEditable ? cellVm.Text : def.Label;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var tb = CreateTextBlock(text, mod.FontSize, mod.IsBold, false, mod.FontFamily);
                    if (format.IsVertical && def.IsEditable)
                    {
                        tb.LayoutTransform = new RotateTransform(-90);
                        tb.HorizontalAlignment = HorizontalAlignment.Center;
                        tb.VerticalAlignment = VerticalAlignment.Center;
                    }
                    else
                    {
                        tb.TextAlignment = TextAlignment.Center;
                        tb.Width = cellW - 1;
                        // Vertikal zentrieren wie die Preview — sonst klebt der
                        // Text an der Zell-Oberkante (bei 4-Zeilen-Analogzellen ~9mm daneben).
                        tb.VerticalAlignment = VerticalAlignment.Center;
                    }
                    var container = new Border { Width = cellW, Height = cellH };
                    container.Child = tb;
                    Canvas.SetLeft(container, cellX);
                    Canvas.SetTop(container, cellY);
                    canvas.Children.Add(container);
                }
            }

            // Col 2: Net Address (Zeilen 1-10) + Net Name (Zeilen 11-20) — nur 35mm
            double col2X = modX + addrW;
            if (familyInfo.HasNetAddressColumn)
            {
                double blockH = MpModuleLayoutFactory.NetAddrBlockRows * dataRowH;
                RenderRotatedText(canvas, mod.NetAddress1, col2X, dataStartY, col2W, blockH,
                    mod.FontSize, mod.FontFamily);
                RenderRotatedText(canvas, mod.NetAddress2, col2X, dataStartY + blockH, col2W, blockH,
                    mod.FontSize, mod.FontFamily);
                if (printGridLines)
                {
                    DrawCellBorder(canvas, col2X, dataStartY, col2W, blockH);
                    DrawCellBorder(canvas, col2X, dataStartY + blockH, col2W, blockH);
                }
            }

            // Col 3: CPU-Name
            double col3X = col2X + col2W;
            RenderRotatedText(canvas, mod.CpuName, col3X, dataStartY, col3W, bandDataH,
                mod.FontSize, mod.FontFamily);
            if (printGridLines)
                DrawCellBorder(canvas, col3X, dataStartY, col3W, bandDataH);
        }

        page.Children.Add(canvas);
        page.Measure(new Size(PageWidthWpf, PageHeightWpf));
        page.Arrange(new Rect(0, 0, PageWidthWpf, PageHeightWpf));
        page.UpdateLayout();
        return page;
    }

    private static void RenderRotatedText(Canvas canvas, string text,
        double x, double y, double w, double h, int fontSize, string fontFamily)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var tb = CreateTextBlock(text, fontSize, false, false, fontFamily);
        tb.LayoutTransform = new RotateTransform(-90);
        tb.HorizontalAlignment = HorizontalAlignment.Center;
        tb.VerticalAlignment = VerticalAlignment.Center;

        var container = new Border { Width = w, Height = h };
        container.Child = tb;
        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);
        canvas.Children.Add(container);
    }
}
