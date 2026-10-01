using Microsoft.Win32;

internal static class LegacyMigrationAuditTests
{
    internal static void Run(Action<bool, string> check)
    {
        var fields = new Dictionary<string, LegacySnapshotLiveAudit.Field>
        {
            ["T.Captured"] = new(RegistryValueKind.DWord, "1"),
            ["T.Value"] = new(RegistryValueKind.String, "1")
        };
        var reordered = fields.Reverse().ToDictionary(p => p.Key, p => p.Value);
        check(LegacySnapshotLiveAudit.Fingerprint(fields) == LegacySnapshotLiveAudit.Fingerprint(reordered), "Migration source fingerprints ignore enumeration order");
        reordered["T.Value"] = new(RegistryValueKind.DWord, "1");
        check(LegacySnapshotLiveAudit.Fingerprint(fields) != LegacySnapshotLiveAudit.Fingerprint(reordered), "Migration fingerprint preserves registry kinds");
        reordered["T.Value"] = new(RegistryValueKind.String, "0");
        check(LegacySnapshotLiveAudit.Fingerprint(fields) != LegacySnapshotLiveAudit.Fingerprint(reordered), "Migration fingerprint detects changed values");
        check(LegacySnapshotLiveAudit.Fingerprint(fields, new()) != LegacySnapshotLiveAudit.Fingerprint(new(), fields), "Migration fingerprint keeps catalog ownership distinct");
        check(Equals(LegacySnapshotLiveAudit.Decode(new(RegistryValueKind.DWord, "-2147483648")), int.MinValue), "Legacy migration copies signed DWORD bits");
        check(Equals(LegacySnapshotLiveAudit.Decode(new(RegistryValueKind.QWord, "9223372036854775807")), long.MaxValue), "Legacy migration copies QWORD exactly");
        check(Equals(LegacySnapshotLiveAudit.Decode(new(RegistryValueKind.ExpandString, "%TEMP%")), "%TEMP%"), "Legacy copy does not expand environment strings");
        check(((byte[])LegacySnapshotLiveAudit.Decode(new(RegistryValueKind.Binary, "AAH/"))).SequenceEqual(new byte[] { 0, 1, 255 }), "Legacy copy preserves binary payloads");
        check(((string[])LegacySnapshotLiveAudit.Decode(new(RegistryValueKind.MultiString, "[\"a\",\"\",\"b\"]"))).SequenceEqual(new[] { "a", "", "b" }), "Legacy copy retains multi-string boundaries for fail-closed validation");
    }
}
