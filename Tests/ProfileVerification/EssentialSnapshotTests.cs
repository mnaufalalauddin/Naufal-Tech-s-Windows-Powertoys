using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class EssentialSnapshotTests
{
    internal static void Run(Action<bool, string> assert)
    {
        Dictionary<string, object?> icon = new() { ["Captured"] = 1, ["Existed"] = 1, ["Kind"] = "String", ["Value"] = "8192" };
        object? Read(string key) => icon.GetValueOrDefault(key);
        foreach (var (value, kind) in new (object, RegistryValueKind)[] {
            ("8192", RegistryValueKind.String), ("%TEST%", RegistryValueKind.ExpandString),
            (-1, RegistryValueKind.DWord), (long.MaxValue, RegistryValueKind.QWord) })
        {
            icon["Kind"] = kind.ToString();
            icon["Value"] = EssentialSnapshotValidation.SerializeIcon(value, kind);
            var saved = EssentialSnapshotValidation.ReadIcon(Read);
            assert(saved.Matches(value, kind), "Legacy Icon Cache value/type roundtrip: " + kind);
            assert(!saved.Matches("wrong", kind), "Restore mismatch is rejected: " + kind);
            assert(!saved.Matches(value, RegistryValueKind.Binary), "Equal value with wrong type is not restored: " + kind);
        }
        foreach (string key in new[] { "Captured", "Existed", "Kind", "Value" })
        {
            var corrupted = new Dictionary<string, object?>(icon); corrupted.Remove(key);
            bool rejected = false;
            try { EssentialSnapshotValidation.ReadIcon(k => corrupted.GetValueOrDefault(k)); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected, "Missing Icon Cache field cannot delete original setting: " + key);
        }
        foreach (object? marker in new object?[] { "1", 0, -1, 2 })
        {
            bool rejected = false;
            try { EssentialSnapshotValidation.HasSnapshot(_ => marker); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected, "Invalid snapshot marker fails closed");
        }
        icon["Existed"] = 0;
        assert(EssentialSnapshotValidation.ReadIcon(Read).Matches(null, null), "Explicit original absence permits deletion");
        assert(!EssentialSnapshotValidation.ReadIcon(Read).Matches("8192", RegistryValueKind.String), "Deletion requires absent read-back");
        icon["Existed"] = 1; icon["Kind"] = "DWord"; icon["Value"] = "not a number";
        bool badNumber = false;
        try { EssentialSnapshotValidation.ReadIcon(Read); } catch (InvalidDataException) { badNumber = true; }
        assert(badNumber, "Corrupt numeric snapshot fails before any mutation");

        string[] names = { "LastAccess", "ShortNames", "Mft", "Refs" };
        Dictionary<string, object?> ntfs = new() { ["Captured"] = 1 };
        foreach (string name in names) { ntfs[name + ".Exists"] = 1; ntfs[name + ".Value"] = 2; }
        var values = EssentialSnapshotValidation.ReadNtfs(k => ntfs.GetValueOrDefault(k), _ => RegistryValueKind.DWord, names);
        assert(values.Count == 4 && values.Values.All(v => v.Matches(2, RegistryValueKind.DWord)), "All NTFS values verified before write");
        foreach (string name in names)
        foreach (string suffix in new[] { ".Exists", ".Value" })
        {
            var corrupted = new Dictionary<string, object?>(ntfs); corrupted.Remove(name + suffix);
            bool rejected = false;
            try { EssentialSnapshotValidation.ReadNtfs(k => corrupted.GetValueOrDefault(k), _ => RegistryValueKind.DWord, names); }
            catch (InvalidDataException) { rejected = true; }
            assert(rejected, "Incomplete NTFS row is not assumed absent: " + name + suffix);
        }
        bool badKind = false;
        try { EssentialSnapshotValidation.ReadNtfs(k => ntfs.GetValueOrDefault(k), _ => RegistryValueKind.String, names); }
        catch (InvalidDataException) { badKind = true; }
        assert(badKind, "Wrong NTFS registry type rejected");
        foreach (object? invalid in new object?[] { null, -1L, (long)uint.MaxValue + 1, "0", -1 })
        {
            bool rejected = false;
            try { EssentialSnapshotValidation.ReadPowerIndex(invalid); } catch (InvalidDataException) { rejected = true; }
            assert(rejected, "Missing/corrupt power index cannot become zero or a skipped restore");
        }
        assert(EssentialSnapshotValidation.ReadPowerIndex(0L) == 0, "Zero is a valid captured power index");
        assert(EssentialSnapshotValidation.ReadPowerIndex((long)uint.MaxValue) == uint.MaxValue, "Full unsigned power index survives QWord snapshot");
    }
}
