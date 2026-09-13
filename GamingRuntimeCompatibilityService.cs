using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal enum GamingRuntimeHealth
    {
        Ready,
        Attention,
        Optional
    }

    internal sealed record GamingRuntimeEntry(
        string Id,
        string Component,
        string Category,
        string Status,
        string Details,
        GamingRuntimeHealth Health,
        bool Enableable,
        string OfficialSource);

    internal sealed record GamingRuntimeOperationResult(
        bool Success,
        bool RestartRecommended,
        string Message);

    internal sealed partial class GamingRuntimeCompatibilityService
    {
        private readonly NativeCommandRunner _commandRunner = new();

        public async Task<IReadOnlyList<SystemReportEntry>> AnalyzeAsync()
        {
            List<SystemReportEntry> rows = new();
            List<UninstallEntry> uninstallEntries = ReadUninstallEntries();
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            List<string> systemFolders = new() { Path.Combine(windows, "System32") };
            if (Environment.Is64BitOperatingSystem)
            {
                systemFolders.Add(Path.Combine(windows, "SysWOW64"));
            }

            AddSection(rows, "CORE RUNTIME");
            AddDirectX(rows, systemFolders);
            await AddDotNet35Async(rows);
            AddDotNet4(rows);
            AddModernVisualCpp(rows, uninstallEntries, "x64");
            AddModernVisualCpp(rows, uninstallEntries, "x86");
            AddVulkan(rows, systemFolders);
            AddMediaFoundation(rows, windows);

            AddSection(rows, "LEGACY / GAME-DEPENDENT");
            AddLegacyVisualCpp(rows, uninstallEntries);
            AddXna(rows, uninstallEntries);
            AddOpenAl(rows, uninstallEntries, systemFolders);
            AddPhysX(rows, uninstallEntries);
            await AddOptionalFeatureAsync(
                rows,
                "DirectPlay (Legacy Components)",
                "DirectPlay",
                optionalWhenDisabled: true);

            AddSection(rows, "WINDOWS INFRASTRUCTURE");
            AddGamingServices(rows);
            foreach ((string id, string name, int expectedStart, string startName) in new[]
                     {
                         ("msiserver", "Windows Installer", 3, "Manual"),
                         ("cryptsvc", "Cryptographic Services", 2, "Automatic"),
                         ("BITS", "Background Intelligent Transfer Service (BITS)", 3, "Manual"),
                         ("TrustedInstaller", "Windows Modules Installer", 3, "Manual")
                     })
            {
                await AddServiceAsync(rows, id, name, expectedStart, startName);
            }

            return rows;
        }

        public async Task<IReadOnlyList<GamingRuntimeEntry>> AnalyzeDetailedAsync()
        {
            IReadOnlyList<SystemReportEntry> report = await AnalyzeAsync();
            List<GamingRuntimeEntry> entries = new();
            string category = "Runtime";
            foreach (SystemReportEntry row in report)
            {
                if (row.IsSection)
                {
                    category = row.Property.Trim('=', ' ');
                    continue;
                }

                string id = GetRuntimeId(row.Property);
                string status = ReadStatus(row.Value);
                GamingRuntimeHealth health = status switch
                {
                    "READY" => GamingRuntimeHealth.Ready,
                    "OPTIONAL" or "UNAVAILABLE" => GamingRuntimeHealth.Optional,
                    _ => GamingRuntimeHealth.Attention
                };
                bool enableable = IsEnableable(id, status);
                entries.Add(new GamingRuntimeEntry(
                    id,
                    row.Property,
                    category,
                    status,
                    row.Value,
                    health,
                    enableable,
                    GetOfficialSource(id)));
            }
            return entries;
        }

        public async Task<GamingRuntimeOperationResult> EnableAsync(GamingRuntimeEntry entry)
        {
            if (!entry.Enableable)
            {
                return new GamingRuntimeOperationResult(
                    false,
                    false,
                    "The selected component has no Windows-native enable operation. Use Official source for its vendor package.");
            }
            if (!WindowsPrivilegeService.IsAdministrator())
            {
                return new GamingRuntimeOperationResult(
                    false,
                    false,
                    "Administrator rights are required.");
            }

            if (entry.Id is "NetFx3" or "DirectPlay")
            {
                string featureName = entry.Id == "NetFx3" ? "NetFx3" : "DirectPlay";
                if (entry.Id == "NetFx3" && ReadWindowsBuild() >= 28000)
                {
                    return new GamingRuntimeOperationResult(
                        false,
                        false,
                        ".NET Framework 3.5 is a standalone installer on Windows build 28000 and later. Open the official Microsoft source instead.");
                }

                string dism = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32",
                    "dism.exe");
                NativeCommandResult result = await _commandRunner.RunAsync(
                    dism,
                    new[]
                    {
                        "/Online",
                        "/Enable-Feature",
                        $"/FeatureName:{featureName}",
                        "/All",
                        "/NoRestart",
                        "/English"
                    },
                    TimeSpan.FromMinutes(12),
                    CancellationToken.None);
                string state = await ReadOptionalFeatureStateAsync(featureName);
                bool verified = RuntimePrerequisiteVerification.IsEnableAccepted(result.ExitCode, state);
                bool pending = RuntimePrerequisiteVerification.IsEnablePending(state);
                return new GamingRuntimeOperationResult(
                    verified,
                    verified && (result.ExitCode == 3010 || pending),
                    verified
                        ? pending ? $"{entry.Component} enable request was accepted; restart Windows to finish enabling it. Current state: {state}."
                            : $"{entry.Component} is enabled and verified. Current state: {state}."
                        : $"DISM returned {result.ExitCode}; verification state: {state}. {result.CombinedOutput}");
            }

            (int expectedStart, string scStart) = entry.Id switch
            {
                "cryptsvc" => (2, "auto"),
                "msiserver" or "BITS" or "TrustedInstaller" => (3, "demand"),
                _ => throw new InvalidOperationException("Unsupported Windows prerequisite.")
            };
            string sc = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "sc.exe");
            NativeCommandResult configure = await _commandRunner.RunAsync(
                sc,
                new[] { "config", entry.Id, "start=", scStart },
                TimeSpan.FromSeconds(30),
                CancellationToken.None);
            if (entry.Id == "cryptsvc" && configure.ExitCode == 0)
            {
                await ServiceRestoreRuntime.EnsureAsync(entry.Id, true, _commandRunner);
            }
            int? verifiedStart = ReadServiceStart(entry.Id);
            string runtime = await ReadServiceRuntimeAsync(entry.Id);
            bool serviceVerified = configure.ExitCode == 0 && RuntimePrerequisiteVerification.ServiceReady(entry.Id, verifiedStart, expectedStart, runtime);
            return new GamingRuntimeOperationResult(
                serviceVerified,
                false,
                serviceVerified
                    ? $"{entry.Component} startup was restored and verified (Start={expectedStart}; Runtime={runtime})."
                    : $"Service configuration returned {configure.ExitCode}; registry verification returned Start={verifiedStart?.ToString(CultureInfo.InvariantCulture) ?? "Unavailable"}; Runtime={runtime}. {configure.CombinedOutput}");
        }

        public GamingRuntimeOperationResult OpenOfficialSource(GamingRuntimeEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.OfficialSource))
            {
                return new GamingRuntimeOperationResult(
                    false,
                    false,
                    "No official external source is registered for this Windows component.");
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = entry.OfficialSource,
                    UseShellExecute = true
                });
                return new GamingRuntimeOperationResult(
                    true,
                    false,
                    "Opened the registered official source.");
            }
            catch (Exception exception)
            {
                return new GamingRuntimeOperationResult(
                    false,
                    false,
                    $"Unable to open the official source. {exception.Message}");
            }
        }

        private static bool IsEnableable(string id, string status)
        {
            if (status is "READY" or "PENDING" or "DISABLING" or "UNAVAILABLE" or "UNKNOWN")
            {
                return false;
            }
            if (id == "NetFx3")
            {
                return ReadWindowsBuild() < 28000;
            }
            return id is "DirectPlay" or "msiserver" or "cryptsvc" or "BITS" or "TrustedInstaller";
        }

        private static string ReadStatus(string details)
        {
            int separator = details.IndexOf('—');
            string value = separator >= 0 ? details[..separator] : details;
            return string.IsNullOrWhiteSpace(value)
                ? "UNKNOWN"
                : value.Trim().ToUpperInvariant();
        }

        private static string GetRuntimeId(string component)
        {
            if (component.StartsWith("DirectX", StringComparison.OrdinalIgnoreCase)) return "DirectXLegacy";
            if (component.StartsWith(".NET Framework 3.5", StringComparison.OrdinalIgnoreCase)) return "NetFx3";
            if (component.StartsWith(".NET Framework 4", StringComparison.OrdinalIgnoreCase)) return "DotNet4";
            if (component.Contains("2015-2022", StringComparison.OrdinalIgnoreCase) && component.Contains("x64", StringComparison.OrdinalIgnoreCase)) return "VCRedistx64";
            if (component.Contains("2015-2022", StringComparison.OrdinalIgnoreCase) && component.Contains("x86", StringComparison.OrdinalIgnoreCase)) return "VCRedistx86";
            if (component.Contains("Legacy Redistributables", StringComparison.OrdinalIgnoreCase)) return "VCLegacy";
            if (component.StartsWith("Microsoft XNA", StringComparison.OrdinalIgnoreCase)) return "XNA4";
            if (component.StartsWith("OpenAL", StringComparison.OrdinalIgnoreCase)) return "OpenAL";
            if (component.Contains("PhysX", StringComparison.OrdinalIgnoreCase)) return "PhysX";
            if (component.StartsWith("Vulkan", StringComparison.OrdinalIgnoreCase)) return "Vulkan";
            if (component.StartsWith("Windows Media Foundation", StringComparison.OrdinalIgnoreCase)) return "MediaFoundation";
            if (component.Contains("Gaming Services", StringComparison.OrdinalIgnoreCase)) return "GamingServices";
            if (component.StartsWith("Windows Installer", StringComparison.OrdinalIgnoreCase)) return "msiserver";
            if (component.StartsWith("Cryptographic Services", StringComparison.OrdinalIgnoreCase)) return "cryptsvc";
            if (component.StartsWith("Background Intelligent", StringComparison.OrdinalIgnoreCase)) return "BITS";
            if (component.StartsWith("Windows Modules Installer", StringComparison.OrdinalIgnoreCase)) return "TrustedInstaller";
            if (component.StartsWith("DirectPlay", StringComparison.OrdinalIgnoreCase)) return "DirectPlay";
            return component;
        }

        private static string GetOfficialSource(string id) => id switch
        {
            "DirectXLegacy" => "https://www.microsoft.com/en-us/download/details.aspx?id=8109",
            "NetFx3" => "https://learn.microsoft.com/en-us/dotnet/framework/install/dotnet-35-windows-11",
            "DotNet4" => "https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481",
            "VCRedistx64" or "VCRedistx86" or "VCLegacy" => "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist",
            "XNA4" => "https://www.microsoft.com/en-us/download/details.aspx?id=27598",
            "OpenAL" => "https://www.openal.org/downloads/",
            "PhysX" => "https://www.nvidia.com/en-us/drivers/physx/physx-9-26-0703-driver/",
            "Vulkan" => "https://vulkan.lunarg.com/sdk/home",
            "GamingServices" => "ms-windows-store://pdp/?ProductId=9MWPM2CQNLHN",
            _ => string.Empty
        };

        private static void AddDirectX(List<SystemReportEntry> rows, IReadOnlyList<string> folders)
        {
            string[] files = { "d3dx9_43.dll", "d3dx11_43.dll", "xinput1_3.dll", "XAudio2_7.dll" };
            int found = folders.Sum(folder => files.Count(file => File.Exists(Path.Combine(folder, file))));
            int total = folders.Count * files.Length;
            string status = found == total ? "READY" : found > 0 ? "PARTIAL" : "MISSING";
            AddRow(rows, "DirectX End-User Runtimes (June 2010)",
                $"{status} — legacy DirectX DLL checks: {found}/{total}.");
        }

        private async Task AddDotNet35Async(List<SystemReportEntry> rows)
        {
            int build = ReadWindowsBuild();
            (bool installed, string version) = ReadDotNet35Registry();
            if (build >= 28000)
            {
                AddRow(rows, ".NET Framework 3.5 (2.0/3.0 included)",
                    installed
                        ? $"READY — standalone runtime detected; version {Fallback(version, "detected")}."
                        : $"MISSING — Windows build {build} uses Microsoft's standalone .NET Framework 3.5 installer.");
                return;
            }

            string featureState = await ReadOptionalFeatureStateAsync("NetFx3");
            string status = RuntimePrerequisiteVerification.FeatureStatus(featureState);
            AddRow(rows, ".NET Framework 3.5 (2.0/3.0 included)",
                $"{status} — feature state {featureState}; version {Fallback(version, "not detected")}." +
                (status is "PENDING" or "DISABLING" ? " Restart Windows to complete the pending servicing operation." : string.Empty));
        }

        private static void AddDotNet4(List<SystemReportEntry> rows)
        {
            int release = 0;
            string version = string.Empty;
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
                release = Convert.ToInt32(key?.GetValue("Release") ?? 0, CultureInfo.InvariantCulture);
                version = Convert.ToString(key?.GetValue("Version"), CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
            }

            string label = GetDotNet4Label(release, version);
            AddRow(rows, string.IsNullOrWhiteSpace(label) ? ".NET Framework 4.x" : $".NET Framework {label}",
                release >= 528040
                    ? $"READY — Release={release}; Version={Fallback(version, "detected")}."
                    : release > 0
                        ? $"PARTIAL — older .NET Framework 4.x detected; Release={release}."
                        : "MISSING — .NET Framework 4.x Full registry marker was not detected.");
        }

        private static void AddModernVisualCpp(
            List<SystemReportEntry> rows,
            IReadOnlyList<UninstallEntry> uninstallEntries,
            string architecture)
        {
            bool installed = false;
            string version = string.Empty;
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using RegistryKey? key = baseKey.OpenSubKey(
                        $@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\{architecture}");
                    if (Convert.ToInt32(key?.GetValue("Installed") ?? 0, CultureInfo.InvariantCulture) == 1)
                    {
                        installed = true;
                        version = Convert.ToString(key?.GetValue("Version"), CultureInfo.InvariantCulture) ?? string.Empty;
                        break;
                    }
                }
                catch
                {
                }
            }

            if (!installed)
            {
                UninstallEntry? hit = uninstallEntries.FirstOrDefault(entry =>
                    Regex.IsMatch(
                        entry.DisplayName,
                        $@"Microsoft Visual C\+\+ 2015-2022 Redistributable.*\({Regex.Escape(architecture)}\)",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
                if (hit is not null)
                {
                    installed = true;
                    version = hit.DisplayVersion;
                }
            }

            AddRow(rows, $"Microsoft Visual C++ 2015-2022 Redistributable ({architecture})",
                installed
                    ? $"READY — version {Fallback(version, "detected")}."
                    : "MISSING — runtime registration was not detected.");
        }

        private static void AddLegacyVisualCpp(
            List<SystemReportEntry> rows,
            IReadOnlyList<UninstallEntry> entries)
        {
            string[] years = { "2005", "2008", "2010", "2012", "2013" };
            List<string> found = years.Where(year => entries.Any(entry => Regex.IsMatch(
                entry.DisplayName,
                $@"Microsoft Visual C\+\+ {year}.*Redistributable",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))).ToList();
            string status = found.Count == years.Length ? "READY" : found.Count > 0 ? "PARTIAL" : "OPTIONAL";
            AddRow(rows, "Microsoft Visual C++ Legacy Redistributables (2005-2013)",
                $"{status} — detected generations: {(found.Count == 0 ? "none" : string.Join(", ", found))}.");
        }

        private static void AddXna(List<SystemReportEntry> rows, IReadOnlyList<UninstallEntry> entries)
        {
            UninstallEntry? hit = entries.FirstOrDefault(entry =>
                Regex.IsMatch(entry.DisplayName, @"Microsoft XNA Framework.*4\.0", RegexOptions.IgnoreCase));
            AddRow(rows, "Microsoft XNA Framework 4.0 Refresh",
                hit is null
                    ? "OPTIONAL — not detected; required only by XNA-based games and tools."
                    : $"READY — {hit.DisplayName} {hit.DisplayVersion}." );
        }

        private static void AddOpenAl(
            List<SystemReportEntry> rows,
            IReadOnlyList<UninstallEntry> entries,
            IReadOnlyList<string> systemFolders)
        {
            bool detected = systemFolders.Any(folder => File.Exists(Path.Combine(folder, "OpenAL32.dll"))) ||
                            entries.Any(entry => entry.DisplayName.StartsWith("OpenAL", StringComparison.OrdinalIgnoreCase));
            AddRow(rows, "OpenAL Runtime", detected
                ? "READY — runtime file or installed-product registration detected."
                : "OPTIONAL — not detected; required by some older 3D-audio games.");
        }

        private static void AddPhysX(List<SystemReportEntry> rows, IReadOnlyList<UninstallEntry> entries)
        {
            bool detected = File.Exists(@"C:\Program Files (x86)\NVIDIA Corporation\PhysX\Common\PhysXLoader.dll") ||
                            entries.Any(entry => entry.DisplayName.Contains("NVIDIA PhysX", StringComparison.OrdinalIgnoreCase));
            AddRow(rows, "NVIDIA PhysX System Software", detected
                ? "READY — PhysX runtime detected."
                : "OPTIONAL — not detected; required only by games using the legacy PhysX runtime.");
        }

        private static void AddVulkan(List<SystemReportEntry> rows, IReadOnlyList<string> systemFolders)
        {
            int count = systemFolders.Count(folder => File.Exists(Path.Combine(folder, "vulkan-1.dll")));
            AddRow(rows, "Vulkan Runtime / Loader", count > 0
                ? $"READY — Vulkan loader detected in {count} system location(s); GPU support still depends on the vendor driver."
                : "MISSING — vulkan-1.dll was not detected.");
        }

        private static void AddMediaFoundation(List<SystemReportEntry> rows, string windows)
        {
            string[] files = { "mf.dll", "mfplat.dll", "mfreadwrite.dll" };
            int found = files.Count(file => File.Exists(Path.Combine(windows, "System32", file)));
            AddRow(rows, "Windows Media Foundation", found == files.Length
                ? "READY — core files are available for game video and cutscene playback."
                : found > 0
                    ? $"PARTIAL — core files detected: {found}/{files.Length}."
                    : "MISSING — Windows N/KN editions may require Media Feature Pack.");
        }

        private static void AddGamingServices(List<SystemReportEntry> rows)
        {
            Package? package = null;
            try
            {
                package = new PackageManager().FindPackagesForUser(string.Empty)
                    .Where(item => string.Equals(
                        item.Id.Name,
                        "Microsoft.GamingServices",
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.Id.Version.Major)
                    .FirstOrDefault();
            }
            catch
            {
            }

            int detectedServices = 0;
            int disabledServices = 0;
            foreach (string serviceName in new[] { "GamingServices", "GamingServicesNet" })
            {
                int? start = ReadServiceStart(serviceName);
                if (start.HasValue)
                {
                    detectedServices++;
                    if (start == 4) disabledServices++;
                }
            }

            if (package is null && detectedServices == 0)
            {
                AddRow(rows, "Microsoft Gaming Services",
                    "OPTIONAL — not detected; mainly required by Xbox app and PC Game Pass titles.");
            }
            else if (disabledServices > 0)
            {
                AddRow(rows, "Microsoft Gaming Services",
                    "DISABLED — one or more related services are disabled.");
            }
            else
            {
                AddRow(rows, "Microsoft Gaming Services",
                    $"READY — package/services detected; services={detectedServices}.");
            }
        }

        private async Task AddServiceAsync(
            List<SystemReportEntry> rows,
            string id,
            string name,
            int expectedStart,
            string startName)
        {
            int? start = ReadServiceStart(id);
            if (!start.HasValue)
            {
                AddRow(rows, name, "UNAVAILABLE — service or startup value was not found.");
                return;
            }
            string runtime = await ReadServiceRuntimeAsync(id);
            AddRow(rows, name,
                start == 4
                    ? $"DISABLED — expected {startName}; runtime={runtime}."
                    : !RuntimePrerequisiteVerification.ServiceReady(id, start, expectedStart, runtime)
                        ? $"CUSTOM — Start={start}; baseline={startName} ({expectedStart}); runtime={runtime}."
                        : $"READY — startup={startName}; runtime={runtime}.");
        }

        private async Task AddOptionalFeatureAsync(
            List<SystemReportEntry> rows,
            string component,
            string featureName,
            bool optionalWhenDisabled)
        {
            string state = await ReadOptionalFeatureStateAsync(featureName);
            string status = RuntimePrerequisiteVerification.FeatureStatus(state, optionalWhenDisabled);
            AddRow(rows, component,
                $"{status} — feature state {state}." +
                (status is "PENDING" or "DISABLING" ? " Restart Windows to complete the pending servicing operation." :
                    status == "OPTIONAL" ? " Enable only for games that require it." : string.Empty));
        }

        private async Task<string> ReadOptionalFeatureStateAsync(string featureName)
        {
            string dism = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32",
                "dism.exe");
            if (!File.Exists(dism))
            {
                return "Unavailable";
            }
            NativeCommandResult result = await _commandRunner.RunAsync(
                dism,
                new[] { "/Online", "/Get-FeatureInfo", $"/FeatureName:{featureName}", "/English" },
                TimeSpan.FromSeconds(45),
                CancellationToken.None);
            Match match = FeatureStatePattern().Match(result.CombinedOutput);
            return result.ExitCode == 0 && match.Success ? match.Groups[1].Value.Trim() : "Unavailable";
        }

        private async Task<string> ReadServiceRuntimeAsync(string serviceName)
        {
            NativeCommandResult result = await _commandRunner.RunAsync(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "sc.exe"),
                new[] { "query", serviceName },
                TimeSpan.FromSeconds(10));
            Match match = ServiceStatePattern().Match(result.CombinedOutput);
            if (result.ExitCode != 0 || !match.Success || !int.TryParse(match.Groups[1].Value, out int state))
            {
                return "Unknown";
            }
            return state switch { 1 => "Stopped", 4 => "Running", _ => $"State{state}" };
        }

        private static List<UninstallEntry> ReadUninstallEntries()
        {
            Dictionary<string, UninstallEntry> entries = new(StringComparer.OrdinalIgnoreCase);
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using RegistryKey? root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (root is null) continue;
                    foreach (string subKeyName in root.GetSubKeyNames())
                    {
                        try
                        {
                            using RegistryKey? key = root.OpenSubKey(subKeyName);
                            string name = Convert.ToString(key?.GetValue("DisplayName"), CultureInfo.InvariantCulture) ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            string version = Convert.ToString(key?.GetValue("DisplayVersion"), CultureInfo.InvariantCulture) ?? string.Empty;
                            entries[$"{name}\0{version}"] = new UninstallEntry(name, version);
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }
            }
            return entries.Values.ToList();
        }

        private static (bool Installed, string Version) ReadDotNet35Registry()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v3.5");
                    if (Convert.ToInt32(key?.GetValue("Install") ?? 0, CultureInfo.InvariantCulture) == 1)
                    {
                        return (true, Convert.ToString(key?.GetValue("Version"), CultureInfo.InvariantCulture) ?? string.Empty);
                    }
                }
                catch
                {
                }
            }
            return (false, string.Empty);
        }

        private static int ReadWindowsBuild()
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string value = Convert.ToString(key?.GetValue("CurrentBuildNumber"), CultureInfo.InvariantCulture) ?? string.Empty;
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int build)
                    ? build
                    : Environment.OSVersion.Version.Build;
            }
            catch
            {
                return Environment.OSVersion.Version.Build;
            }
        }

        private static int? ReadServiceStart(string serviceName)
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}");
                object? value = key?.GetValue("Start");
                return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static string GetDotNet4Label(int release, string version)
        {
            if (release >= 533320) return "4.8.1";
            if (release >= 528040) return "4.8";
            if (release >= 461808) return "4.7.2";
            if (release >= 461308) return "4.7.1";
            if (release >= 460798) return "4.7";
            if (release >= 394802) return "4.6.2";
            if (release >= 394254) return "4.6.1";
            if (release >= 393295) return "4.6";
            return !string.IsNullOrWhiteSpace(version) ? version : release > 0 ? $"Release {release}" : string.Empty;
        }

        private static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;

        private static void AddSection(List<SystemReportEntry> rows, string title) =>
            rows.Add(new SystemReportEntry($"=== {title} ===", string.Empty, true));

        private static void AddRow(List<SystemReportEntry> rows, string property, string value) =>
            rows.Add(new SystemReportEntry(property, value, false));

        private sealed record UninstallEntry(string DisplayName, string DisplayVersion);

        [GeneratedRegex(@"(?im)^\s*State\s*:\s*(Enabled|Disabled(?:\s+with\s+Payload\s+Removed)?|Enable Pending|Disable Pending)\s*$")]
        private static partial Regex FeatureStatePattern();

        [GeneratedRegex(@"(?im)STATE\s*:\s*(\d+)")]
        private static partial Regex ServiceStatePattern();
    }
}
