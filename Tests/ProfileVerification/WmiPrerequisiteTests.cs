using Naufal_Windows_Tech_s_Powertoys;
using System.Runtime.InteropServices;

internal static class WmiPrerequisiteTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        Dictionary<string, string> os = new()
        {
            ["Caption"] = "Microsoft Windows 10 Pro", ["Version"] = "10.0.19045", ["BuildNumber"] = "19045"
        };
        Dictionary<string, string> ram = new() { ["TotalPhysicalMemory"] = "34359738368" };
        foreach (var (caption, build) in new[]
        {
            ("Microsoft Windows 10 Pro", "19045"), ("Microsoft Windows 10 Enterprise LTSC", "17763"),
            ("Microsoft Windows 11 Pro", "28000"), ("Microsoft Windows 11 家庭版", "26100")
        })
        {
            WmiPrerequisiteProbe probe = new((cls, properties) =>
            {
                assert(cls is "Win32_OperatingSystem" or "Win32_ComputerSystem", "Wizard uses cross-version WMI classes");
                if (cls == "Win32_OperatingSystem")
                {
                    assert(properties.SequenceEqual(new[] { "Caption", "Version", "BuildNumber" }), "Wizard verifies actual OS data");
                    return [new() { ["Caption"] = caption, ["Version"] = "10.0." + build, ["BuildNumber"] = build }];
                }
                assert(properties.SequenceEqual(new[] { "TotalPhysicalMemory" }), "Wizard memory check has no DIMM/pagefile dependency");
                return [ram];
            });
            var system = await probe.VerifySystemAsync();
            var memory = await probe.VerifyMemoryAsync();
            assert(system.Verified && system.Status == "PASS", caption + " WMI works without WMIC");
            assert(memory.Verified && memory.Status == "PASS", caption + " RAM verification");
            assert(system.Detail.Contains(build) && memory.Detail.Contains("34359738368"), "Verified details retain actual data");
        }

        foreach (string property in os.Keys)
        foreach (bool missing in new[] { false, true })
        {
            var invalid = new Dictionary<string, string>(os);
            if (missing) invalid.Remove(property); else invalid[property] = "";
            var probe = new WmiPrerequisiteProbe((_, _) => [invalid]);
            assert(!(await probe.VerifySystemAsync()).Verified, "Missing/empty " + property + " does not pass");
        }
        foreach (string value in new[] { "", "0", "-1", "not available", "18446744073709551616", "32 GB" })
        {
            var probe = new WmiPrerequisiteProbe((_, _) => [new() { ["TotalPhysicalMemory"] = value }]);
            var result = await probe.VerifyMemoryAsync();
            assert(result.State == WmiPrerequisiteState.Failed, "Invalid RAM must not be marked unavailable: " + value);
        }
        foreach (var rows in new IReadOnlyList<Dictionary<string, string>>[] { [], [os, os] })
        {
            var probe = new WmiPrerequisiteProbe((_, _) => rows);
            assert(!(await probe.VerifySystemAsync()).Verified, "Empty/ambiguous OS rows are unverified");
            assert(!(await probe.VerifyMemoryAsync()).Verified, "Empty/ambiguous RAM rows are unverified");
        }
        foreach (int code in new[] { unchecked((int)0x80070005), unchecked((int)0x80041003), unchecked((int)0x80041010) })
        {
            var probe = new WmiPrerequisiteProbe((_, _) => throw new COMException("Synthetic WMI fault", code));
            var result = await probe.VerifySystemAsync();
            assert(result.State == WmiPrerequisiteState.Failed && result.Status == "FAILED", "Access/provider error remains a real failure");
            assert(result.Detail.Contains($"0x{code:X8}"), "WMI error preserves HRESULT");
        }
        var providerTimeout = new WmiPrerequisiteProbe((_, _) => throw new TimeoutException("Provider wait expired"));
        var timedOut = await providerTimeout.VerifySystemAsync();
        assert(timedOut.State == WmiPrerequisiteState.TimedOut && !timedOut.Verified, "Provider timeout is not a success");
        assert(timedOut.Status == "WARNING" && timedOut.Detail.Contains("not confirmed"), "Timeout has distinct explanatory status");

        using ManualResetEventSlim release = new();
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        var blocked = new WmiPrerequisiteProbe((_, _) =>
        {
            Interlocked.Increment(ref reads);
            started.TrySetResult();
            release.Wait();
            return [os];
        });
        try
        {
            Task<WmiPrerequisiteResult> first = blocked.VerifySystemAsync(TimeSpan.FromMilliseconds(100));
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            assert((await first).State == WmiPrerequisiteState.TimedOut, "Caller wait is bounded");
            var retry = await blocked.VerifySystemAsync(TimeSpan.FromMilliseconds(30));
            assert(retry.State == WmiPrerequisiteState.TimedOut && reads == 1, "Repeated wizard retries do not accumulate blocked workers");
            release.Set();
            assert((await blocked.VerifySystemAsync()).Verified, "Successful readback after timeout can pass on retry");
        }
        finally { release.Set(); }

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string service = File.ReadAllText(Path.Combine(root, "FirstRunPrerequisiteService.cs"));
        string main = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        assert(!service.Contains("wmic.exe", StringComparison.OrdinalIgnoreCase) && !service.Contains("/Add-Capability"), "Wizard never invokes or installs WMIC");
        assert(!service.Contains("setupWmic") && !main.Contains("CheckBox wmic"), "No selectable obsolete WMIC setup");
        assert(service.Contains("failed |= !system.Verified || !memory.Verified;"), "Both WMI results gate saved completion");
        assert(service.Contains("private static readonly WmiPrerequisiteProbe"), "WMI read gate survives wizard reopening");
        assert(service.Contains("StateSchema = 2"), "Existing completed/suppressed first-run states retained");
        assert(service.Contains("query.ExitCode == 1060 ? \"MISSING\""), "Service read denial is not reported missing");
        assert(main.Contains("CheckBox wmi = new() { IsChecked = true, IsEnabled = false"), "Required WMI verification cannot be unchecked");
        assert(main.Contains("WmiPrerequisiteProbe.SystemStage") && main.Contains("WmiPrerequisiteProbe.MemoryStage"), "Progress uses same WMI stages as backend");
        assert(!main.Contains("Install WMIC via DISM"), "No active WMIC installation label");
    }
}
