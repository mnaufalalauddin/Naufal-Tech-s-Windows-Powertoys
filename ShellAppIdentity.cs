using System;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal static class ShellAppIdentity
    {
        // This must remain stable across upgrades so Start, taskbar pins, and all
        // top-level tool windows are grouped as one application.
        internal const string AppUserModelId = "NaufalTechs.WindowsPowertoys";

        internal static void ApplyToCurrentProcess()
        {
            try
            {
                int result = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
                if (result < 0)
                {
                    Marshal.ThrowExceptionForHR(result);
                }
            }
            catch (Exception exception)
            {
                // Shell grouping is cosmetic; a failure here must never block
                // startup, but is retained in the regular crash diagnostics.
                App.WriteCrashLog(exception, "Setting AppUserModelID");
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
    }
}
