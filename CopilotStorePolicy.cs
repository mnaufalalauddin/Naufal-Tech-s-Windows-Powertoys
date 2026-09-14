using System;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class CopilotStorePolicy
{
    internal const string ProductId = "XP9CXNGPPJ97XX";
    internal const int NoApplications = unchecked((int)0x8A150014);
    internal const string Deferred = "Store-product detection is checked when Copilot is selected. AppX inventory alone cannot verify its desktop variants.";
    internal const int SourceAgreementsNotAccepted = unchecked((int)0x8A150046);
    internal static string[] ListArguments(bool acceptSourceAgreements = false) =>
        WithConsent(["list", "--id", ProductId, "--exact", "--source", "msstore", "--scope", "user", "--disable-interactivity"], acceptSourceAgreements);
    internal static string[] RemoveArguments(bool acceptSourceAgreements = false) =>
        WithConsent(["uninstall", "--id", ProductId, "--exact", "--source", "msstore", "--scope", "user", "--silent", "--disable-interactivity"], acceptSourceAgreements);
    private static string[] WithConsent(string[] arguments, bool accepted) =>
        accepted ? [.. arguments, "--accept-source-agreements"] : arguments;

    internal static string InventoryFailure(NativeCommandResult result) =>
        !result.TimedOut && result.ExitCode == SourceAgreementsNotAccepted
            ? "Microsoft Store source consent is required. Retry the selected operation and review the Microsoft Store source agreement, then choose Agree and continue if you accept it. Copilot availability is not verified; this is not evidence that Copilot is absent."
            : "Copilot Store-product inventory could not be verified (exit " + result.ExitCode + "). " + result.CombinedOutput;
    internal static bool? Installed(NativeCommandResult result)
    {
        if (result.TimedOut) return null;
        if (result.ExitCode == NoApplications) return false;
        // Do not depend on localized names or treat a successful empty table as absence.
        if (result.ExitCode == 0 && result.StandardOutput.Split('\n').Any(line =>
            line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(ProductId, StringComparer.Ordinal))) return true;
        return null;
    }
    internal static BuiltInAppPackage Package(string? error = null) => new(
        "Microsoft Copilot", "", ProductId + ":user", false, false,
        Healthy: error is null, Kind: BuiltInAppKind.CopilotStore, Scope: "user") { InventoryError = error };
}
