using Naufal_Windows_Tech_s_Powertoys;

int assertions = 0;
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
var baseline = new SecurityMitigationSnapshot(22631, true, true, "synthetic-machine", 1, 0, 0, 2,
    new[] { 1, 2 }, new[] { 1, 2 }, new[] { 1, 2, 7 }, false, false, 0, "");
var unmanaged = new ManagementEvidence(false, false, -1, true, "");
Check(unmanaged.Managed == false, "Legacy Enrollments content alone does not mark a personal device as managed");
Check((unmanaged with { RegistryArtifacts = false }).Managed == false, "Empty registry artifacts do not change current API evidence");
Check((unmanaged with { RegistryArtifacts = null }).Managed == false, "Legacy hints do not substitute registration queries");
Check((unmanaged with { DomainJoined = true }).Managed == true, "Domain join still blocks local protection changes");
Check((unmanaged with { MdmRegistered = true }).Managed == true, "MDM API registration still blocks local protection changes");
Check((unmanaged with { CloudJoinType = 1 }).Managed == true, "Entra device join still blocks local protection changes");
foreach (var unknown in new[] { unmanaged with { DomainJoined = null }, unmanaged with { MdmRegistered = null },
    unmanaged with { CloudJoinType = null }, unmanaged with { CloudJoinType = 0 }, unmanaged with { CloudJoinType = 2 },
    unmanaged with { CloudJoinType = 99 }, unmanaged with { ReadError = "API unavailable" } })
    Check(unknown.Managed is null, "Incomplete or work-account-only evidence remains unresolved, never unmanaged");
Check((unmanaged with { MdmRegistered = true, DomainJoined = null }).Managed == true, "Known registration is not discarded when another probe fails");
Check(ManagementEvidence.DecodeCloudJoin(0, false, 0) == -1, "Successful null join buffer means no reported join");
Check(ManagementEvidence.DecodeCloudJoin(1, false, 0) == -1, "S_FALSE is HRESULT success with no reported join, not an API error");
Check(ManagementEvidence.DecodeCloudJoin(1, true, 0) is null, "Success HRESULT does not make an unknown join enum valid");
Check(ManagementEvidence.DecodeCloudJoin(0, true, 1) == 1, "Device join enum preserved");
Check(ManagementEvidence.DecodeCloudJoin(0, true, 2) == 2, "Workplace registration is distinct from device join");
Check(ManagementEvidence.DecodeCloudJoin(-1, false, 0) is null, "Failed API with null buffer is unknown, not no enrollment");
Check(ManagementEvidence.DecodeCloudJoin(-1, true, 1) is null, "Failure never trusts a returned buffer");
Check(ManagementEvidence.DecodeCloudJoin(0, true, 0) is null, "Unknown join enum is unresolved");
Check(ManagementEvidence.DecodeCloudJoin(0, true, 99) is null, "Unexpected join enum is unresolved");
Check(unmanaged.Describe().Contains("not proof of active management"), "Legacy artifact warning retained in diagnostics");
var screenshot = baseline with { Managed = unmanaged.Managed, Management = unmanaged, HvciEnabled = null,
    HvciLocked = null, VbsLocked = null, VbsStatus = 0, CodeIntegrityPolicy = 2 };
var enableBlock = SecurityMitigationPolicy.ControlBlockReason(screenshot, SecurityMitigationAction.EnableMemoryIntegrity, null);
var disableBlock = SecurityMitigationPolicy.ControlBlockReason(screenshot, SecurityMitigationAction.DisableMemoryIntegrity, null);
Check(!enableBlock.Contains("policy owner's controls") && !enableBlock.Contains("management status is unresolved"), "Personal PC is not incorrectly labeled managed");
Check(enableBlock.Contains("firmware-lock") && enableBlock.Contains("Code Integrity") && enableBlock.Contains("VBS must already"), "All remaining enable blockers visible together");
Check(disableBlock.Contains("firmware-lock") && disableBlock.Contains("Code Integrity") && !disableBlock.Contains("VBS must already"), "Disable does not inherit enable-only prerequisite");
Check(SecurityMitigationPolicy.ControlBlockReason(baseline, SecurityMitigationAction.RestoreMemoryIntegrity, null).Contains("No exact"), "Restore UI rejects absent backup before confirmation");
Check(SecurityMitigationPolicy.ControlBlockReason(baseline with { VbsStatus = 0 }, SecurityMitigationAction.RestoreMemoryIntegrity,
    new("synthetic-machine", 1, "Prepared", DateTimeOffset.UtcNow)).Contains("VBS must already"), "Restore-to-enabled checks enable prerequisites");
