using Microsoft.Win32;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Naufal_Windows_Tech_s_Powertoys;

// Guest-only migration rehearsal: production originals are READ, never altered or consumed.
// Production migration code runs against copies in uniquely owned test registry storage.
internal static class LegacySnapshotLiveAudit
{
    internal static void Run(string[] args)
    {
        Guid guest = GuestAuditIdentity.Require(args);
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Guest Administrator rights required.");
        if (System.Diagnostics.Process.GetProcessesByName("Naufal Windows Utility").Length != 0 ||
            System.Diagnostics.Process.GetProcessesByName("Naufal Windows Powertoys").Length != 0)
            throw new InvalidOperationException("Close the application first.");
        int argument = Array.IndexOf(args, "--report-directory");
        if (argument < 0 || argument + 1 >= args.Length) throw new ArgumentException("Missing report directory.");
        string folder = OfflineImagePolicy.LocalPath(args[argument + 1]);
        OfflineImagePolicy.NoReparseAncestors(folder);
        string local = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) + Path.DirectorySeparatorChar;
        if (!folder.StartsWith(local, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Use this guest account's LocalAppData for evidence.");
        if (Directory.Exists(folder)) throw new InvalidOperationException("Use a new migration report directory.");
        Directory.CreateDirectory(folder);
        string log = Path.Combine(folder, "legacy-migration-result.txt");
        void Log(string line) { Console.WriteLine(line); File.AppendAllText(log, line + Environment.NewLine); }
        Log($"Guest-only legacy migration rehearsal: {guest}; {DateTimeOffset.Now:O}");
        Log("Existing production backups are read-only. Only test-owned snapshot copies are changed. This is not an in-place production migration or Windows-setting Apply test.");
        int migrated = 0, unavailable = 0, blocked = 0;
        foreach (var target in SharedPrivacySnapshot.Targets)
        {
            string essentialPath = SharedPrivacySnapshot.ProductionRoot + @"\Essential\Telemetry";
            string debloatPath = SharedPrivacySnapshot.ProductionRoot + @"\Debloat\" + target.Id;
            var essential = ReadFields(essentialPath, target.EssentialTag);
            var debloat = ReadFields(debloatPath, target.DebloatTag);
            string before = Fingerprint(essential, debloat);
            try
            {
                string result = Rehearse(target, essential, debloat, Log);
                if (result == "MIGRATED") migrated++;
                else if (result == "NOT AVAILABLE") unavailable++;
                else blocked++;
            }
            finally
            {
                if (before != Fingerprint(ReadFields(essentialPath, target.EssentialTag), ReadFields(debloatPath, target.DebloatTag)))
                    throw new InvalidOperationException("Production backup fields changed during audit; stop and investigate concurrent activity.");
                Log(target.Id + ": production source tag values/types unchanged (SHA-256 readback).");
            }
        }
        Log($"ACTUAL LEGACY COPIES: migrated={migrated}; missing={unavailable}; blocked={blocked}. Missing/blocked cases are NOT migration passes.");

        // Native registry-format fixtures include originals unlike the current live settings.
        // Restore preparation must use the saved original, never capture current Windows state.
        var t = SharedPrivacySnapshot.Targets[0];
        foreach (var original in new[] {
            new SharedRegistryOriginal(false, RegistryValueKind.String, ""),
            new SharedRegistryOriginal(true, RegistryValueKind.DWord, "1"),
            new SharedRegistryOriginal(true, RegistryValueKind.QWord, "9223372036854775807"),
            new SharedRegistryOriginal(true, RegistryValueKind.Binary, "AAH/"),
            new SharedRegistryOriginal(true, RegistryValueKind.ExpandString, "%TEMP%\\literal") })
        {
            if (Rehearse(t, Fields(t.EssentialTag, original), new(), Log) != "MIGRATED")
                throw new InvalidOperationException("Native fixture did not migrate exactly.");
        }
        var one = Fields(t.EssentialTag, new(true, RegistryValueKind.DWord, "1"));
        var zero = Fields(t.DebloatTag, new(true, RegistryValueKind.DWord, "0"));
        if (Rehearse(t, one, zero, Log) != "BLOCKED") throw new InvalidOperationException("Conflict was not blocked.");
        var partial = Fields(t.EssentialTag, new(true, RegistryValueKind.DWord, "1"));
        partial.Remove(t.EssentialTag + ".Captured");
        if (Rehearse(t, partial, new(), Log) != "BLOCKED") throw new InvalidOperationException("Interrupted snapshot was not blocked.");
        Log("PASS: 7 native registry-format fixtures; no production backup or Windows setting was intentionally written. Test roots retained for inspection. Full Telemetry/JSON/file-backup migration is outside this test.");
    }

    internal sealed record Field(RegistryValueKind Kind, string Data);
    internal static object Decode(Field field) => field.Kind switch
    {
        RegistryValueKind.DWord => int.Parse(field.Data, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => long.Parse(field.Data, System.Globalization.CultureInfo.InvariantCulture),
        RegistryValueKind.Binary or RegistryValueKind.None => Convert.FromBase64String(field.Data),
        RegistryValueKind.MultiString => JsonSerializer.Deserialize<string[]>(field.Data)!,
        _ => field.Data
    };
    private static Dictionary<string, Field> ReadFields(string path, string tag)
    {
        var fields = new Dictionary<string, Field>(StringComparer.Ordinal);
        using var key = Registry.CurrentUser.OpenSubKey(path, false);
        if (key is null) return fields;
        foreach (string name in new[] { tag + ".Captured", tag + ".Exists", tag + ".Kind", tag + ".Value", "Snapshot.Imported", "Snapshot.Complete" })
        {
            object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is null) continue;
            var kind = key.GetValueKind(name);
            string encoded = value switch
            {
                byte[] bytes => Convert.ToBase64String(bytes),
                string[] strings => JsonSerializer.Serialize(strings),
                _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""
            };
            fields.Add(name, new(kind, encoded));
        }
        return fields;
    }
    private static Dictionary<string, Field> Fields(string tag, SharedRegistryOriginal original)
    {
        var result = new Dictionary<string, Field>(StringComparer.Ordinal)
        {
            [tag + ".Captured"] = new(RegistryValueKind.DWord, "1"),
            [tag + ".Exists"] = new(RegistryValueKind.DWord, original.Exists ? "1" : "0")
        };
        if (original.Exists)
        {
            result[tag + ".Kind"] = new(RegistryValueKind.String, original.Kind.ToString());
            result[tag + ".Value"] = new(RegistryValueKind.String, original.Value);
        }
        return result;
    }
    internal static string Fingerprint(params Dictionary<string, Field>[] fields) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fields.Select(f => f.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray())))));

    private static string Rehearse(SharedPrivacyTarget target, Dictionary<string, Field> essential,
        Dictionary<string, Field> debloat, Action<string> log)
    {
        var store = SharedPrivacySnapshotStore.CreateIsolatedAudit(Guid.NewGuid());
        string root = store.AuditBackupRoot;
        using (var existing = Registry.CurrentUser.OpenSubKey(root))
            if (existing is not null) throw new InvalidOperationException("Test root already exists.");
        void Copy(string path, Dictionary<string, Field> fields)
        {
            if (fields.Count == 0) return;
            using var key = Registry.CurrentUser.CreateSubKey(root + path);
            foreach (var field in fields) key.SetValue(field.Key, Decode(field.Value), field.Value.Kind);
            key.Flush();
        }
        Copy(@"\Essential\Telemetry", essential);
        Copy(@"\Debloat\" + target.Id, debloat);
        SharedRegistryOriginal? expected = null;
        bool invalid = false;
        try
        {
            object? Read(Dictionary<string, Field> fields, string name) => fields.TryGetValue(name, out var field) ? Decode(field) : null;
            if (Read(debloat, "Snapshot.Imported") is 1 && Read(debloat, "Snapshot.Complete") is not 1)
                throw new InvalidDataException("Incomplete imported backup.");
            expected = SharedRegistryOriginal.Resolve(new[] {
                SharedRegistryOriginal.Read(n => Read(essential, n), target.EssentialTag),
                SharedRegistryOriginal.Read(n => Read(debloat, n), target.DebloatTag) }, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException or ArgumentException) { invalid = true; }
        bool rejected = false;
        try { store.Prepare(target.Id, SharedSnapshotPreparation.Restore); }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException or ArgumentException)
        { rejected = true; if (!invalid) throw; }
        if (rejected != invalid) throw new InvalidOperationException("Native migration disagreed with preflight.");
        if (invalid && Fingerprint(essential, debloat) != Fingerprint(
            ReadFields(root + @"\Essential\Telemetry", target.EssentialTag), ReadFields(root + @"\Debloat\" + target.Id, target.DebloatTag)))
            throw new InvalidOperationException("Blocked migration changed test legacy originals.");
        using var canonical = Registry.CurrentUser.OpenSubKey(root + @"\SharedPrivacy\" + target.Id);
        if (invalid || expected is null)
        {
            if (canonical is not null) throw new InvalidOperationException("Blocked/missing original produced a canonical record.");
        }
        else
        {
            if (canonical is null || SharedRegistryOriginal.ReadCanonical(n => canonical.GetValue(n, null, RegistryValueOptions.DoNotExpandEnvironmentNames), target) != expected)
                throw new InvalidOperationException("Migrated canonical value/type/absence differs from legacy original.");
            store.Prepare("Telemetry", SharedSnapshotPreparation.Restore);
            store.Prepare(target.Id, SharedSnapshotPreparation.Restore);
            using var mirror = Registry.CurrentUser.OpenSubKey(root + @"\Essential\Telemetry")!;
            if (SharedRegistryOriginal.Read(n => mirror.GetValue(n, null, RegistryValueOptions.DoNotExpandEnvironmentNames), target.EssentialTag) != expected)
                throw new InvalidOperationException("Cross-owner migration lost original.");
        }
        string result = invalid ? "BLOCKED" : expected is null ? "NOT AVAILABLE" : "MIGRATED";
        log(target.Id + ": " + result + "; isolated evidence HKCU\\" + root);
        return result;
    }
}
