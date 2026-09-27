using Naufal_Windows_Tech_s_Powertoys;
using System.Globalization;

internal static class StartupLanguageTests
{
    internal static void Run(Action<bool, string> assert)
    {
        CultureInfo? original = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            string? selected = null;
            ApplicationLanguagePolicy.Initialize(value => selected = value, _ => throw new Exception("Unexpected warning"));
            assert(selected == "en-US", "Startup selects English framework resources");
            assert(CultureInfo.DefaultThreadCurrentUICulture?.Name == "en-US", "Startup sets English managed UI culture");
            foreach (Exception failure in new Exception[] { new InvalidOperationException("Unpackaged API rejected"), new System.Runtime.InteropServices.COMException("Resource service unavailable") })
            {
                Exception? reported = null;
                ApplicationLanguagePolicy.Initialize(_ => throw failure, error => reported = error);
                assert(ReferenceEquals(failure, reported), "Framework preference failure is reported without blocking startup");
                assert(CultureInfo.DefaultThreadCurrentUICulture?.Name == "en-US", "English authored UI survives resource preference failure");
            }
        }
        finally { CultureInfo.DefaultThreadCurrentUICulture = original; }
    }
}
