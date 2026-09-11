using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct MsiNdisMatch(Dictionary<string, string>? Row, string Source);
internal static class MsiNdisIdentity
{
    internal static MsiNdisMatch Match(IReadOnlyList<Dictionary<string, string>> rows, string deviceId, string location, string name)
    {
        bool Same(Dictionary<string, string> row, string field, string value) =>
            !string.IsNullOrWhiteSpace(value) && row.TryGetValue(field, out string? actual) &&
            value.Equals(actual, StringComparison.OrdinalIgnoreCase);
        // Strong identity takes precedence over fallback descriptions, even for identical NIC models.
        foreach (var key in new[] { "PNPDeviceID", "LocationInformationString", "InterfaceDescription", "Name" })
        {
            string expected = key == "PNPDeviceID" ? deviceId : key == "LocationInformationString" ? location : name;
            var matches = rows.Where(row => Same(row, key, expected)).ToArray();
            if (matches.Length == 1) return new(matches[0], "exact " + key);
            if (matches.Length > 1) return new(null, "ambiguous " + key);
        }
        return new(null, "not matched");
    }
}
