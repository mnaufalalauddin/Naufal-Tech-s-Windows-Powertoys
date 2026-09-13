using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class OneDriveAppService
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe";
    private string? _approvedScope;
    private static string ScopeFile => Path.Combine(AppDataPaths.LocalRoot, "Backups", "BuiltInApps", "OneDrive-scope.txt");

    // Read-only, language-independent inventory. Never enumerate user sync folders,
    // load other users' hives, execute registry command strings, or inspect accounts.
    internal static IReadOnlyList<BuiltInAppPackage> ReadInstalled()
    {
        List<BuiltInAppPackage> result = new();
        foreach (string scope in new[] { "user", "machine" })
        {
            bool registered = false;
            foreach (RegistryView view in Environment.Is64BitOperatingSystem
                ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
            {
                using var hive = RegistryKey.OpenBaseKey(scope == "user" ? RegistryHive.CurrentUser : RegistryHive.LocalMachine, view);
                using var key = hive.OpenSubKey(UninstallKey, writable: false);
                if (key is null) continue;
                if (!string.Equals(key.GetValue("DisplayName") as string, "Microsoft OneDrive", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(key.GetValue("Publisher") as string, "Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The OneDrive uninstall registration has an unexpected identity. Review Installed apps in Windows Settings.");
                registered = true;
            }
            var folders = scope == "user" ? new[] { Environment.SpecialFolder.LocalApplicationData }
                : new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 };
            bool executable = false;
            foreach (var folder in folders)
            {
                string root = Environment.GetFolderPath(folder);
                if (string.IsNullOrWhiteSpace(root)) continue;
                string path = scope == "user" ? Path.Combine(root, "Microsoft", "OneDrive", "OneDrive.exe")
                    : Path.Combine(root, "Microsoft OneDrive", "OneDrive.exe");
                try { executable |= (File.GetAttributes(path) & FileAttributes.Directory) == 0; }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                // Access denied and IO errors are NOT proof of absence.
            }
            if (registered || executable) result.Add(OneDriveAppPolicy.Installed(scope, registered && executable));
        }
        return result;
    }

    internal async Task PrepareAsync(bool restore, IReadOnlyList<BuiltInAppPackage> inventory)
    {
        _approvedScope = null;
        var installed = inventory.Where(p => p.Kind == BuiltInAppKind.OneDriveDesktop).ToArray();
        string? saved = null;
        if (restore && installed.Length == 0)
        {
            try { saved = await File.ReadAllTextAsync(ScopeFile); }
            catch (FileNotFoundException) { saved = null; }
            catch (DirectoryNotFoundException) { saved = null; }
        }
        _approvedScope = OneDriveAppPolicy.ResolveScope(restore, installed.Select(p => p.Scope), saved);
    }

    internal async Task ChangeAsync(bool restore, IProgress<CatalogProgressUpdate>? progress, Func<string, Task> log)
    {
        string scope = _approvedScope ?? throw new InvalidOperationException("OneDrive has no approved operation plan.");
        var current = ReadInstalled();
        if (current.Any(p => p.Scope != scope))
            throw new InvalidOperationException("OneDrive installation scope changed after review. Analyze and confirm again.");
        if (restore && current.Any(p => p.Scope == scope && p.Healthy) || !restore && current.Count == 0) return;

        string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");
        if (!File.Exists(winget))
            throw new InvalidOperationException("WinGet is unavailable for this account. Use the Microsoft website button for OneDrive recovery, or Installed apps in Windows Settings to uninstall it.");
        using var commands = new OneDriveCommandSession(scope);
        var source = await commands.RunAsync(winget,
            ["source", "export", "--name", "winget", "--disable-interactivity"], TimeSpan.FromSeconds(30));
        if (source.TimedOut || source.ExitCode != 0 || !OneDriveAppPolicy.IsOfficialSource(source.StandardOutput))
            throw new InvalidOperationException("The official WinGet source could not be verified. No OneDrive changes were started; sources were not reset or replaced.");

        if (!restore)
        {
            // Persist the observed scope BEFORE uninstall. Repeated uninstall of an
            // absent app never overwrites it. No paths, command lines or account data.
            byte[] bytes = Encoding.UTF8.GetBytes(scope);
            using MemoryStream stream = new(bytes, writable: false);
            await AtomicDownloadFile.SaveAsync(stream, ScopeFile, bytes.Length, 1, 16, null, CancellationToken.None);
        }
        string verb = restore ? "Installing: " : "Removing: ";
        progress?.Report(new(verb + "Microsoft OneDrive", null));
        // Keep the shared deployment gate over both the external client and fresh
        // readback. Never kill an installer and then start the next catalog item.
        await DeploymentOperationTimeout.AwaitExternalAsync(async () =>
        {
            var change = await commands.RunAsync(winget,
                OneDriveAppPolicy.Arguments(restore, scope), Timeout.InfiniteTimeSpan);
            await log("Microsoft OneDrive [" + scope + "]: exit " + change.ExitCode + Environment.NewLine + change.CombinedOutput);
            if (change.TimedOut || change.ExitCode != 0)
                throw new InvalidOperationException("OneDrive did not confirm completion. " + change.CombinedOutput);
            progress?.Report(new("Verifying: Microsoft OneDrive", null));
            for (int attempt = 0; attempt < 30; attempt++)
            {
                var after = ReadInstalled();
                if (after.Any(p => p.Scope != scope))
                    throw new InvalidOperationException("OneDrive verification found a different installation scope.");
                if (restore ? after.Any(p => p.Scope == scope && p.Healthy) : after.Count == 0)
                    return true;
                await Task.Delay(1000);
            }
            throw new InvalidOperationException("OneDrive installation state could not be verified. Check Windows Installed apps and use the Microsoft website button, then analyze again.");
        }, verb + "Microsoft OneDrive");
    }
}
