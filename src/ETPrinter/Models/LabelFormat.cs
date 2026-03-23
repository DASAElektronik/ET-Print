namespace ETPrinter.Models;

public enum LabelFormat
{
    // === ET 200SP Formate (bestehend) ===
    HorizontalDoubleHeader,
    HorizontalDouble,
    HorizontalSingle,
    VerticalDoubleHeader,
    VerticalDouble,
    VerticalSingle,

    // === S7-1500 / ET 200MP Standard (35mm Module) ===
    MP_Horizontal,
    MP_Vertical,

    // === ET 200MP 25mm Module ===
    MP25_Horizontal,
    MP25_Vertical
}

public record FormatInfo(
    LabelFormat Format,
    string DisplayName,
    int Columns,
    int RowsPerLabel,
    bool HasHeader,
    bool IsVertical,
    int LabelsPerRow,
    int LabelRows,
    ProductFamily Family = ProductFamily.ET200SP,
    int BandsPerPage = 1,
    int ChannelRowsPerBand = 20
)
{
    public int LabelsPerBand => LabelsPerRow * ChannelRowsPerBand;
    public int LabelsPerPage => LabelsPerBand * BandsPerPage;
}
