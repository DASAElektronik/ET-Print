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
/// Jedes Modul hat 2 Haelften (obere + untere), die zum selben physischen Streifen gehoeren.
/// 5 Module (Spalten) pro A4-Seite.
/// </summary>
public partial class MpPreviewControl : UserControl
{
    private const double PxPerMm = 3.0;

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

        int modulesPerPage = familyInfo.ModulesPerPage;
        double moduleW = printW / modulesPerPage;

        double col0W = moduleW * MpModuleLayoutFactory.Col0Ratio;
        double col1W = moduleW * MpModuleLayoutFactory.Col1Ratio;
        double col2W = moduleW * MpModuleLayoutFactory.Col2Ratio;
        double col3W = moduleW * MpModuleLayoutFactory.Col3Ratio;
        double addrW = col0W + col1W;

        double headerH = familyInfo.EstimatedHeaderHeight * PxPerMm;
        double separatorH = familyInfo.EstimatedSeparatorHeight * PxPerMm;
        double dataRowH = familyInfo.EstimatedChannelRowHeight * PxPerMm;
        double halfDataH = MpModuleLayoutFactory.RowsPerHalf * dataRowH;

        PreviewCanvas.Width = FormatDefinitions.PageWidth * PxPerMm;
        PreviewCanvas.Height = FormatDefinitions.PageHeight * PxPerMm;

        foreach (var mod in vm.MpModules)
        {
            int col = mod.ModuleIndex;
            double modX = marginL + col * moduleW;
            bool isModSelected = mod == vm.SelectedMpModule;

            var layout = MpModuleLayoutFactory.GetLayout(mod.Variant);

            // === EIN ZUSAMMENHAENGENDER STREIFEN pro Modul ===

            // Header (einmal, oben)
            double headerY = marginT;
            DrawCell(modX, headerY, moduleW, headerH,
                mod.HeaderText, HeaderBgBrush, isModSelected, fontSize: 5, isBold: true);

            // Obere Haelfte (Half 0) — direkt unter Header
            double half0DataY = headerY + headerH;
            RenderHalfCells(vm, mod, layout, 0, modX, half0DataY,
                col0W, col1W, addrW, dataRowH, format.IsVertical, isModSelected);
            RenderNetAddrAndCpu(mod, 0, modX + addrW, half0DataY,
                col2W, col3W, dataRowH, halfDataH);

            // Untere Etiketten-Position (immer rendern — leere Gitterstruktur wenn kein Inhalt)
            double dividerY = half0DataY + halfDataH;
            double dividerH = separatorH;

            // Separator/Header der unteren Position
            DrawCell(modX, dividerY, moduleW, dividerH,
                "", HeaderBgBrush, false, fontSize: 5);

            double half1DataY = dividerY + dividerH;

            bool hasHalf1 = layout.AddressCells.Any(c => c.Half == 1);
            if (hasHalf1)
            {
                // Zellen der unteren Position rendern
                RenderHalfCells(vm, mod, layout, 1, modX, half1DataY,
                    col0W, col1W, addrW, dataRowH, format.IsVertical, isModSelected);
            }
            else
            {
                // Leere Gitterstruktur fuer die untere Position
                for (int row = 0; row < MpModuleLayoutFactory.RowsPerHalf; row++)
                {
                    double cellY = half1DataY + row * dataRowH;
                    DrawCell(modX, cellY, addrW, dataRowH, "", EmptyBrush, false);
                }
            }

            // NetAddr + CpuName der unteren Position (immer rendern)
            RenderNetAddrAndCpu(mod, 1, modX + addrW, half1DataY,
                col2W, col3W, dataRowH, halfDataH);

            // Multi-Selection-Markierung: orange Umrandung ueber das gesamte Modul
            if (mod.IsChecked)
            {
                double totalModH = headerH + halfDataH + separatorH + halfDataH;
                var checkedFrame = new Rectangle
                {
                    Width = moduleW,
                    Height = totalModH,
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
        MpModuleLayout layout, int half,
        double modX, double dataStartY,
        double col0W, double col1W, double addrW, double dataRowH,
        bool isVertical, bool isModSelected)
    {
        for (int i = 0; i < mod.AddressCells.Count && i < layout.AddressCells.Length; i++)
        {
            var def = layout.AddressCells[i];
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
                fontSize: 5, rotate: isVertical && def.IsEditable,
                foreground: def.IsEditable ? null : StructTextBrush,
                clickAction: def.IsEditable ? () => SelectCell(vm, mod, cellVm) : null);
        }
    }

    private void RenderNetAddrAndCpu(MpModuleViewModel mod, int half,
        double col2X, double dataStartY,
        double col2W, double col3W, double dataRowH, double halfDataH)
    {
        double blockH = MpModuleLayoutFactory.NetAddrBlockRows * dataRowH;

        string net1 = half == 0 ? mod.NetAddress1 : mod.NetAddress3;
        string net2 = half == 0 ? mod.NetAddress2 : mod.NetAddress4;

        DrawCell(col2X, dataStartY, col2W, blockH,
            net1, NetAddrBgBrush, false, fontSize: 5, rotate: true);
        DrawCell(col2X, dataStartY + blockH, col2W, blockH,
            net2, NetAddrBgBrush, false, fontSize: 5, rotate: true);

        double col3X = col2X + col2W;
        DrawCell(col3X, dataStartY, col3W, halfDataH,
            mod.CpuName, CpuNameBgBrush, false, fontSize: 5, rotate: true);
    }

    private void DrawCell(double x, double y, double w, double h,
        string text, Brush background, bool isSelected,
        double fontSize = 5, bool rotate = false, bool isBold = false,
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
                FontFamily = new FontFamily("Arial"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = foreground ?? Brushes.Black,
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
