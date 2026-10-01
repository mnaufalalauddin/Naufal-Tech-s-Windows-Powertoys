using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal enum StorageItemKind { Feature, Capability }
internal sealed record StorageItem(StorageItemKind Kind, string Name, string State)
{
    public override string ToString() => $"{Name} — {State}";
}
internal sealed record StorageCommand(string Executable, IReadOnlyList<string> Arguments, bool Mutation = false);
internal sealed record StorageOperationResult(string Outcome, string Report, string LogPath, bool RestartRequired);
internal interface IStorageCommandRunner { Task<NativeCommandResult> RunAsync(StorageCommand command); }
internal sealed class StorageNativeRunner : IStorageCommandRunner
{
    public Task<NativeCommandResult> RunAsync(StorageCommand command) => new NativeCommandRunner().RunAsync(
        Path.Combine(Environment.SystemDirectory, command.Executable), command.Arguments,
        // Never forcibly terminate live servicing midway through a mutation.
        command.Mutation ? Timeout.InfiniteTimeSpan : TimeSpan.FromMinutes(15));
}

internal sealed class StorageServicing(IStorageCommandRunner runner, string logRoot)
{
    internal static StorageCommand Dism(params string[] args) => new("dism.exe", new[] { "/Online", "/English" }.Concat(args).ToArray());
    internal static StorageCommand InventoryCommand(StorageItemKind kind) => Dism(kind == StorageItemKind.Feature ? "/Get-Features" : "/Get-Capabilities");
    internal static StorageCommand InfoCommand(StorageItem item) => Dism(item.Kind == StorageItemKind.Feature ? "/Get-FeatureInfo" : "/Get-CapabilityInfo",
        (item.Kind == StorageItemKind.Feature ? "/FeatureName:" : "/CapabilityName:") + ValidateName(item.Name));

