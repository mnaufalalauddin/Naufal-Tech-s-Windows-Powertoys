using Naufal_Windows_Tech_s_Powertoys;

try
{
if (OneDriveExecutionTests.Child(args)) return;
if (args.Contains("--onedrive-user-probe"))
{
    string? report = args.Contains("--probe-report") ? args[Array.IndexOf(args, "--probe-report") + 1] : null;
    try
    {
        await OneDriveExecutionTests.ProbeAsync(args[Array.IndexOf(args, "--onedrive-user-probe") + 1], args.Contains("--require-elevated"));
        if (report is not null) File.WriteAllText(report, "PASS: same-account medium-integrity child; stdout/stderr, exit code and literal argv verified. Parent elevation required=" + args.Contains("--require-elevated"));
    }
    catch (Exception exception)
    {
        if (report is not null) File.WriteAllText(report, "FAIL: " + exception);
        throw;
    }
    return;
}
if (await AuditRegressionTests.RunChildAsync(args)) return;
if (args.Contains("--wizard-wmi-read-probe"))
{
    WmiPrerequisiteProbe probe = new();
    var system = await probe.VerifySystemAsync();
    var memory = await probe.VerifyMemoryAsync();
    Console.WriteLine($"System={system.State}: {system.Detail}");
    Console.WriteLine($"Memory={memory.State}: {memory.Detail}");
    Environment.ExitCode = system.Verified && memory.Verified ? 0 : 1;
    return;
}
if (args.Contains("--restore-read-probe"))
{
    RestoreFollowupTests.ProbeReadOnly();
    return;
}
if (args.Contains("--ntfs-read-probe"))
{
    NtfsPerformanceTests.ProbeReadOnly();
    return;
}
if (args.Contains("--photo-viewer-read-probe"))
{
    PhotoViewerTests.ProbeReadOnly();
    return;
}
if (args.Contains("--print-store-reset-script"))
{
    Console.Write(StoreResetCommand.BuildScript("Microsoft.WindowsStore_22607.1401.8.0_x64__8wekyb3d8bbwe"));
    return;
}
int passed = 0;
void Assert(bool value, string name)
{
    if (!value) throw new Exception("FAILED: " + name);
    passed++;
}
ProfileRegistryValue Set(uint value) => new(true, true, value);
ProfileRegistryValue Absent() => new(true, false, 0);
string plan = "13747e58-d717-436b-8947-c2403425186c";
Dictionary<string, ProfilePowerPair?> Power(bool competitive) => new()
{
    ["PROCTHROTTLEMIN"] = new(competitive ? 100u : 5u, competitive ? 100u : 5u),
    ["PROCTHROTTLEMAX"] = new(100, 100),
    ["PERFINCPOL"] = new(competitive ? 2u : 1u, competitive ? 2u : 1u),
    ["CPMINCORES"] = new(competitive ? 100u : 10u, competitive ? 100u : 10u),
    ["CPMAXCORES"] = new(100, 100)
};
ProfileVerificationInput Input(string profile)
{
    bool competitive = profile == "Competitive Gaming", gaming = profile != "Balanced";
    GamingLiveMmcssSnapshot mmcss = new(profile, competitive ? 0x24 : gaming ? 0x18 : 2, competitive ? 1 : 20,
        profile == "Optimized Gaming" ? 4 : 2, profile == "Optimized Gaming" ? 6 : 8,
        competitive ? "High" : "Medium", gaming ? "High" : "Normal", 10000, competitive ? 1 : 0, competitive ? 1 : 0);
    return new(mmcss, plan, Power(competitive), true, competitive ? "Yes" : null, competitive ? "No" : null,
        true, new[] { new ProfileNetworkInterface("adapter", gaming ? Set(1) : Absent(), gaming ? Set(1) : Absent()) },
        gaming ? Set(1) : Absent(), gaming ? Set(uint.MaxValue) : Absent(), gaming ? Set(0) : Absent(), gaming ? Set(0) : Absent(),
        true, new[] { new ProfileRscAdapter("adapter", !competitive, !competitive) });
}
foreach (string profile in PerformanceProfileVerification.Profiles)
{
    ProfileVerificationInput input = Input(profile);
    var expected = Power(profile == "Competitive Gaming");
    ProfileVerificationResult Verify(ProfileVerificationInput value) => PerformanceProfileVerification.Evaluate(profile, value, plan, expected);
    var perfect = Verify(input);
    Assert(perfect.Total == 23 && perfect.Verified, profile + " 23/23");
    Assert(!Verify(input with { ActivePowerGuid = Guid.Empty.ToString() }).Verified, "wrong plan");
    Assert(!Verify(input with { BcdReadable = false }).Verified, "BCD denied is not default");
    Assert(!Verify(input with { InterfacesReadable = false }).Verified, "interface read failure");
    Assert(!Verify(input with { RscReadable = false }).Verified, "RSC query failure");
    Assert(!Verify(input with { Rsc = new[] { new ProfileRscAdapter("adapter", true, false) } }).Verified, "mixed RSC protocols");
    Assert(Verify(input with { Rsc = Array.Empty<ProfileRscAdapter>() }).Verified, "successful empty RSC is not applicable");
    Assert(!Verify(input with { QosTos = new(false, false, 0) }).Verified, "denied QoS isn't absent");
    foreach (string alias in PerformanceProfileVerification.PowerAliases)
    {
        var altered = new Dictionary<string, ProfilePowerPair?>(input.Power);
        altered[alias] = new(input.Power[alias]!.Value.Ac, 999);
        Assert(!Verify(input with { Power = altered }).Verified, "DC-only mismatch " + alias);
        altered[alias] = null;
        Assert(!Verify(input with { Power = altered }).Verified, "unreadable " + alias);
    }
    var rows = input.Interfaces.Concat(new[] { new ProfileNetworkInterface("bad-adapter", Set(1), Absent()) }).ToArray();
    Assert(!Verify(input with { Interfaces = rows }).Verified, "one adapter TCP ACK mismatch");
    Assert(!Verify(input with { Mmcss = input.Mmcss with { ClockRate = null } }).Verified, "missing MMCSS");
    var state = PerformanceProfileVerification.SelectBest(new[] { Verify(input with { BcdReadable = false }) });
    Assert(state.Profile == "Custom" && state.DisplayText == "Custom - 21/23", "partial cannot disable Apply");
}
var known = new Dictionary<string, string> { ["Optimized Gaming"] = plan };
var list = new GamingLiveCommandResult(0, $"Power Scheme GUID: {plan} (Ultimate Performance) *", "");
Assert(PerformanceProfileVerificationService.ResolveTarget("Optimized Gaming", list, known) == plan, "valid historical power GUID");
Assert(PerformanceProfileVerificationService.ResolveTarget("Balanced", list, known) is null, "reject wrong profile plan");
Assert(PerformanceProfileVerificationService.ResolveTarget("Optimized Gaming", list with { ExitCode = 1 }, known) is null, "failed power list");
Assert(PerformanceProfileVerificationService.ResolveTarget("Optimized Gaming", list with { StandardOutput = $"{plan} (Power saver)" }, known) is null, "reject poisoned historical GUID");
Assert(GamingLiveStatusService.FormatRsc(false, new[] { new ProfileRscAdapter("a", true, true) }) == "UNKNOWN", "RSC denied isn't enabled");
Assert(GamingLiveStatusService.FormatRsc(true, Array.Empty<ProfileRscAdapter>()) == "UNKNOWN", "RSC absent live status");
Assert(GamingLiveStatusService.FormatRsc(true, new[] { new ProfileRscAdapter("a", true, true) }) == "ON", "RSC enabled live status");
Assert(GamingLiveStatusService.FormatRsc(true, new[] { new ProfileRscAdapter("a", false, false) }) == "OFF", "RSC disabled live status");
Assert(GamingLiveStatusService.FormatRsc(true, new[] { new ProfileRscAdapter("a", true, false) }) == "MIXED", "RSC mixed protocols live status");
Assert(GamingLiveStatusService.FormatRsc(true, new[] { new ProfileRscAdapter("a", true, true), new ProfileRscAdapter("b", false, false) }) == "MIXED", "RSC mixed adapters live status");
await AuditRegressionTests.RunAsync(Assert);
await CatalogSelectionTests.RunAsync(Assert);
await CatalogInteractionTests.RunAsync(Assert);
await RecoveredCatalogFeaturesTests.RunAsync(Assert);
await GeneralAuditTests.RunAsync(Assert);
await CatalogAuditTests.RunAsync(Assert);
PowerPolicyReaderTests.Run(Assert);
AppDataPathTests.Run(Assert);
await WmiPrerequisiteTests.RunAsync(Assert);
await BuiltInAppsTests.RunAsync(Assert);
CatalogExpansionTests.Run(Assert);
await CopilotConsentTests.RunAsync(Assert);
await OneDriveExecutionTests.RunAsync(Assert);
await GameModeToggleTests.RunAsync(Assert);
NtfsPerformanceTests.Run(Assert);
PhotoViewerTests.Run(Assert);
await MonitoringTests.RunAsync(Assert);
HeaderLayoutTests.Run(Assert);
RuntimePrerequisiteTests.Run(Assert);
EssentialSnapshotTests.Run(Assert);
RscResultTests.Run(Assert);
await MaintenanceRepairTests.RunAsync(Assert);
await WholeProgramAuditTests.RunAsync(Assert);
await ReauditSeptember9Tests.RunAsync(Assert);
await CatalogAvailabilityTests.RunAsync(Assert);
RestoreFollowupTests.Run(Assert);
await RestoreDefaultsAuditTests.RunAsync(Assert);
Console.WriteLine($"PASS: {passed} regression assertions. No Windows settings changed.");
if (args.Contains("--power-probe")) PowerPolicyReaderTests.ProbeBalanced();
if (args.Contains("--rsc-probe")) await RscResultTests.ProbeAsync();
if (args.Contains("--probe"))
{
    try
    {
        var rsc = await Task.Run(NativeRscReader.Read);
        Console.WriteLine($"Native RSC reader: {rsc.Count} adapters");
        foreach (var adapter in rsc) Console.WriteLine($"  {adapter.Name}: IPv4={adapter.Ipv4Enabled}, IPv6={adapter.Ipv6Enabled}");
        await Task.Run(NativeRscService.ValidateMethodMetadata);
        Console.WriteLine("RSC Enable/Disable method metadata and input construction validated; no method executed.");
    }
    catch (Exception exception) { Console.WriteLine($"Native RSC probe unavailable: {exception.Message}"); }
    var live = await new GamingLiveStatusService().ReadSnapshotAsync();
    Console.WriteLine($"MMCSS-only label: {live.Mmcss.Profile}");
    Console.WriteLine($"Full verification: {live.PerformanceProfile?.DisplayText}");
    foreach (var check in live.PerformanceProfile!.Best.Checks)
        Console.WriteLine($"{(check.Pass ? "PASS" : "FAIL")} {check.Name}: expected {check.Expected}; actual {check.Actual}");
}
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
