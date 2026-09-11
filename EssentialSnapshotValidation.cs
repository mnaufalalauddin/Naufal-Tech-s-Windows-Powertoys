using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record EssentialSavedValue(bool Exists, RegistryValueKind Kind, object? Value)
{
    internal bool Matches(object? actual, RegistryValueKind? actualKind) =>
        !Exists ? actual is null : Kind == actualKind && Equals(Value, actual);
}

// Read legacy formats without changing them. A missing field never means
// "the original value did not exist"; validate the whole snapshot before writes.
internal static class EssentialSnapshotValidation
{
    internal static bool HasSnapshot(Func<string, object?> read)
    {
        object? marker = read("Captured");
        if (marker is null) return false;
        if (marker is not int value || value != 1) throw Invalid();
        return true;
    }

    internal static EssentialSavedValue ReadIcon(Func<string, object?> read)
    {
        if (!HasSnapshot(read)) throw new InvalidDataException("No saved Icon Cache value exists. The snapshot was not changed.");
        if (read("Existed") is not int exists || exists is not (0 or 1)) throw Invalid();
        if (exists == 0) return new(false, RegistryValueKind.String, null);
        if (read("Kind") is not string kindName || !Enum.TryParse(kindName, out RegistryValueKind kind) ||
            read("Value") is not string value) throw Invalid();
        object restored = kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => value,
            RegistryValueKind.DWord when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) => number,
            RegistryValueKind.QWord when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) => number,
            _ => throw Invalid()
        };
        return new(true, kind, restored);
    }

    internal static string SerializeIcon(object value, RegistryValueKind kind) => (value, kind) switch
    {
        (string text, RegistryValueKind.String or RegistryValueKind.ExpandString) => text,
        (int number, RegistryValueKind.DWord) => number.ToString(CultureInfo.InvariantCulture),
        (long number, RegistryValueKind.QWord) => number.ToString(CultureInfo.InvariantCulture),
        _ => throw new InvalidDataException("The Icon Cache registry type cannot be captured losslessly. Apply was not started.")
    };

    internal static IReadOnlyDictionary<string, EssentialSavedValue> ReadNtfs(
        Func<string, object?> read, Func<string, RegistryValueKind> kind, IEnumerable<string> names)
    {
        if (!HasSnapshot(read)) throw new InvalidDataException("No saved NTFS/FileSystem values exist. The snapshot was not changed.");
        Dictionary<string, EssentialSavedValue> values = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (read(name + ".Exists") is not int exists || exists is not (0 or 1)) throw Invalid();
            if (exists == 0) values.Add(name, new(false, RegistryValueKind.DWord, null));
            else if (read(name + ".Value") is int value && kind(name + ".Value") == RegistryValueKind.DWord)
                values.Add(name, new(true, RegistryValueKind.DWord, value));
            else throw Invalid();
        }
        return values;
    }

    internal static uint ReadPowerIndex(object? value) => value switch
    {
        long number when number >= 0 && number <= uint.MaxValue => (uint)number,
        int number when number >= 0 => (uint)number,
        _ => throw new InvalidDataException("The saved storage power snapshot is incomplete or invalid. No power value was restored; backup retained.")
    };

    private static InvalidDataException Invalid() =>
        new("The saved Essential snapshot is incomplete or invalid. No setting was changed; backup retained.");
}
