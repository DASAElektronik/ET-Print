namespace ETPrinter.Services;

public enum ModuleType
{
    DI,  // Digital Input  (E x.x)
    DO,  // Digital Output (A x.x)
    AI,  // Analog Input   (EW x)
    AO   // Analog Output  (AW x)
}

public record ModuleTypeInfo(ModuleType Type, string DisplayName, string Prefix, bool IsBitAddressed);

public record GeneratedLabel(string Header, string Line1, string Line2);

public static class AddressGenerator
{
    public static readonly ModuleTypeInfo[] ModuleTypes =
    [
        new(ModuleType.DI, "DI - Digital Input",   "E",  true),
        new(ModuleType.DO, "DO - Digital Output",  "A",  true),
        new(ModuleType.AI, "AI - Analog Input",    "EW", false),
        new(ModuleType.AO, "AO - Analog Output",   "AW", false),
    ];

    /// <summary>
    /// Generiert Beschriftung fuer ein digitales Modul (DI/DO).
    /// ET200SP Klemmenanordnung: Oben = ungerade Bits, Unten = gerade Bits.
    /// </summary>
    public static GeneratedLabel GenerateDigital(string moduleName, string prefix, int startByte, int byteCount)
    {
        var oddBits = new List<string>();   // Zeile 1 (oben): .1, .3, .5, .7
        var evenBits = new List<string>();  // Zeile 2 (unten): .0, .2, .4, .6

        for (int b = startByte; b < startByte + byteCount; b++)
        {
            for (int bit = 0; bit < 8; bit += 2)
            {
                evenBits.Add($"{prefix} {b}.{bit}");
                oddBits.Add($"{prefix} {b}.{bit + 1}");
            }
        }

        return new GeneratedLabel(
            Header: moduleName,
            Line1: string.Join("  ", oddBits),
            Line2: string.Join("  ", evenBits)
        );
    }

    /// <summary>
    /// Generiert Beschriftung fuer ein analoges Modul (AI/AO).
    /// Jeder Kanal belegt 2 Bytes (1 Wort).
    /// </summary>
    public static GeneratedLabel GenerateAnalog(string moduleName, string prefix, int startByte, int channelCount)
    {
        var channels = new List<string>();

        for (int i = 0; i < channelCount; i++)
        {
            int addr = startByte + (i * 2);
            channels.Add($"{prefix} {addr}");
        }

        // Bei Analog: alle Kanaele in einer Zeile, oder auf zwei Zeilen verteilen
        if (channelCount <= 4)
        {
            return new GeneratedLabel(
                Header: moduleName,
                Line1: string.Join("  ", channels),
                Line2: string.Empty
            );
        }
        else
        {
            int half = channelCount / 2;
            return new GeneratedLabel(
                Header: moduleName,
                Line1: string.Join("  ", channels.Take(half)),
                Line2: string.Join("  ", channels.Skip(half))
            );
        }
    }

    /// <summary>
    /// Generiert Beschriftung basierend auf Modultyp.
    /// </summary>
    public static GeneratedLabel Generate(string moduleName, ModuleType type, int startByte, int count)
    {
        var info = ModuleTypes.First(m => m.Type == type);

        if (info.IsBitAddressed)
            return GenerateDigital(moduleName, info.Prefix, startByte, count);
        else
            return GenerateAnalog(moduleName, info.Prefix, startByte, count);
    }

    /// <summary>
    /// Typische Kanalanzahlen pro Modultyp.
    /// </summary>
    public static int[] GetTypicalCounts(ModuleType type) => type switch
    {
        ModuleType.DI => [1, 2, 4],       // 8, 16, 32 Kanaele (in Bytes)
        ModuleType.DO => [1, 2, 4],       // 8, 16, 32 Kanaele (in Bytes)
        ModuleType.AI => [2, 4, 8],       // 2, 4, 8 Kanaele
        ModuleType.AO => [2, 4, 8],       // 2, 4, 8 Kanaele
        _ => [1, 2, 4]
    };

    /// <summary>
    /// Beschreibung der Anzahl fuer die UI.
    /// </summary>
    public static string GetCountLabel(ModuleType type) => type switch
    {
        ModuleType.DI or ModuleType.DO => "Anzahl Bytes:",
        ModuleType.AI or ModuleType.AO => "Anzahl Kanaele:",
        _ => "Anzahl:"
    };

    /// <summary>
    /// Berechnet die naechste Startadresse nach diesem Modul.
    /// </summary>
    public static int GetNextStartByte(ModuleType type, int startByte, int count) => type switch
    {
        ModuleType.DI or ModuleType.DO => startByte + count,
        ModuleType.AI or ModuleType.AO => startByte + (count * 2),
        _ => startByte + count
    };
}
