using ETPrinter.Models;
using ETPrinter.Services;
using Xunit;

namespace ETPrinter.Tests;

/// <summary>ABSCHLUSSPLAN AP3: eine Geometrie-Quelle fuer Druck, Vorschau und Kalibrierseite.</summary>
public class SheetGeometryTests
{
    private const double Tol = 1e-6;

    private static SheetGeometry Sp(LabelFormat format = LabelFormat.VerticalDoubleHeader, double calX = 0, double calY = 0)
    {
        var settings = new LabelSettings(); // 20.5 / 27.5 / 20.5 / 27.5
        return SheetGeometry.For(FormatDefinitions.Get(format), settings, calX, calY);
    }

    private static SheetGeometry Mp(ProductFamily family = ProductFamily.S71500_ET200MP)
    {
        var settings = new LabelSettings();
        settings.ResetForFamily(family); // 14 / 25 / 19 / 12
        return SheetGeometry.For(FormatDefinitions.GetDefaultFormat(family), settings);
    }

    // ---- ET200SP -----------------------------------------------------------

    [Fact]
    public void Sp_CellSize_MatchesSiemensSheet()
    {
        // 5 x 31 mm = 155 mm Breite, 20 x 12.8 mm = 256 mm Hoehe (PRINT-FORMATS.md)
        var geo = Sp(LabelFormat.HorizontalDouble);
        Assert.Equal(31.0, geo.SpGroupWidth, Tol);
        Assert.Equal(12.8, geo.SpCellHeight, Tol);
        Assert.Equal(0, geo.SpHeaderWidth, Tol);
        Assert.Equal(31.0, geo.SpCellWidth, Tol);
    }

    [Fact]
    public void Sp_HeaderFormat_HeaderIs20Percent()
    {
        var geo = Sp(LabelFormat.VerticalDoubleHeader);
        Assert.Equal(6.2, geo.SpHeaderWidth, Tol);
        Assert.Equal(24.8, geo.SpCellWidth, Tol);
        Assert.Equal(geo.SpGroupWidth, geo.SpHeaderWidth + geo.SpCellWidth, Tol);
    }

    [Fact]
    public void Sp_Index0_IsBottomRight()
    {
        var geo = Sp();
        var r = geo.SpLabelRect(0);
        Assert.Equal(210 - 27.5 - 31.0, r.X, Tol);      // rechte Spalte
        Assert.Equal(297 - 20.5 - 12.8, r.Y, Tol);      // unterste Zeile
        Assert.Equal((4, 19), geo.SpPosition(0));
    }

    [Fact]
    public void Sp_Index4_IsBottomLeft_Index5_OneRowUp()
    {
        var geo = Sp();
        Assert.Equal(27.5, geo.SpLabelRect(4).X, Tol);
        Assert.Equal(geo.SpLabelRect(0).Y, geo.SpLabelRect(4).Y, Tol);
        Assert.Equal(geo.SpLabelRect(0).Y - 12.8, geo.SpLabelRect(5).Y, Tol);
        Assert.Equal(geo.SpLabelRect(0).X, geo.SpLabelRect(5).X, Tol);
    }

    [Fact]
    public void Sp_Index99_IsTopLeft()
    {
        var geo = Sp();
        var r = geo.SpLabelRect(99);
        Assert.Equal(27.5, r.X, Tol);
        Assert.Equal(20.5, r.Y, Tol);
    }

    [Fact]
    public void Sp_HeaderAndContent_TileTheLabel()
    {
        var geo = Sp();
        var label = geo.SpLabelRect(7);
        var header = geo.SpHeaderRect(7);
        var content = geo.SpContentRect(7);
        Assert.Equal(label.X, header.X, Tol);
        Assert.Equal(header.Right, content.X, Tol);
        Assert.Equal(label.Right, content.Right, Tol);
    }

    [Fact]
    public void Sp_Calibration_ShiftsWholeGrid()
    {
        var a = Sp();
        var b = Sp(calX: 1.5, calY: -2.0);
        Assert.Equal(a.SpLabelRect(0).X + 1.5, b.SpLabelRect(0).X, Tol);
        Assert.Equal(a.SpLabelRect(0).Y - 2.0, b.SpLabelRect(0).Y, Tol);
        Assert.Equal(a.GridRect.W, b.GridRect.W, Tol);
    }

    [Fact]
    public void Sp_GridRect_CoversAllLabels()
    {
        var geo = Sp();
        var grid = geo.GridRect;
        Assert.Equal(27.5, grid.X, Tol);
        Assert.Equal(20.5, grid.Y, Tol);
        Assert.Equal(155.0, grid.W, Tol);
        Assert.Equal(256.0, grid.H, Tol);
        Assert.Equal(grid.Right, geo.SpLabelRect(0).Right, Tol);
        Assert.Equal(grid.Bottom, geo.SpLabelRect(0).Bottom, Tol);
    }

    // ---- ET200MP 35mm -------------------------------------------------------

