using System;
using System.Collections.Generic;

namespace Naufal_Windows_Tech_s_Powertoys;

// Repair prerequisites, not a blanket reset of service factory defaults.
// Preserve operational BITS/DoSvc startup modes instead of fighting Windows.
// Runtime still has to reach Running; an enabled startup type alone is not PASS.
internal static class UpdateServiceStartupPolicy
{
    internal static int RepairStart(string serviceName) => serviceName.ToUpperInvariant() switch
    {
        "BITS" or "WUAUSERV" => 3,
        "CRYPTSVC" or "DOSVC" => 2,
        _ => throw new ArgumentException("Service is outside Windows Update repair scope.", nameof(serviceName))
    };

    internal static bool IsOperationalStart(string serviceName, int? start)
    {
        int repairStart = RepairStart(serviceName);
        return AllowsAutomaticOrManual(serviceName) ? start is 2 or 3 : start == repairStart;
    }

    internal static string RequiredStart(string serviceName) => AllowsAutomaticOrManual(serviceName)
        ? "Automatic (2) or Manual (3)"
        : RepairStart(serviceName) == 2 ? "Automatic (2)" : "Manual (3)";

    internal static IReadOnlyList<string> Verify(string serviceName, int? start, string runtime)
    {
        List<string> issues = new();
        if (!IsOperationalStart(serviceName, start))
            issues.Add($"{serviceName} startup type is {start?.ToString() ?? "unavailable"}; required {RequiredStart(serviceName)}.");
        if (!string.Equals(runtime, "Running", StringComparison.OrdinalIgnoreCase))
            issues.Add($"{serviceName} runtime state is {runtime}, expected Running.");
        return issues;
    }

    private static bool AllowsAutomaticOrManual(string serviceName) =>
        string.Equals(serviceName, "BITS", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(serviceName, "DoSvc", StringComparison.OrdinalIgnoreCase);
}
