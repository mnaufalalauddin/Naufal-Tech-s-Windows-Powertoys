using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record OfflineImageCommandResult(int ExitCode, string Output);
internal interface IOfflineImageCommands
{
    Task<OfflineImageCommandResult> RunAsync(IReadOnlyList<string> arguments, Action<int, long>? started, IProgress<string>? progress);
}

internal sealed class OfflineImageCommands : IOfflineImageCommands
{
    public async Task<OfflineImageCommandResult> RunAsync(IReadOnlyList<string> arguments, Action<int, long>? started, IProgress<string>? progress)
    {
        if (arguments.Any(a => a.Equals("/Online", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Online servicing is prohibited in the offline workflow.");
        ProcessStartInfo info = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"))
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("/English"); info.ArgumentList.Add("/NoRestart"); foreach (string argument in arguments) info.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = info };
        if (!process.Start()) throw new InvalidOperationException("DISM did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(), error = process.StandardError.ReadToEndAsync();
        Exception? recordError = null;
        try { started?.Invoke(process.Id, process.StartTime.ToUniversalTime().Ticks); } catch (Exception ex) { recordError = ex; }
        // Servicing is never killed on a timeout or a closed dialog. The UI holds the servicing lease until exit.
        // A crash leaves a pre-operation manifest and DISM logs, so recovery must reconcile mounts first.
        Task exit = process.WaitForExitAsync(); int elapsedMinutes = 0;
        while (await Task.WhenAny(exit, Task.Delay(TimeSpan.FromMinutes(1))) != exit)
        {
            elapsedMinutes++;
            try { progress?.Report($"DISM is still running ({elapsedMinutes} minute(s)). Do not close Windows or delete the workspace. No timeout kill will be performed."); } catch { }
        }
        await exit; string text = (await output) + Environment.NewLine + (await error);
        if (recordError is not null) throw new IOException("DISM exited, but recording its process identity failed. Inspect the owned mount and logs before recovery.", recordError);
        return new(process.ExitCode, text.Trim());
    }
}

internal sealed class OfflineImageService
{
    private readonly IOfflineImageCommands _commands;
    private bool _running;
    public OfflineImageService(IOfflineImageCommands? commands = null) => _commands = commands ?? new OfflineImageCommands();
    public bool Running => _running;

    public async Task<string> InspectAsync(string source, IProgress<string>? progress = null)
    {
        source = OfflineImagePolicy.LocalPath(source); OfflineImagePolicy.NoReparseAncestors(source);
        if (!File.Exists(source) || !Path.GetExtension(source).Equals(".wim", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select an existing local .wim file.");
        return await Run(null, new[] { "/Get-ImageInfo", "/ImageFile:" + source }, progress);
    }

    public async Task<OfflineImageSession> CloneAsync(string source, string workspace, int index, IProgress<string>? progress = null)
    {
        source = OfflineImagePolicy.LocalPath(source); workspace = OfflineImagePolicy.LocalPath(workspace);
        OfflineImagePolicy.SafeWorkspace(workspace); OfflineImagePolicy.NoReparseAncestors(source);
        if (!Directory.Exists(workspace)) throw new InvalidOperationException("Choose an existing workspace folder on a local disk with sufficient free space.");
        string metadata = await InspectAsync(source, progress);
        if (!OfflineImagePolicy.Records(metadata, "Index").Any(r => int.TryParse(r["Index"], out int found) && found == index)) throw new InvalidOperationException("This index was not reported by DISM. Inspect the WIM and choose an existing index.");
        string identity = OfflineImagePolicy.ImageIdentity(await Run(null, new[] { "/Get-ImageInfo", "/ImageFile:" + source, "/Index:" + index }, progress), index);
        // Conservative floor, not a promise of enough room for all selected images.
        // Mount expansion and later export can require substantially more.
        long required = checked(new FileInfo(source).Length * 2 + 10L * 1024 * 1024 * 1024);
        if (new DriveInfo(Path.GetPathRoot(workspace)!).AvailableFreeSpace < required)
            throw new InvalidOperationException("Workspace needs at least twice the WIM size plus 10 GiB free. Larger images may need more for mounting, servicing and export.");
        string directory = Path.Combine(workspace, "NWU-Offline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        OfflineImageSession session = new(directory, source, index) { State = "Cloning", ImageIdentity = identity }; session.Save();
        try
        {
            progress?.Report("Copying the source into a new workspace. The original is opened read-only and is never serviced.");
            using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, true))
            {
                using (FileStream copy = new(session.Clone, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                { await input.CopyToAsync(copy); await copy.FlushAsync(); copy.Flush(true); }
                input.Position = 0; session.SourceSha256 = Convert.ToHexString(await SHA256.HashDataAsync(input));
            }
            using FileStream cloned = File.OpenRead(session.Clone);
            string cloneHash = Convert.ToHexString(await SHA256.HashDataAsync(cloned));
            if (cloneHash != session.SourceSha256) throw new IOException("Clone checksum mismatch; mounting is blocked.");
            session.State = "Cloned"; session.Save(); return session;
        }
        catch (Exception ex)
        {
            session.State = "CloneFailed";
            try { session.Save(); } catch { }
            throw new IOException("Clone was not verified. Retain the session for inspection: " + session.Manifest, ex);
        }
    }

    public async Task MountAsync(OfflineImageSession session, IProgress<string>? progress = null)
    {
        session.Validate(); EnsureIdle(session);
        if (session.State != "Cloned") throw new InvalidOperationException("Only a newly verified clone may be mounted. Recover an existing session instead.");
        using (FileStream copy = File.OpenRead(session.Clone))
            if (Convert.ToHexString(await SHA256.HashDataAsync(copy)) != session.SourceSha256) throw new InvalidOperationException("Clone changed after verification; create a fresh workspace.");
        string identity = OfflineImagePolicy.ImageIdentity(await Run(session, new[] { "/Get-ImageInfo", "/ImageFile:" + session.Clone, "/Index:" + session.Index }, progress), session.Index);
        if (string.IsNullOrEmpty(session.ImageIdentity) || identity != session.ImageIdentity)
            throw new InvalidOperationException("Clone build/edition/architecture no longer matches the source metadata. Create a fresh session.");
        await EnsureNotMounted(session, progress);
        Directory.CreateDirectory(session.Mount);
        if (Directory.EnumerateFileSystemEntries(session.Mount).Any()) throw new InvalidOperationException("The owned mount directory is not empty.");
        session.State = "Mounting"; session.Save();
        await Run(session, new[] { "/Mount-Image", "/ImageFile:" + session.Clone, "/Index:" + session.Index, "/MountDir:" + session.Mount, "/CheckIntegrity" }, progress);
        await AssertOwnedMount(session, progress); session.State = "Mounted"; session.Save();
    }

    public async Task<IReadOnlyList<OfflineImageItem>> InventoryAsync(OfflineImageSession session, IProgress<string>? progress = null)
    {
        await AssertOwnedMount(session, progress);
        string features = await Run(session, new[] { "/Image:" + session.Mount, "/Get-Features", "/Format:List" }, progress);
        string capabilities = await Run(session, new[] { "/Image:" + session.Mount, "/Get-Capabilities" }, progress);
        string apps = await Run(session, new[] { "/Image:" + session.Mount, "/Get-ProvisionedAppxPackages" }, progress);
        return OfflineImagePolicy.Items(features, "Feature", "Feature Name")
            .Concat(OfflineImagePolicy.Items(capabilities, "Capability", "Capability Identity"))
            .Concat(
                OfflineImagePolicy.Records(apps, "DisplayName").Where(r => r.ContainsKey("PackageName"))
                .Select(r => new OfflineImageItem("Provisioned app", r["PackageName"], "Provisioned")))
            .OrderBy(i => i.Kind).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<string> RemoveAsync(OfflineImageSession session, IReadOnlyList<OfflineImageItem> selected, IProgress<string>? progress = null)
    {
        if (selected.Count == 0) throw new InvalidOperationException("Select at least one supported item.");
        if (session.VerificationFailed) throw new InvalidOperationException("A previous operation was not verified. Discard this working copy or inspect the logs; further removals are blocked.");
        IReadOnlyList<OfflineImageItem> current = await InventoryAsync(session, progress);
        OfflineImageItem[] plan = selected.Distinct().ToArray();
        foreach (OfflineImageItem item in plan)
            if (!OfflineImagePolicy.CanRemove(item) || !current.Contains(item)) throw new InvalidOperationException("Selection is unsupported or stale: " + item.Name);
        StringBuilder report = new();
        foreach (OfflineImageItem item in plan)
        {
            await AssertOwnedMount(session, progress);
            session.RecordChange($"{DateTimeOffset.UtcNow:O} Planned removal: {item.Kind} / {item.Name}; before={item.State}");
            session.State = "Servicing"; session.VerificationFailed = true; session.Save();
            string[] command = item.Kind switch
            {
                "Feature" => new[] { "/Image:" + session.Mount, "/Disable-Feature", "/FeatureName:" + item.Name, "/Remove" },
                "Capability" => new[] { "/Image:" + session.Mount, "/Remove-Capability", "/CapabilityName:" + item.Name },
                _ => new[] { "/Image:" + session.Mount, "/Remove-ProvisionedAppxPackage", "/PackageName:" + item.Name }
            };
            await Run(session, command, progress);
            IReadOnlyList<OfflineImageItem> after = await InventoryAsync(session, progress);
            OfflineImageItem? observed = after.FirstOrDefault(i => i.Kind == item.Kind && i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            bool verified = item.Kind switch
            {
                "Feature" => observed is not null && (observed.State.Equals("Disabled with Payload Removed", StringComparison.OrdinalIgnoreCase) || observed.State.Equals("Disabled", StringComparison.OrdinalIgnoreCase)),
                "Capability" => observed is not null && observed.State.Equals("Not Present", StringComparison.OrdinalIgnoreCase),
                _ => observed is null
            };
            if (!verified) throw new InvalidOperationException("Removal could not be verified; commit is blocked. Item: " + item.Name + ". Observed: " + (observed?.State ?? "Not reported"));
            session.RecordChange($"{DateTimeOffset.UtcNow:O} Verified removal: {item.Kind} / {item.Name}; after={observed?.State ?? "Absent from successful inventory"}");
            session.VerificationFailed = false; session.State = "Modified"; session.Save();
            report.AppendLine(item.Name + ": configuration verified in the clone. Boot/application compatibility has not been tested.");
        }
        return report.ToString();
    }

    public async Task<string> AnalyzeStoreAsync(OfflineImageSession session, IProgress<string>? progress = null)
    { await AssertOwnedMount(session, progress); return await Run(session, new[] { "/Image:" + session.Mount, "/Cleanup-Image", "/AnalyzeComponentStore" }, progress); }

    public async Task CleanupAsync(OfflineImageSession session, IProgress<string>? progress = null)
    {
        await AssertOwnedMount(session, progress);
        if (session.VerificationFailed) throw new InvalidOperationException("Previous servicing is unverified. Discard or recover first.");
        await AnalyzeStoreAsync(session, progress);
        session.RecordChange($"{DateTimeOffset.UtcNow:O} Planned component cleanup; no ResetBase; original source retained.");
        session.VerificationFailed = true; session.State = "Servicing"; session.Save();
        await Run(session, new[] { "/Image:" + session.Mount, "/Cleanup-Image", "/StartComponentCleanup" }, progress);
        // No /ResetBase and no direct component-store deletion. Keep before/after analysis in the command logs.
        await AnalyzeStoreAsync(session, progress);
        session.RecordChange($"{DateTimeOffset.UtcNow:O} Cleanup command and analysis completed; no byte-savings or boot-compatibility claim.");
        session.VerificationFailed = false; session.State = "Modified"; session.Save();
    }

    public async Task FinishAsync(OfflineImageSession session, bool commit, IProgress<string>? progress = null)
    {
        await AssertOwnedMount(session, progress);
        if (commit && session.VerificationFailed) throw new InvalidOperationException("Cannot commit unverified changes. Use Discard; the source WIM remains intact.");
        session.State = commit ? "Committing" : "Discarding"; session.Save();
        await Run(session, new[] { "/Unmount-Image", "/MountDir:" + session.Mount, commit ? "/Commit" : "/Discard", "/CheckIntegrity" }, progress);
        await EnsureNotMounted(session, progress); session.State = commit ? "Committed" : "Discarded"; session.Save();
    }

    public async Task<string> ExportAsync(OfflineImageSession session, IProgress<string>? progress = null)
    {
        session.Validate(); EnsureIdle(session);
        if (session.State != "Committed") throw new InvalidOperationException("Commit and unmount successfully before exporting.");
        await EnsureNotMounted(session, progress);
        string output = Path.Combine(session.DirectoryPath, "optimized.wim");
        if (File.Exists(output)) throw new InvalidOperationException("An export already exists; it will not be overwritten.");
        await Run(session, new[] { "/Export-Image", "/SourceImageFile:" + session.Clone, "/SourceIndex:" + session.Index, "/DestinationImageFile:" + output, "/Compress:max", "/CheckIntegrity" }, progress);
        string info = await Run(session, new[] { "/Get-ImageInfo", "/ImageFile:" + output }, progress);
        if (OfflineImagePolicy.Records(info, "Index").Count != 1) throw new InvalidOperationException("Export verification did not report exactly one image. Retain logs and do not deploy the output.");
        session.State = "Exported"; session.Save();
        return output + "\nOne selected index exported as index 1. Size: " + new FileInfo(output).Length.ToString("N0") + " bytes. WIM metadata verified; boot and deployment are not tested.";
    }

    public async Task<string> RecoverAsync(OfflineImageSession session, IProgress<string>? progress = null)
    {
        session.Validate(); EnsureIdle(session);
        string prior = session.State;
        var mounts = await MountRecords(session, progress);
        var matching = mounts.Where(r => r.TryGetValue("Mount Dir", out string? dir) && OfflineImagePolicy.Same(dir, session.Mount)).ToArray();
        if (matching.Length == 0)
        {
            if (prior is not "Cloned" and not "Committed" and not "Exported" and not "Discarded") { session.State = "RecoveryRequired"; session.VerificationFailed = true; session.Save(); }
            return "No registered mount at the owned path. Recorded state: " + session.State + ". No success was inferred and no files were deleted.";
        }
        ValidateMount(session, matching.Single());
        if (!matching[0].GetValueOrDefault("Status", "").Equals("Ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DISM reports a non-OK mount. Automatic remount/cleanup is intentionally blocked; use Microsoft DISM recovery guidance for this exact path and retain the manifest.");
        if (prior is not "Mounted" and not "Modified") session.VerificationFailed = true;
        session.State = session.VerificationFailed ? "RecoveryRequired" : prior; session.Save();
        return "Owned mounted clone confirmed. " + (session.VerificationFailed ? "Interrupted/unknown changes: only Discard is permitted." : "Inventory or explicit Commit/Discard may continue.");
    }

    private static void EnsureIdle(OfflineImageSession session)
    {
        if (session.ProcessId == 0) return;
        try
        {
            using Process existing = Process.GetProcessById(session.ProcessId);
            if (existing.StartTime.ToUniversalTime().Ticks == session.ProcessStartTicks && !existing.HasExited)
                throw new InvalidOperationException("The session's recorded DISM process is still running. Wait for it; do not run recovery concurrently.");
        }
        catch (ArgumentException) { }
    }

    private async Task<IReadOnlyList<Dictionary<string, string>>> MountRecords(OfflineImageSession session, IProgress<string>? progress)
    {
        string text = await Run(session, new[] { "/Get-MountedImageInfo" }, progress);
        if (!text.Contains("The operation completed successfully.", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Mounted-image inventory was not confirmed; refusing to infer an empty inventory.");
        return OfflineImagePolicy.Records(text, "Mount Dir");
    }

    private static void ValidateMount(OfflineImageSession session, Dictionary<string, string> record)
    {
        if (!record.TryGetValue("Image File", out string? file) || !OfflineImagePolicy.Same(file, session.Clone) ||
            !record.TryGetValue("Image Index", out string? index) || !int.TryParse(index, out int parsed) || parsed != session.Index ||
            !record.GetValueOrDefault("Mounted Read/Write", "").Equals("Yes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The registered mount does not match the owned clone/index/read-write mode. No servicing operation was started.");
    }

    private async Task AssertOwnedMount(OfflineImageSession session, IProgress<string>? progress)
    {
        session.Validate(); EnsureIdle(session);
        var matches = (await MountRecords(session, progress)).Where(r => r.TryGetValue("Mount Dir", out string? dir) && OfflineImagePolicy.Same(dir, session.Mount)).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Exactly one owned mount must be registered before servicing.");
        ValidateMount(session, matches[0]);
        if (!matches[0].GetValueOrDefault("Status", "").Equals("Ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Mount status is not OK. Recover or discard through DISM before continuing.");
    }

    private async Task EnsureNotMounted(OfflineImageSession session, IProgress<string>? progress)
    {
        var mounts = await MountRecords(session, progress);
        if (mounts.Any(r => (r.TryGetValue("Mount Dir", out string? dir) && OfflineImagePolicy.Same(dir, session.Mount)) || (r.TryGetValue("Image File", out string? image) && OfflineImagePolicy.Same(image, session.Clone))))
            throw new InvalidOperationException("The clone or mount path is still registered with DISM. No unmounted state was assumed.");
    }

    private async Task<string> Run(OfflineImageSession? session, string[] arguments, IProgress<string>? progress)
    {
        if (_running) throw new InvalidOperationException("Another offline command is still running.");
        if (session is not null) { session.Validate(); EnsureIdle(session); }
        _running = true;
        try
        {
            string? log = session is null ? null : Path.Combine(session.DirectoryPath, "dism-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".log");
            string[] all = log is null ? arguments : arguments.Append("/LogPath:" + log).ToArray();
            if (session is not null) { session.LastOperation = string.Join(" ", arguments); session.ProcessId = 0; session.ProcessStartTicks = 0; session.Save(); }
            progress?.Report("Running: " + string.Join(" ", arguments));
            OfflineImageCommandResult result = await _commands.RunAsync(all, session is null ? null : (id, ticks) => { session.ProcessId = id; session.ProcessStartTicks = ticks; session.Save(); }, progress);
            if (session is not null)
            {
                session.ProcessId = 0; session.ProcessStartTicks = 0; session.Save();
                await File.WriteAllTextAsync(log + ".output.txt", result.Output);
            }
            if (result.ExitCode is not 0 and not 3010) throw new InvalidOperationException("DISM exit " + result.ExitCode + ": " + result.Output);
            return result.Output;
        }
        finally { _running = false; }
    }
}
