using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Naufal_Windows_Tech_s_Powertoys;

internal enum BuiltInAppKind { Appx, OneDriveDesktop }
internal sealed record BuiltInAppTarget(string Id, string Name, IReadOnlyList<string> Families, BuiltInAppKind Kind = BuiltInAppKind.Appx);
internal sealed record BuiltInAppPackage(string Name, string Family, string FullName, bool IsFramework, bool IsResource,
    bool Healthy = true, BuiltInAppKind Kind = BuiltInAppKind.Appx, string? Scope = null);

internal static class BuiltInAppsCatalog
{
    internal const string Title = "Built-in Windows Apps";
    internal const string Description = "Select Windows apps to uninstall or restore.";
    internal const string Warning = "Uninstalling may delete local app data and disable app features. Back up your files first.";
    internal const string Scope = "Store apps affect this account only. OneDrive follows its installation scope; a shared installation affects all users.";
    internal const string RestoreNotice = "Restore reinstalls apps, not deleted personal data. Internet, Store availability and a valid license may be required.";
    internal const string StoreConsent = "Continuing allows Microsoft Store downloads and accepts the Store and package agreements. No purchases will be made.";

    // Product pages checked against Microsoft's Store catalog (see BUILT_IN_APPS.md).
    // Never search by a fuzzy display name or substitute paid/third-party apps.
    // HEVC OEM deliberately requires an explicit Store/license interaction.
    internal static string? StoreProductId(string id) => id switch
    {
        "AV1" => "9MVZQVXJBQ9V", "AVC" => "9PB0TRCNRHFX", "Clock" => "9WZDNCRFJ3PR",
        "Feedback" => "9NBLGGH4R32N", "HEIF" => "9PMMSR1CGPWG", "MediaPlayer" => "9WZDNCRFJ3PT",
        "Bing" => "9NZBF4GT040C", "Clipchamp" => "9P1J8S7CCWWT", "Family" => "9PDJDJS743XF",
        "News" => "9WZDNCRFHVFW", "Todo" => "9NBLGGH5R558", "Outlook" => "9NRX63209R7B",
        "Paint" => "9PCFS5B6T72H", "Photos" => "9WZDNCRFJBH4", "PowerAutomate" => "9NFTCH6J7FHV",
        "QuickAssist" => "9P7BP5VNWKX5", "Solitaire" => "9WZDNCRFHWD2", "Recorder" => "9WZDNCRFHWKN",
        "StartExperiences" => "9PC1H9VN18CM", "StickyNotes" => "9NBLGGH4QGHW", "VP9" => "9N4D0MSMP0PT",
        "Weather" => "9WZDNCRFJ3Q2", "WebMedia" => "9N5TDP8VCMHS", "WebP" => "9PG2DK419DRG",
        "Notepad" => "9MSMLRH6LZF3", _ => null
    };

    internal static Uri StoreUri(BuiltInAppTarget target)
    {
        var approved = ResolveSelection([target.Id]).Single();
        if (approved.Kind == BuiltInAppKind.OneDriveDesktop)
            return new Uri(OneDriveAppPolicy.RecoveryUrl);
        return new Uri("ms-windows-store://pdp/?" + (StoreProductId(approved.Id) is string product
            ? "ProductId=" + product : "PFN=" + Uri.EscapeDataString(approved.Families[0])));
    }

    internal static string[] InstallArguments(string id)
    {
        string product = StoreProductId(ResolveSelection([id]).Single().Id)
            ?? throw new InvalidOperationException("This app requires Microsoft Store recovery.");
        return ["install", "--id", product, "--exact", "--source", "msstore", "--scope", "user",
            "--silent", "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity"];
    }

