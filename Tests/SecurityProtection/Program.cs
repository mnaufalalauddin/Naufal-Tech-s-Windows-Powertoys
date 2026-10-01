using Naufal_Windows_Tech_s_Powertoys;

int assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
var baseline = new SecurityMitigationSnapshot(22631, true, true, "synthetic-machine", 1, 0, 0, 2,
    [1, 2], [1, 2], [1, 2], false, false, 0, "") { Lsa = new(null, false, 0xfffffffe, "") };
foreach (var invalid in new[] {
    baseline with { Administrator = false }, baseline with { ClientWindows = false },
    baseline with { WindowsBuild = 22620 }, baseline with { MachineIdentity = "" },
    baseline with { Managed = null }, baseline with { Managed = true }, baseline with { Lsa = null },
    baseline with { Lsa = baseline.Lsa with { RunAsPpl = 1 } }, baseline with { Lsa = baseline.Lsa with { RunAsPpl = 9 } },
    baseline with { Lsa = baseline.Lsa with { PolicyPresent = true } }, baseline with { Lsa = baseline.Lsa with { PolicyPresent = null } },
    baseline with { Lsa = baseline.Lsa with { EffectiveProtectionLevel = null } }, baseline with { Lsa = baseline.Lsa with { EffectiveProtectionLevel = 9 } },
    baseline with { Lsa = baseline.Lsa with { ReadError = "Denied" } } })
{
    var fake = new Platform(invalid); var store = new Store();
    Check(LsaProtectionPolicy.Enable(fake, store).Outcome == "Blocked", "Invalid evidence blocked");
    Check(fake.Writes == 0 && store.Saves == 0, "No changes on blocked preflight");
}
foreach (int? original in new int?[] { null, 0 })
{
    var fake = new Platform(baseline with { Lsa = baseline.Lsa with { RunAsPpl = original } }); var store = new Store();
    fake.BeforeWrite = () => Check(store.Value is not null && store.Value.OriginalValue == original && store.Value.Outcome.StartsWith("Prepared:"), "Durable original precedes write");
    var result = LsaProtectionPolicy.Enable(fake, store);
    Check(result.Verified && result.Changed && result.Outcome == "RebootRequired", "Verified configuration is not effective protection");
    Check(fake.Value == 2 && fake.Writes == 1, "Only documented non-firmware enable");
    Check(store.Value?.OriginalValue == original, "Exact value/absence saved");
    var snapshot = store.Value;
    result = LsaProtectionPolicy.Enable(fake, store);
    Check(result.Verified && !result.Changed && fake.Writes == 1 && store.Value == snapshot, "Repeated enable preserves original");
}
var platform = new Platform(baseline); var backup = new Store { FailSaveNumber = 1 };
try { LsaProtectionPolicy.Enable(platform, backup); } catch (IOException) { }
Check(platform.Writes == 0, "Initial persistence failure prevents mutation");
platform = new(baseline); backup = new() { FailSaveNumber = 2 };
Check(LsaProtectionPolicy.Enable(platform, backup).Outcome == "Unknown" && backup.Value?.OriginalValue is null, "Final persistence failure is not success");
platform = new(baseline) { IgnoreWrite = true };
Check(LsaProtectionPolicy.Enable(platform, new Store()).Outcome == "VerificationFailed", "Readback mismatch is not success");
platform = new(baseline) { ThrowWrite = true };
Check(LsaProtectionPolicy.Enable(platform, new Store()).Outcome == "Unknown", "Write exception remains unknown");
platform = new(baseline) { StaleRead = true };
Check(LsaProtectionPolicy.Enable(platform, new Store()).Outcome == "Blocked" && platform.Writes == 0, "Changed configuration needs reconfirmation");
foreach (var invalidBackup in new[] { new SecurityMitigationBackup("foreign", null, "Prepared", DateTimeOffset.UtcNow), new SecurityMitigationBackup("synthetic-machine", 1, "Prepared", DateTimeOffset.UtcNow) })
{
    platform = new(baseline);
    Check(LsaProtectionPolicy.Enable(platform, new Store { Value = invalidBackup }).Outcome == "Blocked" && platform.Writes == 0, "Snapshot identity/value validated");
}
Check(LsaProtectionPolicy.Report(baseline).Contains("Process is not protected"), "Live unprotected state explicit");
Check(LsaProtectionPolicy.Report(baseline with { Lsa = baseline.Lsa with { EffectiveProtectionLevel = 4 } }).Contains("LSA-light protected process"), "Live protected state explicit");
Check(LsaProtectionPolicy.Report(baseline with { Lsa = baseline.Lsa with { EffectiveProtectionLevel = null, RunAsPpl = 2 } }).Contains("Unknown (not inferred from registry)"), "Configured protection never substitutes live evidence");
Check(LsaProtectionPolicy.Report(baseline).Contains("Disable / automatic rollback: unavailable"), "Rollback limitation disclosed");
string fixtureRoot = Path.Combine(Path.GetTempPath(), "nwu-lsa-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);
try
{
    string file = Path.Combine(fixtureRoot, "lsa.json");
    var lsaFile = new SecurityMitigationFileStore(file, "LsaProtection");
    var original = new SecurityMitigationBackup("synthetic-machine", null, "Prepared:EnableLsaProtection", DateTimeOffset.UtcNow);
    lsaFile.Save(original);
    Check(lsaFile.Load() == original, "Durable file retains absent original and metadata");
    bool rejected = false;
    try { new SecurityMitigationFileStore(file).Load(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "LSA file cannot be read as HVCI backup");
    string hvciFilePath = Path.Combine(fixtureRoot, "hvci.json");
    new SecurityMitigationFileStore(hvciFilePath).Save(original with { OriginalValue = 1 });
    rejected = false;
    try { new SecurityMitigationFileStore(hvciFilePath, "LsaProtection").Load(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "HVCI file cannot be read as LSA backup");
    rejected = false;
    try { lsaFile.Save(original with { OriginalValue = 1 }); } catch (InvalidDataException) { rejected = true; }
    Check(rejected && lsaFile.Load() == original, "Invalid LSA snapshot cannot overwrite original");
    Check(Directory.GetFiles(fixtureRoot).Length == 2, "Atomic saves leave no temp files");
}
finally
{
    // Only the two named files created by this test are removed; no recursive deletion.
    File.Delete(Path.Combine(fixtureRoot, "lsa.json"));
    File.Delete(Path.Combine(fixtureRoot, "hvci.json"));
    Directory.Delete(fixtureRoot, recursive: false);
}
Console.WriteLine($"PASS: {assertions} LSA protection assertions; mock configuration and test-owned snapshot files only.");

sealed class Platform(SecurityMitigationSnapshot snapshot) : ILsaProtectionPlatform
{
    public int? Value = snapshot.Lsa?.RunAsPpl;
    public int Writes;
    public bool IgnoreWrite, ThrowWrite, StaleRead;
    public Action? BeforeWrite;
    public SecurityMitigationSnapshot Read() => snapshot with { Lsa = snapshot.Lsa is null ? null : snapshot.Lsa with { RunAsPpl = Value } };
    public int? ReadLsaProtection() => StaleRead ? 2 : Value;
    public void EnableLsaProtectionWithoutFirmwareLock() { BeforeWrite?.Invoke(); Writes++; if (ThrowWrite) throw new IOException("Synthetic write failure"); if (!IgnoreWrite) Value = 2; }
}
sealed class Store : ISecurityMitigationBackupStore
{
    public SecurityMitigationBackup? Value;
    public int Saves, FailSaveNumber;
    public SecurityMitigationBackup? Load() => Value;
    public void Save(SecurityMitigationBackup value) { Saves++; if (Saves == FailSaveNumber) throw new IOException("Synthetic disk failure"); Value = value; }
}
