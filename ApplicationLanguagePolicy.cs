using System;
using System.Globalization;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class ApplicationLanguagePolicy
{
    internal static void Initialize(Action<string> setFrameworkLanguage, Action<Exception> reportWarning)
    {
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        try { setFrameworkLanguage("en-US"); }
        catch (Exception exception)
        {
            // A resource-language preference must never prevent startup.
            // Authored strings and root.Language remain English independently.
            reportWarning(exception);
        }
    }
}
