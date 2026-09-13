using System;
using System.Linq;
using Naufal_Windows_Tech_s_Powertoys;

internal static class HeaderLayoutTests
{
    internal static void Run(Action<bool, string> assert)
    {
        int[] percentages = [25, 50, 75, 100, 125, 150, 175, 200];
        double[] geometry = [.70, .80, .90, 1, 1.14, 1.28, 1.42, 1.56];
        foreach (int index in Enumerable.Range(0, 8))
        {
            int percent = percentages[index];
            double scale = geometry[index];
            assert(HeaderLayoutPolicy.PopupFontSize(percent) >= 14 && HeaderLayoutPolicy.PopupFontSize(percent) <= 28,
                $"{percent}% recovery popup stays readable");
            assert(HeaderLayoutPolicy.RecoveryTargetMinimum >= 32, $"{percent}% recovery hit target");
            assert(!HeaderLayoutPolicy.StackClock(1876, percent, scale), $"{percent}% maximized header uses one row");
            assert(HeaderLayoutPolicy.StackClock(300, percent, scale), $"{percent}% narrow header wraps clock");
            double threshold = 680 * Math.Max(percent / 100d, scale);
            assert(HeaderLayoutPolicy.StackClock(threshold - 1, percent, scale), $"{percent}% wraps below breakpoint");
            assert(!HeaderLayoutPolicy.StackClock(threshold, percent, scale), $"{percent}% stable breakpoint");
            // Repeated forward/reverse cycles select the same layout rather than
            // deriving a new size from controls changed by the previous scale.
            foreach (double width in new[] { 400d, 600, 800, 1024, 1280, 1920 })
            for (int repeat = 0; repeat < 5; repeat++)
                assert(HeaderLayoutPolicy.StackClock(width, percent, scale) == (width < threshold),
                    $"{percent}% layout deterministic across cycles");
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1d, 0 })
            assert(!HeaderLayoutPolicy.StackClock(invalid, 100, 1), "unmeasured window not used for layout");
        assert(HeaderLayoutPolicy.PopupFontSize(25) == 14, "smallest app scale does not make recovery menu 4px");
        assert(HeaderLayoutPolicy.PopupFontSize(200) == 28, "largest app scale preserves larger recovery font");
    }
}
