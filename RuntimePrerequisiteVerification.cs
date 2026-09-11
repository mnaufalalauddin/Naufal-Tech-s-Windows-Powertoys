using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class RuntimePrerequisiteVerification
{
    internal static string FeatureStatus(string state, bool optionalWhenDisabled = false) => state.Trim().ToUpperInvariant() switch
    {
        "ENABLED" => "READY",
        "ENABLE PENDING" => "PENDING",
        "DISABLE PENDING" => "DISABLING",
        "DISABLED" or "DISABLED WITH PAYLOAD REMOVED" => optionalWhenDisabled ? "OPTIONAL" : "DISABLED",
        _ => "UNKNOWN"
    };

    internal static bool AwaitingRestart(bool operationSucceeded, bool restartRecommended, string? analyzedStatus) =>
        operationSucceeded && restartRecommended && analyzedStatus == "PENDING";

    internal static bool IsEnablePending(string state) =>
        string.Equals(state.Trim(), "Enable Pending", StringComparison.OrdinalIgnoreCase);

    internal static bool IsEnableAccepted(int exitCode, string state) =>
        exitCode is 0 or 3010 &&
        (string.Equals(state.Trim(), "Enabled", StringComparison.OrdinalIgnoreCase) || IsEnablePending(state));

    internal static bool ServiceReady(string id, int? start, int expectedStart, string runtime) =>
        start == expectedStart &&
        (!string.Equals(id, "cryptsvc", StringComparison.OrdinalIgnoreCase) || runtime == "Running");
}
