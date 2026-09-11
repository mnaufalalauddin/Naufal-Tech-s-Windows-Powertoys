using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed record GamingRuntimeInstallResult(
        bool Success,
        bool Verified,
        int WarningCount,
        bool RestartRequired,
        string Report);

    /// <summary>
    /// Downloads and installs only explicitly mapped runtime packages from
    /// publisher-controlled HTTPS endpoints. Every executable payload must pass
    /// WinVerifyTrust and a publisher-name allow-list before it can run.
    /// </summary>
    internal sealed class GamingRuntimeInstallerService
    {
        private enum InstallerMode
        {
            Executable,
            Msi,
            DirectX,
            OpenAlZip,
            NetFx3Feature,
            LegacyVisualCppBundle
        }

        private sealed record PackageDefinition(
            string Id,
            string Label,
            string FileName,
            Uri? DownloadUri,
            IReadOnlyList<string> Publishers,
            InstallerMode Mode,
            IReadOnlyList<string> InstallArguments,
            IReadOnlyList<string> RepairArguments,
            IReadOnlySet<int> AcceptedExitCodes,
            string PublishedSha256 = "");

        private sealed record PreparedInstaller(
            PackageDefinition Definition,
            string DownloadedPath,
            string ExecutablePath,
            IReadOnlyList<string> Arguments,
            string Signer);

        private sealed record AuthenticodeVerification(
            bool Trusted,
            bool PublisherMatches,
            string Subject,
            string Thumbprint,
            int TrustResult);

        private const long MaximumPackageBytes = 2L * 1024L * 1024L * 1024L;
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private readonly NativeCommandRunner _commandRunner = new();

        public static bool CanInstall(string id) => GetDefinition(id) is not null;

        public static bool CanRepair(string id) => id is
            "DirectXLegacy" or "NetFx3" or "DotNet4" or
            "VCRedistx64" or "VCRedistx86" or "VCLegacy" or
            "XNA4" or "OpenAL" or "PhysX" or "Vulkan";

        public async Task<GamingRuntimeInstallResult> RunAsync(
            GamingRuntimeEntry entry,
            bool repairMode,
            GamingRuntimeCompatibilityService analyzer,
            IProgress<MaintenanceProgressUpdate>? progress = null,
            CancellationToken cancellationToken = default)
        {
            const int stageCount = 5;
            StringBuilder report = new();
            int warnings = 0;
            MaintenanceStageTracker stages = new(progress, () => warnings);
            progress = stages;
            bool restartRequired = false;
            bool verified = false;
            string operation = repairMode ? "REPAIR / RE-INSTALL" : "DOWNLOAD & INSTALL";
            report.AppendLine($"GAMING RUNTIME {operation}");
            report.AppendLine($"Started: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
            report.AppendLine($"Component: {entry.Component}");
            report.AppendLine($"Runtime ID: {entry.Id}");
            report.AppendLine(new string('=', 76));

            try
            {
                Report(progress, 1, stageCount, "Preflight and package plan", "Checking Administrator rights and the signed-package plan...");
                if (!WindowsPrivilegeService.IsAdministrator())
                {
                    throw new InvalidOperationException("Administrator rights are required.");
                }
                PackageDefinition definition = GetDefinition(entry.Id)
                    ?? throw new InvalidOperationException("No automatic official installer is registered for this runtime.");
                string cache = GetCacheDirectory();
                report.AppendLine("[1/5] Preflight and package plan");
                report.AppendLine($"Mode: {definition.Mode}");
                report.AppendLine($"Cache: {cache}");

                IReadOnlyList<PackageDefinition> packages = definition.Mode == InstallerMode.LegacyVisualCppBundle
                    ? GetLegacyVisualCppDefinitions()
                    : new[] { definition };
                report.AppendLine($"Packages planned: {packages.Count}");

                Report(progress, 2, stageCount, "Download official package(s)", "Downloading publisher packages over approved HTTPS hosts...");
                List<(PackageDefinition Definition, string Path)> downloaded = new();
                if (definition.Mode == InstallerMode.NetFx3Feature)
                {
                    report.AppendLine("[2/5] Download official package(s)");
                    report.AppendLine("SKIPPED: DISM and Windows Update manage the Feature-on-Demand payload.");
                    stages.Skip("Windows servicing manages the feature payload.");
                }
                else
                {
                    for (int index = 0; index < packages.Count; index++)
                    {
                        PackageDefinition package = packages[index];
                        string path = Path.Combine(cache, package.FileName);
                        long bytes = await DownloadAsync(
                            package,
                            path,
                            index,
                            packages.Count,
                            progress,
                            stageCount,
                            cancellationToken);
                        downloaded.Add((package, path));
                        report.AppendLine($"Downloaded: {package.Label} | {bytes / 1024d / 1024d:N1} MB | {path}");
                    }
                    report.AppendLine("[2/5] Download official package(s)");
                    report.AppendLine($"Downloaded {downloaded.Count}/{packages.Count} package(s).");
                }

                Report(progress, 3, stageCount, "Verify package integrity and publisher", "Checking SHA-256, Windows trust, and allowed publisher identities...");
                List<PreparedInstaller> prepared = new();
                if (definition.Mode == InstallerMode.NetFx3Feature)
                {
                    string dism = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                        "System32",
                        "dism.exe");
                    if (!File.Exists(dism))
                    {
                        throw new FileNotFoundException("DISM.exe was not found.", dism);
                    }
                    prepared.Add(new PreparedInstaller(
                        definition,
                        dism,
                        dism,
                        new[]
                        {
                            "/Online",
                            "/Enable-Feature",
                            "/FeatureName:NetFx3",
                            "/All",
                            "/NoRestart",
                            "/English"
                        },
                        "Windows servicing"));
                    report.AppendLine("[3/5] Verify package integrity and publisher");
                    report.AppendLine("Windows servicing path verified.");
                }
                else
                {
                    for (int index = 0; index < downloaded.Count; index++)
                    {
                        (PackageDefinition package, string path) = downloaded[index];
                        progress?.Report(new MaintenanceProgressUpdate(
                            3,
                            stageCount,
                            "Verify package integrity and publisher",
                            $"Verifying {package.Label} ({index + 1}/{downloaded.Count})...",
                            (index + 1d) * 100d / downloaded.Count));
                        PreparedInstaller installer = await PrepareInstallerAsync(
                            package,
                            path,
                            repairMode,
                            cache,
                            cancellationToken);
                        prepared.Add(installer);
                        report.AppendLine($"Signature PASS: {package.Label} | {installer.Signer}");
                        if (!string.IsNullOrWhiteSpace(package.PublishedSha256))
                        {
                            report.AppendLine($"Published SHA-256 PASS: {package.PublishedSha256}");
                        }
                    }
                    report.AppendLine("[3/5] Verify package integrity and publisher");
                    report.AppendLine($"Verified {prepared.Count}/{downloaded.Count} installer payload(s).");
                }

                Report(
                    progress,
                    4,
                    stageCount,
                    repairMode ? "Repair / re-install runtime" : "Install runtime",
                    repairMode ? "Repairing or re-installing the selected runtime..." : "Installing the selected runtime...");
                report.AppendLine($"[4/5] {(repairMode ? "Repair / re-install runtime" : "Install runtime")}");
                for (int index = 0; index < prepared.Count; index++)
                {
                    PreparedInstaller installer = prepared[index];
                    progress?.Report(new MaintenanceProgressUpdate(
                        4,
                        stageCount,
                        repairMode ? "Repair / re-install runtime" : "Install runtime",
                        $"Running {installer.Definition.Label} ({index + 1}/{prepared.Count})...",
                        index * 100d / Math.Max(1, prepared.Count)));
                    NativeCommandResult result = await _commandRunner.RunAsync(
                        installer.ExecutablePath,
                        installer.Arguments,
                        TimeSpan.FromMinutes(30),
                        cancellationToken);
                    report.AppendLine($"{installer.Definition.Label}: exit {result.ExitCode}; duration {result.Duration:hh\\:mm\\:ss}");
                    if (!string.IsNullOrWhiteSpace(result.CombinedOutput))
                    {
                        report.AppendLine(result.CombinedOutput);
                    }
                    if (!installer.Definition.AcceptedExitCodes.Contains(result.ExitCode))
                    {
                        throw new InvalidOperationException(
                            $"{installer.Definition.Label} returned exit code {result.ExitCode}.");
                    }
                    if (result.ExitCode is 3010 or 1641)
                    {
                        restartRequired = true;
                    }
                    if (result.ExitCode == 1638)
                    {
                        warnings++;
                        report.AppendLine("WARNING: Windows Installer reports an existing product/version; final read-back decides success.");
                    }
                }

                Report(progress, 5, stageCount, "Post-install read-back verification", "Re-analyzing the selected runtime state...");
                GamingRuntimeEntry? after = null;
                for (int probe = 1; probe <= 20; probe++)
                {
                    await Task.Delay(500, cancellationToken);
                    IReadOnlyList<GamingRuntimeEntry> entries = await analyzer.AnalyzeDetailedAsync();
                    after = entries.FirstOrDefault(item =>
                        string.Equals(item.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
                    verified = after?.Health == GamingRuntimeHealth.Ready;
                    progress?.Report(new MaintenanceProgressUpdate(
                        5,
                        stageCount,
                        "Post-install read-back verification",
                        $"Verifying installed state ({probe}/20): {after?.Status ?? "not detected"}",
                        probe * 5d));
                    if (verified)
                    {
                        break;
                    }
                }
                report.AppendLine("[5/5] Post-install read-back verification");
                report.AppendLine($"Status: {after?.Status ?? "Unavailable"}");
                report.AppendLine($"Details: {after?.Details ?? "The runtime row was not found."}");
                if (!verified && !restartRequired)
                {
                    throw new InvalidOperationException(
                        "The installer completed, but the runtime did not read back as READY.");
                }
                if (!verified && restartRequired)
                {
                    warnings++;
                    report.AppendLine("WARNING: Final read-back is deferred until after the requested restart.");
                }
                report.AppendLine(new string('=', 76));
                report.AppendLine("RUNTIME OPERATION COMPLETED");
                stages.Complete(true);
                return new GamingRuntimeInstallResult(
                    true,
                    verified,
                    warnings,
                    restartRequired,
                    report.ToString().TrimEnd());
            }
            catch (Exception exception)
            {
                report.AppendLine(new string('=', 76));
                report.AppendLine($"FAILED: {exception.Message}");
                stages.Complete(false);
                return new GamingRuntimeInstallResult(
                    false,
                    false,
                    warnings,
                    restartRequired,
                    report.ToString().TrimEnd());
            }
        }

        private async Task<PreparedInstaller> PrepareInstallerAsync(
            PackageDefinition definition,
            string path,
            bool repairMode,
            string cache,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(definition.PublishedSha256))
            {
                string actual = await ComputeSha256Async(path, cancellationToken);
                if (!actual.Equals(definition.PublishedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"SHA-256 mismatch for {definition.Label}. Expected {definition.PublishedSha256}; actual {actual}.");
                }
            }

            if (definition.Mode == InstallerMode.DirectX)
            {
                AuthenticodeVerification outer = VerifyAuthenticode(path, definition.Publishers);
                EnsureTrusted(outer, definition.Label);
                string extract = CreateExtractionDirectory(cache, "DirectX-June2010");
                NativeCommandResult result = await _commandRunner.RunAsync(
                    path,
                    new[] { "/Q", $"/T:{extract}" },
                    TimeSpan.FromMinutes(5),
                    cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"DirectX extraction returned exit code {result.ExitCode}.");
                }
                string setup = Directory.EnumerateFiles(extract, "DXSETUP.exe", SearchOption.AllDirectories)
                    .FirstOrDefault() ?? throw new FileNotFoundException("DXSETUP.exe was not found after extraction.");
                AuthenticodeVerification inner = VerifyAuthenticode(setup, new[] { "Microsoft" });
                EnsureTrusted(inner, "DirectX DXSETUP");
                return new PreparedInstaller(
                    definition,
                    path,
                    setup,
                    new[] { "/silent" },
                    inner.Subject);
            }

            if (definition.Mode == InstallerMode.OpenAlZip)
            {
                string extract = CreateExtractionDirectory(cache, "OpenAL");
                ExtractZipSafely(path, extract);
                string setup = Directory.EnumerateFiles(extract, "oalinst.exe", SearchOption.AllDirectories)
                    .FirstOrDefault() ?? throw new FileNotFoundException("oalinst.exe was not found inside the OpenAL package.");
                AuthenticodeVerification inner = VerifyAuthenticode(setup, definition.Publishers);
                EnsureTrusted(inner, definition.Label);
                return new PreparedInstaller(
                    definition,
                    path,
                    setup,
                    repairMode && definition.RepairArguments.Count > 0
                        ? definition.RepairArguments
                        : definition.InstallArguments,
                    inner.Subject);
            }

            AuthenticodeVerification signature = VerifyAuthenticode(path, definition.Publishers);
            EnsureTrusted(signature, definition.Label);
            if (definition.Mode == InstallerMode.Msi)
            {
                string msiexec = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "System32",
                    "msiexec.exe");
                IReadOnlyList<string> arguments = repairMode
                    ? new[] { "/fa", path, "/qn", "/norestart" }
                    : new[] { "/i", path, "/qn", "/norestart" };
                return new PreparedInstaller(
                    definition,
                    path,
                    msiexec,
                    arguments,
                    signature.Subject);
            }
            return new PreparedInstaller(
                definition,
                path,
                path,
                repairMode && definition.RepairArguments.Count > 0
                    ? definition.RepairArguments
                    : definition.InstallArguments,
                signature.Subject);
        }

        private static async Task<long> DownloadAsync(
            PackageDefinition definition, string destination, int itemIndex, int itemCount,
            IProgress<MaintenanceProgressUpdate>? progress, int stageCount,
            CancellationToken cancellationToken)
        {
            Uri uri = definition.DownloadUri
                ?? throw new InvalidOperationException($"{definition.Label} has no download URI.");
            using HttpResponseMessage response = await SendFollowingRedirectsAsync(
                uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            long? expected = response.Content.Headers.ContentLength;
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
            int lastPercent = -5;
            return await AtomicDownloadFile.SaveAsync(input, destination, expected, 1024, MaximumPackageBytes,
                received =>
                {
                    double fraction = expected > 0 ? Math.Clamp(received / (double)expected.Value, 0d, 1d) : 0d;
                    int percent = (int)Math.Round(fraction * 100d);
                    if (percent >= lastPercent + 5 || percent == 100)
                    {
                        lastPercent = percent;
                        progress?.Report(new MaintenanceProgressUpdate(
                            2, stageCount, "Download official package(s)",
                            $"Downloading {definition.Label} ({itemIndex + 1}/{itemCount}) - {received / 1024d / 1024d:N1} MB",
                            ((itemIndex + fraction) / Math.Max(1, itemCount)) * 100d));
                    }
                }, cancellationToken);
        }

        private static async Task<HttpResponseMessage> SendFollowingRedirectsAsync(
            Uri initial,
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            Uri current = initial;
            for (int redirect = 0; redirect <= 10; redirect++)
            {
                if (!IsApprovedUri(current))
                {
                    throw new InvalidOperationException($"Blocked non-approved HTTPS host: {current.DnsSafeHost}");
                }
                using HttpRequestMessage request = new(HttpMethod.Get, current);
                HttpResponseMessage response = await HttpClient.SendAsync(
                    request,
                    completionOption,
                    cancellationToken);
                if ((int)response.StatusCode is >= 300 and <= 399 && response.Headers.Location is not null)
                {
                    Uri next = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(current, response.Headers.Location);
                    response.Dispose();
                    current = next;
                    continue;
                }
                response.EnsureSuccessStatusCode();
                return response;
            }
            throw new InvalidOperationException("The official runtime download exceeded ten HTTPS redirects.");
        }

        private static bool IsApprovedUri(Uri uri)
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
            return HostIsOrSubdomain(host, "microsoft.com") ||
                   HostIsOrSubdomain(host, "visualstudio.microsoft.com") ||
                   host.Equals("aka.ms", StringComparison.Ordinal) ||
                   HostIsOrSubdomain(host, "openal.org") ||
                   HostIsOrSubdomain(host, "nvidia.com") ||
                   HostIsOrSubdomain(host, "lunarg.com");
        }

        private static bool HostIsOrSubdomain(string host, string root)
        {
            return host.Equals(root, StringComparison.Ordinal) ||
                   host.EndsWith('.' + root, StringComparison.Ordinal);
        }

        private static HttpClient CreateHttpClient()
        {
            HttpClientHandler handler = new()
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                UseCookies = false
            };
            HttpClient client = new(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Naufal-Windows-Powertoys/1.0");
            return client;
        }

        private static async Task<string> ComputeSha256Async(
            string path,
            CancellationToken cancellationToken)
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static AuthenticodeVerification VerifyAuthenticode(
            string path,
            IReadOnlyList<string> expectedPublishers)
        {
            int trustResult = WinVerifyTrustFile(path);
            string subject = string.Empty;
            string thumbprint = string.Empty;
            try
            {
#pragma warning disable SYSLIB0057 // Required to extract the Authenticode signer from a signed PE/MSI payload.
                using X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
                using X509Certificate2 certificate2 = X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                subject = certificate2.Subject;
                thumbprint = certificate2.Thumbprint;
            }
            catch
            {
            }
            bool publisherMatches = expectedPublishers.Any(expected =>
                subject.Contains(expected, StringComparison.OrdinalIgnoreCase));
            return new AuthenticodeVerification(
                trustResult == 0,
                publisherMatches,
                subject,
                thumbprint,
                trustResult);
        }

        private static void EnsureTrusted(
            AuthenticodeVerification verification,
            string label)
        {
            if (!verification.Trusted)
            {
                throw new InvalidDataException(
                    $"Windows did not trust the Authenticode signature for {label} (0x{verification.TrustResult:X8}).");
            }
            if (!verification.PublisherMatches)
            {
                throw new InvalidDataException(
                    $"The trusted signer for {label} is not an approved publisher. Signer={verification.Subject}");
            }
        }

        private static int WinVerifyTrustFile(string path)
        {
            Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            IntPtr pathPointer = Marshal.StringToCoTaskMemUni(path);
            IntPtr fileInfoPointer = IntPtr.Zero;
            try
            {
                WinTrustFileInfo fileInfo = new()
                {
                    StructSize = checked((uint)Marshal.SizeOf<WinTrustFileInfo>()),
                    FilePath = pathPointer
                };
                fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
                WinTrustData data = new()
                {
                    StructSize = checked((uint)Marshal.SizeOf<WinTrustData>()),
                    UiChoice = 2,
                    RevocationChecks = 0,
                    UnionChoice = 1,
                    FileInfo = fileInfoPointer,
                    StateAction = 0,
                    ProviderFlags = 0x40,
                    UiContext = 0
                };
                return WinVerifyTrust(IntPtr.Zero, ref action, ref data);
            }
            finally
            {
                if (fileInfoPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(fileInfoPointer);
                }
                Marshal.FreeCoTaskMem(pathPointer);
            }
        }

        private static string GetCacheDirectory()
        {
            string path = AppDataPaths.RuntimeCacheDirectory;
            Directory.CreateDirectory(path);
            return path;
        }

        private static string CreateExtractionDirectory(string cache, string prefix)
        {
            string root = Path.GetFullPath(cache).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(cache, $"{prefix}-{Guid.NewGuid():N}"));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The extraction path escaped the runtime cache directory.");
            }
            Directory.CreateDirectory(path);
            return path;
        }

        private static void ExtractZipSafely(string archivePath, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The OpenAL archive contains a path outside its extraction directory.");
                }
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
            }
        }

        private static PackageDefinition? GetDefinition(string id)
        {
            HashSet<int> accepted = new() { 0, 3010, 1641 };
            return id switch
            {
                "NetFx3" when ReadWindowsBuild() >= 28000 => new PackageDefinition(
                    id, ".NET Framework 3.5", "DotNet35Setup.exe",
                    new Uri("https://go.microsoft.com/fwlink/?LinkID=2337635"),
                    new[] { "Microsoft" }, InstallerMode.Executable,
                    new[] { "/q", "/norestart" }, new[] { "/repair", "/q", "/norestart" }, accepted),
                "NetFx3" => new PackageDefinition(
                    id, ".NET Framework 3.5 Feature-on-Demand", string.Empty, null,
                    Array.Empty<string>(), InstallerMode.NetFx3Feature,
                    Array.Empty<string>(), Array.Empty<string>(), new HashSet<int> { 0, 3010 }),
                "DirectXLegacy" => new PackageDefinition(
                    id, "DirectX End-User Runtimes (June 2010)", "directx_Jun2010_redist.exe",
                    new Uri("https://download.microsoft.com/download/8/4/A/84A35BF1-DAFE-4AE8-82AF-AD2AE20B6B14/directx_Jun2010_redist.exe"),
                    new[] { "Microsoft" }, InstallerMode.DirectX,
                    Array.Empty<string>(), Array.Empty<string>(), accepted),
                "VCRedistx64" => CreateExecutable(id, "Visual C++ 2015-2022 x64", "vc_redist.x64.exe", "https://aka.ms/vc14/vc_redist.x64.exe", new[] { "Microsoft" }, new[] { "/install", "/quiet", "/norestart" }, new[] { "/repair", "/quiet", "/norestart" }),
                "VCRedistx86" => CreateExecutable(id, "Visual C++ 2015-2022 x86", "vc_redist.x86.exe", "https://aka.ms/vc14/vc_redist.x86.exe", new[] { "Microsoft" }, new[] { "/install", "/quiet", "/norestart" }, new[] { "/repair", "/quiet", "/norestart" }),
                "DotNet4" => CreateExecutable(id, ".NET Framework 4.8.1", "NDP481-x86-x64-AllOS-ENU.exe", "https://download.microsoft.com/download/4/b/2/cd00d4ed-ebdd-49ee-8a33-eabc3d1030e3/NDP481-x86-x64-AllOS-ENU.exe", new[] { "Microsoft" }, new[] { "/q", "/norestart" }, new[] { "/repair", "/q", "/norestart" }),
                "WebView2" => CreateExecutable(id, "Microsoft Edge WebView2 Runtime", "MicrosoftEdgeWebview2Setup.exe", "https://go.microsoft.com/fwlink/p/?LinkId=2124703", new[] { "Microsoft" }, new[] { "/silent", "/install" }, Array.Empty<string>()),
                "VCLegacy" => new PackageDefinition(
                    id, "Visual C++ Legacy Redistributables", string.Empty, null,
                    new[] { "Microsoft" }, InstallerMode.LegacyVisualCppBundle,
                    Array.Empty<string>(), Array.Empty<string>(), accepted),
                "XNA4" => new PackageDefinition(
                    id, "Microsoft XNA Framework 4.0 Refresh", "xnafx40_redist.msi",
                    new Uri("https://download.microsoft.com/download/5/3/A/53A804C8-EC78-43CD-A0F0-2FB4D45603D3/xnafx40_redist.msi"),
                    new[] { "Microsoft" }, InstallerMode.Msi,
                    Array.Empty<string>(), Array.Empty<string>(), accepted),
                "OpenAL" => new PackageDefinition(
                    id, "OpenAL Runtime", "oalinst.zip",
                    new Uri("https://openal.org/downloads/oalinst.zip"),
                    new[] { "Creative Labs Inc", "Creative Labs", "Creative Technology" }, InstallerMode.OpenAlZip,
                    new[] { "/S" }, new[] { "/S" }, new HashSet<int> { 0 }),
                "PhysX" => CreateExecutable(id, "NVIDIA PhysX System Software", "PhysX_9.26.0703_SystemSoftware.exe", "https://us.download.nvidia.com/Windows/9.26.0703/PhysX_9.26.0703_SystemSoftware.exe", new[] { "NVIDIA Corporation", "NVIDIA" }, new[] { "/s" }, new[] { "/s" }),
                "Vulkan" => new PackageDefinition(
                    id, "LunarG Vulkan Runtime / Loader", "VulkanRT-X64-1.4.357.0-Installer.exe",
                    new Uri("https://sdk.lunarg.com/sdk/download/1.4.357.0/windows/VulkanRT-X64-1.4.357.0-Installer.exe"),
                    new[] { "LunarG" }, InstallerMode.Executable,
                    new[] { "/S" }, new[] { "/S" }, accepted,
                    "58a9edee599e06dac0485df9ea7767016cc137c776d5d95ae08301fff2def81c"),
                _ => null
            };
        }

        private static PackageDefinition CreateExecutable(
            string id,
            string label,
            string fileName,
            string uri,
            IReadOnlyList<string> publishers,
            IReadOnlyList<string> installArguments,
            IReadOnlyList<string> repairArguments)
        {
            return new PackageDefinition(
                id,
                label,
                fileName,
                new Uri(uri),
                publishers,
                InstallerMode.Executable,
                installArguments,
                repairArguments,
                new HashSet<int> { 0, 3010, 1641 });
        }

        private static IReadOnlyList<PackageDefinition> GetLegacyVisualCppDefinitions()
        {
            List<PackageDefinition> definitions = new();
            void Add(string year, string arch, string file, string uri, params string[] args)
            {
                definitions.Add(new PackageDefinition(
                    "VCLegacy",
                    $"Visual C++ {year} {arch}",
                    file,
                    new Uri(uri),
                    new[] { "Microsoft" },
                    InstallerMode.Executable,
                    args,
                    args,
                    new HashSet<int> { 0, 1638, 3010, 1641 }));
            }
            Add("2005", "x86", "vc2005_x86.exe", "https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x86.EXE", "/Q", "/R:N");
            if (Environment.Is64BitOperatingSystem) Add("2005", "x64", "vc2005_x64.exe", "https://download.microsoft.com/download/8/B/4/8B42259F-5D70-43F4-AC2E-4B208FD8D66A/vcredist_x64.EXE", "/Q", "/R:N");
            Add("2008", "x86", "vc2008_x86.exe", "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x86.exe", "/Q", "/R:N");
            if (Environment.Is64BitOperatingSystem) Add("2008", "x64", "vc2008_x64.exe", "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x64.exe", "/Q", "/R:N");
            Add("2010", "x86", "vc2010_x86.exe", "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe", "/quiet", "/norestart");
            if (Environment.Is64BitOperatingSystem) Add("2010", "x64", "vc2010_x64.exe", "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x64.exe", "/quiet", "/norestart");
            Add("2012", "x86", "vc2012_x86.exe", "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe", "/install", "/quiet", "/norestart");
            if (Environment.Is64BitOperatingSystem) Add("2012", "x64", "vc2012_x64.exe", "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe", "/install", "/quiet", "/norestart");
            Add("2013", "x86", "vc2013_x86.exe", "https://aka.ms/highdpimfc2013x86enu", "/install", "/quiet", "/norestart");
            if (Environment.Is64BitOperatingSystem) Add("2013", "x64", "vc2013_x64.exe", "https://aka.ms/highdpimfc2013x64enu", "/install", "/quiet", "/norestart");
            return definitions;
        }

        private static int ReadWindowsBuild()
        {
            try
            {
                using Microsoft.Win32.RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string value = Convert.ToString(
                    key?.GetValue("CurrentBuildNumber"),
                    CultureInfo.InvariantCulture) ?? string.Empty;
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int build)
                    ? build
                    : Environment.OSVersion.Version.Build;
            }
            catch
            {
                return Environment.OSVersion.Version.Build;
            }
        }

        private static void Report(
            IProgress<MaintenanceProgressUpdate>? progress,
            int index,
            int count,
            string name,
            string detail)
        {
            progress?.Report(new MaintenanceProgressUpdate(index, count, name, detail));
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            public uint StructSize;
            public IntPtr FilePath;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            public uint StructSize;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UiContext;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true)]
        private static extern int WinVerifyTrust(
            IntPtr windowHandle,
            ref Guid actionId,
            ref WinTrustData trustData);
    }
}
