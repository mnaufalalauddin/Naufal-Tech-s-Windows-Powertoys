using System.Globalization;
using System.Text.RegularExpressions;
using Naufal_Windows_Tech_s_Powertoys;

internal static class Regression
{
    internal static int Run()
    {
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("Localization regression: " + message);
        }
        var options = UiTranslation.LanguageOptions;
        Check(options.Count == 23, "exactly 23 languages");
        Check(options.Select(x => x.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 23, "unique codes");
        Check(UiTranslation.NormalizeLanguageCode(null) == "en", "null fallback");
        Check(UiTranslation.NormalizeLanguageCode("unknown") == "en", "unknown fallback");
        Check(UiTranslation.NormalizeLanguageCode("ZH-tw") == "zh-TW", "case normalization");
        Check(UiTranslation.Translate(null, "id") == "", "null text");
        var english = UiTranslation.GetLanguageTable("en");
        string[] required =
        [
            "Task Monitoring", "No tasks are currently running.", "{0} running task(s).", "60 seconds",
            "Full Repair", "Quick Repair", "Windows Update Fix", "Microsoft Store Fix", "Explorer Fix",
            "Disk Info", "System Report", "Windows Activation", "Office Activation", "Disable Defender",
            "Restore Defender", "BitLocker Manager", "Smart App Control", "Essential Windows Tweaks",
            "Gaming Tweaks", "Games Runtime & Compatibility Check", "GPU Driver Manager",
            "Advanced Windows Tweaks & De-Bloat", "MSI Mode Utility", "Legacy Windows Panels",
            "Apply selected", "Restore selected", "First-Run Setup", "RUN SELECTED", "SKIP FOR NOW",
            "DONT SHOW AGAIN", "OVERALL PROGRESS", "LIVE OUTPUT", "Text scaling", "Copy log",
            "HIGH RISK", "HIGH IMPACT", "EXPERIMENTAL", "SAFE", "LEGACY", "Running", "Failed",
            "Built-in Windows Apps", "Review apps", "Uninstall selected", "Installed", "Not installed", "Needs repair", "Removing",
            "Select Windows apps to uninstall or restore.",
            "Uninstalling may delete local app data and disable app features. Back up your files first.",
            "Only this Windows account is changed. Classic desktop apps, other accounts and provisioned apps are unchanged.",
            "Restore reinstalls apps, not deleted personal data. Internet, Store availability and a valid license may be required.",
            "Continuing allows Microsoft Store downloads and accepts the Store and package agreements. No purchases will be made.",
            "Verify WMI system access", "Verify WMI memory access", "Verify WMI system and memory access",
            "WinGet setup requires Internet access. WMI verification works offline.",
            "Reads operating-system and RAM data directly through WMI on Windows 10 and 11. WMIC is not required and will not be installed or removed."
        ];
        foreach (var option in options)
        {
            string code = option.Code;
            var table = UiTranslation.GetLanguageTable(code);
            const string availabilityKey = "{0} out of {1} have been verified, but {2} tweaks can't be applied due to unavailability on this PC.";
            string availabilityText = UiTranslation.Translate("33 out of 41 have been verified, but 8 tweaks can't be applied due to unavailability on this PC.", code);
            Check(table.ContainsKey(availabilityKey), code + " availability summary translated");
            Check(availabilityText.Contains("33") && availabilityText.Contains("41") && availabilityText.Contains("8") && !availabilityText.Contains('{'), code + " availability numbers preserved including reordered placeholders");
            if (code != "en") Check(availabilityText != "33 out of 41 have been verified, but 8 tweaks can't be applied due to unavailability on this PC.", code + " availability summary is not English fallback");
            UiLocalizedValue badge = new();
            string badgeTranslated = badge.Resolve("33 out of 41 have been verified, but 8 tweaks can't be applied due to unavailability on this PC.", code, UiTranslation.Translate);
            Check(badge.Resolve(badgeTranslated, "en", UiTranslation.Translate) == "33 out of 41 have been verified, but 8 tweaks can't be applied due to unavailability on this PC.", code + " availability badge English round trip");
            Check(UiTranslation.IsSupportedLanguage(code), code + " supported");
            string monitoring = UiTranslation.Translate("7 running task(s).", code);
            Check(monitoring.Contains('7') && !monitoring.Contains('{'), code + " monitoring count formatted");
            if (code != "en") Check(monitoring != "7 running task(s).", code + " monitoring count translated");
            UiLocalizedValue monitorLabel = new();
            string localizedMonitor = monitorLabel.Resolve("Task Monitoring", code, UiTranslation.Translate);
            Check(monitorLabel.Resolve(localizedMonitor, "en", UiTranslation.Translate) == "Task Monitoring", code + " stable monitoring label round trip");
            string monitorTooltip = UiTranslation.Translate("Task Monitoring: IDLE", code);
            Check(monitorTooltip.StartsWith(UiTranslation.Translate("Task Monitoring", code), StringComparison.Ordinal), code + " monitoring tooltip prefix translated without truncation");
            Check(table.Count == english.Count, code + " key count");
            Check(UiTranslation.GetCulture(code).Name == option.CultureName, code + " date culture");
            Check(!string.IsNullOrEmpty(new DateTime(2026, 9, 7).ToString("D", UiTranslation.GetCulture(code))), code + " date formatting");
            Check(UiTranslation.IsRightToLeft(code) == (code is "ar" or "ur"), code + " text direction");
            foreach (string key in required) Check(table.ContainsKey(key), code + " required: " + key);
            foreach (var pair in english)
            {
                Check(table.TryGetValue(pair.Key, out string? value), code + " missing: " + pair.Key);
                Check(!string.IsNullOrWhiteSpace(value), code + " blank: " + pair.Key);
                Check(!value!.Contains('\uFFFD'), code + " corrupt Unicode: " + pair.Key);
                Check(Placeholders(pair.Key).SequenceEqual(Placeholders(value)), code + " placeholders: " + pair.Key);
                UiLocalizedValue tracked = new();
                string initial = tracked.Resolve(pair.Key, code, UiTranslation.Translate);
                Check(initial == UiTranslation.Translate(pair.Key, code), code + " initial rendering");
                Check(tracked.Resolve(initial, "en", UiTranslation.Translate) == pair.Key, code + " English round trip: " + pair.Key);
            }
            string[] originalValues = [@"C:\Windows\SoftwareDistribution", "0x80131501", "Microsoft.WindowsStore_22607.1401.8.0_x64", "893dee8e-2bef-41e0-89c6-b55d0929964c", "powercfg /list", "Naufal Tech's Windows Powertoys", "25%", "200%"];
            foreach (string value in originalValues)
                Check(UiTranslation.Translate(value, code) == value, code + " literal preserved: " + value);
            string formatted = UiTranslation.Translate("37% complete — 2/9", code);
            Check(formatted.Contains("37") && formatted.Contains("2") && formatted.Contains("9"), code + " progress numbers preserved");
            Check(!formatted.Contains('{'), code + " formatted placeholders resolved");
            string elapsed = UiTranslation.Translate("Elapsed: 01:23:45", code);
            Check(elapsed.Contains("01:23:45"), code + " elapsed preserved");
            string summary = UiTranslation.Translate("Analyzed 9/10 | Optimized 3 | Selected 2 | Pending changes 1", code);
            Check(summary.Split(" | ").Length == 4, code + " summary segments preserved");
            Check(UiTranslation.Translate("🔴 LIVE GAMING STATUS", code).StartsWith("🔴 "), code + " red indicator preserved");
            if (code != "en")
            {
                Check(formatted != "37% complete — 2/9", code + " progress translated");
                Check(summary != "Analyzed 9/10 | Optimized 3 | Selected 2 | Pending changes 1", code + " summary translated");
                Check(UiTranslation.Translate("0% complete — Waiting", code) != "0% complete — Waiting", code + " waiting translated");
                Check(UiTranslation.Translate("1. Verify WinGet", code) != "1. Verify WinGet", code + " numbered stage translated");
            }
            foreach (int scale in new[] { 25, 50, 75, 100, 125, 150, 175, 200 })
                Check(UiTranslation.Translate($"Text scaling: {scale}%", code).Contains(scale.ToString(CultureInfo.InvariantCulture)), code + " text scale retained");
        }
        UiLocalizedValue changing = new();
        string rendered = "Applying Taskbar Widgets";
        foreach (var language in options.Concat(options.Reverse()))
        {
            rendered = changing.Resolve(rendered, language.Code, UiTranslation.Translate);
            Check(rendered == UiTranslation.Translate("Applying Taskbar Widgets", language.Code), "repeated language switch: " + language.Code);
        }
        Check(changing.Resolve("Restoring Taskbar Widgets", "id", UiTranslation.Translate) == "Memulihkan: Widget Taskbar", "backend text update replaces canonical source");
        Check(changing.Resolve(changing.Rendered!, "en", UiTranslation.Translate) == "Restoring Taskbar Widgets", "dynamic source English round trip");
        Check(UiTranslation.Translate("Applying failed", "id") == "Gagal", "failed action is not rendered as success");
        Check(UiTranslation.Translate("2 of 9", "ja") == "全 9 件中 2 件", "reordered Japanese placeholders");
        Check(UiTranslation.Translate("Elapsed: 01:23:45", "ar").Contains("\u206801:23:45\u2069"), "RTL isolates Latin numeric value");
        Check(UiTranslation.Translate("Apply selected\r\n\r\nRestore selected", "id") == "Terapkan pilihan\r\n\r\nPulihkan pilihan", "paragraph boundaries preserved");
        string testRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string viewSource = File.ReadAllText(Path.Combine(testRoot, "UiTranslation.cs"));
        Check(!viewSource.Contains("TextBox.TextProperty"), "raw logs and editable input are never translated");
        Check(!viewSource.Contains("VisualTreeHelper."), "do not localize template-generated duplicate TextBlocks");
        Check(viewSource.Contains("RegisterPropertyChangedCallback"), "dynamic authored display updates observed");
        Check(viewSource.Contains("ToolTipService.ToolTipProperty"), "tooltips localized");
        Check(viewSource.Contains("AutomationProperties.NameProperty"), "accessibility captions localized");
        Check(File.ReadAllText(Path.Combine(testRoot, "ToolWindow.cs")).Contains("_headingText.Text = _canonicalTitle;"), "tool heading retains canonical source");
        Check(!File.ReadAllText(Path.Combine(testRoot, "MainWindow.xaml.cs")).Contains("bool indonesian ="), "wizard not limited to two languages");
        string mainSource = File.ReadAllText(Path.Combine(testRoot, "MainWindow.xaml.cs"));
        Check(!Regex.IsMatch(mainSource, @"\.Text\s*\+="), "never append new canonical text to a translated display property");
        Check(mainSource.Contains("UiTranslation.Release(RootLayout);"), "main window localization cleanup");
        Check(File.ReadAllText(Path.Combine(testRoot, "ToolWindow.cs")).Contains("UiTranslation.Release(_root);"), "tool window localization cleanup");
        assertions += LanguageSwitchRegression.Run();
        return assertions;
    }

    internal static string[] Placeholders(string value) => Regex.Matches(value,
        @"(?<!\{)\{([A-Za-z_][A-Za-z_0-9]*|\d+)(?:,[+-]?\d+)?(?::[^{}]+)?\}(?!\})")
        .Select(match => match.Groups[1].Value).Order(StringComparer.Ordinal).ToArray();
}
