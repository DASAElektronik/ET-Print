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
            // Leere Seiten ueberspringen — jede Seite ist ein Siemens-Etikettenbogen.
            // Auto-Advance legt nach dem letzten Etikett immer eine neue Seite an,
            // ohne diesen Skip endete praktisch jeder Druck mit einem Leerbogen.
            if (!pageLabels.Any(IsPrintable)) continue;
            var page = CreatePage(pageLabels, format, settings, printGridLines,
                calibrationOffsetX, calibrationOffsetY);
            AddPage(document, page);
        }

        return document;
    }

    /// <summary>Einzige Druck-Entscheidung fuer ET200SP-Etiketten (Seite, Schnittkante, Inhalt).</summary>
    public static bool IsPrintable(LabelViewModel label) => label.HasText && label.IsPrintEnabled;

    /// <summary>Einzige Druck-Entscheidung fuer ET200MP-Module.</summary>
    public static bool IsPrintable(MpModuleViewModel module) => module.IsPrintEnabled && module.HasPrintableContent;

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

        // Papierformat explizit A4 Hochformat: Drucker mit Standard Letter/A5 oder
        // Querformat skalieren/beschneiden sonst die fest 210x297 mm grosse Seite,
        // und die Kalibrierung stimmt nicht mehr.
        try
        {
            printDialog.PrintTicket.PageMediaSize = new System.Printing.PageMediaSize(System.Printing.PageMediaSizeName.ISOA4);
            printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Portrait;
        }
        catch (Exception ex) when (ex is System.Printing.PrintSystemException or InvalidOperationException)
        {
            Log.Warn($"PrintTicket A4 nicht setzbar: {ex.Message}");
        }

        printDialog.PrintDocument(document.DocumentPaginator, jobTitle);
        return true;
    }

    /// <summary>
    /// Shrink-to-fit-Container: verkleinert den Inhalt, wenn er nicht in w x h passt,
    /// vergroessert aber nie (DownOnly) und zentriert ihn. Ersetzt das fruehere
    /// "..."-Trimming, das z.B. "EW 10" in 6,4 mm hohen Vertikal-Slots zu "E..." machte.
    /// Preview nutzt dieselbe Viewbox-Konstruktion (MainWindow.xaml), damit beide
    /// identisch reagieren.
    /// </summary>
    private static Viewbox FitBox(UIElement child, double w, double h) => new()
    {
        Width = w,
        Height = h,
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        Child = child
    };

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

        // Geometrie ausschliesslich aus SheetGeometry (Position 1 = unten rechts)
        var geo = SheetGeometry.For(format, settings, calOffsetX, calOffsetY);
        double cellW = geo.SpCellWidth * MmToWpf;
        double cellH = geo.SpCellHeight * MmToWpf;
        double headerW = geo.SpHeaderWidth * MmToWpf;
        double groupW = geo.SpGroupWidth * MmToWpf;

        foreach (var label in labels)
        {
            // Schnittkanten + Inhalt nur bei befuellten und druckaktiven Etiketten
            if (!IsPrintable(label)) continue;

            var r = geo.SpLabelRect(label.Index).Scale(MmToWpf);
            if (printGridLines)
                DrawCellBorder(canvas, r.X, r.Y, groupW, cellH);
            RenderLabel(canvas, label, format, settings, r.X, r.Y, cellW, cellH, headerW, printGridLines);
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

        var tb = CreateTextBlock(text, fontSize, isBold: isBold, isItalic: false, fontFamily: fontFamily, fit: true);
        tb.LayoutTransform = new RotateTransform(-90);

        container.Child = FitBox(tb, headerW, cellH);
        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);
        canvas.Children.Add(container);
    }

    private static void RenderHorizontalText(
        Canvas canvas, LabelViewModel label,
        double x, double y, double cellW, double cellH, int rowsPerLabel)
    {
        double lineH = cellH / Math.Max(rowsPerLabel, 1);

        // Leerslots des Generators entfernen (sonst ~50 Zeichen fuer 31 mm)
        string line1 = label.Line1Display;
        string line2 = label.Line2Display;

        if (line1.Length > 0)
        {
            var tb = CreateTextBlock(line1, label.CellFontSize, label.CellIsBold, label.CellIsItalic, label.CellFontFamily, fit: true);
            var container = new Border { Width = cellW, Height = lineH, Child = FitBox(tb, cellW - 1, lineH) };
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);
            canvas.Children.Add(container);
        }

        if (rowsPerLabel >= 2 && line2.Length > 0)
        {
            var tb = CreateTextBlock(line2, label.CellFontSize, label.CellIsBold, label.CellIsItalic, label.CellFontFamily, fit: true);
            var container = new Border { Width = cellW, Height = lineH, Child = FitBox(tb, cellW - 1, lineH) };
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

            if (string.IsNullOrWhiteSpace(parts[i])) continue;

            // Text 90 Grad gedreht, shrink-to-fit in den Slot (partW x rowHeight)
            var tb = CreateTextBlock(parts[i], fontSize, isBold, isItalic, fontFamily, fit: true);
            tb.LayoutTransform = new RotateTransform(-90);

            var container = new Border
            {
                Width = partW,
                Height = rowHeight,
                Child = FitBox(tb, partW, rowHeight)
            };
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

        // Dieselbe Geometrie wie der echte Druck (SP-Raster bzw. MP-Baender) —
        // die Fadenkreuze sitzen exakt auf den Ecken des Etikettenrasters.
        var grid = SheetGeometry.For(format, settings, calOffsetX, calOffsetY).GridRect.Scale(MmToWpf);

        // Fadenkreuz 1: Ecke unten rechts
        DrawCrosshair(canvas, grid.Right, grid.Bottom, "unten rechts");

        // Fadenkreuz 2: Ecke oben links
        DrawCrosshair(canvas, grid.X, grid.Y, "oben links");

        // Info-Text in der Mitte
        var info = new TextBlock
        {
            Text = $"ET-Printer Kalibrierung\n\n" +
                   $"Format: {format.DisplayName}\n" +
                   $"Ränder: L={settings.MarginLeft:0.0} O={settings.MarginTop:0.0} " +
                   $"R={settings.MarginRight:0.0} U={settings.MarginBottom:0.0} mm\n" +
                   $"Kalibrierung: X={calOffsetX:+0.0;-0.0;0.0} Y={calOffsetY:+0.0;-0.0;0.0} mm\n\n" +
                   $"Auf Normalpapier drucken, mit Siemens-Blatt\n" +
                   $"uebereinanderlegen und gegen Licht halten.\n" +
                   $"Fadenkreuze müssen auf die perforierten\n" +
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

    /// <param name="fit">true = Text wird per FitBox verkleinert statt mit "..." gekappt
    /// (kein Trimming, damit die Messung die volle Breite liefert).</param>
    private static TextBlock CreateTextBlock(string text, int fontSize, bool isBold, bool isItalic,
        string fontFamily = "Arial", bool fit = false)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(fontFamily),
            FontSize = fontSize * PtToDip,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal,
            TextTrimming = fit ? TextTrimming.None : TextTrimming.CharacterEllipsis
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
            if (!pageModules.Any(IsPrintable)) continue; // Leerbogen vermeiden
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

        // Geometrie ausschliesslich aus SheetGeometry (identisch zur MP-Vorschau)
        var geo = SheetGeometry.For(format, settings, calOffsetX, calOffsetY);

        foreach (var mod in modules)
        {
            // Leere Module komplett ueberspringen (keine Schnittkanten). Reine
            // Pinout-Streifen (SIWAREX) zaehlen als Inhalt — siehe HasPrintableContent.
            if (!IsPrintable(mod)) continue;

            int idx = mod.ModuleIndex;
            // Katalog-Belegung (konkretes Siemens-Modul) oder Varianten-Default
            var definitions = MpModuleLayoutFactory.GetDefinitions(mod.GetModule());

            // Header oben im Streifen — globaler Header-Style aus settings.
            // Mehrzeilig (Device / Module / Slot wie im Excel-Template): Zeilenumbrueche
            // bleiben, lange Zeilen werden umbrochen statt mit "..." gekappt; passt es
            // in der Hoehe nicht, verkleinert die FitBox.
            var header = geo.MpHeaderRect(idx).Scale(MmToWpf);
            if (!string.IsNullOrWhiteSpace(mod.HeaderText))
            {
                var tb = CreateTextBlock(mod.HeaderText.Replace("\r\n", "\n"),
                    settings.HeaderFontSize, settings.HeaderIsBold, false, mod.FontFamily, fit: true);
                tb.MaxWidth = header.Width - 2;
                tb.TextWrapping = TextWrapping.Wrap;
                tb.TextAlignment = TextAlignment.Center;
                var container = new Border { Width = header.Width, Height = header.Height, Child = FitBox(tb, header.Width - 2, header.Height - 1) };
                Canvas.SetLeft(container, header.X);
                Canvas.SetTop(container, header.Y);
                canvas.Children.Add(container);
            }
            if (printGridLines)
                DrawCellBorder(canvas, header.X, header.Y, header.Width, header.Height);

            // Adresszellen (alle Layouts definieren nur Half 0 = ein Band)
            for (int i = 0; i < mod.AddressCells.Count && i < definitions.Length; i++)
            {
                var def = definitions[i];
                if (def.Half != 0) continue;

                var cellVm = mod.AddressCells[i];
                var cell = geo.MpCellRect(idx, def).Scale(MmToWpf);

                if (printGridLines)
                    DrawCellBorder(canvas, cell.X, cell.Y, cell.Width, cell.Height);

                string text = def.IsEditable ? cellVm.Text : def.Label;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var tb = CreateTextBlock(text, mod.FontSize, mod.IsBold, mod.IsItalic, mod.FontFamily);
                    if (format.IsVertical && def.IsEditable)
                    {
                        tb.LayoutTransform = new RotateTransform(-90);
                        tb.HorizontalAlignment = HorizontalAlignment.Center;
                        tb.VerticalAlignment = VerticalAlignment.Center;
                    }
                    else
                    {
                        tb.TextAlignment = TextAlignment.Center;
                        tb.Width = cell.Width - 1;
                        // Vertikal zentrieren wie die Preview — sonst klebt der
                        // Text an der Zell-Oberkante (bei 4-Zeilen-Analogzellen ~9mm daneben).
                        tb.VerticalAlignment = VerticalAlignment.Center;
                    }
                    var container = new Border { Width = cell.Width, Height = cell.Height };
                    container.Child = tb;
                    Canvas.SetLeft(container, cell.X);
                    Canvas.SetTop(container, cell.Y);
                    canvas.Children.Add(container);
                }
            }

            // Col 2: Net Address (Zeilen 1-10) + Net Name (Zeilen 11-20) — nur 35mm
            if (geo.Family.HasNetAddressColumn)
            {
                var n1 = geo.MpNetAddressRect(idx, 0).Scale(MmToWpf);
                var n2 = geo.MpNetAddressRect(idx, 1).Scale(MmToWpf);
                RenderRotatedText(canvas, mod.NetAddress1, n1.X, n1.Y, n1.Width, n1.Height,
                    mod.FontSize, mod.FontFamily, mod.IsItalic);
                RenderRotatedText(canvas, mod.NetAddress2, n2.X, n2.Y, n2.Width, n2.Height,
                    mod.FontSize, mod.FontFamily, mod.IsItalic);
                if (printGridLines)
                {
                    DrawCellBorder(canvas, n1.X, n1.Y, n1.Width, n1.Height);
                    DrawCellBorder(canvas, n2.X, n2.Y, n2.Width, n2.Height);
                }
            }

            // Col 3: CPU-Name
            var cpu = geo.MpCpuRect(idx).Scale(MmToWpf);
            RenderRotatedText(canvas, mod.CpuName, cpu.X, cpu.Y, cpu.Width, cpu.Height,
                mod.FontSize, mod.FontFamily, mod.IsItalic);
            if (printGridLines)
                DrawCellBorder(canvas, cpu.X, cpu.Y, cpu.Width, cpu.Height);
        }

        page.Children.Add(canvas);
        page.Measure(new Size(PageWidthWpf, PageHeightWpf));
        page.Arrange(new Rect(0, 0, PageWidthWpf, PageHeightWpf));
        page.UpdateLayout();
        return page;
    }

    private static void RenderRotatedText(Canvas canvas, string text,
        double x, double y, double w, double h, int fontSize, string fontFamily, bool isItalic = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var tb = CreateTextBlock(text, fontSize, false, isItalic, fontFamily);
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
