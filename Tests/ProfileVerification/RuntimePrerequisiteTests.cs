using Naufal_Windows_Tech_s_Powertoys;

internal static class RuntimePrerequisiteTests
{
    internal static void Run(Action<bool, string> assert)
    {
        foreach (var (state, expected) in new[] {
            ("Enabled", "READY"), (" Enable Pending ", "PENDING"),
            ("Disable Pending", "DISABLING"), ("Disabled", "DISABLED"),
            ("Disabled with Payload Removed", "DISABLED"),
            ("Unavailable", "UNKNOWN"), ("unexpected", "UNKNOWN"), ("", "UNKNOWN") })
        {
            assert(RuntimePrerequisiteVerification.FeatureStatus(state) == expected,
                "Runtime state is classified exactly: " + state);
            foreach (int exitCode in new[] { 0, 3010, 1, -1, 5 })
            {
                bool accepted = exitCode is 0 or 3010 && expected is "READY" or "PENDING";
                assert(RuntimePrerequisiteVerification.IsEnableAccepted(exitCode, state) == accepted,
                    "DISM acceptance requires success code and enabled/enable-pending: " + state + "/" + exitCode);
            }
        }
        assert(RuntimePrerequisiteVerification.FeatureStatus("Disabled", true) == "OPTIONAL", "Disabled optional runtime is not an error");
        assert(RuntimePrerequisiteVerification.FeatureStatus("Disable Pending", true) == "DISABLING", "Pending removal is never ready/optional");
        assert(RuntimePrerequisiteVerification.FeatureStatus("Unavailable", true) == "UNKNOWN", "Unreadable runtime is not confirmed absent");
        foreach (bool success in new[] { false, true })
        foreach (bool restart in new[] { false, true })
        foreach (string? state in new[] { "PENDING", "READY", "DISABLING", "UNKNOWN", null })
            assert(RuntimePrerequisiteVerification.AwaitingRestart(success, restart, state) ==
                (success && restart && state == "PENDING"), "Final pending-restart outcome cannot hide a failed command/read");

        foreach (string id in new[] { "cryptsvc", "CRYPTSVC", "msiserver", "BITS", "TrustedInstaller" })
        foreach (int? start in new int?[] { null, 2, 3, 4 })
        foreach (string runtime in new[] { "Running", "Stopped", "Unknown" })
        {
            bool automatic = id.Equals("cryptsvc", StringComparison.OrdinalIgnoreCase);
            int expected = automatic ? 2 : 3;
            assert(RuntimePrerequisiteVerification.ServiceReady(id, start, expected, runtime) ==
                (start == expected && (!automatic || runtime == "Running")),
                "Required automatic service must run; manual on-demand services may stop: " + id);
        }
    }
}