Check(SecurityMitigationPolicy.ControlBlockReason(baseline with { VbsStatus = 0 }, SecurityMitigationAction.RestoreMemoryIntegrity,
    new("synthetic-machine", 0, "Prepared", DateTimeOffset.UtcNow)).Length == 0, "Restore-to-disabled does not require running VBS");
Check(SecurityMitigationPolicy.ControlBlockReason(baseline, SecurityMitigationAction.EnableMemoryIntegrity,
    new("foreign", 1, "Prepared", DateTimeOffset.UtcNow)).Contains("another machine"), "UI and backend both reject foreign snapshot");
Check(SecurityMitigationPolicy.Report(screenshot).Contains("Enable HVCI blocked") && SecurityMitigationPolicy.Report(screenshot).Contains("Disable HVCI blocked"), "Export reports direction-specific restrictions");
string appSecurityReport = SecurityMitigationPolicy.Report(screenshot, includeHvciControls: false);
Check(appSecurityReport.Contains("read-only in this utility"), "Current app HVCI report is explicitly read-only");
Check(!appSecurityReport.Contains("Enable HVCI blocked") && !appSecurityReport.Contains("Enable/Disable here"), "Current app does not advertise removed HVCI controls");
Check(SecurityMitigationPolicy.BlockReason(baseline with { Managed = null }, false).Contains("not a confirmed managed-device"), "Unknown management is not called active enrollment");
foreach (var blocked in new[] { baseline with { Administrator = false }, baseline with { ClientWindows = false },
    baseline with { WindowsBuild = 18363 }, baseline with { MachineIdentity = "" }, baseline with { EvidenceError = "denied" },
    baseline with { Managed = true }, baseline with { Managed = null }, baseline with { PolicyPresent = true },
    baseline with { PolicyPresent = null }, baseline with { HvciLocked = 1 }, baseline with { HvciLocked = null },
    baseline with { VbsLocked = 1 }, baseline with { VbsLocked = null }, baseline with { Configured = null },
    baseline with { Running = null }, baseline with { Hardware = null }, baseline with { VbsStatus = null },
    baseline with { CodeIntegrityPolicy = 2 }, baseline with { CodeIntegrityPolicy = null }, baseline with { HvciEnabled = 9 } })
{
    var platform = new SecurityFake(blocked);
    var result = SecurityMitigationPolicy.Execute(SecurityMitigationAction.DisableMemoryIntegrity, platform, new Store());
    Check(!result.Verified && platform.Writes == 0, "Ineligible evidence prevents security write");
}
Check(SecurityMitigationPolicy.BlockReason(baseline with { VbsStatus = 1 }, true).Length > 0, "Enable needs running VBS");
Check(SecurityMitigationPolicy.BlockReason(baseline with { Hardware = new[] { 2 } }, true).Length > 0, "Enable needs virtualization hardware evidence");
var security = new SecurityFake(baseline); var store = new Store();
var changed = SecurityMitigationPolicy.Execute(SecurityMitigationAction.DisableMemoryIntegrity, security, store);
Check(changed.Verified && changed.Changed && changed.Outcome == "RebootRequired", "Readback is configuration only, restart required");
Check(store.Value?.OriginalValue == 1 && security.Writes == 1, "Original retained before mutation");
Check(SecurityMitigationPolicy.Execute(SecurityMitigationAction.DisableMemoryIntegrity, security, store).Outcome == "AlreadyConfigured" && security.Writes == 1, "Already configured does not write");
Check(SecurityMitigationPolicy.Execute(SecurityMitigationAction.RestoreMemoryIntegrity, security, store).Verified && security.Value == 1, "Exact restore uses original");
security = new SecurityFake(baseline with { HvciEnabled = null }); store = new Store();
SecurityMitigationPolicy.Execute(SecurityMitigationAction.EnableMemoryIntegrity, security, store);
Check(store.Value is not null && store.Value.OriginalValue is null, "Missing original value captured as absence");
SecurityMitigationPolicy.Execute(SecurityMitigationAction.RestoreMemoryIntegrity, security, store);
Check(security.Value is null, "Restore removes only the original absent value");
security = new SecurityFake(baseline); store = new Store { FailSave = true };
try { SecurityMitigationPolicy.Execute(SecurityMitigationAction.DisableMemoryIntegrity, security, store); } catch (IOException) { }
Check(security.Writes == 0, "Initial snapshot write failure blocks mutation");
Check(!SecurityMitigationPolicy.Execute(SecurityMitigationAction.RestoreMemoryIntegrity, security, new Store()).Verified && security.Writes == 0, "No invented default when backup absent");
store = new Store { Value = new("other-machine", 1, "Prepared", DateTimeOffset.UtcNow) };
Check(!SecurityMitigationPolicy.Execute(SecurityMitigationAction.DisableMemoryIntegrity, security, store).Verified && security.Writes == 0, "Foreign backup rejected");
security = new SecurityFake(baseline) { IgnoreWrite = true };
Check(SecurityMitigationPolicy.Execute(SecurityMitigationAction.DisableMemoryIntegrity, security, new Store()).Outcome == "VerificationFailed", "Write success is not readback success");
Check(SecurityMitigationPolicy.Report(baseline).Contains("CPU speculative-execution mitigations: Not measured"), "Unknown CPU mitigation status honest");

