using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct OptionalProcessState(
        string Name,
        int RunningCount);

    internal sealed class GamingActionsService : IToolActionService, IDisposable
    {
        private const string BackupRoot =
            @"Software\Naufal Windows Tech\Powertoys\Backups\GamingActions";
        private const string LockPagesBackupPath = BackupRoot + @"\LockPages";
        private const string AutoMtuBackupPath = BackupRoot + @"\AutoMtu";

        private static readonly string[] SessionCleanupProcesses =
        {
            "OneDrive", "OneDriveStandaloneUpdater", "Dropbox", "DropboxUpdate",
            "GoogleDriveFS", "Adobe Desktop Service", "AdobeIPCBroker",
            "AdobeUpdateService", "CCXProcess", "CCLibrary", "GoogleUpdate",
            "MicrosoftEdgeUpdate", "DiscordUpdater", "DiscordCrashHandler",
            "DiscordCrashHandler64", "EpicWebHelper", "UplayWebCore",
            "BlizzardUpdateAgent", "AcroTray", "NVIDIA Share",
            "NVIDIA GeForce Experience", "RadeonSoftware"
        };

        private static readonly string[] OptionalProcesses =
        {
            "OneDrive", "Teams", "ms-teams", "GameBar", "GameBarFTServer",
            "Widgets", "WidgetService", "msedgeupdate", "GoogleUpdate",
            "NvTelemetryContainer", "OfficeClickToRun", "Skype",
            "PhoneExperienceHost", "TiWorker", "NvBackend",
            "DiscordOverlayDockLauncher"
        };

        private static readonly HashSet<string> ProtectedProcesses = new(
            new[]
            {
                "system", "idle", "csrss", "wininit", "winlogon", "services",
                "lsass", "svchost", "dwm", "audiodg", "explorer", "msmpeng",
                "securityhealthservice", "securityhealthsystray", "nissrv",
                "smartscreen", "nvcontainer", "nvdisplay.container", "nvidia share",
                "nvcaplayer", "nvsphelper64", "amdow", "amdrsserv",
                "radeonsoftware", "cnext", "igfxem", "igfxhk", "igfxtray",
                "realtekaudiouniversalservice", "rtkauduservice64"
            },
            StringComparer.OrdinalIgnoreCase);

        private static readonly IReadOnlyList<ToolActionDefinition> Actions =
            new[]
            {
                new ToolActionDefinition(
                    "LockPagesCurrentUser",
                    "CPU / Kernel",
                    "Lock Pages in Memory - Current User",
                    "Assigns SeLockMemoryPrivilege to the current Windows user and preserves the complete original account list.",
                    "Apply",
                    true,
                    true,
                    "Assign Lock Pages in Memory to the current user? This is a security-sensitive user right and takes effect for newly created logon tokens.",
                    "Restore the exact account list captured before assigning Lock Pages in Memory?",
                    Risk: ToolActionRisk.Danger),
                new ToolActionDefinition(
                    "AutoMtu",
                    "Network",
                    "MTU Auto-Detection / Apply",
                    "Detects path MTU with IPv4 Don't Fragment probes, applies it to the active default-route adapter, and verifies read-back.",
                    "Detect / Apply",
                    true,
                    true,
                    "Probe the current IPv4 path MTU and apply the detected value to the active adapter? Internet/ICMP access is required.",
                    "Restore every captured IPv4 adapter MTU and verify read-back?",
                    Risk: ToolActionRisk.Warning),
                new ToolActionDefinition(
                    "NoLowMemCheck",
                    "Boot / BCD / Legacy",
                    "nolowmem Legacy Compatibility Check",
                    "Checks the current BCD value and Windows applicability without writing a no-op option.",
                    "Check",
                    false,
                    false),
                new ToolActionDefinition(
                    "ShaderCache",
                    "GPU / Cache Maintenance",
                    "Clear GPU Shader Cache",
                    "Closes no games automatically, then removes deletable DirectX and NVIDIA shader-cache files from the detected cache locations. Games and graphics drivers rebuild the cache as needed, so the first launch afterward can temporarily compile shaders or stutter.",
                    "Run",
                    true,
                    false,
                    "Clear detected DirectX/NVIDIA shader-cache contents? Temporary loading time or stutter can occur while caches rebuild.",
                    Risk: ToolActionRisk.Primary),
                new ToolActionDefinition(
                    "RestorePoint",
                    "System / Session Actions",
                    "Create System Restore Point",
                    "Creates and verifies a Windows System Restore checkpoint for the system drive before additional tuning. System Protection must be enabled and Windows may enforce its checkpoint-frequency policy.",
                    "Run",
                    true,
                    false,
                    Risk: ToolActionRisk.Success),
                new ToolActionDefinition(
                    "GameSessionCleanup",
                    "System / Session Actions",
                    "Game Session Cleanup",
                    "Stops only allow-listed optional sync, updater, launcher, and overlay processes for the current gaming session. Windows, security, audio, desktop, and display-driver processes are protected; stopped applications can start again later.",
                    "Run",
                    false,
                    false,
                    "Stop optional sync, updater, and overlay background processes for this gaming session? They can restart later.",
                    Risk: ToolActionRisk.Warning),
                new ToolActionDefinition(
                    "ProcessManager",
                    "Memory / Process",
                    "Process Manager",
                    "Lists detected optional background processes with identity and memory information, then lets you stop selected entries. A protected-process allow/deny policy prevents terminating Windows, security, desktop, audio, and display-driver components.",
                    "Open",
                    false,
                    false),
                new ToolActionDefinition(
                    "QuickMemoryTrim",
                    "Memory / Process",
                    "Quick Memory Trim",
                    "Requests an empty working set for eligible running processes and reports the measured result. It does not purge the system standby list; trimmed pages may be faulted back into memory when applications become active again.",
                    "Run",
                    false,
                    false),
                new ToolActionDefinition(
                    "DeepMemoryClean",
                    "Memory / Process",
                    "Deep Standby Memory Clean",
                    "Trims working sets and purges the standby list. This can temporarily increase page faults and disk I/O.",
                    "Run",
                    true,
                    false,
                    "Run working-set trim and purge the standby list now?",
                    Risk: ToolActionRisk.Danger),
                new ToolActionDefinition(
                    "PriorityWatchdog",
                    "Memory / Process",
                    "Foreground Priority Watchdog + 0.5 ms Timer",
                    "Raises eligible foreground-process priority every 12 seconds and requests a 0.5 ms timer resolution while active.",
                    "Start",
                    false,
                    true,
                    "Start the foreground priority watchdog and 0.5 ms timer-resolution request?",
                    "Stop the watchdog, restore tracked process priorities, and release the timer-resolution request?",
                    "Stop",
                    ToolActionRisk.Danger),
                new ToolActionDefinition(
                    "UsbLatencyPower",
                    "Network / USB",
                    "USB Latency / Power Optimization",
                    "Applies USBXHCI selective-suspend, low-latency, controller power-saving, and per-device USB power-management settings as one option.",
                    "Apply",
                    true,
                    true,
                    "Apply the USB latency/power preset and save every original USBXHCI and per-device value? A reboot is recommended.",
                    "Restore every captured USBXHCI and per-device power-management value?",
                    Risk: ToolActionRisk.Warning),
                new ToolActionDefinition(
                    "EthernetLatency",
                    "Network / USB",
                    "Ethernet Latency Settings",
                    "Applies interrupt moderation, flow-control, offload, power, wake, and energy-saving values exposed by the Ethernet driver.",
                    "Apply",
                    true,
                    true,
                    "Apply the Ethernet latency preset to detected physical adapters and save the original driver values?",
                    "Restore the captured Ethernet driver values exactly?",
                    Risk: ToolActionRisk.Warning),
                new ToolActionDefinition(
                    "WifiLatency",
                    "Network / USB",
                    "Wi-Fi Latency Settings",
                    "Applies Preferred 5 GHz Band, disabled Interrupt Moderation, and Lowest Roaming Aggressiveness where the Wi-Fi driver exposes them.",
                    "Apply",
                    true,
                    true,
                    "Apply the Wi-Fi latency preset and save the original driver values? Reconnect or reboot may be required.",
                    "Restore the captured Wi-Fi advanced driver values exactly?",
                    Risk: ToolActionRisk.Warning),
                new ToolActionDefinition(
                    "DnsCloudflare",
                    "Network / USB",
                    "DNS - Cloudflare",
                    "Sets active physical adapters to Cloudflare IPv4 DNS: 1.1.1.1 and 1.0.0.1.",
                    "Apply",
                    true,
                    true,
                    "Save the original DNS configuration and apply Cloudflare DNS to active physical adapters?",
                    "Restore the original DNS configuration captured before the first DNS preset?",
                    Risk: ToolActionRisk.Primary),
                new ToolActionDefinition(
                    "DnsGoogle",
                    "Network / USB",
                    "DNS - Google",
                    "Sets active physical adapters to Google IPv4 DNS: 8.8.8.8 and 8.8.4.4.",
                    "Apply",
                    true,
                    true,
                    "Save the original DNS configuration and apply Google DNS to active physical adapters?",
                    "Restore the original DNS configuration captured before the first DNS preset?",
                    Risk: ToolActionRisk.Primary)
            };

        private readonly NativeCommandRunner _commandRunner = new();
        private readonly GamingDeviceNetworkService _deviceNetworkService = new();
        private readonly object _watchdogLock = new();
        private readonly Dictionary<int, ProcessPriorityClass> _changedPriorities = new();
        private Timer? _watchdogTimer;
        private bool _timerResolutionActive;

        public IReadOnlyList<ToolActionDefinition> GetActions() => Actions;

        public async Task<ToolActionResult> RunAsync(ToolActionDefinition definition)
        {
            if (definition.RequiresAdministrator && !WindowsPrivilegeService.IsAdministrator())
            {
                return new ToolActionResult(false, "Administrator rights are required.");
            }

            try
            {
                return definition.Id switch
                {
                    "LockPagesCurrentUser" => await ApplyLockPagesAsync(),
                    "AutoMtu" => await ApplyAutoMtuAsync(),
                    "NoLowMemCheck" => await CheckNoLowMemAsync(),
                    "ShaderCache" => await ClearShaderCacheAsync(),
                    "RestorePoint" => CreateRestorePoint(),
                    "GameSessionCleanup" => CleanupGameSession(),
                    "ProcessManager" => new ToolActionResult(
                        true,
                        "Process Manager opened."),
                    "QuickMemoryTrim" => TrimMemory(deep: false),
                    "DeepMemoryClean" => TrimMemory(deep: true),
                    "PriorityWatchdog" => StartPriorityWatchdog(),
                    "UsbLatencyPower" => await _deviceNetworkService.ApplyUsbAsync(),
                    "EthernetLatency" => await _deviceNetworkService.ApplyEthernetAsync(),
                    "WifiLatency" => await _deviceNetworkService.ApplyWifiAsync(),
                    "DnsCloudflare" => await _deviceNetworkService.ApplyCloudflareDnsAsync(),
                    "DnsGoogle" => await _deviceNetworkService.ApplyGoogleDnsAsync(),
                    _ => new ToolActionResult(false, $"Unsupported gaming action: {definition.Id}")
                };
            }
            catch (Exception exception)
            {
                return new ToolActionResult(false, exception.Message);
            }
        }

        public async Task<ToolActionResult> RestoreAsync(ToolActionDefinition definition)
        {
            if (!definition.SupportsRestore)
            {
                return new ToolActionResult(false, "This action does not have a restore operation.");
            }
            if (definition.RequiresAdministrator && !WindowsPrivilegeService.IsAdministrator())
            {
                return new ToolActionResult(false, "Administrator rights are required.");
            }

            try
            {
                return definition.Id switch
                {
                    "LockPagesCurrentUser" => await RestoreLockPagesAsync(),
                    "AutoMtu" => await RestoreAutoMtuAsync(),
                    "PriorityWatchdog" => StopPriorityWatchdog(),
                    "UsbLatencyPower" => await _deviceNetworkService.RestoreUsbAsync(),
                    "EthernetLatency" => await _deviceNetworkService.RestoreEthernetAsync(),
                    "WifiLatency" => await _deviceNetworkService.RestoreWifiAsync(),
                    "DnsCloudflare" or "DnsGoogle" => await _deviceNetworkService.RestoreDnsAsync(),
                    _ => new ToolActionResult(false, $"Unsupported gaming restore: {definition.Id}")
                };
            }
            catch (Exception exception)
            {
                return new ToolActionResult(false, exception.Message);
            }
        }

        public IReadOnlyList<OptionalProcessState> ReadOptionalProcesses()
        {
            return OptionalProcesses
                .Select(name => new OptionalProcessState(
                    name,
                    CountProcesses(name)))
                .ToArray();
        }

        public ToolActionResult EndOptionalProcesses(IEnumerable<string> names)
        {
            HashSet<string> allowed = new(OptionalProcesses, StringComparer.OrdinalIgnoreCase);
            int ended = 0;
            int failed = 0;
            foreach (string name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!allowed.Contains(name))
                {
                    failed++;
                    continue;
                }
                foreach (Process process in GetProcesses(name))
                {
                    using (process)
                    {
                        try
                        {
                            process.Kill(entireProcessTree: true);
                            if (process.WaitForExit(5000)) ended++;
                            else failed++;
                        }
                        catch
                        {
                            failed++;
                        }
                    }
                }
            }
            return new ToolActionResult(
                failed == 0,
                $"Ended {ended} optional process(es). Failed/denied: {failed}.");
        }

        private async Task<ToolActionResult> ApplyLockPagesAsync()
        {
            PrivilegeAccounts before = await ReadPrivilegeAccountsAsync("SeLockMemoryPrivilege");
            string sid = "*" + (WindowsIdentity.GetCurrent().User?.Value ??
                throw new InvalidOperationException("Unable to read the current user SID."));
            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(
                       LockPagesBackupPath,
                       writable: true))
            {
                if (backup.GetValue("Captured") is null)
                {
                    backup.SetValue("Found", before.Found ? 1 : 0, RegistryValueKind.DWord);
                    backup.SetValue("Accounts", before.Accounts.ToArray(), RegistryValueKind.MultiString);
                    backup.SetValue("Sid", sid, RegistryValueKind.String);
                    backup.SetValue("Captured", 1, RegistryValueKind.DWord);
                }
            }

            string[] target = before.Accounts
                .Append(sid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            await WritePrivilegeAccountsAsync("SeLockMemoryPrivilege", target);
            PrivilegeAccounts after = await ReadPrivilegeAccountsAsync("SeLockMemoryPrivilege");
            bool verified = after.Accounts.Contains(sid, StringComparer.OrdinalIgnoreCase);
            return new ToolActionResult(
                verified,
                verified
                    ? $"SeLockMemoryPrivilege assigned and verified for {sid}. Sign out and back in before testing it."
                    : "SeLockMemoryPrivilege read-back did not include the current user SID.");
        }

        private async Task<ToolActionResult> RestoreLockPagesAsync()
        {
            using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(
                LockPagesBackupPath,
                writable: false);
            bool defaults = backup is null;
            if (!defaults && (backup!.GetValue("Captured") is not int captured || captured != 1 ||
                backup.GetValue("Found") is not int found || found is not (0 or 1) ||
                backup.GetValue("Accounts") is not string[]))
            {
                throw new InvalidOperationException(
                    "The Lock Pages in Memory snapshot is incomplete. No accounts were changed; backup retained.");
            }
            string[] accounts = defaults ? Array.Empty<string>() : (string[])backup!.GetValue("Accounts")!;
            foreach (string account in accounts)
            {
                if (account.StartsWith('*')) _ = new SecurityIdentifier(account[1..]);
                else _ = new NTAccount(account).Translate(typeof(SecurityIdentifier));
            }
            await WritePrivilegeAccountsAsync("SeLockMemoryPrivilege", accounts);
            PrivilegeAccounts after = await ReadPrivilegeAccountsAsync("SeLockMemoryPrivilege");
            bool verified = accounts
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .SequenceEqual(
                    after.Accounts.OrderBy(value => value, StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
            if (verified)
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    LockPagesBackupPath,
                    throwOnMissingSubKey: false);
            }
            return new ToolActionResult(
                verified,
                verified
                    ? defaults
                        ? "No original backup was found. SeLockMemoryPrivilege was returned to the documented Windows default: no assigned accounts. Group Policy may reapply domain-managed assignments."
                        : "The exact captured SeLockMemoryPrivilege account list was restored and verified."
                    : "Lock Pages in Memory restore did not match the captured account list.");
        }

        private async Task<PrivilegeAccounts> ReadPrivilegeAccountsAsync(string privilege)
        {
            string file = Path.Combine(
                AppDataPaths.GetTemporaryDirectory(),
                $"nwp-rights-{Guid.NewGuid():N}.inf");
            try
            {
                NativeCommandResult result = await _commandRunner.RunAsync(
                    "secedit.exe",
                    new[] { "/export", "/cfg", file, "/areas", "USER_RIGHTS", "/quiet" },
                    TimeSpan.FromSeconds(30));
                if (result.ExitCode != 0 || !File.Exists(file))
                {
                    throw new InvalidOperationException(
                        "secedit export failed. " + result.CombinedOutput);
                }
                foreach (string line in File.ReadAllLines(file))
                {
                    int equals = line.IndexOf('=');
                    if (equals < 0 ||
                        !line[..equals].Trim().Equals(privilege, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string[] accounts = line[(equals + 1)..]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    return new PrivilegeAccounts(true, accounts);
                }
                return new PrivilegeAccounts(false, Array.Empty<string>());
            }
            finally
            {
                TryDeleteFile(file);
            }
        }

        private async Task WritePrivilegeAccountsAsync(
            string privilege,
            IReadOnlyList<string> accounts)
        {
            string token = Guid.NewGuid().ToString("N");
            string file = Path.Combine(AppDataPaths.GetTemporaryDirectory(), $"nwp-rights-{token}.inf");
            string database = Path.Combine(AppDataPaths.GetTemporaryDirectory(), $"nwp-rights-{token}.sdb");
            try
            {
                string content =
                    "[Unicode]\r\nUnicode=yes\r\n[Version]\r\nsignature=\"$CHICAGO$\"\r\n" +
                    "Revision=1\r\n[Privilege Rights]\r\n" +
                    $"{privilege} = {string.Join(',', accounts.Distinct(StringComparer.OrdinalIgnoreCase))}\r\n";
                File.WriteAllText(file, content, Encoding.Unicode);
                NativeCommandResult result = await _commandRunner.RunAsync(
                    "secedit.exe",
                    new[] { "/configure", "/db", database, "/cfg", file, "/areas", "USER_RIGHTS", "/quiet" },
                    TimeSpan.FromSeconds(45));
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "secedit configure failed. " + result.CombinedOutput);
                }
            }
            finally
            {
                TryDeleteFile(file);
                TryDeleteFile(database);
                TryDeleteFile(database + ".jfm");
            }
        }

        private async Task<ToolActionResult> ApplyAutoMtuAsync()
        {
            NetworkInterface adapter = FindDefaultIpv4Interface() ??
                throw new InvalidOperationException(
                    "No active default-route IPv4 adapter was found.");
            IPv4InterfaceProperties properties = adapter.GetIPProperties().GetIPv4Properties() ??
                throw new InvalidOperationException("Unable to read the adapter IPv4 MTU.");
            int detected = await DetectPathMtuAsync("1.1.1.1");

            using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(
                       $@"{AutoMtuBackupPath}\{properties.Index}",
                       writable: true))
            {
                if (backup.GetValue("Captured") is null)
                {
                    backup.SetValue("Alias", adapter.Name, RegistryValueKind.String);
                    backup.SetValue("Index", properties.Index, RegistryValueKind.DWord);
                    backup.SetValue("Mtu", properties.Mtu, RegistryValueKind.DWord);
                    backup.SetValue("AdapterId", adapter.Id, RegistryValueKind.String);
                    backup.Flush();
                    backup.SetValue("Captured", 1, RegistryValueKind.DWord);
                }
            }

            await SetInterfaceMtuAsync(adapter.Name, detected);
            int after = ReadInterfaceMtu(properties.Index);
            bool verified = after == detected;
            return new ToolActionResult(
                verified,
                verified
                    ? $"MTU auto-detection verified. Adapter={adapter.Name}; Before={properties.Mtu}; Detected={detected}; After={after}."
                    : $"MTU read-back failed. Expected {detected}, got {after}.");
        }

        private async Task<ToolActionResult> RestoreAutoMtuAsync()
        {
            using RegistryKey? root = Registry.CurrentUser.OpenSubKey(AutoMtuBackupPath, writable: false);
            if (root is null || root.GetSubKeyNames().Length == 0)
                throw new InvalidOperationException("No MTU backup is available. MTU depends on adapter, media, and tunnel configuration; a universal 1500-byte default cannot safely be assumed.");
            var plans = new List<(string Id, int Mtu)>();
            int unavailable = 0;
            var adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (string child in root.GetSubKeyNames())
            {
                using RegistryKey? backup = root.OpenSubKey(child, writable: false);
                if (backup?.GetValue("Captured") is not int captured || captured != 1 ||
                    backup.GetValue("Alias") is not string alias || string.IsNullOrWhiteSpace(alias) ||
                    backup.GetValue("Index") is not int index || index < 0 ||
                    backup.GetValue("Mtu") is not int mtu || mtu is < 576 or > 65535)
                    throw new InvalidOperationException("An MTU snapshot is incomplete. No adapter was changed.");
                string? savedId = backup.GetValue("AdapterId") as string;
                if (savedId is not null && !Guid.TryParse(savedId, out _))
                    throw new InvalidOperationException("Invalid MTU adapter identity. No adapter was changed.");
                NetworkInterface? adapter = savedId is not null
                    ? adapters.FirstOrDefault(a => Guid.TryParse(a.Id, out var id) && id == Guid.Parse(savedId))
                    : adapters.FirstOrDefault(a => a.Name == alias && a.GetIPProperties().GetIPv4Properties()?.Index == index);
                // Legacy snapshots must match BOTH alias and index, not either in isolation.
                if (adapter is null) { unavailable++; continue; }
                plans.Add((adapter.Id, mtu));
            }
            var restored = new List<string>();
            foreach (var plan in plans)
            {
                var current = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(a => a.Id == plan.Id) ??
                    throw new InvalidOperationException("The MTU adapter disappeared during Restore. Backup retained.");
                int index = current.GetIPProperties().GetIPv4Properties()?.Index ??
                    throw new InvalidOperationException("IPv4 interface disappeared during Restore.");
                if (ReadInterfaceMtu(index) != plan.Mtu) await SetInterfaceMtuAsync(current.Name, plan.Mtu);
                if (ReadInterfaceMtu(index) != plan.Mtu)
                    throw new InvalidOperationException($"MTU restore verification failed for {current.Name}. Backup retained.");
                restored.Add($"{current.Name}={plan.Mtu}");
            }
            if (unavailable == 0)
                Registry.CurrentUser.DeleteSubKeyTree(AutoMtuBackupPath, throwOnMissingSubKey: false);
            return new(unavailable == 0, "Restoring captured IPv4 MTU. Verified: " + string.Join(", ", restored) +
                $"; unavailable adapters={unavailable}.", SkippedUnavailable: unavailable > 0);
        }

        private async Task<int> DetectPathMtuAsync(string target)
        {
            using Ping ping = new();
            async Task<bool> TestAsync(int bytes)
            {
                byte[] buffer = new byte[bytes];
                PingReply reply = await ping.SendPingAsync(
                    target,
                    900,
                    buffer,
                    new PingOptions(64, dontFragment: true));
                return reply.Status == IPStatus.Success;
            }

            if (!await TestAsync(548))
            {
                throw new InvalidOperationException(
                    "Path-MTU probing failed at the minimum test size. Internet/ICMP access may be unavailable.");
            }
            int low = 548;
            int high = 1472;
            int best = 548;
            while (low <= high)
            {
                int middle = (low + high) / 2;
                if (await TestAsync(middle))
                {
                    best = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return best + 28;
        }

        private async Task SetInterfaceMtuAsync(string alias, int mtu)
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                "netsh.exe",
                new[]
                {
                    "interface", "ipv4", "set", "subinterface", alias,
                    $"mtu={mtu.ToString(CultureInfo.InvariantCulture)}", "store=persistent"
                },
                TimeSpan.FromSeconds(20));
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result.CombinedOutput)
                        ? $"netsh failed with exit code {result.ExitCode}."
                        : result.CombinedOutput);
            }
        }

        private static NetworkInterface? FindDefaultIpv4Interface()
        {
            IPAddress localAddress;
            try
            {
                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("1.1.1.1", 53);
                localAddress = ((IPEndPoint)socket.LocalEndPoint!).Address;
            }
            catch
            {
                return null;
            }
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(item => item.OperationalStatus == OperationalStatus.Up)
                .FirstOrDefault(item => item.GetIPProperties().UnicastAddresses.Any(
                    address => address.Address.Equals(localAddress)));
        }

        private static int ReadInterfaceMtu(int interfaceIndex)
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    IPv4InterfaceProperties? properties = adapter.GetIPProperties().GetIPv4Properties();
                    if (properties?.Index == interfaceIndex)
                    {
                        return properties.Mtu;
                    }
                }
                catch
                {
                    // Continue if an adapter disappears during enumeration.
                }
            }
            return -1;
        }

        private async Task<ToolActionResult> CheckNoLowMemAsync()
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                "bcdedit.exe",
                new[] { "/enum", "{current}" },
                TimeSpan.FromSeconds(15));
            string? value = null;
            foreach (string line in result.StandardOutput.Split(
                         new[] { '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("nolowmem", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                value = trimmed["nolowmem".Length..].Trim();
                break;
            }
            Version version = Environment.OSVersion.Version;
            bool legacyOnly = version.Major < 6 ||
                (version.Major == 6 && version.Minor < 2);
            return new ToolActionResult(
                true,
                $"Windows version: {version}; Current BCD: {value ?? "<not configured>"}; " +
                $"Applicable: {(legacyOnly ? "LEGACY WINDOWS ONLY" : "NO - IGNORED BY THIS WINDOWS VERSION")}. No BCD value was written.");
        }

        private static async Task<ToolActionResult> ClearShaderCacheAsync()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string[] roots =
            {
                Path.Combine(localAppData, "D3DSCache"),
                Path.Combine(localAppData, "NVIDIA", "DXCache"),
                Path.Combine(localAppData, "NVIDIA", "GLCache"),
                Path.Combine(programData, "NVIDIA Corporation", "NV_Cache")
            };
            CacheMetrics before = await Task.Run(() => MeasureCache(roots));
            int removedRoots = 0;
            int failed = 0;
            foreach (string root in roots.Where(Directory.Exists))
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(root))
                {
                    try
                    {
                        if (Directory.Exists(entry))
                        {
                            Directory.Delete(entry, recursive: true);
                        }
                        else
                        {
                            File.SetAttributes(entry, FileAttributes.Normal);
                            File.Delete(entry);
                        }
                        if (!File.Exists(entry) && !Directory.Exists(entry))
                        {
                            removedRoots++;
                        }
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
            CacheMetrics after = await Task.Run(() => MeasureCache(roots));
            bool verified = before.Files == 0 ||
                after.Files < before.Files ||
                after.Bytes < before.Bytes;
            return new ToolActionResult(
                verified,
                $"Shader cache {(verified ? "cleanup verified" : "was not reduced")}. " +
                $"Before={before.Files} file(s), {FormatBytes(before.Bytes)}; " +
                $"After={after.Files} file(s), {FormatBytes(after.Bytes)}; " +
                $"Removed roots={removedRoots}; Locked/failed={failed}.");
        }

        private static CacheMetrics MeasureCache(IEnumerable<string> roots)
        {
            long files = 0;
            long bytes = 0;
            EnumerationOptions options = new()
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (string root in roots.Where(Directory.Exists))
            {
                try
                {
                    foreach (string file in Directory.EnumerateFiles(root, "*", options))
                    {
                        files++;
                        try { bytes += new FileInfo(file).Length; } catch { }
                    }
                }
                catch { }
            }
            return new CacheMetrics(files, bytes);
        }

        private static ToolActionResult CreateRestorePoint()
        {
            RestorePointInfo info = new()
            {
                EventType = BeginSystemChange,
                RestorePointType = ModifySettings,
                SequenceNumber = 0,
                Description = $"Naufal Tech's Windows Powertoys - {DateTime.Now:yyyy-MM-dd HH:mm}"
            };
            if (!SRSetRestorePoint(ref info, out StateManagerStatus status))
            {
                throw new InvalidOperationException(
                    $"Could not create the System Restore point (Win32 error {Marshal.GetLastWin32Error()}). System Protection may be disabled or Windows may be enforcing its frequency limit.");
            }
            if (status.Status != 0)
            {
                throw new InvalidOperationException(
                    $"System Restore returned error {status.Status}.");
            }

            RestorePointInfo end = info with
            {
                EventType = EndSystemChange,
                SequenceNumber = status.SequenceNumber
            };
            SRSetRestorePoint(ref end, out _);
            return new ToolActionResult(
                true,
                $"System Restore point created: {info.Description}");
        }

        private static ToolActionResult CleanupGameSession()
        {
            List<string> stopped = new();
            foreach (string name in SessionCleanupProcesses)
            {
                foreach (Process process in GetProcesses(name))
                {
                    using (process)
                    {
                        try
                        {
                            int id = process.Id;
                            string processName = process.ProcessName;
                            process.Kill(entireProcessTree: true);
                            stopped.Add($"{processName} (PID {id})");
                        }
                        catch { }
                    }
                }
            }
            return new ToolActionResult(
                true,
                stopped.Count > 0
                    ? $"Stopped {stopped.Count} optional background process(es): {string.Join(", ", stopped)}"
                    : "No matching optional background processes were running.");
        }

        private static ToolActionResult TrimMemory(bool deep)
        {
            int trimmed = 0;
            int failed = 0;
            int ownId = Environment.ProcessId;
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    try
                    {
                        if (process.Id <= 4 || process.Id == ownId)
                        {
                            continue;
                        }
                        if (EmptyWorkingSet(process.Handle))
                        {
                            trimmed++;
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
            }

            if (deep)
            {
                int command = 4;
                uint status = NtSetSystemInformation(80, ref command, sizeof(int));
                if (status != 0)
                {
                    throw new InvalidOperationException(
                        $"NtSetSystemInformation(MemoryPurgeStandbyList) returned NTSTATUS 0x{status:X8}.");
                }
            }
            return new ToolActionResult(
                true,
                $"{(deep ? "Deep clean" : "Working-set trim")} completed. Trimmed={trimmed}; Failed/denied={failed}.");
        }

        private ToolActionResult StartPriorityWatchdog()
        {
            lock (_watchdogLock)
            {
                if (_watchdogTimer is not null)
                {
                    return new ToolActionResult(true, "Foreground priority watchdog is already active.");
                }
                uint current = 0;
                uint status = NtSetTimerResolution(5000, true, ref current);
                if (status != 0)
                {
                    throw new InvalidOperationException(
                        $"NtSetTimerResolution returned NTSTATUS 0x{status:X8}.");
                }
                _timerResolutionActive = true;
                RunPriorityWatchdogCycle();
                _watchdogTimer = new Timer(
                    _ => RunPriorityWatchdogCycle(),
                    null,
                    TimeSpan.FromSeconds(12),
                    TimeSpan.FromSeconds(12));
                return new ToolActionResult(
                    true,
                    $"Foreground priority watchdog is active. Requested timer resolution: 0.5 ms; current resolution: {current / 10000d:N3} ms.");
            }
        }

        private ToolActionResult StopPriorityWatchdog()
        {
            lock (_watchdogLock)
            {
                _watchdogTimer?.Dispose();
                _watchdogTimer = null;
                int restored = 0;
                foreach ((int id, ProcessPriorityClass priority) in _changedPriorities.ToArray())
                {
                    try
                    {
                        using Process process = Process.GetProcessById(id);
                        process.PriorityClass = priority;
                        restored++;
                    }
                    catch { }
                }
                _changedPriorities.Clear();
                if (_timerResolutionActive)
                {
                    uint current = 0;
                    NtSetTimerResolution(5000, false, ref current);
                    _timerResolutionActive = false;
                }
                return new ToolActionResult(
                    true,
                    $"Priority watchdog stopped; {restored} tracked process priority value(s) restored and the timer-resolution request was released.");
            }
        }

        private void RunPriorityWatchdogCycle()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero)
                {
                    return;
                }
                GetWindowThreadProcessId(window, out uint processId);
                if (processId <= 4 || processId == Environment.ProcessId)
                {
                    return;
                }
                using Process process = Process.GetProcessById((int)processId);
                if (ProtectedProcesses.Contains(process.ProcessName))
                {
                    return;
                }
                lock (_watchdogLock)
                {
                    if (!_changedPriorities.ContainsKey(process.Id))
                    {
                        _changedPriorities[process.Id] = process.PriorityClass;
                    }
                    process.PriorityClass = ProcessPriorityClass.High;
                }
            }
            catch { }
        }

        private static int CountProcesses(string name)
        {
            Process[] processes = GetProcesses(name);
            int count = processes.Length;
            foreach (Process process in processes)
            {
                process.Dispose();
            }
            return count;
        }

        private static Process[] GetProcesses(string name)
        {
            try
            {
                return Process.GetProcessesByName(name);
            }
            catch
            {
                return Array.Empty<Process>();
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024d && unit < units.Length - 1)
            {
                size /= 1024d;
                unit++;
            }
            return $"{size:N1} {units[unit]}";
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            StopPriorityWatchdog();
        }

        private const int BeginSystemChange = 100;
        private const int EndSystemChange = 101;
        private const int ModifySettings = 12;

        [DllImport("srclient.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SRSetRestorePoint(
            ref RestorePointInfo restorePointInfo,
            out StateManagerStatus stateManagerStatus);

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr process);

        [DllImport("ntdll.dll")]
        private static extern uint NtSetSystemInformation(
            int informationClass,
            ref int information,
            int informationLength);

        [DllImport("ntdll.dll")]
        private static extern uint NtSetTimerResolution(
            uint desiredResolution,
            [MarshalAs(UnmanagedType.Bool)] bool setResolution,
            ref uint currentResolution);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr window,
            out uint processId);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private record struct RestorePointInfo
        {
            public int EventType;
            public int RestorePointType;
            public long SequenceNumber;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Description;
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly record struct StateManagerStatus(
            int Status,
            long SequenceNumber);

        private readonly record struct PrivilegeAccounts(
            bool Found,
            IReadOnlyList<string> Accounts);

        private readonly record struct CacheMetrics(long Files, long Bytes);
    }
}
