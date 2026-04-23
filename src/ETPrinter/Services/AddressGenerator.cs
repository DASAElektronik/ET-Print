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
    /// ET200SP vertikales Etikett hat 8 Plaetze pro Reihe (BU mit 16 Klemmen = 8 oben + 8 unten).
    /// Adressen werden alternierend verteilt wie bei digital:
    ///   - Ungerade Kanaele (K1, K3, K5, K7) in Line1 (oben)
    ///   - Gerade Kanaele   (K0, K2, K4, K6) in Line2 (unten)
    /// Restplaetze bleiben als leere Slots erhalten (fuer Rahmen-Druck auf Blanko-A4).
    /// </summary>
    public static GeneratedLabel GenerateAnalog(string moduleName, string prefix, int startByte, int channelCount)
    {
        const int SlotsPerRow = 8; // ET200SP BaseUnit A0/A1 hat 8 Klemmen pro Reihe

        var oben = new string[SlotsPerRow];
        var unten = new string[SlotsPerRow];
        for (int i = 0; i < SlotsPerRow; i++) { oben[i] = string.Empty; unten[i] = string.Empty; }

        int maxChannels = Math.Min(channelCount, SlotsPerRow * 2);
        for (int k = 0; k < maxChannels; k++)
        {
            int addr = startByte + (k * 2);
            string addressStr = $"{prefix} {addr}";
            int slotPos = k / 2; // Position 0..7 in der Reihe
            if (k % 2 == 0)
                unten[slotPos] = addressStr;
            else
                oben[slotPos] = addressStr;
        }

        return new GeneratedLabel(
            Header: moduleName,
            Line1: string.Join("  ", oben),
            Line2: string.Join("  ", unten)
        );
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
