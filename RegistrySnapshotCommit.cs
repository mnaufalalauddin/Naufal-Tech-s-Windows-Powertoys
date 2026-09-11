using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class RegistrySnapshotCommit
{
    public static bool HasCompleteSet(
        Func<string, object?> read,
        IEnumerable<string> tags,
        string label)
    {
        string[] required = tags.Distinct(StringComparer.Ordinal).ToArray();
        if (required.Length == 0)
        {
            return false;
        }

        int captured = required.Count(tag => IsCaptured(read, tag));
        if (captured == 0)
        {
            return false;
        }
        if (captured != required.Length)
        {
            throw new InvalidDataException(
                $"The saved snapshot for {label} is incomplete ({captured}/{required.Length}). Restore was not started and the backup was retained.");
        }
        return true;
    }

    public static bool IsCaptured(Func<string, object?> read, string tag)
    {
        object? marker = read(tag + ".Captured");
        if (marker is null) return false;
        object? exists = read(tag + ".Exists");
        if (marker is not int captured || captured != 1 ||
            exists is not int present || present is not (0 or 1) ||
            (present == 1 && (read(tag + ".Value") is not string ||
                read(tag + ".Kind") is not string kind ||
                !Enum.TryParse(kind, out RegistryValueKind parsed) ||
                !Enum.IsDefined(parsed))))
            throw new InvalidDataException("The saved registry snapshot is incomplete: " + tag + ". No value was restored or overwritten.");
        return true;
    }

    public static void Write(Action<string, object, RegistryValueKind> write, Action flush,
        string tag, bool exists, RegistryValueKind kind, string serialized)
    {
        write(tag + ".Exists", exists ? 1 : 0, RegistryValueKind.DWord);
        if (exists)
        {
            write(tag + ".Kind", kind.ToString(), RegistryValueKind.String);
            write(tag + ".Value", serialized, RegistryValueKind.String);
        }
        flush();
        write(tag + ".Captured", 1, RegistryValueKind.DWord);
        flush();
    }

    public static void Capture(RegistryKey backup, string tag, RegistryKey? source,
        string name, Func<object, RegistryValueKind, string> serialize)
    {
        if (IsCaptured(key => backup.GetValue(key), tag)) return;
        object? value = source?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        RegistryValueKind kind = value is null ? RegistryValueKind.String : source!.GetValueKind(name);
        string serialized = value is null ? "" : serialize(value, kind);
        Write(backup.SetValue, backup.Flush, tag, value is not null, kind, serialized);
    }

    public static void RequireRestored(Func<string, object?> read, string tag,
        object? actual, RegistryValueKind? actualKind, Func<object, RegistryValueKind, string> serialize)
    {
        if (!IsCaptured(read, tag)) throw new InvalidDataException("No saved snapshot for " + tag + ".");
        bool existed = (int)read(tag + ".Exists")! == 1;
        bool matches = !existed ? actual is null : actual is not null && actualKind.HasValue &&
            string.Equals(actualKind.Value.ToString(), read(tag + ".Kind") as string, StringComparison.Ordinal) &&
            string.Equals(serialize(actual, actualKind.Value), read(tag + ".Value") as string, StringComparison.Ordinal);
        if (!matches) throw new InvalidDataException("Restore read-back did not match the saved value for " + tag + ". Backup retained.");
    }
}
