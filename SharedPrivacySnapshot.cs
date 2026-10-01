using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

// Audited aliases only. Other overlapping actions retain independent-owner rejection.
internal sealed record SharedPrivacyTarget(string Id, string Path, string Name)
{
    internal string EssentialTag => "Telemetry." + Hash("CurrentUser|" + Path + "|" + Name);
    internal string DebloatTag => Hash(Path + "|" + Name);
    private static string Hash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text) hash = unchecked((hash ^ c) * 16777619);
        return hash.ToString("X8", CultureInfo.InvariantCulture);
    }
}

internal sealed record SharedRegistryOriginal(bool Exists, RegistryValueKind Kind, string Value)
{
    internal static SharedRegistryOriginal ReadCanonical(Func<string, object?> read, SharedPrivacyTarget target)
    {
        if (read("Path") as string != target.Path || read("Name") as string != target.Name)
            throw new InvalidDataException("Shared snapshot target identity is incomplete or mismatched. Backup retained.");
        // An existing key with only identity fields can be left by an interrupted
        // first commit. Never recapture the (possibly already modified) live value.
        return Read(read, "Original") ?? throw new InvalidDataException(
            "The canonical original was not committed. No state was guessed; backup retained.");
    }

    internal static SharedRegistryOriginal? Read(Func<string, object?> read, string tag)
    {
        if (!RegistrySnapshotCommit.IsCaptured(read, tag))
        {
            if (new[] { ".Exists", ".Kind", ".Value" }.Any(s => read(tag + s) is not null))
                throw new InvalidDataException("An unfinished shared snapshot was found. Backup retained; no system values changed.");
            return null;
        }
        if ((int)read(tag + ".Exists")! == 0) return new(false, RegistryValueKind.String, "");
        var kind = Enum.Parse<RegistryValueKind>((string)read(tag + ".Kind")!);
        string value = (string)read(tag + ".Value")!;
        Validate(kind, value);
        return new(true, kind, value);
    }

    internal static void Validate(RegistryValueKind kind, string value)
    {
        // The legacy codec cannot losslessly represent every MULTI_SZ/REG_NONE value.
        switch (kind)
        {
            case RegistryValueKind.DWord: _ = int.Parse(value, CultureInfo.InvariantCulture); break;
            case RegistryValueKind.QWord: _ = long.Parse(value, CultureInfo.InvariantCulture); break;
            case RegistryValueKind.Binary: _ = Convert.FromBase64String(value); break;
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString: break;
            default: throw new InvalidDataException("Unsupported shared snapshot value kind. Original retained.");
        }
    }

    internal static SharedRegistryOriginal? Resolve(IEnumerable<SharedRegistryOriginal?> candidates,
        Func<SharedRegistryOriginal>? capture)
    {
        var originals = candidates.Where(x => x is not null).Distinct().ToArray();
        if (originals.Length > 1)
            throw new InvalidDataException("Shared registry snapshots disagree. No original state was guessed; all backups were retained.");
        return originals.Length == 1 ? originals[0] : capture?.Invoke();
    }

    internal void Write(RegistryKey key, string tag) =>
        RegistrySnapshotCommit.Write(key.SetValue, key.Flush, tag, Exists, Kind, Value);
}

internal enum SharedSnapshotPreparation { Capture, Restore, Validate }

