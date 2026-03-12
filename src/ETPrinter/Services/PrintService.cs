using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using ETPrinter.Models;
using ETPrinter.ViewModels;

namespace ETPrinter.Services;

public static class PrintService
{
    private const double MmToWpf = 96.0 / 25.4;
    private const double PageWidthWpf = 210.0 * MmToWpf;
    private const double PageHeightWpf = 297.0 * MmToWpf;

    private static readonly FontFamily Arial = new("Arial");

    public static void Print(
        IReadOnlyList<LabelViewModel> labels,
        FormatInfo format,
        LabelSettings settings,
        bool printGridLines = false,
        double calibrationOffsetX = 0,
        double calibrationOffsetY = 0)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return;

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PageWidthWpf, PageHeightWpf);

        var page = CreatePage(labels, format, settings, printGridLines,
            calibrationOffsetX, calibrationOffsetY);
        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);

        printDialog.PrintDocument(document.DocumentPaginator, "ET200SP Etiketten");
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

        foreach (var label in labels)
        {
            int i = label.Index;
            int col = i % format.LabelsPerRow;
            int row = i / format.LabelsPerRow;

            // Spiegeln: Index 0 = unten rechts auf dem physischen Blatt
            int physCol = (format.LabelsPerRow - 1) - col;
            int physRow = (format.LabelRows - 1) - row;

            double x = marginLeft + physCol * groupW;
            double y = marginTop + physRow * cellH;

            // Zellenrahmen nur bei Option "Gitterlinien drucken"
            if (printGridLines)
                DrawCellBorder(canvas, x, y, groupW, cellH);

            // Inhalt nur bei befuellten Etiketten
            if (label.HasText)
                RenderLabel(canvas, label, format, x, y, cellW, cellH, headerW, printGridLines);
        }

        page.Children.Add(canvas);
        page.Measure(new Size(PageWidthWpf, PageHeightWpf));
        page.Arrange(new Rect(0, 0, PageWidthWpf, PageHeightWpf));
        page.UpdateLayout();

        return page;
    }

    private static void DrawCellBorder(Canvas canvas, double x, double y, double w, double h)
    {
        var rect = new Rectangle
        {
            Width = w,
            Height = h,
            Stroke = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            StrokeThickness = 0.5,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        canvas.Children.Add(rect);
    }

    private static void RenderLabel(
        Canvas canvas, LabelViewModel label, FormatInfo format,
        double x, double y, double cellW, double cellH, double headerW,
        bool printGridLines)
    {
        double contentX = x;

        if (format.HasHeader)
        {
            if (!string.IsNullOrWhiteSpace(label.Header))
                RenderHeader(canvas, label.Header, x, y, headerW, cellH, label.CellFontSize);

            if (printGridLines)
            {
                var line = new Line
                {
                    X1 = x + headerW, Y1 = y,
                    X2 = x + headerW, Y2 = y + cellH,
                    Stroke = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
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
        double x, double y, double headerW, double cellH, int fontSize)
    {
        var container = new Border
        {
            Width = headerW,
            Height = cellH
        };

        var tb = CreateTextBlock(text, fontSize, isBold: true, isItalic: false);
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
            var tb = CreateTextBlock(label.Line1, label.CellFontSize, label.CellIsBold, label.CellIsItalic);
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
            var tb = CreateTextBlock(label.Line2, label.CellFontSize, label.CellIsBold, label.CellIsItalic);
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

        if (rowsPerLabel >= 2 && line2Parts.Length > 0)
        {
            double rowH = cellH / 2.0;
            RenderAddressRow(canvas, line1Parts, x, y, cellW, rowH,
                label.CellFontSize, label.CellIsBold, label.CellIsItalic, printGridLines);
            RenderAddressRow(canvas, line2Parts, x, y + rowH, cellW, rowH,
                label.CellFontSize, label.CellIsBold, label.CellIsItalic, printGridLines);
        }
        else
        {
            RenderAddressRow(canvas, line1Parts, x, y, cellW, cellH,
                label.CellFontSize, label.CellIsBold, label.CellIsItalic, printGridLines);
        }
    }

    private static void RenderAddressRow(
        Canvas canvas, string[] parts,
        double x, double y, double totalWidth, double rowHeight,
        int fontSize, bool isBold, bool isItalic, bool printGridLines)
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
                    Stroke = new SolidColorBrush(Color.FromRgb(160, 160, 160)),
                    StrokeThickness = 0.3
                };
                Canvas.SetLeft(rect, px);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }

            // Text 90 Grad gedreht
            var tb = CreateTextBlock(parts[i], fontSize, isBold, isItalic);
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

    public static void PrintCalibrationPage(
        FormatInfo format,
        LabelSettings settings,
        double calOffsetX,
        double calOffsetY)
    {
        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
            return;

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(PageWidthWpf, PageHeightWpf);

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
        double gridHeight = format.LabelRows * cellH;

        // Fadenkreuz 1: Ecke unten rechts (Reihe 1, Spalte 1)
        double x1 = marginLeft + gridWidth;
        double y1 = marginTop + gridHeight;
        DrawCrosshair(canvas, x1, y1, "Reihe 1 / unten rechts");

        // Fadenkreuz 2: Ecke oben links (Reihe 20, Spalte 5)
        double x2 = marginLeft;
        double y2 = marginTop;
        DrawCrosshair(canvas, x2, y2, "Reihe 20 / oben links");

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
            FontFamily = Arial,
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

        var pageContent = new PageContent();
        ((IAddChild)pageContent).AddChild(page);
        document.Pages.Add(pageContent);

        printDialog.PrintDocument(document.DocumentPaginator, "ET200SP Kalibrierung");
    }

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
            FontFamily = Arial,
            FontSize = 7,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
        };
        Canvas.SetLeft(tb, cx + 4 * MmToWpf);
        Canvas.SetTop(tb, cy + 2 * MmToWpf);
        canvas.Children.Add(tb);
    }

    private static TextBlock CreateTextBlock(string text, int fontSize, bool isBold, bool isItalic)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = Arial,
            FontSize = fontSize,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }
}
