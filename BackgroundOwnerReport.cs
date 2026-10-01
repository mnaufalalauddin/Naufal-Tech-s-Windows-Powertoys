using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record BackgroundProcess(uint Id, uint ParentId, string Name, string Path,
    DateTimeOffset? Created, ulong? WorkingSetBytes, ulong? PrivateBytes, uint? Threads, uint? Handles);
internal sealed record BackgroundService(uint ProcessId, string Name, string State);
internal sealed record BackgroundOwnerSnapshot(DateTimeOffset Captured,
    IReadOnlyList<BackgroundProcess> Before, IReadOnlyList<BackgroundProcess> Processes,
    IReadOnlyList<BackgroundService> Services, string ServiceError);

internal static class BackgroundOwnerReport
{
    internal static IReadOnlyList<SystemReportEntry> Build(BackgroundOwnerSnapshot snapshot)
    {
        var current = snapshot.Processes.GroupBy(p => p.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        var before = snapshot.Before.GroupBy(p => p.Id).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.Single());
        List<SystemReportEntry> rows = [
            new("BACKGROUND OWNER FINDER", "", true),
            new("Snapshot", snapshot.Captured.ToString("O", CultureInfo.InvariantCulture), false),
            new("Scope", $"{snapshot.Processes.Count} process records; read-only, on demand. Processes can exit during or after the scan.", false),
            new("Evidence", "Parent relationships are Windows-reported and checked for PID reuse using creation times, not proof of application ownership. Service PID matches require the same process identity before and after service enumeration.", false),
            new("Limitations", "Startup/task attribution, package ownership, publisher/signature checks and per-app CPU/disk attribution are not collected. No process is terminated and no application is uninstalled.", false),
            new("Memory", "Working set includes shared pages; private committed bytes are not physical RAM. Do not add process working sets to claim total RAM savings. Unknown counters are not zero.", false),
            new("Privacy", "Executable paths may contain user names. Review before sharing exported reports. Command lines and environment variables are not collected.", false),
            new("Service inventory", string.IsNullOrEmpty(snapshot.ServiceError) ? $"{snapshot.Services.Count} records; PID 0 is never assigned to an application." : "Unknown: " + snapshot.ServiceError, false)
        ];
        foreach (var process in snapshot.Processes.OrderByDescending(p => p.WorkingSetBytes.HasValue).ThenByDescending(p => p.WorkingSetBytes).ThenBy(p => p.Id))
        {
            rows.Add(new($"{process.Name} — PID {process.Id}", "", true));
            rows.Add(new("Executable", string.IsNullOrWhiteSpace(process.Path) ? "Unknown / unavailable to this reader" : process.Path, false));
            rows.Add(new("Created", process.Created?.ToString("O", CultureInfo.InvariantCulture) ?? "Unknown", false));
            rows.Add(new("Working set / private commit", $"{Memory(process.WorkingSetBytes)} / {Memory(process.PrivateBytes)}", false));
            rows.Add(new("Threads / handles", $"{process.Threads?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"} / {process.Handles?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"}", false));
            var parent = Parent(process, current, out string evidence);
            rows.Add(new("Parent relationship", evidence, false));
            bool stable = process.Id != 0 && process.Created.HasValue && before.TryGetValue(process.Id, out var earlier) && earlier.Created == process.Created;
            string services = !string.IsNullOrEmpty(snapshot.ServiceError) ? "Unknown: service inventory failed" :
                !stable ? "Unknown: process identity was not stable across the service scan" :
                string.Join(", ", snapshot.Services.Where(s => s.ProcessId == process.Id).Select(s => s.Name + " (" + s.State + ")"));
            rows.Add(new("Hosted services (PID evidence)", services.Length == 0 ? "No service PID match observed; not proof of non-service ownership" : services, false));
            if (IsWebView(process)) rows.Add(new("WebView2 owner candidate", WebViewCandidate(process, current), false));
        }
        return rows;
    }

    internal static BackgroundProcess? Parent(BackgroundProcess process, IReadOnlyDictionary<uint, BackgroundProcess> processes, out string evidence)
    {
        if (process.ParentId == 0) { evidence = "Unknown / no nonzero parent reported"; return null; }
        if (process.ParentId == process.Id) { evidence = "Unknown: self-referencing parent PID"; return null; }
        if (!processes.TryGetValue(process.ParentId, out var parent)) { evidence = $"PID {process.ParentId}: parent not present in snapshot (may have exited)"; return null; }
        if (!process.Created.HasValue || !parent.Created.HasValue) { evidence = $"PID {process.ParentId}: candidate only; creation time unavailable"; return null; }
        if (parent.Created > process.Created) { evidence = $"PID {process.ParentId}: rejected; PID reused after child creation"; return null; }
        evidence = $"{parent.Name} (PID {parent.Id}); Windows-reported parent, creation-time check passed";
        return parent;
    }

    internal static string WebViewCandidate(BackgroundProcess process, IReadOnlyDictionary<uint, BackgroundProcess> processes)
    {
        HashSet<uint> seen = [process.Id];
        for (int depth = 0; depth < 16; depth++)
        {
            var parent = Parent(process, processes, out string reason);
            if (parent is null) return "Unknown. " + reason + ". Keep the shared runtime; identify the owning app first.";
            if (!seen.Add(parent.Id)) return "Unknown: cyclic parent chain. No ownership claim.";
            if (!IsWebView(parent)) return $"{parent.Name} (PID {parent.Id}) is the first non-WebView2 ancestor, a candidate only. Confirm the application before changing its background/startup settings; do not remove the shared runtime based on this observation.";
            process = parent;
        }
        return "Unknown: ancestry depth limit reached. No ownership claim.";
    }

    internal static bool IsWebView(BackgroundProcess process) => process.Name.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase);
    private static string Memory(ulong? bytes) => bytes.HasValue ? (bytes.Value / 1048576d).ToString("N1", CultureInfo.InvariantCulture) + " MiB" : "Unknown";

    internal static DateTimeOffset? ParseCreationDate(string value)
    {
        // WMI DMTF datetime, with explicit UTC offset. Wildcards are unknown.
        if (value.Length != 25 || value[14] != '.' || value[21] is not ('+' or '-') ||
            !DateTime.TryParseExact(value[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
            !int.TryParse(value.AsSpan(15, 6), NumberStyles.None, CultureInfo.InvariantCulture, out int micros) ||
            !int.TryParse(value.AsSpan(22, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) || minutes > 840) return null;
        try { return new DateTimeOffset(date.AddTicks(micros * 10L), TimeSpan.FromMinutes(value[21] == '-' ? -minutes : minutes)); }
        catch (ArgumentException) { return null; }
    }
}
