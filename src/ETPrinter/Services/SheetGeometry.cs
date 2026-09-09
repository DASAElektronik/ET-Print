using ETPrinter.Models;

namespace ETPrinter.Services;

/// <summary>Rechteck in Millimetern auf dem A4-Blatt (Ursprung oben links).</summary>
public readonly record struct RectMm(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;

    /// <summary>Skaliert in Geraete-Einheiten (Druck: 96/25.4 DIP je mm, Vorschau: 3 px je mm).</summary>
    public System.Windows.Rect Scale(double factor) => new(X * factor, Y * factor, W * factor, H * factor);
}

/// <summary>
/// EINZIGE Quelle fuer die Seitengeometrie in Millimetern. Druck (PrintService),
/// Kalibrierseite und Vorschau (MpPreviewControl, SP-XAML ueber MainViewModel)
/// rechnen ausschliesslich hierueber — frueher war die MP-Geometrie dreifach
/// dupliziert und die SP-Vorschau wich mit einer festen 18-px-Kopfspalte ~2 % vom
/// Druck ab. Die Umrechnung in Pixel/DIP passiert erst beim Aufrufer (Scale).
/// </summary>
public sealed class SheetGeometry
{
    public const double PageWidthMm = FormatDefinitions.PageWidth;
    public const double PageHeightMm = FormatDefinitions.PageHeight;

    public FormatInfo Format { get; }
    public ProductFamilyInfo Family { get; }
    public LabelSettings Settings { get; }

    /// <summary>Kalibrier-Versatz in mm (positiv = rechts/unten). Nur fuer den Druck;
    /// die Vorschau zeigt das unverschobene Raster.</summary>
    public double CalibrationX { get; }
    public double CalibrationY { get; }

    private SheetGeometry(FormatInfo format, LabelSettings settings, double calX, double calY)
    {
        Format = format;
        Family = ProductFamilyDefinitions.Get(format.Family);
        Settings = settings;
        CalibrationX = calX;
        CalibrationY = calY;
    }

    public static SheetGeometry For(FormatInfo format, LabelSettings settings, double calX = 0, double calY = 0)
        => new(format, settings, calX, calY);

    // ------------------------------------------------------------------
    // Gemeinsam
    // ------------------------------------------------------------------

    /// <summary>Linke obere Ecke des Etikettenrasters (mm, inkl. Kalibrierung).</summary>
    public double OriginX => Settings.MarginLeft + CalibrationX;
    public double OriginY => Settings.MarginTop + CalibrationY;

    /// <summary>Druckbereich-Breite zwischen linkem und rechtem Rand.</summary>
    public double PrintWidth => PageWidthMm - Settings.MarginLeft - Settings.MarginRight;

    /// <summary>Druckbereich-Hoehe zwischen oberem und unterem Rand (nur ET200SP relevant).</summary>
    public double PrintHeight => PageHeightMm - Settings.MarginTop - Settings.MarginBottom;

    /// <summary>Gesamtes Etikettenraster (fuer Kalibrier-Fadenkreuze: Ecken oben links / unten rechts).</summary>
    public RectMm GridRect => Format.IsModuleBased
        ? new RectMm(OriginX, OriginY, PrintWidth, MpTotalHeight)
        : new RectMm(OriginX, OriginY, SpGroupWidth * Format.LabelsPerRow, SpCellHeight * Format.LabelRows);

    // ------------------------------------------------------------------
    // ET 200SP: LabelsPerRow x LabelRows Etiketten, Position 1 = unten rechts
    // ------------------------------------------------------------------

    /// <summary>Breite einer Etikettengruppe (Kopfspalte + Textfeld).</summary>
    public double SpGroupWidth => PrintWidth / Format.LabelsPerRow;

    /// <summary>Kopfspalte: 20 % der Gruppenbreite bei "+Kopfzeile"-Formaten (Excel-Template: 804/4058).</summary>
    public double SpHeaderWidth => Format.HasHeader ? SpGroupWidth * 0.2 : 0;

    public double SpCellWidth => SpGroupWidth - SpHeaderWidth;
    public double SpCellHeight => PrintHeight / Format.LabelRows;

    /// <summary>Physische Spalte/Zeile eines Etiketts: Index 0 = unten rechts (wie der
    /// Siemens-Bogen 6ES7193-6LA10-0AA0 nummeriert), Index steigt nach links und nach oben.</summary>
    public (int physCol, int physRow) SpPosition(int index)
    {
        int col = index % Format.LabelsPerRow;
        int row = index / Format.LabelsPerRow;
        return ((Format.LabelsPerRow - 1) - col, (Format.LabelRows - 1) - row);
    }

