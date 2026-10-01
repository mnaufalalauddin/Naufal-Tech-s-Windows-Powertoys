using Naufal_Windows_Tech_s_Powertoys;

internal static class BackgroundOwnerTests
{
    internal static void Run(Action<bool, string> check)
    {
        var time = new DateTimeOffset(2026, 9, 29, 1, 0, 0, TimeSpan.Zero);
        BackgroundProcess P(uint id, uint parent, string name, int seconds) => new(id, parent, name, "", time.AddSeconds(seconds), null, null, null, null);
        var owner = P(1, 0, "example.exe", 0);
        var view = P(2, 1, "msedgewebview2.exe", 1);
        var renderer = P(3, 2, "msedgewebview2.exe", 2);
        Dictionary<uint, BackgroundProcess> map = new() { [1] = owner, [2] = view, [3] = renderer };
        check(BackgroundOwnerReport.Parent(view, map, out _) == owner, "Parent creation time matches chronological process chain");
        check(BackgroundOwnerReport.WebViewCandidate(renderer, map).Contains("example.exe") && BackgroundOwnerReport.WebViewCandidate(renderer, map).Contains("candidate only"), "WebView ancestor is candidate not proven owner");
        map[1] = owner with { Created = time.AddMinutes(1) };
        check(BackgroundOwnerReport.Parent(view, map, out var reused) is null && reused.Contains("reused"), "Reused PID cannot claim parent ownership");
        map[1] = owner with { Created = null };
        check(BackgroundOwnerReport.Parent(view, map, out _) is null, "Missing creation time cannot prove parent identity");
        map.Remove(1);
        check(BackgroundOwnerReport.Parent(view, map, out var missing) is null && missing.Contains("not present"), "Exited parent remains unknown");
        map[1] = owner;
        check(BackgroundOwnerReport.Parent(view with { ParentId = 2 }, map, out _) is null, "Self-parent is rejected");
        check(BackgroundOwnerReport.Parent(owner, map, out _) is null, "PID zero is not assigned as application owner");
        var cycle = new Dictionary<uint, BackgroundProcess> { [2] = view with { ParentId = 3, Created = time }, [3] = renderer with { ParentId = 2, Created = time } };
        check(BackgroundOwnerReport.WebViewCandidate(cycle[2], cycle).Contains("cyclic"), "Cyclic ancestry terminates without guessing");
        check(BackgroundOwnerReport.ParseCreationDate("20260929080000.123456+420") == time.AddTicks(1234560), "DMTF datetime offset and microseconds are preserved");
        foreach (string invalid in new[] { "", "20260929080000.******+420", "20261329080000.123456+420", "20260929080000.123456+999", "not a date" })
            check(BackgroundOwnerReport.ParseCreationDate(invalid) is null, "Malformed/wildcard datetime stays unknown");
        var snapshot = new BackgroundOwnerSnapshot(time, [owner, view], [owner, view], [new(1, "SharedService", "Running"), new(0, "StoppedService", "Stopped")], "");
        var rows = BackgroundOwnerReport.Build(snapshot);
        check(rows.Any(r => r.Property == "Hosted services (PID evidence)" && r.Value.Contains("SharedService")), "Stable process receives observed service PID association");
        check(!rows.Any(r => r.Value.Contains("StoppedService")), "PID zero service not attached to another process");
        check(rows.Any(r => r.Property == "Working set / private commit" && r.Value == "Unknown / Unknown"), "Unavailable memory is never zero");
        rows = BackgroundOwnerReport.Build(snapshot with { Before = [] });
        check(!rows.Any(r => r.Value.Contains("SharedService")), "New or recycled process identity not matched to old service observation");
        rows = BackgroundOwnerReport.Build(snapshot with { ServiceError = "Access denied" });
        check(rows.Any(r => r.Property == "Service inventory" && r.Value.Contains("Unknown: Access denied")), "Failed service enumeration preserves uncertainty");
    }

    internal static async Task ProbeAsync()
    {
        var snapshot = await BackgroundOwnerService.ReadAsync();
        var rows = BackgroundOwnerReport.Build(snapshot);
        if (snapshot.Processes.Count == 0) throw new Exception("Native process inventory returned zero records.");
        Console.WriteLine($"Native background inventory: {snapshot.Processes.Count} processes, {snapshot.Services.Count} services, {snapshot.Processes.Count(p => p.Created.HasValue)} creation timestamps; {rows.Count} report rows. Service provider: {(snapshot.ServiceError.Length == 0 ? "available" : "unavailable")}. No paths/command lines printed; no processes changed.");
    }
}