foreach (string bad in new[] { @"C:\", @"relative\file.wim", @"\\server\share\file.wim", @"C:\file.wim:stream", "C:\\a\nfile.wim" })
{
    bool rejected = false; try { OfflineImagePolicy.LocalPath(bad); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Reject unsafe offline path " + bad);
}
Check(OfflineImagePolicy.CanRemove(new("Feature", "TelnetClient", "Enabled")), "Allowlisted enabled feature selectable");
Check(!OfflineImagePolicy.CanRemove(new("Feature", "TelnetClient", "Disabled")), "Do not remove already-disabled feature");
Check(!OfflineImagePolicy.CanRemove(new("Feature", "NetFx3", "Enabled")), "Dependencies are not arbitrary removals");
Check(!OfflineImagePolicy.CanRemove(new("Provisioned app", "Microsoft.WindowsStore_test", "Provisioned")), "Store is not allowlisted");
Check(OfflineImagePolicy.CanRemove(new("Provisioned app", "Microsoft.BingNews_1.0_x64__test", "Provisioned")), "Known provisioned app selectable");
Check(!OfflineImagePolicy.CanRemove(new("Provisioned app", "Microsoft.BingNewsImposter_1.0_x64__test", "Provisioned")), "Package boundary respected");
Check(OfflineImagePolicy.Items("Feature Name : TelnetClient\nState : Enabled", "Feature", "Feature Name").Single().State == "Enabled", "DISM inventory parser");
string imageInfo = "Index : 2\nArchitecture : x64\nEdition : Professional\nInstallation : Client\nVersion : 10.0.22631";
Check(OfflineImagePolicy.ImageIdentity(imageInfo, 2).Contains("Edition=Professional"), "Detailed image identity includes version, edition and architecture");
foreach (string invalid in new[] { imageInfo.Replace("x64", "unknown"), imageInfo.Replace("Client", "Server"), imageInfo.Replace("10.0.22631", "6.3.9600"), imageInfo.Replace("Index : 2", "Index : 1"), "Index : 2\nName : unknown" })
{
    bool rejected = false; try { OfflineImagePolicy.ImageIdentity(invalid, 2); } catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Unsupported/unknown target metadata rejected");
}

string root = Path.Combine(Path.GetTempPath(), "NWU-module-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string owned = Path.Combine(root, "NWU-Offline-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(owned);
string source = Path.Combine(root, "source.wim"); File.WriteAllText(source, "synthetic WIM fixture — never passed to real DISM");
try
{
    var session = new OfflineImageSession(owned, source, 2) { State = "Mounted", SourceSha256 = "fixture", VerificationFailed = true };
    session.Save(); var loaded = OfflineImageSession.Load(session.Manifest);
    Check(loaded.Index == 2 && loaded.State == "Mounted" && loaded.VerificationFailed, "Session round trip preserves uncertain state");
    Check(Directory.GetFiles(owned, "*.new").Length == 0, "Manifest atomically replaced");
    var fake = new OfflineFake(); var offline = new OfflineImageService(fake);
    bool noSelection = false; try { await offline.RemoveAsync(session, Array.Empty<OfflineImageItem>()); } catch (InvalidOperationException) { noSelection = true; }
    Check(noSelection && fake.Calls.Count == 0, "Empty selection performs no servicing");
    bool uncertain = false; try { await offline.RemoveAsync(session, new[] { new OfflineImageItem("Feature", "TelnetClient", "Enabled") }); } catch (InvalidOperationException) { uncertain = true; }
    Check(uncertain && fake.Calls.Count == 0, "Unverified previous operation blocks further removal");
    string Mount(string file) => $"Mount Dir : {session.Mount}\nImage File : {file}\nImage Index : 2\nMounted Read/Write : Yes\nStatus : Ok\nThe operation completed successfully.";
    fake = new OfflineFake(Mount(source)); offline = new(fake);
    bool wrong = false; try { await offline.FinishAsync(session, false); } catch (InvalidOperationException) { wrong = true; }
    Check(wrong && fake.Calls.Count == 1 && fake.Calls[0].Contains("/Get-MountedImageInfo"), "Foreign source mount never unmounted or modified");
    fake = new OfflineFake(Mount(session.Clone)); offline = new(fake);
    bool commitBlocked = false; try { await offline.FinishAsync(session, true); } catch (InvalidOperationException) { commitBlocked = true; }
    Check(commitBlocked && fake.Calls.Count == 1, "Commit prohibited after unverified operation");
    fake = new OfflineFake(Mount(session.Clone), "The operation completed successfully.", "The operation completed successfully."); offline = new(fake);
    await offline.FinishAsync(session, false);
    Check(session.State == "Discarded" && fake.Calls[1].Contains("/Discard"), "Explicit discard limited to owned mount");
    Check(!fake.Calls.SelectMany(x => x).Any(a => a == "/Online" || a == "/ResetBase"), "Offline commands never service host or ResetBase");
    Check(fake.Calls[1].Contains("/MountDir:" + session.Mount), "Unmount targets exact owned directory");
    session.State = "Servicing"; session.VerificationFailed = true;
    fake = new OfflineFake(Mount(session.Clone)); offline = new(fake);
    string recovery = await offline.RecoverAsync(session);
    Check(session.State == "RecoveryRequired" && recovery.Contains("only Discard"), "Interrupted session not promoted to success");
    session.State = "Committing";
    fake = new OfflineFake("The operation completed successfully."); offline = new(fake);
    await offline.RecoverAsync(session);
    Check(session.State == "RecoveryRequired", "Missing mount after interrupted commit does not infer success");
    session.State = "Mounted";
    fake = new OfflineFake("unexpected output"); offline = new(fake);
    bool inventoryUnknown = false; try { await offline.RecoverAsync(session); } catch (InvalidOperationException) { inventoryUnknown = true; }
    Check(inventoryUnknown, "Unrecognized mount output not treated as empty inventory");
    session.State = "Mounted"; session.VerificationFailed = false;
    fake = new OfflineFake(Mount(session.Clone), "Feature Name : TelnetClient\nState : Enabled", "", "",
        Mount(session.Clone), "The operation completed successfully.", Mount(session.Clone),
        "Feature Name : TelnetClient\nState : Disabled with Payload Removed", "", ""); offline = new(fake);
    string removed = await offline.RemoveAsync(session, new[] { new OfflineImageItem("Feature", "TelnetClient", "Enabled") });
    Check(session.State == "Modified" && !session.VerificationFailed && removed.Contains("configuration verified"), "Offline removal freshly inventories and verifies state after mutation");
    Check(fake.Calls[5].Contains("/Image:" + session.Mount) && fake.Calls[5].Contains("/FeatureName:TelnetClient"), "Offline removal targets owned mount and exact selected identity");
    Check(OfflineImageSession.Load(session.Manifest).Changes.Count == 2, "Durable manifest retains planned and verified per-item changes");
    fake = new OfflineFake(Mount(session.Clone), "Feature Name : TelnetClient\nState : Disabled", "", ""); offline = new(fake);
    bool stale = false; try { await offline.RemoveAsync(session, new[] { new OfflineImageItem("Feature", "TelnetClient", "Enabled") }); } catch (InvalidOperationException) { stale = true; }
    Check(stale && fake.Calls.Count == 4, "Stale offline selection rejected before removal");
    fake = new OfflineFake(Mount(session.Clone), "Feature Name : TelnetClient\nState : Enabled", "", "",
        Mount(session.Clone), "The operation completed successfully.", Mount(session.Clone),
        "Feature Name : TelnetClient\nState : Enabled", "", ""); offline = new(fake);
    bool failedReadback = false; try { await offline.RemoveAsync(session, new[] { new OfflineImageItem("Feature", "TelnetClient", "Enabled") }); } catch (InvalidOperationException) { failedReadback = true; }
    Check(failedReadback && session.VerificationFailed, "Failed poststate blocks commit and additional removals; original retained");
    Check(File.ReadAllText(source).StartsWith("synthetic WIM fixture"), "Original source unchanged in every mocked workflow");
    Check(!Directory.Exists(session.Mount), "No real image was mounted");
}
finally
{
    // This fixture only creates files in two exact directories; never delete recursively.
    foreach (string file in Directory.GetFiles(owned)) File.Delete(file);
    Directory.Delete(owned);
    foreach (string file in Directory.GetFiles(root)) File.Delete(file);
    Directory.Delete(root);
}
Console.WriteLine($"PASS: {assertions} security/offline assertions. Fake writes and fake DISM only; host configuration unchanged.");

sealed class SecurityFake(SecurityMitigationSnapshot state) : ISecurityMitigationPlatform
{
    public int? Value = state.HvciEnabled; public int Writes; public bool IgnoreWrite;
    public SecurityMitigationSnapshot Read() => state with { HvciEnabled = Value };
    public int? ReadMemoryIntegrity() => Value;
    public void WriteMemoryIntegrity(int? value) { Writes++; if (!IgnoreWrite) Value = value; }
}
sealed class Store : ISecurityMitigationBackupStore
{
    public SecurityMitigationBackup? Value; public bool FailSave;
    public SecurityMitigationBackup? Load() => Value;
    public void Save(SecurityMitigationBackup value) { if (FailSave) throw new IOException("Synthetic log failure"); Value = value; }
}
sealed class OfflineFake(params string[] outputs) : IOfflineImageCommands
{
    private readonly Queue<string> _outputs = new(outputs);
    public List<string[]> Calls = [];
    public Task<OfflineImageCommandResult> RunAsync(IReadOnlyList<string> args, Action<int, long>? started, IProgress<string>? progress)
    { Calls.Add(args.ToArray()); return Task.FromResult(new OfflineImageCommandResult(0, _outputs.Dequeue())); }
}
