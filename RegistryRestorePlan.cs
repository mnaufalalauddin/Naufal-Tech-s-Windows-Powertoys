using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record RestoreRegistryTarget(RegistryHive Hive, string Path, string Name);
internal sealed record RestoreRegistryValue(RestoreRegistryTarget Target, object? Value, RegistryValueKind? Kind);

internal static class RegistryRestorePlan
{
    internal static void RequireTags(Func<string, object?> read, IEnumerable<string> tags,
        Func<string, RegistryValueKind, object> decode)
    {
        foreach (string tag in tags)
        {
            if (!RegistrySnapshotCommit.IsCaptured(read, tag))
                throw new InvalidOperationException($"The original snapshot is incomplete: {tag}. Backup retained; defaults were not substituted.");
            if ((int)read(tag + ".Exists")! == 1)
            {
                var kind = Enum.Parse<RegistryValueKind>((string)read(tag + ".Kind")!);
                if (!IsSupported(kind)) throw new InvalidOperationException("Unsupported registry snapshot kind. Backup retained.");
                _ = decode((string)read(tag + ".Value")!, kind);
            }
        }
    }

    internal static void RestoreTagged(Func<string, object?> read, string tag,
        RestoreRegistryTarget target, Func<string, RegistryValueKind, object> decode)
    {
        RequireTags(read, new[] { tag }, decode);
        bool exists = (int)read(tag + ".Exists")! == 1;
        RegistryValueKind? kind = exists ? Enum.Parse<RegistryValueKind>((string)read(tag + ".Kind")!) : null;
        object? value = exists ? decode((string)read(tag + ".Value")!, kind!.Value) : null;
        Execute(new[] { new RestoreRegistryValue(target, value, kind) });
    }

    // Decode and validate the ENTIRE snapshot before requesting any write access.
    internal static IReadOnlyList<RestoreRegistryValue> ReadIndexed(Func<string, object?> read,
        IReadOnlyList<RestoreRegistryTarget> targets, Func<string, RegistryValueKind, object> decode)
    {
        if (read("Snapshot.Captured") is not int captured || captured != 1 ||
            read("Snapshot.Count") is not int count || count != targets.Count || count == 0)
            throw new InvalidOperationException("The original snapshot is incomplete. No defaults were substituted; backup retained.");
        var result = new List<RestoreRegistryValue>(count);
        for (int i = 0; i < count; i++)
        {
            string prefix = $"Item.{i}.";
            var target = targets[i];
            if (read(prefix + "Hive") is not string hive || hive != target.Hive.ToString() ||
                read(prefix + "Path") is not string path || !path.Equals(target.Path, StringComparison.OrdinalIgnoreCase) ||
                read(prefix + "Name") is not string name || !name.Equals(target.Name, StringComparison.OrdinalIgnoreCase) ||
                read(prefix + "Exists") is not int exists || exists is not (0 or 1))
                throw new InvalidOperationException("The original snapshot does not match this catalog's registry targets. Backup retained.");
            if (exists == 0) result.Add(new(target, null, null));
            else
            {
                if (read(prefix + "Kind") is not string kindText ||
                    !Enum.TryParse(kindText, out RegistryValueKind kind) || !IsSupported(kind) ||
                    read(prefix + "Value") is not string value)
                    throw new InvalidOperationException("The original snapshot contains an invalid registry value. Backup retained.");
                result.Add(new(target, decode(value, kind), kind));
            }
        }
        return result;
    }

    internal static bool IsSupported(RegistryValueKind kind) => kind is RegistryValueKind.DWord or
        RegistryValueKind.QWord or RegistryValueKind.String or RegistryValueKind.ExpandString or
        RegistryValueKind.MultiString or RegistryValueKind.Binary or RegistryValueKind.None;

    internal static bool Matches(object? left, object? right) => (left, right) switch
    {
        (byte[] a, byte[] b) => a.SequenceEqual(b),
        (string[] a, string[] b) => a.SequenceEqual(b, StringComparer.Ordinal),
        _ => Equals(left, right)
    };

    internal static void Execute(IReadOnlyList<RestoreRegistryValue> plan,
        Func<RestoreRegistryTarget, (object? Value, RegistryValueKind? Kind)> read,
        Action<RestoreRegistryValue> write)
    {
        foreach (var entry in plan)
        {
            var actual = read(entry.Target); // Access denied must NOT become "absent".
            if (!Matches(actual.Value, entry.Value) || (entry.Value is not null && actual.Kind != entry.Kind)) write(entry);
        }
        // Also recheck earlier entries: another writer may have changed them.
        foreach (var entry in plan)
        {
            var actual = read(entry.Target);
            if (!Matches(actual.Value, entry.Value) || (entry.Value is not null && actual.Kind != entry.Kind))
                throw new InvalidOperationException($"Restore verification failed for {entry.Target.Name}. Backup retained.");
        }
    }

    internal static void Execute(IReadOnlyList<RestoreRegistryValue> plan) => Execute(plan, ReadNative, WriteNative);

    internal static (object? Value, RegistryValueKind? Kind) ReadNative(RestoreRegistryTarget target)
    {
        using var root = RegistryKey.OpenBaseKey(target.Hive, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
        using var key = root.OpenSubKey(target.Path, writable: false);
        object? value = key?.GetValue(target.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return (value, value is null ? null : key!.GetValueKind(target.Name));
    }

    private static void WriteNative(RestoreRegistryValue entry)
    {
        using var root = RegistryKey.OpenBaseKey(entry.Target.Hive, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
        if (entry.Value is null)
        {
            using var key = root.OpenSubKey(entry.Target.Path, writable: true);
            key?.DeleteValue(entry.Target.Name, throwOnMissingValue: false);
        }
        else
        {
            using var key = root.CreateSubKey(entry.Target.Path, writable: true);
            key.SetValue(entry.Target.Name, entry.Value, entry.Kind!.Value);
        }
    }
}
