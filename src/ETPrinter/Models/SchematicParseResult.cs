namespace ETPrinter.Models;

public class ParsedModule
{
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleType { get; set; } = string.Empty;
    public int StartByte { get; set; }
    public int ChannelCount { get; set; }
    public List<ParsedChannel> Channels { get; set; } = [];
}

public class ParsedChannel
{
    public int ChannelNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SchematicParseResult
{
    public List<ParsedModule> Modules { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int PagesScanned { get; set; }
}
