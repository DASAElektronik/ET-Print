using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;

namespace ETPrinter.Controls;

/// <summary>
/// Canvas-basierte Vorschau fuer ET200MP Module.
/// Rendert die exakte Zellenstruktur inkl. Merges wie im Siemens Excel-Template.
/// </summary>
public partial class MpPreviewControl : UserControl
{
    // Vorschau-Skalierung: 3px/mm (wie bei ET200SP A4-Preview 630x891)
    private const double PxPerMm = 3.0;

    private static readonly Brush EmptyBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
    private static readonly Brush FilledBrush = new SolidColorBrush(Color.FromRgb(208, 232, 208));
    private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromRgb(184, 212, 240));
    private static readonly Brush HeaderBgBrush = new SolidColorBrush(Color.FromRgb(216, 216, 232));
    private static readonly Brush NetAddrBgBrush = new SolidColorBrush(Color.FromRgb(232, 232, 248));
    private static readonly Brush CpuNameBgBrush = new SolidColorBrush(Color.FromRgb(248, 240, 232));
    private static readonly Brush StructBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
    private static readonly Brush CellBorderBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));
    private static readonly Brush SelectedCellBorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));

    public MpPreviewControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindToModules();
    }

    private void BindToModules()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.MpModules.CollectionChanged += (_, _) => Render();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(vm.SelectedMpModule) or nameof(vm.SelectedMpCell))
                    Render();
            };
            Render();
        }
    }

    public void Render()
    {
        PreviewCanvas.Children.Clear();
        if (DataContext is not MainViewModel vm) return;
        if (!vm.IsModuleBased || vm.MpModules.Count == 0) return;

        var format = vm.SelectedFormat;
        var familyInfo = ProductFamilyDefinitions.Get(format.Family);
        var settings = vm.Settings;

        // Druckbereich berechnen
        double printW = (FormatDefinitions.PageWidth - settings.MarginLeft - settings.MarginRight) * PxPerMm;
        double printH = (FormatDefinitions.PageHeight - settings.MarginTop - settings.MarginBottom) * PxPerMm;
        double marginL = settings.MarginLeft * PxPerMm;
        double marginT = settings.MarginTop * PxPerMm;

        int modulesPerBand = familyInfo.ModulesPerBand;
        double moduleW = printW / modulesPerBand;

        // Spaltenbreiten innerhalb eines Moduls
        double col0W = moduleW * MpModuleLayoutFactory.Col0Ratio;
        double col1W = moduleW * MpModuleLayoutFactory.Col1Ratio;
        double col2W = moduleW * MpModuleLayoutFactory.Col2Ratio;
        double col3W = moduleW * MpModuleLayoutFactory.Col3Ratio;
        double addrW = col0W + col1W;  // Gesamtbreite Adressbereich

        // Zeilenhoehen
        double headerH = familyInfo.EstimatedHeaderHeight * PxPerMm;
        double separatorH = familyInfo.EstimatedSeparatorHeight * PxPerMm;
        double dataRowH = familyInfo.EstimatedChannelRowHeight * PxPerMm;
        double bandDataH = MpModuleLayoutFactory.RowsPerBand * dataRowH;
        double bandTotalH = headerH + bandDataH;

        PreviewCanvas.Width = FormatDefinitions.PageWidth * PxPerMm;
        PreviewCanvas.Height = FormatDefinitions.PageHeight * PxPerMm;

        foreach (var mod in vm.MpModules)
        {
            int band = mod.Band;
            int col = mod.ColumnInBand;

            double bandStartY = marginT + band * (bandTotalH + separatorH);
            double modX = marginL + col * moduleW;
            double headerY = bandStartY;
            double dataStartY = bandStartY + headerH;

            bool isModSelected = mod == vm.SelectedMpModule;

            // === Header-Zeile (gemergt ueber 4 Spalten) ===
            DrawCell(modX, headerY, moduleW, headerH,
                mod.HeaderText, HeaderBgBrush, isModSelected, fontSize: 5, rotate: false, isBold: true);

            // === Adress-Zellen (Col 0+1) ===
            var layout = MpModuleLayoutFactory.GetLayout(mod.Variant);
            for (int i = 0; i < mod.AddressCells.Count && i < layout.AddressCells.Length; i++)
            {
                var def = layout.AddressCells[i];
                var cellVm = mod.AddressCells[i];

                double cellX = modX + (def.StartCol == 0 ? 0 : col0W);
                double cellW = def.ColSpan == 2 ? addrW : (def.StartCol == 0 ? col0W : col1W);
                double cellY = dataStartY + def.StartRow * dataRowH;
                double cellH = def.RowSpan * dataRowH;

                bool isCellSelected = isModSelected && cellVm == vm.SelectedMpCell;
                Brush bg = !def.IsEditable ? StructBrush
                    : cellVm.HasText ? FilledBrush
                    : EmptyBrush;

                DrawCell(cellX, cellY, cellW, cellH,
                    cellVm.Text, bg, isCellSelected,
                    fontSize: 5, rotate: format.IsVertical,
                    clickAction: () => SelectCell(vm, mod, cellVm));
            }

            // === Col 2: Netzadresse (2 Bloecke a 10 Zeilen, 90° rotiert) ===
            double col2X = modX + addrW;
            double block1H = MpModuleLayoutFactory.NetAddrBlockRows * dataRowH;
            DrawCell(col2X, dataStartY, col2W, block1H,
                mod.NetAddress1, NetAddrBgBrush, false, fontSize: 5, rotate: true);
            DrawCell(col2X, dataStartY + block1H, col2W, block1H,
                mod.NetAddress2, NetAddrBgBrush, false, fontSize: 5, rotate: true);

            // === Col 3: CPU-Name (20 Zeilen, 90° rotiert) ===
            double col3X = col2X + col2W;
            DrawCell(col3X, dataStartY, col3W, bandDataH,
                mod.CpuName, CpuNameBgBrush, false, fontSize: 5, rotate: true);
        }

        // === Separator-Zeile zwischen den Baendern ===
        if (format.BandsPerPage > 1)
        {
            double sepY = marginT + bandTotalH;
            var sepRect = new Rectangle
            {
                Width = printW, Height = separatorH,
                Fill = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Stroke = CellBorderBrush, StrokeThickness = 0.3
            };
            Canvas.SetLeft(sepRect, marginL);
            Canvas.SetTop(sepRect, sepY);
            PreviewCanvas.Children.Add(sepRect);
        }
    }

    private void DrawCell(double x, double y, double w, double h,
        string text, Brush background, bool isSelected,
        double fontSize = 5, bool rotate = false, bool isBold = false,
        Action? clickAction = null)
    {
        var rect = new Rectangle
        {
            Width = w, Height = h,
            Fill = isSelected ? SelectedBrush : background,
            Stroke = isSelected ? SelectedCellBorderBrush : CellBorderBrush,
            StrokeThickness = isSelected ? 1.5 : 0.3,
            Cursor = clickAction != null ? Cursors.Hand : null
        };
        if (clickAction != null)
            rect.MouseLeftButtonDown += (_, _) => clickAction();
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
        PreviewCanvas.Children.Add(rect);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var tb = new TextBlock
            {
                Text = text.Replace("\n", " / "),
                FontSize = fontSize,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                FontFamily = new FontFamily("Arial"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brushes.Black,
                MaxWidth = rotate ? h - 2 : w - 2,
                MaxHeight = rotate ? w - 2 : h - 2
            };

            if (rotate)
            {
                tb.LayoutTransform = new RotateTransform(-90);
                tb.Measure(new Size(h, w));
                Canvas.SetLeft(tb, x + (w + tb.DesiredSize.Width) / 2);
                Canvas.SetTop(tb, y + (h + tb.DesiredSize.Height) / 2);
            }
            else
            {
                tb.Measure(new Size(w - 2, h - 2));
                Canvas.SetLeft(tb, x + 1);
                Canvas.SetTop(tb, y + (h - tb.DesiredSize.Height) / 2);
            }
            PreviewCanvas.Children.Add(tb);
        }
    }

    private static void SelectCell(MainViewModel vm, MpModuleViewModel mod, MpAddressCellViewModel cell)
    {
        vm.SelectedMpModule = mod;
        vm.SelectedMpCell = cell;
    }
}