    internal static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 512 || name.Any(c => !(char.IsAsciiLetterOrDigit(c) || "._~-".Contains(c))))
            throw new ArgumentException("Select an exact feature or capability identity from the inventory.");
        return name;
    }

    internal static IReadOnlyList<StorageItem> ParseInventory(StorageItemKind kind, string output)
    {
        List<StorageItem> items = [];
        string? name = null;
        string nameKey = kind == StorageItemKind.Feature ? "Feature Name" : "Capability Identity";
        foreach (string line in output.Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            string key = line[..colon].Trim(), value = line[(colon + 1)..].Trim();
            if (key.Equals(nameKey, StringComparison.OrdinalIgnoreCase)) name = value;
            else if (key.Equals("State", StringComparison.OrdinalIgnoreCase) && name is not null)
            {
                try { items.Add(new(kind, ValidateName(name), value)); } catch (ArgumentException) { }
                name = null;
            }
        }
        return items.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static string? ReadState(string output) => output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("State", StringComparison.OrdinalIgnoreCase))
        .Select(l => l.Split(':', 2)).Where(p => p.Length == 2 && p[0].Trim().Equals("State", StringComparison.OrdinalIgnoreCase)).Select(p => p[1].Trim()).FirstOrDefault();
    internal static bool IsDesired(StorageItemKind kind, string? state, bool enabled) => string.Equals(state,
        kind == StorageItemKind.Feature ? enabled ? "Enabled" : "Disabled" : enabled ? "Installed" : "Not Present", StringComparison.OrdinalIgnoreCase);

    public async Task<(IReadOnlyList<StorageItem> Items, string Report)> InventoryAsync(StorageItemKind kind)
    {
        NativeCommandResult result = await runner.RunAsync(InventoryCommand(kind));
        return (result.ExitCode == 0 && !result.TimedOut ? ParseInventory(kind, result.CombinedOutput) : Array.Empty<StorageItem>(), Describe(result));
    }

    public async Task<string> InspectAsync(StorageCommand command) => Describe(await runner.RunAsync(command));

    public async Task<StorageOperationResult> ChangeAsync(StorageItem item, bool enabled)
    {
        NativeCommandResult before = await runner.RunAsync(InfoCommand(item));
        string? state = ReadState(before.StandardOutput);
        string header = $"Storage servicing: {item.Kind} {item.Name}\nRequested: {(enabled ? "Enable / install" : "Disable / remove")}\nBefore:\n{Describe(before)}\n";
        string log = StartLog(header);
        if (before.ExitCode != 0 || before.TimedOut || string.IsNullOrWhiteSpace(state))
            return Finish(log, "Unknown", header + "No mutation: current state could not be verified.", false);
        if (IsDesired(item.Kind, state, enabled)) return Finish(log, "AlreadyApplied", header + "No change needed.", false);
        if (state.Contains("Pending", StringComparison.OrdinalIgnoreCase)) return Finish(log, "RebootRequired", header + "No mutation: finish pending Windows servicing and reboot first.", true);
        if (!string.Equals(state, item.State, StringComparison.OrdinalIgnoreCase))
            return Finish(log, "Blocked", header + "No mutation: inventory changed after selection. Reload and review the new state.", false);
        bool known = item.Kind == StorageItemKind.Feature
            ? state is "Enabled" or "Disabled" or "Disabled with Payload Removed"
            : state is "Installed" or "Not Present";
        if (!known) return Finish(log, "Unknown", header + "No mutation: this servicing state is not supported.", false);
        string operation = item.Kind == StorageItemKind.Feature ? enabled ? "/Enable-Feature" : "/Disable-Feature" : enabled ? "/Add-Capability" : "/Remove-Capability";
        StorageCommand command = Dism(operation, (item.Kind == StorageItemKind.Feature ? "/FeatureName:" : "/CapabilityName:") + ValidateName(item.Name), "/NoRestart") with { Mutation = true };
        // DISM owns dependency checks. Do not silently add /All, /Remove, /Force or policy bypasses.
        return await ExecuteLoggedAsync(log, header, command, async result =>
        {
            NativeCommandResult after = await runner.RunAsync(InfoCommand(item));
            string? afterState = ReadState(after.StandardOutput);
            bool pending = result.ExitCode == 3010 || afterState?.Contains("Pending", StringComparison.OrdinalIgnoreCase) == true;
            string outcome = result.ExitCode is not (0 or 3010) || result.TimedOut ? "Failed" : pending ? "RebootRequired" :
                after.ExitCode == 0 && !after.TimedOut && IsDesired(item.Kind, afterState, enabled) ? "Verified" : "VerificationPending";
            return (outcome, "After:\n" + Describe(after) + "\nRecovery: use the opposite action only after reviewing the saved before-state. Installation may require Windows Update or matching source media; this log is not a payload backup.", pending);
        });
    }

    public async Task<StorageOperationResult> CleanupAsync(bool resetBase, string irreversibleConsent)
    {
        if (resetBase && irreversibleConsent != "RESETBASE") throw new InvalidOperationException("Type RESETBASE to confirm the permanent loss of uninstalling existing updates.");
        NativeCommandResult before = await runner.RunAsync(Dism("/Cleanup-Image", "/AnalyzeComponentStore"));
        string header = $"Component store cleanup; ResetBase={resetBase}\nBefore:\n{Describe(before)}\nRecovery: no application rollback. ResetBase permanently prevents uninstalling existing updates.\n";
        string log = StartLog(header);
        if (before.ExitCode != 0 || before.TimedOut) return Finish(log, "Unknown", header + "No mutation: component-store analysis failed.", false);
        var args = new List<string> { "/Online", "/English", "/Cleanup-Image", "/StartComponentCleanup", "/NoRestart" };
        if (resetBase) args.Add("/ResetBase");
        return await ExecuteLoggedAsync(log, header, new("dism.exe", args, true), async result =>
        {
            NativeCommandResult after = await runner.RunAsync(Dism("/Cleanup-Image", "/AnalyzeComponentStore"));
            string outcome = result.ExitCode is not (0 or 3010) || result.TimedOut ? "Failed" : result.ExitCode == 3010 ? "RebootRequired" : after.ExitCode == 0 && !after.TimedOut ? "CompletedWithAnalysis" : "VerificationPending";
            return (outcome, "After:\n" + Describe(after) + "\nCompletion is the DISM result, not a guarantee of reclaimed bytes. Compare component analysis; Explorer sizes double-count hard links.", result.ExitCode == 3010);
        });
    }

    public async Task<StorageOperationResult> ExportDriversAsync(string parentDirectory)
    {
        string parent = OfflineImagePolicy.LocalPath(parentDirectory);
        OfflineImagePolicy.NoReparseAncestors(parent);
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Choose an existing export parent directory.");
        string target = Path.Combine(parent, "NWU-Driver-Export-" + Guid.NewGuid().ToString("N"));
        string header = "Export third-party driver packages; no driver removal.\nDestination: " + target + "\n";
        string log = StartLog(header);
        Directory.CreateDirectory(target);
        return await ExecuteLoggedAsync(log, header, new("pnputil.exe", new[] { "/export-driver", "*", target }, true), result =>
        {
            int count = Directory.EnumerateFiles(target, "*.inf", new EnumerationOptions
            { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }).Count();
            string outcome = result.ExitCode != 0 || result.TimedOut ? "Failed" : count > 0 ? "Exported" : "NoPackagesExported";
            return Task.FromResult((outcome, $"Exported INF files: {count}. This is not a full system/device configuration backup; preserve this directory separately.", false));
        });
    }

    private async Task<StorageOperationResult> ExecuteLoggedAsync(string log, string header, StorageCommand command,
        Func<NativeCommandResult, Task<(string Outcome, string Detail, bool Restart)>> verify)
    {
        string report = header + "Command: " + command.Executable + " " + string.Join(" ", command.Arguments) + "\n";
        Append(log, report + "Operation started; outcome unknown until completion.\n");
        try
        {
            NativeCommandResult result = await runner.RunAsync(command);
            report += "Command result:\n" + Describe(result) + "\n";
            Append(log, report);
            var check = await verify(result);
            return Finish(log, check.Outcome, report + check.Detail, check.Restart);
        }
        catch (Exception ex)
        {
            // A command may have completed before read-back/logging failed; never label it unchanged.
            try { Append(log, "Interrupted / Unknown: " + ex + "\n"); } catch { }
            throw new InvalidOperationException("Operation outcome is unknown; inspect Windows state before retrying. Log: " + log, ex);
        }
    }
    private string StartLog(string header)
    {
        Directory.CreateDirectory(logRoot);
        string path = Path.Combine(logRoot, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        byte[] text = Encoding.UTF8.GetBytes("NAUFAL WINDOWS UTILITY — STORAGE SERVICING\nUTC: " + DateTimeOffset.UtcNow.ToString("O") + "\nOutcome: Planned\n" + header);
        stream.Write(text); stream.Flush(true);
        return path;
    }
    private static void Append(string path, string text)
    {
        using FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        stream.Write(Encoding.UTF8.GetBytes(text)); stream.Flush(true);
    }
    private static StorageOperationResult Finish(string log, string outcome, string report, bool restart)
    {
        report += "\nOutcome: " + outcome + "\nLog: " + log;
        Append(log, report + "\n");
        return new(outcome, report, log, restart);
    }
    private static string Describe(NativeCommandResult result) => $"Exit: {result.ExitCode}; Timed out: {result.TimedOut}\n{result.CombinedOutput}";
}
