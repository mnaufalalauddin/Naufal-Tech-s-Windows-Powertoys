using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class HeaderLayoutPolicy
{
    internal const double RecoveryTargetMinimum = 32;
    internal const double RecoveryFontMinimum = 12;

    // Use an absolute scale, never the last rendered width. Layout transitions
    // must not accumulate size errors when cycling 25 -> ... -> 200 -> 25.
    internal static bool StackClock(double availableWidth, int percent, double geometryScale) =>
        double.IsFinite(availableWidth) && availableWidth > 0 &&
        availableWidth < 680 * Math.Max(percent / 100d, geometryScale);

    internal static double PopupFontSize(int percent) => Math.Clamp(14 * percent / 100d, 14, 28);
}
