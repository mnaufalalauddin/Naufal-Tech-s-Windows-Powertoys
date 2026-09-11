using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed class GamingDeviceNetworkService
    {
        private const string BackupRoot =
            @"Software\Naufal Windows Tech\Powertoys\Backups\GamingDeviceNetwork";
        private const string UsbBackupPath = BackupRoot + @"\UsbLatencyPower";
        private const string EthernetBackupPath = BackupRoot + @"\EthernetLatency";
        private const string WifiBackupPath = BackupRoot + @"\WifiLatency";
        private const string DnsBackupPath = BackupRoot + @"\DnsOriginal";
        private const string AdapterClassPath =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

        private static readonly string[] UsbDeviceValueNames =
        {
            "EnhancedPowerManagementEnabled",
            "SelectiveSuspendEnabled",
            "AllowIdleIrpInD3",
            "DeviceSelectiveSuspended"
        };

        private static readonly string[] EthernetDisableValueNames =
        {
            "*EEE", "AdvancedEEE", "EnableGreenEthernet", "EnablePME", "ULPMode",
            "EnableSavePowerNow", "ReduceSpeedOnPowerDown", "DMACoalescing",
            "*InterruptModeration", "InterruptModeration",
            "*FlowControl", "FlowControl",
            "*LsoV2IPv4", "*LsoV2IPv6",
            "*IPChecksumOffloadIPv4", "*TCPChecksumOffloadIPv4",
            "*TCPChecksumOffloadIPv6", "*UDPChecksumOffloadIPv4",
            "*UDPChecksumOffloadIPv6", "*WakeOnMagicPacket", "*WakeOnPattern"
        };

        private static readonly string[] WifiPreferredBandNames =
        {
            "PreferredBand", "BandPreference", "Preferred Band"
        };

        private static readonly string[] WifiInterruptNames =
        {
            "*InterruptModeration", "InterruptModeration"
        };

        private static readonly string[] WifiRoamingNames =
        {
            "RoamingAggressiveness", "RoamAggressiveness", "RoamingSensitivityLevel"
        };

        private readonly NativeCommandRunner _commandRunner = new();

        public Task<ToolActionResult> ApplyUsbAsync()
        {
            return Task.Run(() =>
            {
                List<RegistryTarget> targets = BuildUsbTargets();
                CaptureSnapshot(UsbBackupPath, targets);
                ApplyResult applied = ApplyTargets(targets);
                return new ToolActionResult(
                    applied.Verified > 0 && applied.Failed == 0,
                    $"USB latency/power optimization applied. " +
                    $"Verified={applied.Verified}; failed/denied={applied.Failed}; " +
                    "USBXHCI and per-device original values are saved. Reboot recommended.");
            });
        }

        public Task<ToolActionResult> RestoreUsbAsync()
        {
            return Task.Run(() => RestoreSnapshotResult(
                UsbBackupPath,
                "USB controller and per-device power settings"));
        }

        public Task<ToolActionResult> ApplyEthernetAsync()
        {
            return Task.Run(() =>
            {
                NetworkInterface[] adapters = GetPhysicalLikeAdapters(wifi: false).ToArray();
                if (adapters.Length == 0)
                {
                    throw new InvalidOperationException("No physical Ethernet adapter was detected.");
                }

                List<RegistryTarget> targets = new();
                int supportedAdapters = 0;
                foreach (NetworkInterface adapter in adapters)
                {
                    string? classPath = FindAdapterClassPath(adapter.Id);
                    if (classPath is null)
                    {
                        continue;
                    }
                    supportedAdapters++;
                    targets.Add(new RegistryTarget(
                        classPath,
                        "PnPCapabilities",
                        RegistryValueKind.DWord,
                        24));
                    foreach (string name in EthernetDisableValueNames)
                    {
                        RegistryTarget? target = BuildExistingNumericTarget(classPath, name, 0);
                        if (target.HasValue)
                        {
                            targets.Add(target.Value);
                        }
                    }
                }

                if (targets.Count == 0)
                {
                    throw new InvalidOperationException(
                        "The detected Ethernet adapter did not expose a writable driver class key.");
                }

                CaptureSnapshot(EthernetBackupPath, targets);
                ApplyResult applied = ApplyTargets(targets);
                return new ToolActionResult(
                    applied.Verified > 0 && applied.Failed == 0,
                    $"Ethernet latency settings applied to {supportedAdapters} adapter(s). " +
                    $"Verified driver values={applied.Verified}; failed/unsupported={applied.Failed}. " +
                    "Reconnect the adapter or reboot so the driver reloads its advanced values.");
            });
        }

        public Task<ToolActionResult> RestoreEthernetAsync()
        {
            return Task.Run(() => RestoreSnapshotResult(
                EthernetBackupPath,
                "Ethernet driver latency and power settings"));
        }

        public async Task<ToolActionResult> ApplyWifiAsync()
        {
            NetworkInterface[] adapters = GetPhysicalLikeAdapters(wifi: true).ToArray();
            if (adapters.Length == 0)
            {
                throw new InvalidOperationException("No physical Wi-Fi adapter was detected.");
            }

            List<RegistryTarget> targets = new();
            foreach (NetworkInterface adapter in adapters)
            {
                string? classPath = FindAdapterClassPath(adapter.Id);
                if (classPath is null)
                {
                    continue;
                }

                AddWifiTarget(
                    targets,
                    classPath,
                    WifiPreferredBandNames,
                    text => (text.Contains("5", StringComparison.OrdinalIgnoreCase) &&
                             text.Contains("GHz", StringComparison.OrdinalIgnoreCase)) ||
                            text.Contains("5G", StringComparison.OrdinalIgnoreCase),
                    "2");
                AddWifiTarget(
                    targets,
                    classPath,
                    WifiInterruptNames,
                    text => text.Contains("Disabled", StringComparison.OrdinalIgnoreCase) ||
                            text.Equals("Off", StringComparison.OrdinalIgnoreCase),
                    "0");
                AddWifiTarget(
                    targets,
                    classPath,
                    WifiRoamingNames,
                    text => text.Contains("Lowest", StringComparison.OrdinalIgnoreCase),
                    "0");

                NativeCommandResult autoConfig = await _commandRunner.RunAsync(
                    "netsh.exe",
                    new[] { "wlan", "set", "autoconfig", "enabled=yes", $"interface={adapter.Name}" },
                    TimeSpan.FromSeconds(15));
                if (autoConfig.ExitCode != 0)
                {
                    // The registry-backed driver properties can still be applied.
                }
            }

            if (targets.Count == 0)
            {
                throw new InvalidOperationException(
                    "The Wi-Fi driver does not expose Preferred Band, Interrupt Moderation, or Roaming Aggressiveness registry parameters.");
            }

            CaptureSnapshot(WifiBackupPath, targets);
            ApplyResult applied = ApplyTargets(targets);
            return new ToolActionResult(
                applied.Verified > 0 && applied.Failed == 0,
                $"Wi-Fi latency settings applied. Verified driver values={applied.Verified}; " +
                $"failed/unsupported={applied.Failed}. Reconnect Wi-Fi or reboot to reload the driver values.");
        }

        public Task<ToolActionResult> RestoreWifiAsync()
        {
            return Task.Run(() => RestoreSnapshotResult(
                WifiBackupPath,
                "Wi-Fi advanced driver settings"));
        }

        public Task<ToolActionResult> ApplyCloudflareDnsAsync() =>
            ApplyDnsPresetAsync("Cloudflare", new[] { "1.1.1.1", "1.0.0.1" });

        public Task<ToolActionResult> ApplyGoogleDnsAsync() =>
            ApplyDnsPresetAsync("Google", new[] { "8.8.8.8", "8.8.4.4" });

        public async Task<ToolActionResult> RestoreDnsAsync()
        {
            using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(DnsBackupPath, writable: false);
            bool defaults = backup is null;
            IReadOnlyList<RegistryValueSnapshot> snapshot = defaults
                ? GetPhysicalLikeAdapters().Where(a => a.OperationalStatus == OperationalStatus.Up)
                    .Select(a => new RegistryValueSnapshot($@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{{{NormalizeGuid(a.Id)}}}",
                        "NameServer", false, RegistryValueKind.String, null)).ToArray()
                : LoadSnapshot(DnsBackupPath);
            if (snapshot.Count == 0)
                return new(false, "No applicable network adapter is available on this PC.", SkippedUnavailable: true);
            int restored = 0, unavailable = 0;
            var errors = new List<string>();
            foreach (var item in snapshot)
            {
                string id = ExtractInterfaceId(item.Path);
                NetworkInterface? adapter = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(a => NormalizeGuid(a.Id) == NormalizeGuid(id));
                if (adapter is null) { unavailable++; continue; }
                string[] addresses = item.Exists ? ParseDnsAddresses(item.Value as string) : Array.Empty<string>();
                try
                {
                    if (!VerifyDnsServers(adapter.Id, addresses))
                    {
                        if (addresses.Length > 0) await SetDnsServersAsync(adapter.Name, addresses);
                        else await SetDnsDhcpAsync(adapter.Name);
                    }
                    if (!VerifyDnsServers(adapter.Id, addresses))
                        throw new InvalidOperationException("DNS read-back did not match the requested server order or DHCP configuration.");
                    if (!defaults)
                        RegistryRestorePlan.Execute(new[] { new RestoreRegistryValue(new(RegistryHive.LocalMachine, item.Path, item.Name),
                            item.Exists ? item.Value : null, item.Exists ? item.Kind : null) });
                    restored++;
                }
                catch (Exception exception) { errors.Add(adapter.Name + ": " + exception.Message); }
            }
            // Do not rewrite registry state behind a failed netsh command, or discard absent adapters' backups.
            if (!defaults && errors.Count == 0 && unavailable == 0)
                Registry.CurrentUser.DeleteSubKeyTree(DnsBackupPath, throwOnMissingSubKey: false);
            bool success = errors.Count == 0 && unavailable == 0;
            return new ToolActionResult(success,
                (defaults ? "No original backup was found. Restoring Windows-default DNS supplied by DHCP. " : "Restoring the captured DNS configuration. ") +
                $"Verified adapters={restored}; unavailable={unavailable}; failed={errors.Count}. " + string.Join(" | ", errors),
                SkippedUnavailable: errors.Count == 0 && unavailable > 0);
        }

        private async Task<ToolActionResult> ApplyDnsPresetAsync(
            string name,
            IReadOnlyList<string> addresses)
        {
            NetworkInterface[] adapters = GetPhysicalLikeAdapters()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .ToArray();
            if (adapters.Length == 0)
            {
                throw new InvalidOperationException(
                    "No active physical Ethernet or Wi-Fi adapter was detected.");
            }

            List<RegistryTarget> snapshotTargets = adapters
                .Select(adapter => new RegistryTarget(
                    $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{{{NormalizeGuid(adapter.Id)}}}",
                    "NameServer",
                    RegistryValueKind.String,
                    string.Empty))
                .ToList();
            CaptureSnapshot(DnsBackupPath, snapshotTargets);

            int changed = 0;
            int failed = 0;
            foreach (NetworkInterface adapter in adapters)
            {
                try
                {
                    await SetDnsServersAsync(adapter.Name, addresses);
                    if (VerifyDnsServers(adapter.Id, addresses))
                    {
                        changed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }
            await FlushDnsAsync();
            return new ToolActionResult(
                changed > 0 && failed == 0,
                $"DNS preset {name} applied and verified on {changed} active physical adapter(s). " +
                $"Servers: {string.Join(", ", addresses)}. Failed/unsupported={failed}.");
        }

        private async Task SetDnsServersAsync(string alias, IReadOnlyList<string> addresses)
        {
            if (addresses.Count == 0)
            {
                throw new ArgumentException("At least one DNS server is required.", nameof(addresses));
            }

            NativeCommandResult first = await _commandRunner.RunAsync(
                "netsh.exe",
                new[]
                {
                    "interface", "ipv4", "set", "dnsservers", $"name={alias}",
                    "source=static", $"address={addresses[0]}", "register=primary", "validate=no"
                },
                TimeSpan.FromSeconds(20));
            EnsureCommandSucceeded(first, $"Could not set the primary DNS server on {alias}");

            for (int index = 1; index < addresses.Count; index++)
            {
                NativeCommandResult additional = await _commandRunner.RunAsync(
                    "netsh.exe",
                    new[]
                    {
                        "interface", "ipv4", "add", "dnsservers", $"name={alias}",
                        $"address={addresses[index]}", $"index={index + 1}", "validate=no"
                    },
                    TimeSpan.FromSeconds(20));
                EnsureCommandSucceeded(additional, $"Could not add DNS server on {alias}");
            }
        }

        private async Task SetDnsDhcpAsync(string alias)
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                "netsh.exe",
                new[]
                {
                    "interface", "ipv4", "set", "dnsservers", $"name={alias}", "source=dhcp"
                },
                TimeSpan.FromSeconds(20));
            EnsureCommandSucceeded(result, $"Could not restore automatic DNS on {alias}");
        }

        private async Task FlushDnsAsync()
        {
            await _commandRunner.RunAsync(
                "ipconfig.exe",
                new[] { "/flushdns" },
                TimeSpan.FromSeconds(20));
        }

        private static bool VerifyDnsServers(
            string interfaceId,
            IReadOnlyList<string> expected)
        {
            string path =
                $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{{{NormalizeGuid(interfaceId)}}}";
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
            string[] actual = ParseDnsAddresses(Convert.ToString(
                key?.GetValue("NameServer", string.Empty, RegistryValueOptions.DoNotExpandEnvironmentNames),
                CultureInfo.InvariantCulture));
            return DnsMatches(expected, actual);
        }

        internal static bool DnsMatches(IReadOnlyList<string> expected, IReadOnlyList<string> actual) =>
            expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase);

        private static List<RegistryTarget> BuildUsbTargets()
        {
            List<RegistryTarget> targets = new()
            {
                new RegistryTarget(
                    @"SYSTEM\CurrentControlSet\Services\USBXHCI\Parameters",
                    "DisableSelectiveSuspend",
                    RegistryValueKind.DWord,
                    1),
                new RegistryTarget(
                    @"SYSTEM\CurrentControlSet\Services\USBXHCI\Parameters",
                    "LowLatencyMode",
                    RegistryValueKind.DWord,
                    1),
                new RegistryTarget(
                    @"SYSTEM\CurrentControlSet\Services\USBXHCI\Parameters",
                    "IdleEnabled",
                    RegistryValueKind.DWord,
                    0),
                new RegistryTarget(
                    @"SYSTEM\CurrentControlSet\Services\USBXHCI\Parameters",
                    "EnhancedPowerManagementEnabled",
                    RegistryValueKind.DWord,
                    0)
            };

            const string usbRootPath = @"SYSTEM\CurrentControlSet\Enum\USB";
            using RegistryKey? usbRoot = Registry.LocalMachine.OpenSubKey(usbRootPath, writable: false);
            if (usbRoot is null)
            {
                return targets;
            }

            foreach (string deviceName in SafeSubKeyNames(usbRoot))
            {
                using RegistryKey? device = usbRoot.OpenSubKey(deviceName, writable: false);
                if (device is null)
                {
                    continue;
                }
                foreach (string instanceName in SafeSubKeyNames(device))
                {
                    string parametersPath =
                        $@"{usbRootPath}\{deviceName}\{instanceName}\Device Parameters";
                    using RegistryKey? parameters = Registry.LocalMachine.OpenSubKey(
                        parametersPath,
                        writable: false);
                    if (parameters is null)
                    {
                        continue;
                    }
                    foreach (string valueName in UsbDeviceValueNames)
                    {
                        targets.Add(new RegistryTarget(
                            parametersPath,
                            valueName,
                            RegistryValueKind.DWord,
                            0));
                    }
                }
            }
            return targets;
        }

        private static IEnumerable<NetworkInterface> GetPhysicalLikeAdapters(bool? wifi = null)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                string identity = $"{adapter.Name} {adapter.Description}";
                bool isWifi = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    ContainsAny(identity, "Wi-Fi", "WiFi", "Wireless", "WLAN", "802.11");
                bool excluded = adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or
                        NetworkInterfaceType.Tunnel or NetworkInterfaceType.Unknown ||
                    ContainsAny(
                        identity,
                        "Bluetooth", "Virtual", "Hyper-V", "VMware", "VirtualBox",
                        "TAP", "VPN", "Loopback", "Pseudo");
                bool networkType = isWifi || adapter.NetworkInterfaceType is
                    NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or
                    NetworkInterfaceType.FastEthernetFx or NetworkInterfaceType.FastEthernetT;
                if (excluded || !networkType || (wifi.HasValue && wifi.Value != isWifi))
                {
                    continue;
                }
                yield return adapter;
            }
        }

        private static string? FindAdapterClassPath(string interfaceId)
        {
            using RegistryKey? root = Registry.LocalMachine.OpenSubKey(
                AdapterClassPath,
                writable: false);
            if (root is null)
            {
                return null;
            }
            string normalized = NormalizeGuid(interfaceId);
            foreach (string childName in SafeSubKeyNames(root))
            {
                using RegistryKey? child = root.OpenSubKey(childName, writable: false);
                string current = NormalizeGuid(Convert.ToString(
                    child?.GetValue("NetCfgInstanceId"),
                    CultureInfo.InvariantCulture));
                if (current.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return $@"{AdapterClassPath}\{childName}";
                }
            }
            return null;
        }

        private static RegistryTarget? BuildExistingNumericTarget(
            string path,
            string name,
            int desiredValue)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
            if (key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is null)
            {
                return null;
            }
            RegistryValueKind kind;
            try
            {
                kind = key.GetValueKind(name);
            }
            catch
            {
                return null;
            }
            object value = kind switch
            {
                RegistryValueKind.DWord => desiredValue,
                RegistryValueKind.QWord => (long)desiredValue,
                RegistryValueKind.String or RegistryValueKind.ExpandString =>
                    desiredValue.ToString(CultureInfo.InvariantCulture),
                _ => string.Empty
            };
            return kind is RegistryValueKind.Binary or RegistryValueKind.MultiString or
                RegistryValueKind.Unknown
                ? null
                : new RegistryTarget(path, name, kind, value);
        }

        private static void AddWifiTarget(
            ICollection<RegistryTarget> targets,
            string classPath,
            IEnumerable<string> candidateNames,
            Func<string, bool> displayMatch,
            string fallbackRawValue)
        {
            foreach (string name in candidateNames)
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(classPath, writable: false);
                object? current = key?.GetValue(
                    name,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (current is null)
                {
                    continue;
                }
                RegistryValueKind kind;
                try { kind = key!.GetValueKind(name); }
                catch { continue; }

                string raw = ResolveNdiEnumRawValue(
                    classPath,
                    name,
                    displayMatch,
                    fallbackRawValue);
                object value = kind switch
                {
                    RegistryValueKind.DWord => int.TryParse(
                        raw,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int number) ? number : 0,
                    RegistryValueKind.QWord => long.TryParse(
                        raw,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out long number) ? number : 0L,
                    _ => raw
                };
                targets.Add(new RegistryTarget(classPath, name, kind, value));
                return;
            }
        }

        private static string ResolveNdiEnumRawValue(
            string classPath,
            string keyword,
            Func<string, bool> displayMatch,
            string fallback)
        {
            using RegistryKey? enumKey = Registry.LocalMachine.OpenSubKey(
                $@"{classPath}\Ndi\Params\{keyword}\enum",
                writable: false);
            if (enumKey is null)
            {
                return fallback;
            }
            foreach (string raw in SafeValueNames(enumKey))
            {
                string display = Convert.ToString(
                    enumKey.GetValue(raw),
                    CultureInfo.InvariantCulture) ?? string.Empty;
                if (displayMatch(display))
                {
                    return raw;
                }
            }
            return SafeValueNames(enumKey).Contains(fallback, StringComparer.OrdinalIgnoreCase)
                ? fallback
                : SafeValueNames(enumKey).FirstOrDefault() ?? fallback;
        }

        private static void CaptureSnapshot(
            string backupPath,
            IReadOnlyList<RegistryTarget> targets)
        {
            using RegistryKey backup = Registry.CurrentUser.CreateSubKey(
                backupPath,
                writable: true);
            if (Convert.ToInt32(
                    backup.GetValue("Captured", 0),
                    CultureInfo.InvariantCulture) == 1)
            {
                return;
            }

            backup.SetValue("Count", targets.Count, RegistryValueKind.DWord);
            for (int index = 0; index < targets.Count; index++)
            {
                RegistryTarget target = targets[index];
                string prefix = $"Item.{index}.";
                backup.SetValue(prefix + "Path", target.Path, RegistryValueKind.String);
                backup.SetValue(prefix + "Name", target.Name, RegistryValueKind.String);
                using RegistryKey? source = Registry.LocalMachine.OpenSubKey(
                    target.Path,
                    writable: false);
                object? value = source?.GetValue(
                    target.Name,
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                bool exists = value is not null;
                backup.SetValue(prefix + "Exists", exists ? 1 : 0, RegistryValueKind.DWord);
                if (!exists)
                {
                    continue;
                }
                RegistryValueKind kind = source!.GetValueKind(target.Name);
                backup.SetValue(prefix + "Kind", (int)kind, RegistryValueKind.DWord);
                backup.SetValue(prefix + "Value", Serialize(value!, kind), RegistryValueKind.String);
            }
            backup.SetValue("Captured", 1, RegistryValueKind.DWord);
        }

        private static IReadOnlyList<RegistryValueSnapshot> LoadSnapshot(string backupPath)
        {
            using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(backupPath, writable: false);
            if (backup is null)
                throw new InvalidOperationException("No original configuration snapshot exists. This hardware-specific option has no universal Windows default; use the adapter/controller manufacturer's reset mechanism.");
            return ParseSnapshot(backupPath, key => backup.GetValue(key));
        }

        internal static IReadOnlyList<RegistryValueSnapshot> ParseSnapshot(string backupPath, Func<string, object?> read)
        {
            if (read("Captured") is not int captured || captured != 1 ||
                read("Count") is not int count || count is <= 0 or > 16384)
                throw new InvalidOperationException("The device snapshot is incomplete. Backup retained; defaults were not substituted.");
            var result = new List<RegistryValueSnapshot>(count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < count; index++)
            {
                string prefix = $"Item.{index}.";
                if (read(prefix + "Path") is not string path ||
                    read(prefix + "Name") is not string name ||
                    read(prefix + "Exists") is not int exists || exists is not (0 or 1) ||
                    !IsAllowedSnapshotTarget(backupPath, path, name) || !seen.Add(path + "|" + name))
                    throw new InvalidOperationException("The device snapshot contains incomplete or unexpected targets. No values were restored.");
                RegistryValueKind kind = RegistryValueKind.String;
                object? value = null;
                if (exists == 1)
                {
                    if (read(prefix + "Kind") is not int savedKind || !RegistryRestorePlan.IsSupported((RegistryValueKind)savedKind) ||
                        read(prefix + "Value") is not string serialized)
                        throw new InvalidOperationException("The device snapshot value is incomplete. Backup retained.");
                    kind = (RegistryValueKind)savedKind;
                    value = Deserialize(serialized, kind);
                }
                if (backupPath == DnsBackupPath && exists == 1 &&
                    (kind != RegistryValueKind.String || value is not string dns ||
                     ParseDnsAddresses(dns).Any(address => !IPAddress.TryParse(address, out var ip) ||
                         ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)))
                    throw new InvalidOperationException("The saved IPv4 DNS configuration is invalid. No adapter was changed.");
                result.Add(new RegistryValueSnapshot(path, name, exists == 1, kind, value));
            }
            return result;
        }

        internal static bool IsAllowedSnapshotTarget(string backupPath, string path, string name)
        {
            string[] parts = path.Split('\\');
            if (backupPath == DnsBackupPath)
                return path.StartsWith(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\", StringComparison.OrdinalIgnoreCase) &&
                    parts.Length == 7 && Guid.TryParse(parts[^1], out _) && name == "NameServer";
            if (backupPath == UsbBackupPath)
                return (path.Equals(@"SYSTEM\CurrentControlSet\Services\USBXHCI\Parameters", StringComparison.OrdinalIgnoreCase) &&
                        new[] { "DisableSelectiveSuspend", "LowLatencyMode", "IdleEnabled", "EnhancedPowerManagementEnabled" }.Contains(name)) ||
                    (path.StartsWith(@"SYSTEM\CurrentControlSet\Enum\USB\", StringComparison.OrdinalIgnoreCase) &&
                        parts.Length == 7 && parts[^1] == "Device Parameters" && UsbDeviceValueNames.Contains(name));
            bool classPath = path.StartsWith(AdapterClassPath + "\\", StringComparison.OrdinalIgnoreCase) &&
                parts.Length == 6 && parts[^1].Length == 4 && parts[^1].All(char.IsDigit);
            if (!classPath) return false;
            return backupPath == EthernetBackupPath ? name == "PnPCapabilities" || EthernetDisableValueNames.Contains(name) :
                backupPath == WifiBackupPath && WifiPreferredBandNames.Concat(WifiInterruptNames).Concat(WifiRoamingNames).Contains(name);
        }

        private static RestoreResult RestoreSnapshot(string backupPath, bool deleteOnSuccess = true)
        {
            var snapshot = LoadSnapshot(backupPath);
            int restored = 0, failed = 0, unavailable = 0;
            foreach (var item in snapshot)
            {
                try
                {
                    using RegistryKey? key = Registry.LocalMachine.OpenSubKey(item.Path, writable: false);
                    if (key is null) { unavailable++; continue; }
                    RegistryRestorePlan.Execute(new[] { new RestoreRegistryValue(new(RegistryHive.LocalMachine, item.Path, item.Name),
                        item.Exists ? item.Value : null, item.Exists ? item.Kind : null) });
                    restored++;
                }
                catch { failed++; }
            }
            if (deleteOnSuccess && failed == 0 && unavailable == 0)
                Registry.CurrentUser.DeleteSubKeyTree(backupPath, throwOnMissingSubKey: false);
            return new(restored, failed, unavailable);
        }

        private static ToolActionResult RestoreSnapshotResult(string backupPath, string description)
        {
            RestoreResult result = RestoreSnapshot(backupPath);
            return new(result.Restored > 0 && result.Failed == 0 && result.Unavailable == 0,
                $"Restoring captured {description}. Verified={result.Restored}; unavailable on this PC={result.Unavailable}; failed/denied={result.Failed}.",
                SkippedUnavailable: result.Failed == 0 && result.Unavailable > 0);
        }

        private static ApplyResult ApplyTargets(IEnumerable<RegistryTarget> targets)
        {
            int verified = 0;
            int failed = 0;
            foreach (RegistryTarget target in targets)
            {
                try
                {
                    using RegistryKey key = Registry.LocalMachine.CreateSubKey(
                        target.Path,
                        writable: true);
                    key.SetValue(target.Name, target.Value, target.Kind);
                    if (VerifyTarget(target))
                    {
                        verified++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }
            return new ApplyResult(verified, failed);
        }

        private static bool VerifyTarget(RegistryTarget target)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                target.Path,
                writable: false);
            object? actual = key?.GetValue(
                target.Name,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            return RegistryValuesEqual(actual, target.Value, target.Kind);
        }

        private static bool VerifySnapshotValue(RegistryValueSnapshot snapshot)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                snapshot.Path,
                writable: false);
            object? actual = key?.GetValue(
                snapshot.Name,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            return snapshot.Exists
                ? RegistryValuesEqual(actual, snapshot.Value, snapshot.Kind)
                : actual is null;
        }

        private static bool RegistryValuesEqual(
            object? left,
            object? right,
            RegistryValueKind kind)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }
            return kind switch
            {
                RegistryValueKind.Binary =>
                    ((byte[])left).SequenceEqual((byte[])right),
                RegistryValueKind.MultiString =>
                    ((string[])left).SequenceEqual((string[])right, StringComparer.Ordinal),
                RegistryValueKind.DWord => Convert.ToInt32(left, CultureInfo.InvariantCulture) ==
                    Convert.ToInt32(right, CultureInfo.InvariantCulture),
                RegistryValueKind.QWord => Convert.ToInt64(left, CultureInfo.InvariantCulture) ==
                    Convert.ToInt64(right, CultureInfo.InvariantCulture),
                _ => string.Equals(
                    Convert.ToString(left, CultureInfo.InvariantCulture),
                    Convert.ToString(right, CultureInfo.InvariantCulture),
                    StringComparison.Ordinal)
            };
        }

        private static string Serialize(object value, RegistryValueKind kind) => kind switch
        {
            RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
            RegistryValueKind.MultiString => string.Join("\u001f", (string[])value),
            RegistryValueKind.DWord => Convert.ToInt32(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => Convert.ToInt64(value, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        private static object Deserialize(string value, RegistryValueKind kind) => kind switch
        {
            RegistryValueKind.Binary => Convert.FromBase64String(value),
            RegistryValueKind.MultiString => string.IsNullOrEmpty(value)
                ? Array.Empty<string>()
                : value.Split('\u001f'),
            RegistryValueKind.DWord => int.Parse(value, CultureInfo.InvariantCulture),
            RegistryValueKind.QWord => long.Parse(value, CultureInfo.InvariantCulture),
            _ => value
        };

        private static string[] SafeSubKeyNames(RegistryKey key)
        {
            try { return key.GetSubKeyNames(); }
            catch { return Array.Empty<string>(); }
        }

        private static string[] SafeValueNames(RegistryKey key)
        {
            try { return key.GetValueNames(); }
            catch { return Array.Empty<string>(); }
        }

        private static string NormalizeGuid(string? value) =>
            (value ?? string.Empty).Trim().Trim('{', '}');

        private static string ExtractInterfaceId(string path)
        {
            int slash = path.LastIndexOf('\\');
            return slash >= 0 ? path[(slash + 1)..] : path;
        }

        private static string[] ParseDnsAddresses(string? value) =>
            (value ?? string.Empty).Split(
                new[] { ',', ' ', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static bool ContainsAny(string value, params string[] needles) =>
            needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

        private static void EnsureCommandSucceeded(
            NativeCommandResult result,
            string message)
        {
            if (result.ExitCode != 0 || result.TimedOut)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result.CombinedOutput)
                        ? $"{message} (exit code {result.ExitCode})."
                        : $"{message}. {result.CombinedOutput}");
            }
        }

        private readonly record struct RegistryTarget(
            string Path,
            string Name,
            RegistryValueKind Kind,
            object Value);

        internal readonly record struct RegistryValueSnapshot(
            string Path,
            string Name,
            bool Exists,
            RegistryValueKind Kind,
            object? Value);

        private readonly record struct ApplyResult(int Verified, int Failed);

        private readonly record struct RestoreResult(int Restored, int Failed, int Unavailable = 0);
    }
}
