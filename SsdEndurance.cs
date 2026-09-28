using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

// Model-scoped subset of MIT-licensed third-party vendor rules. See notices. Never interpret a
// generic attribute ID as a percentage without a matching model family.
internal static class SsdEndurance
{
    internal sealed record Estimate(int Remaining, string Rule);
    internal static Estimate? Evaluate(string model, string firmware, IReadOnlyList<SmartAttribute> attributes)
    {
        string m = model.Trim().ToUpperInvariant();
        byte id; bool raw = false, offset100 = false;
        if (m.StartsWith("SAMSUNG SSD ") || m.StartsWith("SAMSUNG MZ"))
        {
            bool enterprise = new[] { "SM863", "PM863", "SM883", "PM883", "SM843T", "PM853T", "MZ7KM", "MZ7KH", "MZ7LM", "MZ7LH" }.Any(m.Contains);
            id = enterprise ? (byte)0xE9 : (byte)0xB1;
        }
        else if (m.StartsWith("INTEL SSDS") || m.StartsWith("SOLIDIGM SSDS")) id = 0xE9;
        else if (m.StartsWith("CRUCIAL ") || m.StartsWith("MICRON ") || m.StartsWith("MTFD") || (m.StartsWith("CT") && m.Contains("SSD"))) id = 0xCA;
        else if (m.StartsWith("KINGSTON SA400")) { id = 0xE7; raw = !firmware.StartsWith("03070009", StringComparison.OrdinalIgnoreCase); }
        else if (m.StartsWith("KINGSTON SKC600")) id = 0xA9;
        else if (new[] { "KINGSTON SM2280", "KINGSTON SEDC400", "KINGSTON SKC310", "KINGSTON SHSS", "KINGSTON SUV300", "KINGSTON SKC400" }.Any(m.StartsWith)) id = 0xE7;
        else if (m.StartsWith("KIOXIA-EXCERIA SATA SSD") || m.StartsWith("TOSHIBA-TR")) { id = 0xAD; offset100 = true; }
        else return null;
        var matches = attributes.Where(a => a.Id == id).ToArray();
        if (matches.Length != 1) return null;
        var a = matches[0];
        int remaining = a.Current;
        if (raw)
        {
            if (!ulong.TryParse(a.Raw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value) || value == 0xFFFFFFFFFFFF) return null;
            remaining = (int)(value & 255);
        }
        if (offset100) remaining -= 100;
        // The offset-100 family treats zero/negative values as unreported.
        if (remaining < 0 || remaining > 100 || (offset100 && remaining == 0)) return null;
        return new(remaining, $"{model}; attribute 0x{id:X2}; " + (raw ? "raw byte 0" : offset100 ? "normalized current minus 100" : "normalized current") + "; model-specific endurance rule");
    }
}
