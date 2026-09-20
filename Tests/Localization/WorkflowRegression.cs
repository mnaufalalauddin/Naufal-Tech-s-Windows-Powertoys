using System.Text.RegularExpressions;
using Microsoft.UI.Xaml.Controls;
using Naufal_Windows_Tech_s_Powertoys;

internal static class WorkflowRegression
{
    internal static int Run(string root)
    {
        int assertions = 0;
        void Check(bool ok, string message)
        {
            assertions++;
            if (!ok) throw new InvalidOperationException("Workflow localization: " + message);
        }
        string[] files = ["Workflow", "WorkflowResults", "WorkflowDialogs", "GamingDescriptions", "PolicyNotices", "DashboardLabels", "RepairConfirmations", "RuntimeStatus", "PrivacyWarnings", "PrivacyRemainingDescriptions", "About", "EssentialRemainingActions", "GamingRemainingConfirmations", "CommonResults", "EssentialActionResults", "AvailabilityReasons", "GamingStatusResults", "RegistryReadStates"];
        foreach (string file in files.Concat(new[] { "OperationOutcomes", "EssentialRemainingTweaks", "PhotoViewerMessages", "EssentialOutcomes", "AdvancedDescriptions", "GamingRemainingDescriptions", "ServiceDescriptions", "GamingBootDescriptions", "DebloatDetails", "RestoreResults", "VerificationStates", "EssentialLabDescriptions", "GamingLabGpuDescriptions", "GamingLabCoreDescriptions", "RepairOutcomes", "RuntimeCompositions", "RuntimeAnalysis", "CatalogConfirmations" }))
        {
            string source = File.ReadAllText(Path.Combine(root, $"NativeUiCatalog.{file}.cs"));
            Match data = Regex.Match(source, "\"\"\"[\\r\\n]+(?<data>[\\s\\S]*?)[\\r\\n]+\"\"\";");
            Check(data.Success, file + " has auditable UTF-8 rows");
            string[] keys = data.Groups["data"].Value.Split('\n')[0].TrimEnd('\r').Split('|')[1..];
            foreach (string key in keys)
            foreach (var language in UiTranslation.LanguageOptions)
            {
                var table = UiTranslation.GetLanguageTable(language.Code);
                Check(table.ContainsKey(key), language.Code + " exact resource: " + key);
                // Standard technical units may be shared across locales and are
                // not UI prose fallback. Other rows must be localized.
                if (language.Code != "en" && key != "{0} Mbps" && key != "Version {0}")
                    Check(table[key] != key, language.Code + " no English copy for " + file + ": " + key);
            }
            if (file == "GamingDescriptions")
            {
                string gaming = File.ReadAllText(Path.Combine(root, "GamingTweaksService.cs"));
                foreach (string key in keys)
                    Check(gaming.Contains("\"" + key + "\"", StringComparison.Ordinal),
                        "Gaming description must exactly match the current catalog source");
            }
            if (file is "CommonResults" or "EssentialActionResults" or "AvailabilityReasons" or "GamingStatusResults" or "RegistryReadStates")
            {
                string backend = string.Join("\n", Directory.EnumerateFiles(root, "*.cs")
                    .Where(path => !Path.GetFileName(path).StartsWith("NativeUiCatalog", StringComparison.Ordinal) &&
                        !Path.GetFileName(path).StartsWith("UiTranslation", StringComparison.Ordinal))
                    .Select(File.ReadAllText));
                foreach (string key in keys)
                    Check(backend.Contains("\"" + key + "\"", StringComparison.Ordinal),
                        file + " must match an actual backend message: " + key);
            }
            if (file is "PolicyNotices" or "PrivacyWarnings")
            {
                string policySource = string.Join("\n", new[] { "PrivacyPolicyCatalog.cs", "DebloatCatalogService.cs", "PerformanceLabService.cs" }
                    .Select(name => File.ReadAllText(Path.Combine(root, name))));
                foreach (string key in keys)
                    Check(policySource.Contains("\"" + key + "\"", StringComparison.Ordinal),
                        "Policy notice must match an actual source caption");
            }
        }

        const string technical = "Microsoft.WindowsStore_22607.1401.8.0_x64 0x80131501 C:\\Windows\\SoftwareDistribution";
        string[] dynamic =
        [
            "Apply completed for 7 item(s). See the progress window for details.",
            "Restore completed for 8 item(s). See the progress window for details.",
            "Apply completed with 2 failure(s). See the progress window for details.",
            "Restore completed with 3 failure(s). See the progress window for details.",
            "Applied and verified 6 change(s). Restart Windows for settings marked as reboot-sensitive.",
            "Restored and verified 5 item(s).",
            "Current states loaded; 4 item(s) are unavailable on this PC and their Select and ON/OFF controls remain disabled.",
            "Checking 41 current setting(s)... You can review and resize this window while the checks finish.",
            "Processed 2/9: " + technical,
            "Unable to load the catalog state: " + technical,
            "Actual: Applied 3/41",
            "Before: PARTIAL",
            "After: Not applicable 2",
            "Result: Failed",
            "74% complete",
            "Windows Photo Viewer is already ON.",
            "Restoring: NTFS Performance Options\r\nThe exact captured NTFS/FileSystem state was restored and verified.",
            "Actual: No matching service is installed\r\nThis service group is not installed on this PC.",
            "Apply 5 selected tweak(s)? 2 already-applied item(s) will be skipped.",
            "Restore 7 applied item(s) to their captured pre-tweak state; when no snapshot exists, a documented Windows default is used instead.\r\n3 already-restored item(s) will be skipped.",
            "Restore 9 applied item(s) to Windows-controlled/default behavior. This intentionally discards saved pre-tweak snapshots for these items.",
            "Before: Not captured",
            "Before: PARTIAL / saved restore state — Applied 3/41; Not applicable 2; Verification failed 1",
            "After: OFF — Applied 0/1; Not applicable 1",
            "After: Unavailable on this PC — " + technical,
            "After: Unable to read — " + technical,
            "No original backup was found. No original backup is available, and no verified Windows default is registered for this option. No values were changed.",
            "Applied 2/3. " + technical,
            "Restored 4/5. " + technical,
            "Registration=37/37. Ready in Open with; choose Windows Photo Viewer in Default Apps for PNG/JPG and other image types.",
            "Actual: Registration=36/37. Registration is incomplete. Apply to repair image handlers; ON does not mean the default app was changed.",
            "Windows Photo Viewer is now ON.\r\nRestart Explorer, sign out, or reboot to make the UI change visible.\r\nRegistration=37/37. Ready in Open with; choose Windows Photo Viewer in Default Apps for PNG/JPG and other image types.",
            "Verification did not match the requested state. Actual: " + technical,
            "SysMain is now APPLIED.",
            "SysMain is now RESTORED. A reboot is recommended.",
            "SysMain is now APPLIED. Restart Windows before evaluating the result.",
            "SysMain is now RESTORED and all applicable child settings were verified."
        ];
        string[] runtime =
        [
            "Copy failed: " + technical,
            "Save failed: " + technical,
            "Saved: " + technical,
            "Example.exe  [RUNNING x3]",
            "Example.exe  [not running]",
            "3 optional process group(s) currently running.",
            "Invalid limit for PCI Device X. Use an empty value or 1-2048.",
            "4 active PCI device(s) loaded. Edit MSI, Limit, or Interrupt Priority, then press Apply changes.",
            "5 device(s) loaded in read-only mode. Run the app as Administrator to apply changes.",
            "Analyzed 17 component(s) | Ready 10 | Attention 5 | Optional 2. Select a row for available actions.",
            "Installed: 31/132",
            "Warnings: 3.",
            "Completed. Warnings: 2.",
            "50 Mbps",
            "Installed",
            "Needs repair",
            "Not installed",
            "Not verified",
            "Unavailable",
            "100% complete",
            "Decryption completed."
        ];
        Panel panel = new();
        TextBlock status = new() { Text = dynamic[0] };
        TextBox rawLog = new() { Text = technical, IsReadOnly = true };
        panel.Children.Add(status);
        panel.Children.Add(rawLog);
        UiTranslation.Observe(panel);
        try
        {
            foreach (var from in UiTranslation.LanguageOptions)
            foreach (var to in UiTranslation.LanguageOptions)
            {
                UiTranslation.Apply(panel, from.Code);
                foreach (string canonical in dynamic)
                {
                    // Exercise a live backend assignment before switching languages.
                    status.Text = canonical;
                    UiTranslation.Apply(panel, to.Code);
                    string expected = UiTranslation.Translate(canonical, to.Code);
                    Check(status.Text == expected, from.Code + " -> " + to.Code + " dynamic source retained");
                    if (to.Code != "en") Check(expected != canonical, to.Code + " dynamic sentence translated");
                    foreach (Match number in Regex.Matches(canonical, "[0-9]+"))
                        Check(expected.Contains(number.Value, StringComparison.Ordinal), "numeric value preserved");
                    Check(!expected.Contains('{'), "no unresolved format placeholder");
                    if (canonical.Contains(technical, StringComparison.Ordinal))
                        Check(expected.Contains(technical, StringComparison.Ordinal), "technical argument preserved exactly");
                    string padded = "\r\n  " + canonical + "  \r\n";
                    Check(UiTranslation.Translate(padded, to.Code) == "\r\n  " + expected + "  \r\n",
                        "paragraph and padding preserved");
                    Check(rawLog.Text == technical, "raw diagnostic log untouched");
                }
                foreach (string canonical in runtime)
                {
                    status.Text = canonical;
                    UiTranslation.Apply(panel, to.Code);
                    string expected = UiTranslation.Translate(canonical, to.Code);
                    Check(status.Text == expected, from.Code + " -> " + to.Code + " runtime status source retained");
                    if (to.Code != "en" && canonical != "50 Mbps")
                        Check(expected != canonical, to.Code + " runtime status translated");
                    if (canonical.Contains(technical, StringComparison.Ordinal))
                        Check(expected.Contains(technical, StringComparison.Ordinal), "runtime technical value preserved");
                }
            }
        }
        finally { UiTranslation.Release(panel); }
        Check(status.CallbackCount == 0, "display callbacks released");
        var auditedSurfaces = SurfaceCoverageAudit.Inspect(root);
        Check(auditedSurfaces.Any(surface => surface.Kind == "Backend display Message" &&
            surface.Text == "This action does not have a restore operation."),
            "semantic audit includes operation-result messages, not just UI assignments");
        Check(auditedSurfaces.Any(surface => surface.Kind == "Backend display Message" && !surface.Resolved),
            "unresolved backend expressions must remain visible in audit output");
        foreach (var language in UiTranslation.LanguageOptions.Where(language => language.Code != "en"))
        {
            const string combined = "Before: PARTIAL / saved restore state — Applied 3/41; Not applicable 2; Verification failed 1";
            string translated = UiTranslation.Translate(combined, language.Code);
            foreach (string fragment in new[] { "saved restore state", "Applied 3/41", "Not applicable 2", "Verification failed 1" })
            {
                string expectedFragment = UiTranslation.Translate(fragment, language.Code);
                Check(expectedFragment != fragment, language.Code + " individual verification fragment translated");
                Check(translated.Contains(expectedFragment, StringComparison.Ordinal),
                    language.Code + " whole verification message includes localized fragment: " + fragment);
            }
            string opaque = @"driver diagnostic: 0x80131501; Applied 3/41 C:\Windows\example.dll";
            Check(UiTranslation.Translate(opaque, language.Code) == opaque,
                "punctuation in unknown diagnostic does not trigger status splitting");
        }
        Check(UiTranslation.Translate("Restore completed with 3 failure(s). See the progress window for details.", "id") ==
            "Pemulihan selesai dengan 3 kegagalan. Lihat rincian di jendela progres.", "Indonesian failure wording");
        return assertions;
    }
}
