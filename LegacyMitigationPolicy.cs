using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class LegacyMitigationPolicy
{
    internal const string ActionId = "SecurityMitigationsPerformance";
    internal const string ApplyBlocked = "Unsupported Apply: the legacy combined CPU-mitigation/HVCI override has no CPU/build-specific validation or effective-protection verification. A generic bitmask must not be applied to every PC. Existing settings and backup IDs are retained for Restore. Granular replacement controls are not implemented yet; no settings were changed.";
    internal static bool IsBlocked(string id) => string.Equals(id, ActionId, StringComparison.OrdinalIgnoreCase);
}
