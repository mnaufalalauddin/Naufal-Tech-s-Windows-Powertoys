using System;
using System.Collections.Generic;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal enum AppRemovalRecommendation { Recommended, Optional, NotRecommended }

internal static class BuiltInAppsExpansion
{
    // Stable logical IDs preserve the original catalog's restore/audit identities.
    // Names after the fourth separator are exact AppX Identity.Name aliases, not wildcards.
    // Short IDs from older debloat lists are expanded explicitly; never substring-search packages.
    internal const string Entries = """
Builder3D|3D Builder|Microsoft.3DBuilder|Optional
Viewer3D|3D Viewer|Microsoft.Microsoft3DViewer|Optional
ACG|ACG Media Player|ACGMediaPlayer|Optional|A278AB0D.ACGMediaPlayer
Actipro|Actipro Software|ActiproSoftwareLLC|NotRecommended|ActiproSoftwareLLC.562882FEEB491
AdobeExpress|Adobe Photoshop Express|AdobeSystemsIncorporated.AdobePhotoshopExpress|Optional
Clock|Alarms & Clock|Microsoft.WindowsAlarms|Optional
Amazon|Amazon|Amazon.com.Amazon|Recommended
Asphalt|Asphalt 8|Asphalt8Airborne|Recommended|GAMELOFTSA.Asphalt8Airborne
SketchBook|Autodesk SketchBook|AutodeskSketchBook|Optional|AutodeskInc.AutodeskSketchBook
BingFinance|Bing Finance|Microsoft.BingFinance|Recommended
BingFood|Bing Food And Drink|Microsoft.BingFoodAndDrink|Recommended
BingHealth|Bing Health And Fitness|Microsoft.BingHealthAndFitness|Recommended
News|Bing News / Microsoft News|Microsoft.BingNews|Optional|Microsoft.News
Bing|Bing Search|Microsoft.BingSearch|Optional
BingSports|Bing Sports|Microsoft.BingSports|Recommended
BingTranslator|Bing Translator|Microsoft.BingTranslator|Optional
BingTravel|Bing Travel|Microsoft.BingTravel|Recommended
Weather|Bing Weather|Microsoft.BingWeather|Optional
BubbleWitch|Bubble Witch 3|king.com.BubbleWitch3Saga|Recommended
Caesars|Caesars Slots|CaesarsSlotsFreeCasino|Recommended|Playtika.CaesarsSlotsFreeCasino
CandyCrush|Candy Crush Saga|king.com.CandyCrushSaga|Recommended
CandySoda|Candy Crush Soda|king.com.CandyCrushSodaSaga|Recommended
Clipchamp|Microsoft Clipchamp|Clipchamp.Clipchamp|Optional
CookingFever|Cooking Fever|COOKINGFEVER|Recommended|Nordcurrent.COOKINGFEVER
AIHub|Copilot+ AI Hub|Microsoft.Windows.AIHub|Optional
Cortana|Cortana|Microsoft.549981C3F5F10|Recommended
MobileDevices|Cross Device Experience|MicrosoftWindows.CrossDevice|Optional
Cyberlink|Cyberlink Media Suite|CyberLinkMediaSuiteEssentials|Optional|CyberLinkCorp.CyberLinkMediaSuiteEssentials
DellDelivery|Dell Digital Delivery Services|DellInc.DellDigitalDelivery|NotRecommended
DellMobile|Dell Mobile Connect|DellInc.DellMobileConnect|Optional
DellSupport|Dell SupportAssist|DellInc.DellSupportAssistforPCs|NotRecommended
DisneyKingdoms|Disney Magic Kingdoms|DisneyMagicKingdoms|Recommended|GAMELOFTSA.DisneyMagicKingdoms
DisneyPlus|Disney+|Disney.37853FC22B2CE|Optional
Drawboard|Drawboard PDF|DrawboardPDF|Optional|Drawboard.DrawboardPDF
Duolingo|Duolingo|Duolingo-LearnLanguagesforFree|Optional|D5EA27B7.Duolingo-LearnLanguagesforFree
Eclipse|Eclipse Manager|EclipseManager|Optional|46928bounde.EclipseManager
Facebook|Facebook|FACEBOOK.FACEBOOK|Optional
Family|Family Safety|MicrosoftCorporationII.MicrosoftFamily|NotRecommended
FarmVille|FarmVille 2|FarmVille2CountryEscape|Recommended|9E2F88E3.FarmVille2CountryEscape
Flipboard|Flipboard|Flipboard|Optional|Flipboard.Flipboard
GetHelp|Get Help|Microsoft.GetHelp|Optional
GetStarted|Get Started|Microsoft.GetStarted|Optional
HiddenCity|Hidden City|HiddenCity|Recommended|828B5831.HiddenCityMysteryofShadows
HPAI|HP AI Experience Center|AD2F1837.HPAIExperienceCenter|Optional
HPMusic|HP Connected Music|AD2F1837.HPConnectedMusic|Optional
HPPhoto|HP Connected Photo|AD2F1837.HPConnectedPhoto|Optional|AD2F1837.HPConnectedPhotopoweredbySnapfish
HPDesktop|HP Desktop Support Utilities|AD2F1837.HPDesktopSupportUtilities|NotRecommended
HPClean|HP Easy Clean|AD2F1837.HPEasyClean|Optional
HPFiles|HP File Viewer|AD2F1837.HPFileViewer|Optional
HPJump|HP JumpStarts|AD2F1837.HPJumpStarts|Recommended
HPDiagnostics|HP PC Hardware Diagnostics|AD2F1837.HPPCHardwareDiagnosticsWindows|NotRecommended
HPPower|HP Power Manager|AD2F1837.HPPowerManager|NotRecommended
HPPrinter|HP Printer Control|AD2F1837.HPPrinterControl|NotRecommended
HPPrivacy|HP Privacy Settings|AD2F1837.HPPrivacySettings|NotRecommended
HPDrop|HP QuickDrop|AD2F1837.HPQuickDrop|Optional
HPTouch|HP QuickTouch|AD2F1837.HPQuickTouch|NotRecommended
HPRegistration|HP Registration|AD2F1837.HPRegistration|Recommended
HPSupport|HP Support Assistant|AD2F1837.HPSupportAssistant|NotRecommended
HPShield|HP Sure Shield AI|AD2F1837.HPSureShieldAI|NotRecommended
HPSystem|HP System Information|AD2F1837.HPSystemInformation|Optional
HPWelcome|HP Welcome|AD2F1837.HPWelcome|Recommended
HPWork|HP WorkWell|AD2F1837.HPWorkWell|Optional
Hulu|Hulu|HULULLC.HULUPLUS|Optional
IHeart|IHeartRadio|iHeartRadio|Optional|ClearChannelRadioDigital.iHeartRadio
Instagram|Instagram|Facebook.Instagram|Optional
LenovoVantage|Lenovo Vantage|E046963F.LenovoCompanion|NotRecommended
LenovoService|Lenovo Vantage Service|LenovoCompanyLimited.LenovoVantageService|NotRecommended
LGMonitor|LG Monitor App|LGElectronics.LGMonitorApp|NotRecommended
LinkedIn|LinkedIn|LinkedinforWindows|Optional|7EE7776C.LinkedInforWindows
Wallpaper|Live Wallpaper|Sidia.LiveWallpaper|Optional
Mail|Mail & Calendar|Microsoft.windowscommunicationsapps|Optional
March|March of Empires|MarchofEmpires|Recommended|GAMELOFTSA.MarchofEmpires
MediaPlayer|Media Player|Microsoft.ZuneMusic|Optional
Messaging|Messaging|Microsoft.Messaging|Recommended
M365Companions|Microsoft 365 Companions|Microsoft.M365Companions|Optional
Copilot|Microsoft Copilot|XP9CXNGPPJ97XX|Optional|Microsoft.Copilot
Journal|Microsoft Journal|Microsoft.MicrosoftJournal|Optional
PCManager|Microsoft PC Manager|Microsoft.PCManager|Optional
Teams|Microsoft Teams|MSTeams|Optional|MicrosoftTeams
MixedReality|Mixed Reality Portal|Microsoft.MixedReality.Portal|NotRecommended
Movies|Movies & TV|Microsoft.ZuneVideo|Optional
MyHP|myHP|AD2F1837.myHP|NotRecommended
Netflix|Netflix|4DF9E0F8.Netflix|Optional
SpeedTest|Network Speed Test|Microsoft.NetworkSpeedTest|Optional
NYT|NYT Crossword|NYTCrossword|Recommended|TheNewYorkTimes.NYTCrossword
OfficeHub|Office Hub|Microsoft.MicrosoftOfficeHub|Optional
OneCalendar|One Calendar|OneCalendar|Optional|CodeSpark.OneCalendar
OneConnect|One Connect|Microsoft.OneConnect|Optional
OneDrive|Microsoft OneDrive|Microsoft.OneDrive|NotRecommended
OneNote|OneNote|Microsoft.Office.OneNote|NotRecommended
Outlook|Outlook for Windows|Microsoft.OutlookForWindows|Optional
Paint|Paint|Microsoft.Paint|Optional
Paint3D|Paint 3D|Microsoft.MSPaint|Optional
Pandora|Pandora|PandoraMediaInc|Optional|PandoraMediaInc.29680B314EFC2
People|People|Microsoft.People|Optional
PhoneLink|Phone Link|Microsoft.YourPhone|Optional
Photos|Photos|Microsoft.Windows.Photos|NotRecommended
Phototastic|Phototastic Collage|PhototasticCollage|Optional|ThumbmunkeysLtd.PhototasticCollage
PicsArt|PicsArt|PicsArt-PhotoStudio|Optional|PicsArt-PhotoStudio
Polarr|Polarr Photo Studio|PolarrPhotoEditorAcademicEdition|Optional|Polarr.PolarrPhotoEditorAcademicEdition
PowerAutomate|Power Automate|Microsoft.PowerAutomateDesktop|Optional
PowerBI|Power BI|Microsoft.MicrosoftPowerBIForWindows|Optional
PrimeVideo|Prime Video|AmazonVideo.PrimeVideo|Optional
Print3D|Print 3D|Microsoft.Print3D|Optional
QuickAssist|Quick Assist|MicrosoftCorporationII.QuickAssist|Optional
RemoteDesktop|Remote Desktop|Microsoft.RemoteDesktop|Optional
RoyalRevolt|Royal Revolt|flaregamesGmbH.RoyalRevolt|Recommended|flaregamesGmbH.RoyalRevolt2
Skype|Skype (UWP)|Microsoft.SkypeApp|Recommended
Sling|Sling TV|SlingTV|Optional|SlingTVLLC.SlingTV
Solitaire|Solitaire & Casual Games|Microsoft.MicrosoftSolitaireCollection|Recommended
Recorder|Sound Recorder|Microsoft.WindowsSoundRecorder|Optional
Spotify|Spotify|SpotifyAB.SpotifyMusic|Optional
StickyNotes|Sticky Notes|Microsoft.MicrosoftStickyNotes|NotRecommended
Sway|Sway|Microsoft.Office.Sway|Optional
TikTok|TikTok|BytedancePte.Ltd.TikTok|Recommended
TuneIn|TuneIn Radio|TuneInRadio|Optional|TuneIn.TuneInRadio
Whiteboard|Whiteboard|Microsoft.Whiteboard|Optional
StartExperiences|Widgets Experience|Microsoft.StartExperiencesApp|Optional
WidgetsRuntime|Widgets Platform Runtime|Microsoft.WidgetsPlatformRuntime|NotRecommended
Maps|Windows Maps|Microsoft.WindowsMaps|Optional
WebExperience|Windows Web Experience Pack|MicrosoftWindows.Client.WebExperience|NotRecommended
WinZip|WinZip|WinZipUniversal|Optional|WinZipComputing.WinZipUniversal
XboxCompanion|Xbox Console Companion|Microsoft.XboxApp|Optional
XboxGameOverlay|Xbox Game Overlay|Microsoft.XboxGameOverlay|Optional
XboxGaming|Xbox Gaming App|Microsoft.GamingApp|NotRecommended
XboxOverlay|Xbox Gaming Overlay|Microsoft.XboxGamingOverlay|Optional
XboxIdentity|Xbox Identity Provider|Microsoft.XboxIdentityProvider|NotRecommended
XboxSpeech|Xbox Speech To Text|Microsoft.XboxSpeechToTextOverlay|NotRecommended
XboxTCUI|Xbox TCUI Framework|Microsoft.Xbox.TCUI|NotRecommended
""";

