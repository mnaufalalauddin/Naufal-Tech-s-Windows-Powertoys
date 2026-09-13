using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Naufal_Windows_Tech_s_Powertoys;

// The desktop sync client is NOT the OneDrive Store viewer / an AppX family.
internal static class OneDriveAppPolicy
{
    internal const string RecoveryUrl = "https://support.microsoft.com/en-us/onedrive/reinstall-onedrive";
    internal const string Warning = "Uninstalling OneDrive stops syncing. Finish syncing first. A shared installation is removed for all users. Your OneDrive files are not deleted by this tool.";
    internal const string RestoreNotice = "OneDrive restore uses WinGet and the previous installation scope, or this account if no scope was saved. Internet and agreement acceptance are required. Sign in and choose sync folders again afterward.";
    internal const string RecoveryButton = "Microsoft website";
    internal const string SourceConsent = "Continuing allows WinGet to use its official source and accept the source agreements.";
    internal static bool IsScope(string? scope) => scope is "user" or "machine";

    internal static string ReadSavedScope(string? saved)
    {
        if (saved is null) return "user"; // Microsoft's documented installation default.
        if (!IsScope(saved.Trim())) throw new InvalidOperationException("The saved OneDrive installation scope is invalid. No installation was started.");
        return saved.Trim();
    }

    internal static string? ResolveScope(bool restore, IEnumerable<string?> installedScopes, string? saved)
    {
        var scopes = installedScopes.ToArray();
        if (scopes.Length > 1)
            throw new InvalidOperationException("Multiple OneDrive installation scopes were detected. Resolve them in Windows Settings before changing OneDrive here.");
        if (scopes.Any(s => !IsScope(s))) throw new InvalidOperationException("OneDrive installation scope could not be verified.");
        return scopes.Length == 1 ? scopes[0] : restore ? ReadSavedScope(saved) : null;
    }

    internal static string[] Arguments(bool restore, string scope)
    {
        if (!IsScope(scope)) throw new ArgumentException("Unknown OneDrive installation scope.", nameof(scope));
        List<string> args = [restore ? "install" : "uninstall", "--id", "Microsoft.OneDrive", "--exact",
            "--source", "winget", "--scope", scope, "--silent", "--accept-source-agreements", "--disable-interactivity"];
        if (restore) args.Add("--accept-package-agreements");
        return args.ToArray();
    }

    internal static bool IsOfficialSource(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json.Trim().TrimStart('\uFEFF'));
            var source = document.RootElement;
            return source.ValueKind == JsonValueKind.Object &&
                source.GetProperty("Name").GetString() == "winget" &&
                source.GetProperty("Type").GetString() == "Microsoft.PreIndexed.Package" &&
                source.GetProperty("Arg").GetString()?.TrimEnd('/') == "https://cdn.winget.microsoft.com/cache";
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        { return false; }
    }

    internal static BuiltInAppPackage Installed(string scope, bool healthy)
    {
        if (!IsScope(scope)) throw new ArgumentException("Unknown OneDrive installation scope.", nameof(scope));
        return new("Microsoft OneDrive", "", "Microsoft.OneDrive:" + scope, false, false, healthy,
            BuiltInAppKind.OneDriveDesktop, scope);
    }
}
