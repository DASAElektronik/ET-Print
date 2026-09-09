using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ETPrinter.Models;
using ETPrinter.Services;
using ETPrinter.ViewModels;

namespace ETPrinter.Controls;

/// <summary>
/// Canvas-basierte Vorschau fuer ET200MP Module.
/// Der A4-Bogen hat ModulesPerPage Streifen-Positionen: Band 0 (oben, Header 25.7mm)
/// und Band 1 (unten, Header 20.6mm) mit je ColumnsPerPage Spalten (35mm: 5, 25mm: 10).
/// Jede Position = 1 Modul.
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

    // Entprelltes Rendern: jeder Tastendruck im Header/einer Zelle und jeder
    // Seitenwechsel (Clear + 10-20x Add) loeste frueher einen kompletten Neuaufbau
    // von 1000-1700 Canvas-Elementen aus — pro Ereignis.
    private readonly DispatcherTimer _renderTimer;
    private MainViewModel? _vm;
    private NotifyCollectionChangedEventHandler? _collectionHandler;
    private PropertyChangedEventHandler? _propertyHandler;

    // Opacity der Elemente des gerade gezeichneten Moduls (0.4 = vom Druck ausgeschlossen)
    private double _moduleOpacity = 1.0;

    public MpPreviewControl()
    {
        InitializeComponent();
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(40) };
        _renderTimer.Tick += (_, _) => { _renderTimer.Stop(); Render(); };
        DataContextChanged += (_, _) => BindToModules();
    }

    private void BindToModules()
    {
        // Altes ViewModel abmelden — sonst rendert jede frueher gebundene Instanz mit
        if (_vm is not null)
        {
            if (_collectionHandler is not null) _vm.MpModules.CollectionChanged -= _collectionHandler;
            if (_propertyHandler is not null) _vm.PropertyChanged -= _propertyHandler;
        }
        _vm = DataContext as MainViewModel;
        if (_vm is null) return;

        _collectionHandler = (_, _) => ScheduleRender();
        _propertyHandler = (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.SelectedMpModule)
                                or nameof(MainViewModel.SelectedMpCell)
                                or nameof(MainViewModel.MpPreviewRefreshToken)
                                or nameof(MainViewModel.IsModuleBased))
                ScheduleRender();
        };
        _vm.MpModules.CollectionChanged += _collectionHandler;
        _vm.PropertyChanged += _propertyHandler;
        Render();
    }

    /// <summary>Render-Anforderung buendeln (mehrere Ereignisse -> ein Neuaufbau).</summary>
    public void ScheduleRender()
    {
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    /// <summary>Ausstehenden Neuaufbau sofort ausfuehren (Test-Automation: Screenshot
    /// direkt nach einer Aenderung, bevor der Debounce-Timer gefeuert hat).</summary>
    public void FlushRender()
    {
        if (_renderTimer.IsEnabled)
        {
            _renderTimer.Stop();
            Render();
        }
    }

    public void Render()
    {
        PreviewCanvas.Children.Clear();
        if (DataContext is not MainViewModel vm) return;
        if (!vm.IsModuleBased || vm.MpModules.Count == 0) return;

        var format = vm.SelectedFormat;
        var settings = vm.Settings;

        // Geometrie ausschliesslich aus SheetGeometry (identisch zum Druck; ohne
        // Kalibrier-Versatz, die Vorschau zeigt das unverschobene Raster)
        var geo = SheetGeometry.For(format, settings);

        PreviewCanvas.Width = FormatDefinitions.PageWidth * PxPerMm;
        PreviewCanvas.Height = FormatDefinitions.PageHeight * PxPerMm;

        foreach (var mod in vm.MpModules)
        {
            int idx = mod.ModuleIndex;
            bool isModSelected = mod == vm.SelectedMpModule;
            _moduleOpacity = mod.PrintOpacity;

            // Katalog-Belegung (konkretes Siemens-Modul) oder Varianten-Default
            var definitions = MpModuleLayoutFactory.GetDefinitions(mod.GetModule());

            // Header — globaler Header-Style aus Settings, mehrzeilig wie im Druck.
            var header = geo.MpHeaderRect(idx).Scale(PxPerMm);
            double headerFs = settings.HeaderFontSize * PtToPx;
            DrawCell(header.X, header.Y, header.Width, header.Height,
                mod.HeaderText, HeaderBgBrush, isModSelected, fontSize: headerFs,
                isBold: settings.HeaderIsBold, fontFamily: mod.FontFamily,
                wrap: true,
                clickAction: () => SelectModule(vm, mod));

            RenderHalfCells(vm, mod, definitions, 0, geo, format.IsVertical, isModSelected);
            RenderNetAddrAndCpu(mod, geo);

            // Multi-Selection-Markierung: orange Umrandung ueber die Streifen-Position
            if (mod.IsChecked)
            {
                var r = geo.MpModuleRect(idx).Scale(PxPerMm);
                var checkedFrame = new Rectangle
                {
                    Width = r.Width,
                    Height = r.Height,
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                    StrokeThickness = 2,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(checkedFrame, r.X);
                Canvas.SetTop(checkedFrame, r.Y);
                PreviewCanvas.Children.Add(checkedFrame);
            }
        }
        _moduleOpacity = 1.0;
    }

    private void RenderHalfCells(MainViewModel vm, MpModuleViewModel mod,
        MpCellDefinition[] definitions, int half, SheetGeometry geo,
        bool isVertical, bool isModSelected)
    {
        for (int i = 0; i < mod.AddressCells.Count && i < definitions.Length; i++)
        {
            var def = definitions[i];
            if (def.Half != half) continue;

            var cellVm = mod.AddressCells[i];
            var cell = geo.MpCellRect(mod.ModuleIndex, def).Scale(PxPerMm);
            double cellX = cell.X, cellY = cell.Y, cellW = cell.Width, cellH = cell.Height;

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
                isBold: mod.IsBold, isItalic: mod.IsItalic, fontFamily: mod.FontFamily,
                foreground: def.IsEditable ? null : StructTextBrush,
                clickAction: def.IsEditable ? () => SelectCell(vm, mod, cellVm) : null);
        }
    }

    private void RenderNetAddrAndCpu(MpModuleViewModel mod, SheetGeometry geo)
    {
        double fs = mod.FontSize * PtToPx;
        int idx = mod.ModuleIndex;

        // Net-Address-Spalte nur bei 35mm (25mm hat keine)
        if (geo.Family.HasNetAddressColumn)
        {
            var n1 = geo.MpNetAddressRect(idx, 0).Scale(PxPerMm);
            var n2 = geo.MpNetAddressRect(idx, 1).Scale(PxPerMm);
            DrawCell(n1.X, n1.Y, n1.Width, n1.Height,
                mod.NetAddress1, NetAddrBgBrush, false, fontSize: fs, rotate: true,
                isItalic: mod.IsItalic, fontFamily: mod.FontFamily);
            DrawCell(n2.X, n2.Y, n2.Width, n2.Height,
                mod.NetAddress2, NetAddrBgBrush, false, fontSize: fs, rotate: true,
                isItalic: mod.IsItalic, fontFamily: mod.FontFamily);
        }

        var cpu = geo.MpCpuRect(idx).Scale(PxPerMm);
        DrawCell(cpu.X, cpu.Y, cpu.Width, cpu.Height,
            mod.CpuName, CpuNameBgBrush, false, fontSize: fs, rotate: true,
            isItalic: mod.IsItalic, fontFamily: mod.FontFamily);
    }

    private void DrawCell(double x, double y, double w, double h,
        string text, Brush background, bool isSelected,
        double fontSize = 5, bool rotate = false, bool isBold = false,
        bool isItalic = false,
        string fontFamily = "Arial",
        bool wrap = false,
        Brush? foreground = null, Action? clickAction = null)
    {
        var rect = new Rectangle
        {
            Width = w, Height = h,
            Fill = isSelected ? SelectedBrush : background,
            Stroke = isSelected ? SelectedCellBorderBrush : CellBorderBrush,
            StrokeThickness = isSelected ? 1.5 : 0.3,
            Cursor = clickAction != null ? Cursors.Hand : null,
            Opacity = _moduleOpacity
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
                Text = wrap ? text.Replace("\r\n", "\n") : text.Replace("\n", " / "),
                FontSize = fontSize,
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal,
                FontFamily = new FontFamily(fontFamily),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = foreground ?? Brushes.Black,
                MaxWidth = rotate ? h - 2 : w - 2,
                MaxHeight = rotate ? w - 2 : h - 2
            };
            if (wrap)
            {
                tb.TextWrapping = TextWrapping.Wrap;
                tb.TextAlignment = TextAlignment.Center;
                tb.TextTrimming = TextTrimming.None;
            }

            // Wie im Druck (RenderRotatedText): Border als Container zentriert den
            // Text auf beiden Achsen — identische Struktur garantiert Preview = Druck.
            var container = new Border
            {
                Width = w, Height = h,
                Child = tb,
                IsHitTestVisible = false,
                Opacity = _moduleOpacity
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
            vm.ToggleCheck(mod);
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
            vm.ToggleCheck(mod);
            vm.SelectedMpCell = cell;
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
