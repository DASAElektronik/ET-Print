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
/// Der A4-Bogen hat 10 Streifen-Positionen: Band 0 (oben, Header 25.7mm) und
/// Band 1 (unten, Header 20.6mm) mit je 5 Spalten. Jede Position = 1 Modul.
/// </summary>
public partial class MpPreviewControl : UserControl
{
    private const double PxPerMm = 3.0;
    // FontSize ist in Punkt (docs/PRINT-FORMATS: 7pt Standard); 1pt = 25.4/72 mm.
    private const double PtToPx = PxPerMm * 25.4 / 72.0;

    private static readonly Brush EmptyBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
    private static readonly Brush FilledBrush = new SolidColorBrush(Color.FromRgb(208, 232, 208));
    private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromRgb(184, 212, 240));
    private static readonly Brush HeaderBgBrush = new SolidColorBrush(Color.FromRgb(216, 216, 232));
    private static readonly Brush NetAddrBgBrush = new SolidColorBrush(Color.FromRgb(232, 232, 248));
    private static readonly Brush CpuNameBgBrush = new SolidColorBrush(Color.FromRgb(248, 240, 232));
    private static readonly Brush StructBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
    private static readonly Brush StructTextBrush = new SolidColorBrush(Color.FromRgb(140, 140, 140));
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
                if (e.PropertyName is nameof(vm.SelectedMpModule)
                                    or nameof(vm.SelectedMpCell)
                                    or nameof(vm.MpPreviewRefreshToken))
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

        double printW = (FormatDefinitions.PageWidth - settings.MarginLeft - settings.MarginRight) * PxPerMm;
        double marginL = settings.MarginLeft * PxPerMm;
        double marginT = settings.MarginTop * PxPerMm;

        double moduleW = printW / familyInfo.ColumnsPerPage;

        double col0W = moduleW * MpModuleLayoutFactory.Col0Ratio;
        double col1W = moduleW * MpModuleLayoutFactory.Col1Ratio;
        double col2W = moduleW * MpModuleLayoutFactory.Col2Ratio;
        double col3W = moduleW * MpModuleLayoutFactory.Col3Ratio;
        double addrW = col0W + col1W;

        double headerH = familyInfo.EstimatedHeaderHeight * PxPerMm;
        double band2HeaderH = familyInfo.EstimatedBand2HeaderHeight * PxPerMm;
        double dataRowH = familyInfo.EstimatedChannelRowHeight * PxPerMm;
        double bandDataH = MpModuleLayoutFactory.RowsPerHalf * dataRowH;

        PreviewCanvas.Width = FormatDefinitions.PageWidth * PxPerMm;
        PreviewCanvas.Height = FormatDefinitions.PageHeight * PxPerMm;

        foreach (var mod in vm.MpModules)
        {
            // 10 Positionen: Band 0 oben (hoher Header), Band 1 unten (flacher Header)
            int band = familyInfo.BandOf(mod.ModuleIndex);
            int col = familyInfo.ColumnOf(mod.ModuleIndex);
            double modX = marginL + col * moduleW;
            bool isModSelected = mod == vm.SelectedMpModule;

            // Katalog-Belegung (konkretes Siemens-Modul) oder Varianten-Default
            var definitions = MpModuleLayoutFactory.GetDefinitions(mod.GetModule());

            double modHeaderH = band == 0 ? headerH : band2HeaderH;
            double headerY = band == 0 ? marginT : marginT + headerH + bandDataH;

            // Header — globaler Header-Style aus Settings.
            double headerFs = settings.HeaderFontSize * PtToPx;
            DrawCell(modX, headerY, moduleW, modHeaderH,
                mod.HeaderText, HeaderBgBrush, isModSelected, fontSize: headerFs,
                isBold: settings.HeaderIsBold, fontFamily: mod.FontFamily,
                clickAction: () => SelectModule(vm, mod));

            double dataStartY = headerY + modHeaderH;
            RenderHalfCells(vm, mod, definitions, 0, modX, dataStartY,
                col0W, col1W, addrW, dataRowH, format.IsVertical, isModSelected);
            RenderNetAddrAndCpu(mod, modX + addrW, dataStartY,
                col2W, col3W, dataRowH, bandDataH);

            // Multi-Selection-Markierung: orange Umrandung ueber die Streifen-Position
            if (mod.IsChecked)
            {
                var checkedFrame = new Rectangle
                {
                    Width = moduleW,
                    Height = modHeaderH + bandDataH,
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(checkedFrame, modX);
                Canvas.SetTop(checkedFrame, headerY);
                PreviewCanvas.Children.Add(checkedFrame);
            }
        }
    }

    private void RenderHalfCells(MainViewModel vm, MpModuleViewModel mod,
        MpCellDefinition[] definitions, int half,
        double modX, double dataStartY,
        double col0W, double col1W, double addrW, double dataRowH,
        bool isVertical, bool isModSelected)
    {
        for (int i = 0; i < mod.AddressCells.Count && i < definitions.Length; i++)
        {
            var def = definitions[i];
            if (def.Half != half) continue;

            var cellVm = mod.AddressCells[i];

            double cellX = modX + (def.StartCol == 0 ? 0 : col0W);
            double cellW = def.ColSpan == 2 ? addrW : (def.StartCol == 0 ? col0W : col1W);
            double cellY = dataStartY + def.StartRow * dataRowH;
            double cellH = def.RowSpan * dataRowH;

            bool isCellSelected = isModSelected && cellVm == vm.SelectedMpCell;

            string displayText;
            Brush bg;
            if (!def.IsEditable)
            {
                bg = StructBrush;
                displayText = def.Label;
            }
            else
            {
                bg = cellVm.HasText ? FilledBrush : EmptyBrush;
                displayText = cellVm.Text;
            }

            DrawCell(cellX, cellY, cellW, cellH,
                displayText, bg, isCellSelected,
                fontSize: mod.FontSize * PtToPx, rotate: isVertical && def.IsEditable,
                isBold: mod.IsBold, fontFamily: mod.FontFamily,
                foreground: def.IsEditable ? null : StructTextBrush,
                clickAction: def.IsEditable ? () => SelectCell(vm, mod, cellVm) : null);
        }
    }

    private void RenderNetAddrAndCpu(MpModuleViewModel mod,
        double col2X, double dataStartY,
        double col2W, double col3W, double dataRowH, double bandDataH)
    {
        double blockH = MpModuleLayoutFactory.NetAddrBlockRows * dataRowH;
        double fs = mod.FontSize * PtToPx;

        DrawCell(col2X, dataStartY, col2W, blockH,
            mod.NetAddress1, NetAddrBgBrush, false, fontSize: fs, rotate: true, fontFamily: mod.FontFamily);
        DrawCell(col2X, dataStartY + blockH, col2W, blockH,
            mod.NetAddress2, NetAddrBgBrush, false, fontSize: fs, rotate: true, fontFamily: mod.FontFamily);

        double col3X = col2X + col2W;
        DrawCell(col3X, dataStartY, col3W, bandDataH,
            mod.CpuName, CpuNameBgBrush, false, fontSize: fs, rotate: true, fontFamily: mod.FontFamily);
    }

    private void DrawCell(double x, double y, double w, double h,
        string text, Brush background, bool isSelected,
        double fontSize = 5, bool rotate = false, bool isBold = false,
        string fontFamily = "Arial",
        Brush? foreground = null, Action? clickAction = null)
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
                FontFamily = new FontFamily(fontFamily),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = foreground ?? Brushes.Black,
                MaxWidth = rotate ? h - 2 : w - 2,
                MaxHeight = rotate ? w - 2 : h - 2
            };

            // Wie im Druck (RenderRotatedText): Border als Container zentriert den
            // Text auf beiden Achsen — identische Struktur garantiert Preview = Druck.
            var container = new Border
            {
                Width = w, Height = h,
                Child = tb,
                IsHitTestVisible = false
            };
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            if (rotate)
                tb.LayoutTransform = new RotateTransform(-90);
            Canvas.SetLeft(container, x);
            Canvas.SetTop(container, y);
            PreviewCanvas.Children.Add(container);
        }
    }

    /// <summary>Klick auf den Modul-Header: Modul selektieren (ohne Zellwechsel).
    /// Wichtig fuer Band-2-Positionen, die sonst nur ueber Zellklicks erreichbar waeren.</summary>
    private static void SelectModule(MainViewModel vm, MpModuleViewModel mod)
    {
        var mods = Keyboard.Modifiers;
        if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            vm.CheckRangeToMpModule(mod);
        }
        else if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
        {
            mod.IsChecked = !mod.IsChecked;
            vm.SelectedMpModule = mod;
            vm.NotifyMpPreviewChanged();
        }
        else
        {
            vm.ClearAllMpModuleChecks();
            vm.SelectedMpModule = mod;
        }
    }

    private static void SelectCell(MainViewModel vm, MpModuleViewModel mod, MpAddressCellViewModel cell)
    {
        var mods = Keyboard.Modifiers;
        if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            // Shift+Klick: Range von SelectedMpModule bis mod markieren
            vm.CheckRangeToMpModule(mod);
            vm.SelectedMpCell = cell;
        }
        else if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
        {
            // Strg+Klick: Modul-Markierung togglen
            mod.IsChecked = !mod.IsChecked;
            vm.SelectedMpModule = mod;
            vm.SelectedMpCell = cell;
            vm.NotifyMpPreviewChanged();
        }
        else
        {
            // Normal-Klick: alle Checks aufheben, Single-Selection
            vm.ClearAllMpModuleChecks();
            vm.SelectedMpModule = mod;
            vm.SelectedMpCell = cell;
        }
    }
}
