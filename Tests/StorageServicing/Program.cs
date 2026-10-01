using Naufal_Windows_Tech_s_Powertoys;

int count = 0;
void Check(bool value, string title) { count++; if (!value) throw new Exception(title); }
NativeCommandResult Ok(string output = "ok", int exit = 0) => new(exit, output, "", false, TimeSpan.Zero);
string temp = Path.Combine(Path.GetTempPath(), "NWU-StorageTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var features = StorageServicing.ParseInventory(StorageItemKind.Feature, "Feature Name : B\nState : Disabled\nFeature Name : A\nState : Enabled\nFeature Name : A\nState : Enabled\n");
    Check(features.Count == 2 && features[0].Name == "A", "Sorted distinct inventory");
    Check(StorageServicing.ParseInventory(StorageItemKind.Capability, "Capability Identity : X~~~~0.0.1.0\nState : Installed").Single().State == "Installed", "Capability format");
    Check(StorageServicing.ParseInventory(StorageItemKind.Feature, "Feature Name : /bad\nState : Enabled").Count == 0, "Reject argument-like identity");
    Check(StorageServicing.ParseInventory(StorageItemKind.Feature, "Feature Name : incomplete").Count == 0, "No fabricated state");
    foreach (string invalid in new[] { "", "x /Force", "x:y", "x\ny", "*", "../x" })
    {
        bool rejected = false; try { StorageServicing.ValidateName(invalid); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid identifier " + invalid);
    }
    Check(StorageServicing.ReadState("State : Enabled\nOther : abc") == "Enabled", "Exact state parse");
    Check(StorageServicing.ReadState("Status : Enabled") is null, "Unknown state preserved");
    Check(!StorageServicing.IsDesired(StorageItemKind.Feature, "Enable Pending", true), "Pending not effective");
    var item = new StorageItem(StorageItemKind.Feature, "TestFeature", "Disabled");
    var fake = new Fake(Ok("State : Disabled"), Ok(), Ok("State : Enabled"));
    var service = new StorageServicing(fake, temp);
    var result = await service.ChangeAsync(item, true);
    Check(result.Outcome == "Verified", "Verified after readback");
    Check(fake.Commands.Count == 3 && fake.Commands[1].Mutation, "Pre mutation post sequence");
    Check(fake.Commands[1].Arguments.Contains("/NoRestart"), "No automatic reboot");
    Check(!fake.Commands[1].Arguments.Any(a => a is "/All" or "/Force" or "/Remove"), "No automatic dependency/payload expansion");
    Check(File.ReadAllText(result.LogPath).Contains("State : Disabled"), "Original state retained");
    fake = new Fake(Ok("State : Disabled"), Ok(), Ok("State : Disabled"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "VerificationPending", "Exit zero insufficient");
    fake = new Fake(Ok("State : Disabled"), Ok("done", 3010), Ok("State : Enable Pending"));
    result = await new StorageServicing(fake, temp).ChangeAsync(item, true);
    Check(result.Outcome == "RebootRequired" && result.RestartRequired, "3010 pending");
    fake = new Fake(Ok("State : Enabled"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "AlreadyApplied" && fake.Commands.Count == 1, "No repeated writes");
    fake = new Fake(Ok("State : Disable Pending"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "RebootRequired" && fake.Commands.Count == 1, "No servicing on pending item");
    fake = new Fake(Ok("Access denied", 5));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "Unknown" && fake.Commands.Count == 1, "Unreadable prestate blocks mutation");
    fake = new Fake(Ok("Unknown output"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "Unknown", "Unparseable prestate blocks mutation");
    fake = new Fake(Ok("Before"), Ok(), Ok("After"));
    result = await new StorageServicing(fake, temp).CleanupAsync(false, "");
    Check(result.Outcome == "CompletedWithAnalysis" && !fake.Commands[1].Arguments.Contains("/ResetBase"), "Ordinary cleanup isolated");
    fake = new Fake(); bool resetRejected = false;
    try { await new StorageServicing(fake, temp).CleanupAsync(true, "yes"); } catch (InvalidOperationException) { resetRejected = true; }
    Check(resetRejected && fake.Commands.Count == 0, "ResetBase explicit phrase required before commands");
    fake = new Fake(Ok("analysis failed", 87));
    Check((await new StorageServicing(fake, temp).CleanupAsync(false, "")).Outcome == "Unknown" && fake.Commands.Count == 1, "Failed analysis blocks cleanup");
    fake = new Fake(Ok("Before"), Ok(), Ok("After"));
    await new StorageServicing(fake, temp).CleanupAsync(true, "RESETBASE");
    Check(fake.Commands[1].Arguments.Contains("/ResetBase"), "Explicit ResetBase available");
    fake = new Fake(Ok("State : Installed"), Ok(), Ok("State : Not Present"));
    result = await new StorageServicing(fake, temp).ChangeAsync(new(StorageItemKind.Capability, "Test~~~~0.0.1.0", "Installed"), false);
    Check(result.Outcome == "Verified" && fake.Commands[1].Arguments.Contains("/Remove-Capability"), "Capability removal verified");
    fake = new Fake(Ok("State : Disabled"), Ok("failure", 5), Ok("State : Disabled"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "Failed", "Mutation failure not hidden");
    fake = new Fake(Ok("State : Disabled"));
    string blocked = Path.Combine(temp, "file-not-directory"); File.WriteAllText(blocked, "test");
    bool logBlocked = false; try { await new StorageServicing(fake, blocked).ChangeAsync(item, true); } catch (IOException) { logBlocked = true; }
    Check(logBlocked && fake.Commands.Count == 1, "Cannot mutate without durable initial log");
    fake = new Fake(Ok("State : Disabled with Payload Removed"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item, true)).Outcome == "Blocked" && fake.Commands.Count == 1, "Stale selection blocks mutation");
    fake = new Fake(Ok("State : Unexpected"));
    Check((await new StorageServicing(fake, temp).ChangeAsync(item with { State = "Unexpected" }, true)).Outcome == "Unknown" && fake.Commands.Count == 1, "Unknown state blocks mutation even when preview matches");
    Console.WriteLine($"PASS: {count} storage servicing assertions. Fake commands only; no Windows servicing changes.");
}
finally
{
    foreach (string file in Directory.GetFiles(temp)) File.Delete(file);
    Directory.Delete(temp);
}

sealed class Fake(params NativeCommandResult[] results) : IStorageCommandRunner
{
    private readonly Queue<NativeCommandResult> _results = new(results);
    public List<StorageCommand> Commands { get; } = [];
    public Task<NativeCommandResult> RunAsync(StorageCommand command) { Commands.Add(command); return Task.FromResult(_results.Dequeue()); }
}
