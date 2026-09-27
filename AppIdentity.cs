using System;
using System.Reflection;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class AppIdentity
{
    internal const string Product = "Naufal Windows Utility";
    internal const string Developer = "Muhammad Naufal Alauddin";
    // Verified from this repository's origin remote.
    internal const string SourceUrl = "https://github.com/mnaufalalauddin/Naufal-Windows-Utility";
    internal static string Version => typeof(AppIdentity).Assembly.GetName().Version?.ToString() ?? "";
    internal static string DisplayVersion => "v" + Version;
}