    [Fact]
    public void Mp_ModuleWidth_IsPrintWidthOverColumns()
    {
        var geo = Mp();
        Assert.Equal((210 - 25 - 12) / 5.0, geo.ModuleWidth, Tol);
        Assert.Equal(geo.ModuleWidth, geo.Col0Width + geo.Col1Width + geo.Col2Width + geo.Col3Width, 1e-9);
    }

    [Fact]
    public void Mp_Band1AndBand2_Headers()
    {
        var geo = Mp();
        var h0 = geo.MpHeaderRect(0);
        Assert.Equal(25.0, h0.X, Tol);
        Assert.Equal(14.0, h0.Y, Tol);
        Assert.Equal(25.7, h0.H, Tol);

        var h5 = geo.MpHeaderRect(5); // Band 2, Spalte 1
        Assert.Equal(25.0, h5.X, Tol);
        Assert.Equal(14.0 + 25.7 + 20 * 5.6, h5.Y, Tol);
        Assert.Equal(20.6, h5.H, Tol);

        var h7 = geo.MpHeaderRect(7); // Band 2, Spalte 3
        Assert.Equal(25.0 + 2 * geo.ModuleWidth, h7.X, Tol);
    }

    [Fact]
    public void Mp_CellRect_FollowsDefinition()
    {
        var geo = Mp();
        var def = new MpCellDefinition(Half: 0, StartRow: 3, RowSpan: 2, StartCol: 1, ColSpan: 1, IsEditable: true);
        var r = geo.MpCellRect(0, def);
        Assert.Equal(25.0 + geo.Col0Width, r.X, Tol);
        Assert.Equal(geo.MpDataStartY(0) + 3 * 5.6, r.Y, Tol);
        Assert.Equal(geo.Col1Width, r.W, Tol);
        Assert.Equal(2 * 5.6, r.H, Tol);

        var merged = new MpCellDefinition(0, 0, 4, 0, 2, true);
        Assert.Equal(geo.AddressWidth, geo.MpCellRect(0, merged).W, Tol);
    }

    [Fact]
    public void Mp_NetAddrAndCpu_ColumnsTileTheModule()
    {
        var geo = Mp();
        var n1 = geo.MpNetAddressRect(2, 0);
        var n2 = geo.MpNetAddressRect(2, 1);
        var cpu = geo.MpCpuRect(2);
        Assert.Equal(geo.MpModuleX(2) + geo.AddressWidth, n1.X, Tol);
        Assert.Equal(n1.Bottom, n2.Y, Tol);
        Assert.Equal(n1.Right, cpu.X, Tol);
        Assert.Equal(geo.MpModuleX(2) + geo.ModuleWidth, cpu.Right, Tol);
        Assert.Equal(geo.BandDataHeight, cpu.H, Tol);
    }

    [Fact]
    public void Mp_GridRect_IsFixedHeightIndependentOfBottomMargin()
    {
        var geo = Mp();
        var grid = geo.GridRect;
        Assert.Equal(173.0, grid.W, Tol);
        Assert.Equal(25.7 + 112 + 20.6 + 112, grid.H, Tol);

        // "Rand unten" hat auf das MP-Raster keinen Einfluss
        var settings = new LabelSettings();
        settings.ResetForFamily(ProductFamily.S71500_ET200MP);
        settings.MarginBottom = 40;
        var geo2 = SheetGeometry.For(FormatDefinitions.Get(LabelFormat.MP_Horizontal), settings);
        Assert.Equal(grid.H, geo2.GridRect.H, Tol);
    }

    // ---- ET200MP 25mm -------------------------------------------------------

    [Fact]
    public void Mp25_TenColumns_NoNetAddressColumn()
    {
        var geo = Mp(ProductFamily.S71500_ET200MP_25mm);
        Assert.Equal(173.0 / 10, geo.ModuleWidth, Tol);
        Assert.Equal(0, geo.Col2Width, Tol);
        Assert.False(geo.Family.HasNetAddressColumn);
        Assert.Equal(1, geo.BandOf(10));
        Assert.Equal(0, geo.ColumnOf(10));
        Assert.Equal(geo.MpHeaderRect(0).X, geo.MpHeaderRect(10).X, Tol);
        Assert.Equal(20.6, geo.MpHeaderRect(19).H, Tol);
    }

    // ---- Wrapper-Kompatibilitaet ----------------------------------------------

    [Fact]
    public void GetCellSize_MatchesGeometry()
    {
        var settings = new LabelSettings();
        var fmt = FormatDefinitions.Get(LabelFormat.HorizontalDoubleHeader);
        var (w, h, hw) = FormatDefinitions.GetCellSize(fmt, settings);
        var geo = SheetGeometry.For(fmt, settings);
        Assert.Equal(geo.SpCellWidth, w, Tol);
        Assert.Equal(geo.SpCellHeight, h, Tol);
        Assert.Equal(geo.SpHeaderWidth, hw, Tol);
    }

    [Fact]
    public void RectMm_Scale_ConvertsUnits()
    {
        var r = new RectMm(10, 20, 30, 40).Scale(3);
        Assert.Equal(30, r.X);
        Assert.Equal(60, r.Y);
        Assert.Equal(90, r.Width);
        Assert.Equal(120, r.Height);
    }
}
