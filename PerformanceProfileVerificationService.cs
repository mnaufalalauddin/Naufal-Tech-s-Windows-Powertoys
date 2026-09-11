using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal static class PerformanceProfileVerificationService
    {
        private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        private const string Ultimate = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string Subgroup = "54533251-82be-4824-96c1-47b60b740d00";
        private const string InterfaceRoot = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        private const string QosRoot = @"SOFTWARE\Policies\Microsoft\Windows\Psched";
        private static readonly string[] SettingGuids =
        {
            "893dee8e-2bef-41e0-89c6-b55d0929964c", "bc5038f7-23e0-4960-96da-33abaf5935ec",
            "465e1f50-b610-473a-ab58-00d1077dc418", "0cc5b647-c1df-4637-891a-dec35c318583",
            "ea062031-0e34-4ff1-9b6d-eb1059334028"
        };

        public static Task<PerformanceProfileState> ReadAsync(GamingLiveStatusSnapshot live) => Task.Run(() =>
        {
            Dictionary<string, ProfilePowerPair?> power = ReadPower(live.PowerPlanGuid);
            List<ProfileNetworkInterface> interfaces = new();
            bool interfacesReadable = true;
            try
            {
                using RegistryKey? root = Registry.LocalMachine.OpenSubKey(InterfaceRoot);
                interfacesReadable = root is not null;
                foreach (string name in root?.GetSubKeyNames() ?? Array.Empty<string>())
                    interfaces.Add(new(name, ReadRegistry($@"{InterfaceRoot}\{name}", "TCPNoDelay"),
                        ReadRegistry($@"{InterfaceRoot}\{name}", "TcpAckFrequency")));
            }
            catch { interfacesReadable = false; }

            ProfileVerificationInput input = new(
                live.Mmcss, live.PowerPlanGuid, power, live.Bcd.ExitCode == 0,
                ParseBcd(live.Bcd.StandardOutput, "disabledynamictick"),
                ParseBcd(live.Bcd.StandardOutput, "useplatformclock"),
                interfacesReadable, interfaces,
                ReadRegistry(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TcpNoDelay"),
                ReadRegistry(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex"),
                ReadRegistry(QosRoot, "NonBestEffortLimit"), ReadRegistry(QosRoot, "DisableUserTOSSetting"),
                live.RscReadable, live.RscAdapters);

            Dictionary<string, string> known = ReadKnownPowerGuids();
            List<ProfileVerificationResult> results = new();
            foreach (string profile in PerformanceProfileVerification.Profiles)
            {
                string? target = ResolveTarget(profile, live.PowerCfgList, known);
                Dictionary<string, ProfilePowerPair?> expected = new();
                Dictionary<string, ProfilePowerPair?> targetPower = ReadPower(target);
                for (int i = 0; i < SettingGuids.Length; i++)
                {
                    string alias = PerformanceProfileVerification.PowerAliases[i];
                    expected[alias] = profile == "Competitive Gaming"
                        ? new ProfilePowerPair(i == 2 ? 2u : 100u, i == 2 ? 2u : 100u)
                        : ReadDefaultPair(profile == "Balanced" ? Balanced : HighPerformance, SettingGuids[i]) ?? targetPower[alias];
                }
                results.Add(PerformanceProfileVerification.Evaluate(profile, input, target, expected));
            }
            return PerformanceProfileVerification.SelectBest(results);
        });

        internal static string? ResolveTarget(string profile, GamingLiveCommandResult list, IReadOnlyDictionary<string, string> known)
        {
            if (list.ExitCode != 0) return null;
            string template = profile == "Balanced" ? Balanced : Ultimate;
            string expectedName = profile == "Balanced" ? "Balanced" : "Ultimate Performance";
            List<(string Guid, string Name)> plans = new();
            foreach (string line in list.StandardOutput.Split('\n'))
            {
                Match match = Regex.Match(line, @"(?i)([0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12})\s*\((.*)\)");
                if (match.Success) plans.Add((match.Groups[1].Value, match.Groups[2].Value.Trim()));
            }
            bool Valid((string Guid, string Name) plan) =>
                plan.Guid.Equals(template, StringComparison.OrdinalIgnoreCase) ||
                plan.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase);
            if (known.TryGetValue(profile, out string? previous))
            {
                foreach (var plan in plans)
                    if (plan.Guid.Equals(previous, StringComparison.OrdinalIgnoreCase) && Valid(plan)) return plan.Guid;
            }
            foreach (var plan in plans)
                if (plan.Guid.Equals(template, StringComparison.OrdinalIgnoreCase)) return plan.Guid;
            foreach (var plan in plans)
                if (Valid(plan)) return plan.Guid;
            return null;
        }

        internal static Dictionary<string, string> ReadKnownPowerGuids(string? statePath = null)
        {
            Dictionary<string, string> known = new(StringComparer.Ordinal);
            try
            {
                string path = statePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "WindowsPowerToys", "PerformanceProfile_LastTransaction.json");
                if (!File.Exists(path)) return known;
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = document.RootElement;
                if (root.TryGetProperty("KnownPowerGuids", out JsonElement entries) && entries.ValueKind == JsonValueKind.Object)
                    foreach (JsonProperty property in entries.EnumerateObject())
                        Add(property.Name, property.Value);
                bool successfulTransaction = !root.TryGetProperty("Success", out JsonElement success) || success.ValueKind == JsonValueKind.True;
                if (successfulTransaction && root.TryGetProperty("Profile", out JsonElement profile) && profile.ValueKind == JsonValueKind.String &&
                    root.TryGetProperty("TargetPowerGuid", out JsonElement target))
                    Add(profile.GetString()!, target);
            }
            catch { /* Invalid historical metadata cannot certify a profile. */ }
            return known;

            void Add(string name, JsonElement value)
            {
                name = name switch { "Gaming" => "Competitive Gaming", "Streaming" => "Optimized Gaming", _ => name };
                if (PerformanceProfileVerification.Profiles.Contains(name) && value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(value.GetString(), out Guid guid)) known.TryAdd(name, guid.ToString());
            }
        }

        private static string? ParseBcd(string output, string name)
        {
            Match match = Regex.Match(output, $@"(?im)^\s*{Regex.Escape(name)}\s+(\S+)\s*$");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static Dictionary<string, ProfilePowerPair?> ReadPower(string? guid)
        {
            Dictionary<string, ProfilePowerPair?> values = new();
            for (int i = 0; i < SettingGuids.Length; i++)
                values[PerformanceProfileVerification.PowerAliases[i]] = ReadPair(guid, SettingGuids[i]);
            return values;
        }

        private static ProfilePowerPair? ReadPair(string? scheme, string setting)
        {
            if (!Guid.TryParse(scheme, out _)) return null;
            try
            {
                PowerPolicyPair pair = NativePowerPolicyReader.ReadRequiredPair(scheme!, setting);
                return new(pair.Ac, pair.Dc);
            }
            catch (System.ComponentModel.Win32Exception) { return null; }
        }

        private static ProfilePowerPair? ReadDefaultPair(string scheme, string setting)
        {
            string path = $@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\{Subgroup}\{setting}\DefaultPowerSchemeValues\{scheme}";
            ProfileRegistryValue ac = ReadRegistry(path, "ACSettingIndex");
            ProfileRegistryValue dc = ReadRegistry(path, "DCSettingIndex");
            return ac.Readable && ac.Exists && dc.Readable && dc.Exists ? new(ac.Value, dc.Value) : null;
        }

        private static ProfileRegistryValue ReadRegistry(string path, string name)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path);
                object? raw = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return raw is null ? new(true, false, 0) :
                    new(true, true, raw is int value ? unchecked((uint)value) : Convert.ToUInt32(raw, CultureInfo.InvariantCulture));
            }
            catch { return new(false, false, 0); }
        }

    }
}