internal static class SharedPrivacySnapshot
{
    internal const string ProductionRoot = @"Software\Naufal Windows Tech\Powertoys\Backups";
    private static readonly SharedPrivacySnapshotStore Store = new();
    internal static readonly IReadOnlyList<SharedPrivacyTarget> Targets = Array.AsReadOnly(new[]
    {
        new SharedPrivacyTarget("AdvertisingId", @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled"),
        new SharedPrivacyTarget("TailoredExperiences", @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled")
    });

    internal static CatalogEffect Describe(string owner, CatalogEffect effect)
    {
        var target = Targets.FirstOrDefault(t => (owner == "Telemetry" || owner == t.Id) &&
            CatalogEffect.Registry("HKCU", t.Path, t.Name, "DWord", 0).Target == effect.Target);
        return target is null ? effect : effect with { SharedOriginalOwner = "SharedPrivacy/" + target.Id };
    }

    internal static void Prepare(string owner, SharedSnapshotPreparation mode) => Store.Prepare(owner, mode);
    internal static void RetireUnused(string owner) => Store.RetireUnused(owner);
}

// Same capture/restore ownership implementation, with immutable per-instance storage.
// The shipping application can only construct the production store. The test-only
// factory cannot accept an arbitrary registry path or redirect another store.
internal sealed class SharedPrivacySnapshotStore
{
    private readonly string Root;
    private static readonly object Sync = new();
    internal SharedPrivacySnapshotStore() => Root = SharedPrivacySnapshot.ProductionRoot;
#if PROFILE_LIVE_AUDIT
    private SharedPrivacySnapshotStore(string root) => Root = root;
    internal string AuditBackupRoot => Root;
    internal static SharedPrivacySnapshotStore CreateIsolatedAudit(Guid runId)
    {
        if (runId == Guid.Empty) throw new ArgumentException("A unique nonempty audit ID is required.", nameof(runId));
        return new(@"Software\Naufal Windows Tech\Powertoys\VmValidation\Snapshots\" + runId.ToString("N"));
    }
#endif

    internal void Prepare(string owner, SharedSnapshotPreparation mode)
    {
        var targets = SharedPrivacySnapshot.Targets.Where(t => owner == "Telemetry" || t.Id == owner).ToArray();
        if (targets.Length == 0) return;
        lock (Sync)
        {
            var plan = new List<(SharedPrivacyTarget Target, SharedRegistryOriginal? Original)>();
            // Validate all legacy owners before persisting even the first mirrored tag.
            foreach (var target in targets)
            {
                using var canonical = Registry.CurrentUser.OpenSubKey(Root + @"\SharedPrivacy\" + target.Id);
                using var essential = Registry.CurrentUser.OpenSubKey(Root + @"\Essential\Telemetry");
                using var debloat = Registry.CurrentUser.OpenSubKey(Root + @"\Debloat\" + target.Id);
                var canonicalOriginal = canonical is null ? null : SharedRegistryOriginal.ReadCanonical(
                    k => canonical.GetValue(k, null, RegistryValueOptions.DoNotExpandEnvironmentNames), target);
                if (debloat?.GetValue("Snapshot.Imported") is int imported && imported == 1 &&
                    debloat.GetValue("Snapshot.Complete") is not 1)
                    throw new InvalidDataException("The legacy privacy snapshot import is incomplete. Backup retained.");
                var resolved = SharedRegistryOriginal.Resolve(new[]
                {
                    canonicalOriginal, Read(essential, target.EssentialTag), Read(debloat, target.DebloatTag)
                }, mode == SharedSnapshotPreparation.Capture ? () => Capture(target) : null);
                plan.Add((target, resolved));
            }
            if (mode == SharedSnapshotPreparation.Validate) return;
            foreach (var (target, original) in plan)
            {
                if (original is null) continue; // Restore never captures an already modified current value.
                using var canonical = Registry.CurrentUser.CreateSubKey(Root + @"\SharedPrivacy\" + target.Id, true);
                if (Read(canonical, "Original") is null)
                {
                    canonical.SetValue("Path", target.Path);
                    canonical.SetValue("Name", target.Name);
                    original.Write(canonical, "Original"); // Durable authoritative original first.
                }
                string ownerPath = owner == "Telemetry" ? @"\Essential\Telemetry" : @"\Debloat\" + owner;
                string tag = owner == "Telemetry" ? target.EssentialTag : target.DebloatTag;
                using var mirror = Registry.CurrentUser.CreateSubKey(Root + ownerPath, true);
                if (Read(mirror, tag) is null) original.Write(mirror, tag);
            }
        }
    }

    internal void RetireUnused(string owner)
    {
        lock (Sync)
        foreach (var target in SharedPrivacySnapshot.Targets.Where(t => owner == "Telemetry" || t.Id == owner))
        {
            using var essential = Registry.CurrentUser.OpenSubKey(Root + @"\Essential\Telemetry");
            using var debloat = Registry.CurrentUser.OpenSubKey(Root + @"\Debloat\" + target.Id);
            // Retain the original while any participating catalog still owns a backup.
            if (essential is not null || debloat is not null) continue;
            using var root = Registry.CurrentUser.OpenSubKey(Root + @"\SharedPrivacy", true);
            root?.DeleteSubKeyTree(target.Id, false);
            root?.Flush();
        }
    }

    private static SharedRegistryOriginal? Read(RegistryKey? key, string tag) =>
        key is null ? null : SharedRegistryOriginal.Read(k => key.GetValue(k, null, RegistryValueOptions.DoNotExpandEnvironmentNames), tag);

    private static SharedRegistryOriginal Capture(SharedPrivacyTarget target)
    {
        using var key = Registry.CurrentUser.OpenSubKey(target.Path);
        object? value = key?.GetValue(target.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return new(false, RegistryValueKind.String, "");
        RegistryValueKind kind = key!.GetValueKind(target.Name);
        string serialized = kind == RegistryValueKind.Binary ? Convert.ToBase64String((byte[])value) :
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        SharedRegistryOriginal.Validate(kind, serialized);
        return new(true, kind, serialized);
    }
}
