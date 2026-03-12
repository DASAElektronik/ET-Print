namespace ETPrinter.Models;

public enum LabelFormat
{
    HorizontalDoubleHeader,
    HorizontalDouble,
    HorizontalSingle,
    VerticalDoubleHeader,
    VerticalDouble,
    VerticalSingle
}

public record FormatInfo(
    LabelFormat Format,
    string DisplayName,
    int Columns,
    int RowsPerLabel,
    bool HasHeader,
    bool IsVertical,
    int LabelsPerRow,
    int LabelRows
)
{
    public int LabelsPerPage => LabelsPerRow * LabelRows;
}
