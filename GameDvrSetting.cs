using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class GameDvrSetting
{
    internal static void Apply(bool enabled, Action captureOriginal, Action<string, int> write)
    {
        // Capture every original value before the first write. The existing
        // snapshot helper keeps the first baseline across repeated ON/OFF.
        captureOriginal();
        int value = enabled ? 1 : 0;
        write("GameDVR_Enabled", value);
        write("AppCaptureEnabled", value);
        write("AllowGameDVR", value);
        // Enabling capture must not also opt the user into background recording.
        if (!enabled) write("HistoricalCaptureEnabled", 0);
    }
}
