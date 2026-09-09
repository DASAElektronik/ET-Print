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
    /// Festes Raster wie GenerateAnalog: 8 Klemmenplaetze pro Reihe (BaseUnit A0/A1),
    /// Restplaetze bleiben leer, damit Adressen ueber den richtigen Klemmen sitzen.
    /// Maximal 2 Bytes (16 Klemmen) pro Etikett — mehr passt physisch nicht.
    /// </summary>
    public static GeneratedLabel GenerateDigital(string moduleName, string prefix, int startByte, int byteCount)
    {
        const int SlotsPerRow = 8;
        const int MaxBytesPerLabel = 2;

        var oben = new string[SlotsPerRow];
        var unten = new string[SlotsPerRow];
        for (int i = 0; i < SlotsPerRow; i++) { oben[i] = string.Empty; unten[i] = string.Empty; }

        int maxBytes = Math.Min(byteCount, MaxBytesPerLabel);
        for (int b = 0; b < maxBytes; b++)
        {
            int byteNum = startByte + b;
            for (int bit = 0; bit < 8; bit++)
            {
                int slotPos = b * 4 + bit / 2;  // Byte 0 -> Slots 0-3, Byte 1 -> Slots 4-7
                string addressStr = $"{prefix} {byteNum}.{bit}";
                if (bit % 2 == 0)
                    unten[slotPos] = addressStr;
                else
                    oben[slotPos] = addressStr;
            }
        }

        return new GeneratedLabel(
            Header: moduleName,
            Line1: string.Join("  ", oben),
            Line2: string.Join("  ", unten)
        );
    }

    /// <summary>
    /// Anzahl, die tatsaechlich auf EIN Etikett passt (Digital: 2 Bytes = 16 Klemmen,
    /// Analog: 16 Kanaele). Fuer Auto-Advance, damit gekappte Adressen nicht
    /// uebersprungen werden, sondern auf dem naechsten Etikett weitergehen.
    /// </summary>
    public static int GetEffectiveCount(ModuleType type, int count)
    {
        var info = ModuleTypes.First(m => m.Type == type);
        return info.IsBitAddressed ? Math.Min(count, 2) : Math.Min(count, 16);
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
        ModuleType.AI or ModuleType.AO => "Anzahl Kanäle:",
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

    /// <summary>Verschraenkt Zeile 1 (ungerade Bits, oben) und Zeile 2 (gerade Bits,
    /// unten) slotweise kanal-aufsteigend zu einer Zeile (fuer einzeilige Formate).</summary>
    public static string MergeAddressLines(string line1, string line2)
    {
        var odd = line1.Split("  ", StringSplitOptions.None);
        var even = line2.Split("  ", StringSplitOptions.None);
        var merged = new List<string>();
        for (int i = 0; i < Math.Max(odd.Length, even.Length); i++)
        {
            if (i < even.Length && !string.IsNullOrWhiteSpace(even[i])) merged.Add(even[i]);
            if (i < odd.Length && !string.IsNullOrWhiteSpace(odd[i])) merged.Add(odd[i]);
        }
        return string.Join("  ", merged);
    }
}
