using Microsoft.UI.Windowing;
using System;
using System.IO;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal static class AppWindowIcon
    {
        internal static void Apply(AppWindow window)
        {
            try
            {
                // Absolute path also works for shortcuts with a different working directory.
                window.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "NaufalWindowsPowertoys.ico"));
            }
            catch (Exception exception)
            {
                // A missing/corrupt cosmetic asset must not prevent a tool from opening.
                App.WriteCrashLog(exception, "Setting window icon");
            }
        }
    }
}
