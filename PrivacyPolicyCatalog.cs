using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record PrivacyPolicySetting(RegistryHive Hive, string Path, string Name, int Value);
internal sealed record PrivacyPolicyOption(string Id, string Name, string Description, string Warning,
    string Support, IReadOnlyList<PrivacyPolicySetting> Settings);

internal static class PrivacyPolicyCatalog
{
    private const RegistryHive HKLM = RegistryHive.LocalMachine, HKCU = RegistryHive.CurrentUser;
    private const string Edge = @"SOFTWARE\Policies\Microsoft\Edge";
    private const string Content = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    internal const string VerificationNotice = "Verifies saved configuration, not the behavior of every Windows or browser version. Restart the affected app; managed policies may override local settings.";
    internal const string EncryptionWarning = "HIGH RISK: Future device encryption will not start automatically. Existing encrypted drives stay encrypted. This does not decrypt drives, remove recovery keys, or override organizational encryption requirements. Not recommended for devices using Recall.";

    // Restore with no snapshot removes these explicit policy/preferences overrides
    // (Not configured), not an invented OEM default. Sources: CATALOG_EXPANSION_2026-09-14.md.
    internal static readonly IReadOnlyList<PrivacyPolicyOption> Options = Array.AsReadOnly(new[]
    {
        P("LocationAppAccess", "App Location Access", "Blocks Windows location policy and app location permission; merged with Location Tracking.",
            "Location-dependent apps, maps, automatic time-zone detection and nearby-device functions can stop working.", "Pro",
            S(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1),
            S(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessLocation", 2)),
        P("PaintAI", "Paint AI Features", "Disables Paint Cocreator, Generative Fill and Image Creator through the documented Paint policy location.",
            "Applies to supported, updated Windows 11 Paint versions. This does not remove every non-generative image editing tool.", "PaintAI",
            S(HKLM, @"Software\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableCocreator", 1),
            S(HKLM, @"Software\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableGenerativeFill", 1),
            S(HKLM, @"Software\Microsoft\Windows\CurrentVersion\Policies\Paint", "DisableImageCreator", 1)),
        P("FindMyDevice", "Find My Device", "Disables cloud location registration used by Find My Device.",
            "You may no longer locate this PC or its pen after loss or theft.", "Pro",
            S(HKLM, @"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0)),
        P("LockScreenTips", "Lock Screen Tips", "Disables lock-screen overlay facts, tips and suggestions for this account.", "", "Any",
            S(HKCU, Content, "SubscribedContent-338387Enabled", 0), S(HKCU, Content, "RotatingLockScreenOverlayEnabled", 0)),
        P("Settings365Ads", "Settings Home Microsoft 365 Promotions", "Disables cloud consumer-account promotional content, including supported Settings Home offers. Not a universal ad blocker.",
            "Microsoft documents this policy for Windows 11 Enterprise, Education and IoT Enterprise, not Home or Pro.", "Enterprise11",
            S(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", 1)),
        P("BingSearch", "Bing Web Search", "Disables web search suggestions and legacy Cortana integration while retaining local file and app search. Copilot removal is part of Windows AI.",
            "Search behavior varies by Windows build; restart Explorer or sign out after applying.", "Any",
            S(HKCU, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1),
            S(HKLM, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0)),
        P("PhoneLinkStart", "Phone Link in Start", "Hides the mobile-device companion panel in Start without uninstalling Phone Link or unlinking your phone.",
            "Requires a Windows build that exposes the mobile-device panel in Start.", "PhoneStart",
            S(HKCU, @"Software\Microsoft\Windows\CurrentVersion\Start\Companions\Microsoft.YourPhone_8wekyb3d8bbwe", "IsEnabled", 0)),
        P("EdgePromotions", "Edge Ads & Newsfeed", "Disables Edge new-tab news content, default sponsored/top sites, shopping assistance, and browser recommendations.",
            "Edge policies do not block advertisements inside arbitrary websites. Verify supported policies at edge://policy.", "Edge",
            S(HKLM, Edge, "NewTabPageContentEnabled", 0), S(HKLM, Edge, "NewTabPageHideDefaultTopSites", 1),
            S(HKLM, Edge, "EdgeShoppingAssistantEnabled", 0), S(HKLM, Edge, "ShowRecommendationsEnabled", 0)),
        P("EdgeAI", "Edge AI Features", "Disables supported Copilot/sidebar integration, page-context sharing, inline compose, AI history search and local GenAI model download.",
            "AI policies are version-dependent. This does not block access to AI websites or disable browser security updates.", "Edge",
            S(HKLM, Edge, "HubsSidebarEnabled", 0), S(HKLM, Edge, "CopilotPageContext", 0),
            S(HKLM, Edge, "CopilotCDPPageContext", 0), S(HKLM, Edge, "EdgeEntraCopilotPageContext", 0),
            S(HKLM, Edge, "ComposeInlineEnabled", 0), S(HKLM, Edge, "EdgeHistoryAISearchEnabled", 0),
            S(HKLM, Edge, "GenAILocalFoundationalModelSettings", 1), S(HKLM, Edge, "NewTabPageBingChatEnabled", 0)),
        P("BraveExtras", "Brave AI, Crypto & Extras", "Disables Leo AI, Wallet, Rewards, VPN, Talk and News using Brave policies. Shields and browser updates are left enabled.",
            "Export wallet recovery information first. Crypto-wallet access, rewards and the listed browser services will be unavailable until restored.", "Brave",
            S(HKLM, @"SOFTWARE\Policies\BraveSoftware\Brave", "BraveAIChatEnabled", 0),
            S(HKLM, @"SOFTWARE\Policies\BraveSoftware\Brave", "BraveWalletDisabled", 1),
            S(HKLM, @"SOFTWARE\Policies\BraveSoftware\Brave", "BraveRewardsDisabled", 1),
            S(HKLM, @"SOFTWARE\Policies\BraveSoftware\Brave", "BraveVPNDisabled", 1),
            S(HKLM, @"SOFTWARE\Policies\BraveSoftware\Brave", "BraveTalkDisabled", 1),
            S(HKLM, @"SOFTWARE\Policies\BraveSoftware\Brave", "BraveNewsDisabled", 1)),
        P("PreventDeviceEncryption", "BitLocker Automatic Device Encryption", "Apply prevents future automatic device encryption. Restore replays the saved setting, or removes the override when no backup exists. Existing encryption is unchanged.",
            EncryptionWarning, "Any", S(HKLM, @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1))
    });

    private static PrivacyPolicySetting S(RegistryHive hive, string path, string name, int value) => new(hive, path, name, value);
    private static PrivacyPolicyOption P(string id, string name, string description, string warning, string support,
        params PrivacyPolicySetting[] settings) => new(id, name, description, warning, support, Array.AsReadOnly(settings));

    internal static bool EditionSupports(string support, int build, string edition) => support switch
    {
        "Enterprise11" => build >= 22000 && (edition.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) ||
            edition.Contains("Education", StringComparison.OrdinalIgnoreCase)),
        "Pro" => build >= 17763 && (edition.Contains("Professional", StringComparison.OrdinalIgnoreCase) ||
            edition.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) || edition.Contains("Education", StringComparison.OrdinalIgnoreCase)),
        "PhoneStart" => build >= 22631,
        "PaintAI" => build >= 22621 && (edition.Contains("Professional", StringComparison.OrdinalIgnoreCase) ||
            edition.Contains("Enterprise", StringComparison.OrdinalIgnoreCase) || edition.Contains("Education", StringComparison.OrdinalIgnoreCase)),
        _ => build >= 17763
    };
}
