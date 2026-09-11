using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record MaintenanceCompletion(bool Success, int WarningCount, string[] Stages)
{
    internal static bool IsTerminal(string status) => status is "PASS" or "WARNING" or "FAILED" or "SKIPPED" or "NOT VERIFIED";

    internal static bool CanReport(IReadOnlyList<string> stages, int activeStage, MaintenanceProgressUpdate update) =>
        update.StageCount == stages.Count && update.StageIndex >= 1 && update.StageIndex <= stages.Count &&
        !IsTerminal(stages[update.StageIndex - 1]) &&
        (update.StageIndex >= activeStage || IsTerminal(update.Status)) &&
        (update.Status == "RUNNING" || IsTerminal(update.Status));

    internal static MaintenanceCompletion Evaluate(bool reportedSuccess, int warnings, IReadOnlyList<string> stages)
    {
        string[] final = stages.Select(status => IsTerminal(status) ? status :
            reportedSuccess ? "NOT VERIFIED" : status == "WAITING" ? "SKIPPED" : "FAILED").ToArray();
        bool success = reportedSuccess && final.Length > 0 &&
            final.All(status => status is "PASS" or "WARNING" or "SKIPPED");
        return new(success, Math.Max(Math.Max(0, warnings), final.Count(status => status == "WARNING")), final);
    }
}
