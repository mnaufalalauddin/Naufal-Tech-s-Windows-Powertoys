using System;
using System.Text.RegularExpressions;

namespace Naufal_Windows_Tech_s_Powertoys;

internal enum DriverVersionMatch { Matched, Mismatch, Unavailable }

internal static class GpuDriverVersionVerification
{
    // NVIDIA's marketing version is the last five Device Manager digits.
    // Intel's published four-part driver version is directly comparable.
    // AMD Auto-Detect package versions do not identify the selected display INF.
    internal static DriverVersionMatch Compare(string vendor, string active, string target)
    {
        if (vendor.Equals("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            string actual = NvidiaMarketingVersion(active);
            if (actual.Length == 0 || !Regex.IsMatch(target, @"^\d{3}\.\d{2}$")) return DriverVersionMatch.Unavailable;
            return actual == target ? DriverVersionMatch.Matched : DriverVersionMatch.Mismatch;
        }
        if (vendor.Equals("Intel", StringComparison.OrdinalIgnoreCase) &&
            Version.TryParse(active, out Version? installed) && installed.Revision >= 0 &&
            Version.TryParse(target, out Version? expected) && expected.Revision >= 0)
            return installed == expected ? DriverVersionMatch.Matched : DriverVersionMatch.Mismatch;
        return DriverVersionMatch.Unavailable;
    }

    internal static string NvidiaMarketingVersion(string value)
    {
        if (Regex.IsMatch(value, @"^\d{3}\.\d{2}$")) return value;
        if (!Version.TryParse(value, out Version? parsed) || parsed.Revision < 0) return "";
        string digits = value.Replace(".", "", StringComparison.Ordinal);
        if (digits.Length < 5) return "";
        string tail = digits[^5..];
        return $"{tail[..3]}.{tail[3..]}";
    }
}