    internal static IReadOnlyList<BuiltInAppTarget> Merge(IEnumerable<BuiltInAppTarget> original)
    {
        var result = original.ToDictionary(t => t.Id, StringComparer.Ordinal);
        foreach (string line in Entries.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] fields = line.Split('|');
            result.TryGetValue(fields[0], out var previous);
            var names = fields.Skip(4).Prepend(fields[2]).Where(n => n != "XP9CXNGPPJ97XX")
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            result[fields[0]] = (previous ?? new(fields[0], fields[1], Array.Empty<string>())) with
            {
                Name = fields[1], Recommendation = Enum.Parse<AppRemovalRecommendation>(fields[3]),
                AppId = fields[2], ExactPackageNames = previous is null || fields[0] == "News" ? names : Array.Empty<string>()
            };
        }
        foreach (string id in new[] { "AV1", "AVC", "HEIF", "HEVC", "VP9", "WebMedia", "WebP", "Notepad" })
            result[id] = result[id] with { Recommendation = AppRemovalRecommendation.NotRecommended };
        return Array.AsReadOnly(result.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    internal static string Label(AppRemovalRecommendation value) => value switch
    {
        AppRemovalRecommendation.Recommended => "Recommended",
        AppRemovalRecommendation.NotRecommended => "Not Recommended",
        _ => "Optional"
    };
    internal static string Reason(AppRemovalRecommendation value) => value switch
    {
        AppRemovalRecommendation.Recommended => "Remove if unused. Personal data and later Store availability are not guaranteed.",
        AppRemovalRecommendation.NotRecommended => "Keep unless you understand the impact on hardware, security, gaming, media support or saved data.",
        _ => "Personal choice: keep this app if you use its features. Back up local data before removal."
    };
}