    internal static bool IsMicrosoftStoreSource(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json.Trim().TrimStart('\uFEFF'));
            var source = document.RootElement;
            return source.ValueKind == JsonValueKind.Object &&
                source.GetProperty("Name").GetString() == "msstore" &&
                source.GetProperty("Type").GetString() == "Microsoft.Rest" &&
                source.GetProperty("Arg").GetString()?.TrimEnd('/') == "https://storeedgefd.dsx.mp.microsoft.com/v9.0";
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        { return false; }
    }

    // Exact family identities, not display-name matching or wildcards. The OEM
    // HEVC package is singular (VideoExtension); the paid plural package is not
    // implicitly included. Classic Win32 Paint/Notepad/Teams are not targets.
    internal static readonly IReadOnlyList<BuiltInAppTarget> Targets = Array.AsReadOnly(new[]
    {
        App("AV1", "AV1 Video Extension", "Microsoft.AV1VideoExtension"),
        App("AVC", "AVC Encoder Video Extension", "Microsoft.AVCEncoderVideoExtension"),
        App("Clock", "Clock", "Microsoft.WindowsAlarms"),
        App("DevHome", "Dev Home", "Microsoft.Windows.DevHome"),
        App("Feedback", "Feedback Hub", "Microsoft.WindowsFeedbackHub"),
        App("GetHelp", "Get Help", "Microsoft.GetHelp"),
        App("HEIF", "HEIF Image Extension", "Microsoft.HEIFImageExtension"),
        App("HEVC", "HEVC Video Extension from Device Manufacturer", "Microsoft.HEVCVideoExtension"),
        App("MediaPlayer", "Media Player", "Microsoft.ZuneMusic"),
        App("Bing", "Microsoft Bing", "Microsoft.BingSearch"),
        new BuiltInAppTarget("Clipchamp", "Microsoft Clipchamp", Array.AsReadOnly(new[] { "Clipchamp.Clipchamp_yxz26nhyzhsrt" })),
        App("Family", "Microsoft Family", "MicrosoftCorporationII.MicrosoftFamily"),
        App("News", "Microsoft News", "Microsoft.BingNews"),
        new BuiltInAppTarget("Teams", "Microsoft Teams", Array.AsReadOnly(new[] { "MSTeams_8wekyb3d8bbwe", "MicrosoftTeams_8wekyb3d8bbwe" })),
        App("Todo", "Microsoft To Do", "Microsoft.Todos"),
        new BuiltInAppTarget("MobileDevices", "Mobile Devices", Array.AsReadOnly(new[] { "MicrosoftWindows.CrossDevice_cw5n1h2txyewy" })),
        App("Outlook", "Outlook for Windows", "Microsoft.OutlookForWindows"),
        App("Paint", "Paint", "Microsoft.Paint"),
        App("PhoneLink", "Phone Link", "Microsoft.YourPhone"),
        App("Photos", "Photos", "Microsoft.Windows.Photos"),
        App("PowerAutomate", "Power Automate", "Microsoft.PowerAutomateDesktop"),
        App("QuickAssist", "Quick Assist", "MicrosoftCorporationII.QuickAssist"),
        App("Solitaire", "Solitaire & Casual Games", "Microsoft.MicrosoftSolitaireCollection"),
        App("Recorder", "Sound Recorder", "Microsoft.WindowsSoundRecorder"),
        App("StartExperiences", "Start Experiences App", "Microsoft.StartExperiencesApp"),
        App("StickyNotes", "Sticky Notes", "Microsoft.MicrosoftStickyNotes"),
        App("VP9", "VP9 Video Extensions", "Microsoft.VP9VideoExtensions"),
        App("Weather", "Weather", "Microsoft.BingWeather"),
        App("WebMedia", "Web Media Extensions", "Microsoft.WebMediaExtensions"),
        App("WebP", "WebP Image Extension", "Microsoft.WebpImageExtension"),
        App("Notepad", "Windows Notepad", "Microsoft.WindowsNotepad"),
        new BuiltInAppTarget("OneDrive", "Microsoft OneDrive", Array.Empty<string>(), BuiltInAppKind.OneDriveDesktop)
    });

    private static BuiltInAppTarget App(string id, string name, string packageName) =>
        new(id, name, Array.AsReadOnly(new[] { packageName + "_8wekyb3d8bbwe" }));

    internal static bool Matches(BuiltInAppTarget target, BuiltInAppPackage package)
    {
        if (target.Kind != package.Kind) return false;
        if (target.Kind == BuiltInAppKind.OneDriveDesktop)
            return target.Id == "OneDrive" && package.Name == "Microsoft OneDrive" && package.Family.Length == 0 &&
                !package.IsFramework && !package.IsResource && OneDriveAppPolicy.IsScope(package.Scope) &&
                package.FullName == "Microsoft.OneDrive:" + package.Scope;
        if (package.IsFramework || package.IsResource ||
            !target.Families.Contains(package.Family, StringComparer.OrdinalIgnoreCase)) return false;
        int separator = package.Family.LastIndexOf('_');
        return separator > 0 &&
            string.Equals(package.Name, package.Family[..separator], StringComparison.OrdinalIgnoreCase) &&
            package.FullName.StartsWith(package.Name + "_", StringComparison.OrdinalIgnoreCase) &&
            package.FullName.EndsWith(package.Family[separator..], StringComparison.OrdinalIgnoreCase) &&
            package.FullName.IndexOfAny(['\\', '/', ':', '*', '?']) < 0;
    }

    internal static IReadOnlyList<BuiltInAppTarget> ResolveSelection(IEnumerable<string> ids)
    {
        string[] selected = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0 || selected.Any(id => !Targets.Any(t => t.Id == id)))
            throw new ArgumentException("Select at least one approved application.");
        return Targets.Where(t => selected.Contains(t.Id, StringComparer.Ordinal)).ToArray();
    }
}