    /// <summary>Gesamtes Etikett (Kopfspalte + Textfeld).</summary>
    public RectMm SpLabelRect(int index)
    {
        var (physCol, physRow) = SpPosition(index);
        return new RectMm(OriginX + physCol * SpGroupWidth, OriginY + physRow * SpCellHeight, SpGroupWidth, SpCellHeight);
    }

    /// <summary>Kopfspalte (links im Etikett), Breite 0 ohne Kopfzeile.</summary>
    public RectMm SpHeaderRect(int index)
    {
        var r = SpLabelRect(index);
        return new RectMm(r.X, r.Y, SpHeaderWidth, r.H);
    }

    /// <summary>Textfeld rechts von der Kopfspalte.</summary>
    public RectMm SpContentRect(int index)
    {
        var r = SpLabelRect(index);
        return new RectMm(r.X + SpHeaderWidth, r.Y, SpCellWidth, r.H);
    }

    // ------------------------------------------------------------------
    // ET 200MP: ModulesPerPage Streifen in 2 Baendern, von oben gerastert
    // mit festen Hoehen (Header 25.7 / 20.6 mm, 20 Zeilen x 5.6 mm je Band).
    // "Rand unten" hat hier keine Wirkung.
    // ------------------------------------------------------------------

    public double ModuleWidth => PrintWidth / Family.ColumnsPerPage;
    public double Col0Width => ModuleWidth * Family.Col0Ratio;
    public double Col1Width => ModuleWidth * Family.Col1Ratio;
    public double Col2Width => ModuleWidth * Family.Col2Ratio;
    public double Col3Width => ModuleWidth * Family.Col3Ratio;
    public double AddressWidth => Col0Width + Col1Width;

    public double Band1HeaderHeight => Family.EstimatedHeaderHeight;
    public double Band2HeaderHeight => Family.EstimatedBand2HeaderHeight;
    public double DataRowHeight => Family.EstimatedChannelRowHeight;
    public double BandDataHeight => Family.RowsPerHalf * DataRowHeight;
    public double MpTotalHeight => Band1HeaderHeight + BandDataHeight + Band2HeaderHeight + BandDataHeight;

    public int BandOf(int moduleIndex) => Family.BandOf(moduleIndex);
    public int ColumnOf(int moduleIndex) => Family.ColumnOf(moduleIndex);

    public double MpModuleX(int moduleIndex) => OriginX + ColumnOf(moduleIndex) * ModuleWidth;
    public double MpHeaderHeight(int moduleIndex) => BandOf(moduleIndex) == 0 ? Band1HeaderHeight : Band2HeaderHeight;
    public double MpHeaderY(int moduleIndex) => BandOf(moduleIndex) == 0
        ? OriginY
        : OriginY + Band1HeaderHeight + BandDataHeight;
    public double MpDataStartY(int moduleIndex) => MpHeaderY(moduleIndex) + MpHeaderHeight(moduleIndex);

    public RectMm MpHeaderRect(int moduleIndex) =>
        new(MpModuleX(moduleIndex), MpHeaderY(moduleIndex), ModuleWidth, MpHeaderHeight(moduleIndex));

    /// <summary>Kompletter Streifen (Header + Datenbereich), z.B. fuer die Multi-Selection-Markierung.</summary>
    public RectMm MpModuleRect(int moduleIndex) =>
        new(MpModuleX(moduleIndex), MpHeaderY(moduleIndex), ModuleWidth, MpHeaderHeight(moduleIndex) + BandDataHeight);

    /// <summary>Adress-/Strukturzelle laut Layout-Definition.</summary>
    public RectMm MpCellRect(int moduleIndex, MpCellDefinition def)
    {
        double x = MpModuleX(moduleIndex) + (def.StartCol == 0 ? 0 : Col0Width);
        double w = def.ColSpan == 2 ? AddressWidth : (def.StartCol == 0 ? Col0Width : Col1Width);
        double y = MpDataStartY(moduleIndex) + def.StartRow * DataRowHeight;
        double h = def.RowSpan * DataRowHeight;
        return new RectMm(x, y, w, h);
    }

    /// <summary>Netzadress-Block 0 (Zeilen 1-10) bzw. 1 (Zeilen 11-20) — nur 35mm.</summary>
    public RectMm MpNetAddressRect(int moduleIndex, int block)
    {
        double blockH = MpModuleLayoutFactory.NetAddrBlockRows * DataRowHeight;
        return new RectMm(MpModuleX(moduleIndex) + AddressWidth, MpDataStartY(moduleIndex) + block * blockH, Col2Width, blockH);
    }

    /// <summary>CPU-Name-Spalte ueber die volle Datenhoehe.</summary>
    public RectMm MpCpuRect(int moduleIndex) =>
        new(MpModuleX(moduleIndex) + AddressWidth + Col2Width, MpDataStartY(moduleIndex), Col3Width, BandDataHeight);
}
