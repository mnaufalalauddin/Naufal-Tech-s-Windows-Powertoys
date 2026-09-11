namespace Naufal_Windows_Tech_s_Powertoys;

internal enum RepairCommandOutcome { Passed, Warning, Failed }

internal static class RepairCommandClassification
{
    // Nonzero SFC exits retain the reference app's CBS-review warning behavior.
    // Our synthetic failure code or a timeout means the scan did not complete.
    internal static RepairCommandOutcome Sfc(NativeCommandResult result) =>
        result.TimedOut || result.ExitCode < 0 ? RepairCommandOutcome.Failed :
        result.ExitCode == 0 ? RepairCommandOutcome.Passed : RepairCommandOutcome.Warning;

    internal static bool DismSucceeded(NativeCommandResult result) =>
        !result.TimedOut && result.ExitCode is 0 or 3010;
}
