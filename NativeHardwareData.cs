using System;
using System.Collections.Generic;
using System.Globalization;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class NativeHardwareData
{
    internal static IReadOnlyList<Dictionary<string, string>> Query(string ns, string cls, params string[] properties)
    {
        List<Dictionary<string, string>> rows = new();
        NativeRscReader.Visit((_, instance) =>
        {
            Dictionary<string, string> row = new(StringComparer.OrdinalIgnoreCase);
            foreach (string property in properties)
            {
                try { row[property] = NativeRscReader.ReadValue(instance, property); }
                catch { row[property] = string.Empty; } // unsupported property is not a fabricated value
            }
            rows.Add(row);
        }, ns, "SELECT * FROM " + cls);
        return rows;
    }

    internal static IReadOnlyList<BitLockerVolumeInfo> ReadBitLockerVolumes()
    {
        List<BitLockerVolumeInfo> rows = new();
        NativeRscReader.Visit((services, instance) =>
        {
            string mount = NativeRscReader.ReadValue(instance, "DriveLetter");
            if (string.IsNullOrWhiteSpace(mount)) return;
            string conversion = "Unknown", protection = "Unknown", locked = "Unknown";
            int? percent = null;
            try
            {
                var result = NativeRscReader.ReadVolumeMethod(services, instance, "GetConversionStatus", "ConversionStatus", "EncryptionPercentage");
                conversion = ConversionName(result["ConversionStatus"]);
                if (int.TryParse(result["EncryptionPercentage"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number is >= 0 and <= 100)
                    percent = number;
            }
            catch { /* Locked or unavailable provider data remains Unknown. */ }
            try { protection = ProtectionName(NativeRscReader.ReadVolumeMethod(services, instance, "GetProtectionStatus", "ProtectionStatus")["ProtectionStatus"]); } catch { }
            try { locked = NativeRscReader.ReadVolumeMethod(services, instance, "GetLockStatus", "LockStatus")["LockStatus"] switch { "0" => "Unlocked", "1" => "Locked", _ => "Unknown" }; } catch { }
            rows.Add(new(mount, conversion, percent, protection, locked));
        }, @"ROOT\CIMV2\Security\MicrosoftVolumeEncryption", "SELECT * FROM Win32_EncryptableVolume");
        return rows;
    }

    internal static string ConversionName(string value) => value switch
    {
        "0" => "FullyDecrypted", "1" => "FullyEncrypted", "2" => "EncryptionInProgress",
        "3" => "DecryptionInProgress", "4" => "EncryptionPaused", "5" => "DecryptionPaused", _ => "Unknown"
    };
    internal static string ProtectionName(string value) => value switch { "0" => "Off", "1" => "On", _ => "Unknown" };
    internal static string DiskHealth(string value) => value switch { "0" => "Healthy", "1" => "Warning", "2" => "Unhealthy", _ => "Unknown" };
    internal static string DiskMedia(string value) => value switch { "3" => "HDD", "4" => "SSD", "5" => "SCM", "0" => "Unspecified", _ => "Unknown" };
}
