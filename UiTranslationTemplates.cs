using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static partial class UiTranslation
{
    private sealed record TextPattern(string Key, Regex Pattern, int Arguments, bool TranslateArguments);

    // Explicit display templates only. Never do word-by-word replacements in
    // errors, commands, paths, package names, GUIDs or user-entered text.
    private static readonly TextPattern[] DisplayPatterns =
    {
        Pattern("Version {0}"),
        Pattern("{0}  [RUNNING x{1}]"),
        Pattern("{0}  [not running]"),
        Pattern("{0} optional process group(s) currently running.", true),
        Pattern("Invalid limit for {0}. Use an empty value or 1-2048."),
        Pattern("{0} active PCI device(s) loaded. Edit MSI, Limit, or Interrupt Priority, then press Apply changes.", true),
        Pattern("{0} device(s) loaded in read-only mode. Run the app as Administrator to apply changes.", true),
        Pattern("Save failed: {0}"),
        Pattern("Saved: {0}"),
        Pattern("Analyzed {0} component(s)", true),
        Pattern("Ready {0}", true),
        Pattern("Attention {0}", true),
        Pattern("Optional {0}. Select a row for available actions.", true),
        Pattern("Apply {0} selected tweak(s)? {1} already-applied item(s) will be skipped.", true),
        Pattern("Restore {0} applied item(s) to {1}", translateArguments: true),
        Pattern("{0} already-restored item(s) will be skipped.", true),
        Pattern("Unavailable on this PC — {0}", translateArguments: true),
        Pattern("Unable to read — {0}", translateArguments: true),
        Pattern("Verification failed {0}", true),
        Pattern("No original backup was found. {0}", translateArguments: true),
        Pattern("Applied {0}/{1}. {2}", translateArguments: true),
        Pattern("Restored {0}/{1}. {2}", translateArguments: true),
        Pattern("Registration={0}/{1}. {2}", translateArguments: true),
        Pattern("{0} is already {1}.", translateArguments: true),
        Pattern("Verification did not match the requested state. Actual: {0}", translateArguments: true),
        Pattern("{0} is now {1}. A reboot is recommended.", translateArguments: true),
        Pattern("{0} is now {1}. Restart Windows before evaluating the result.", translateArguments: true),
        Pattern("{0} is now {1} and all applicable child settings were verified.", translateArguments: true),
        Pattern("{0} is now {1}.", translateArguments: true),
        Pattern("Copy failed: {0}", translateArguments: true),
        Pattern("Installed: {0}/{1}", true),
        Pattern("Warnings: {0}.", true),
        Pattern("{0} Warnings: {1}.", translateArguments: true),
        Pattern("{0} Mbps", true),
        Pattern("Apply completed for {0} item(s). See the progress window for details.", true),
        Pattern("Restore completed for {0} item(s). See the progress window for details.", true),
        Pattern("Apply completed with {0} failure(s). See the progress window for details.", true),
        Pattern("Restore completed with {0} failure(s). See the progress window for details.", true),
        Pattern("Current states loaded; {0} item(s) are unavailable on this PC and their Select and ON/OFF controls remain disabled.", true),
        Pattern("Checking {0} current setting(s)... You can review and resize this window while the checks finish.", true),
        Pattern("Applied and verified {0} change(s). Restart Windows for settings marked as reboot-sensitive.", true),
        Pattern("Restored and verified {0} item(s).", true),
        Pattern("Processed {0}/{1}: {2}", translateArguments: true),
        Pattern("Unable to load the catalog state: {0}", translateArguments: true),
        Pattern("Actual: {0}", translateArguments: true),
        Pattern("Before: {0}", translateArguments: true),
        Pattern("After: {0}", translateArguments: true),
        Pattern("Result: {0}", translateArguments: true),
        Pattern("Applied {0}/{1}", true),
        Pattern("Not applicable {0}", true),
        Pattern("{0}% complete", true),
        Pattern("{0} running task(s).", true),
        Pattern("{0}% complete — {1}/{2}", true),
        Pattern("{0}% processed — {1}/{2}; errors or unverified items", true),
        Pattern("{0}% processed — {1}/{2}", true),
        Pattern("{0} out of {1} have been verified, but {2} tweaks can't be applied due to unavailability on this PC.", true),
        Pattern("Verification failed for {0} item(s). See the progress window for details.", true),
        Pattern("Windows step: {0}% — Working", true),
        Pattern("{0}% complete — {1}", translateArguments: true),
        Pattern("Analyzed {0}/{1}", true), Pattern("Optimized {0}", true),
        Pattern("Selected {0}", true), Pattern("Pending changes {0}", true),
        Pattern("Waiting for: {0}", translateArguments: true),
        Pattern("{0} of {1}", true),
        Pattern("Elapsed: {0}"), Pattern("Updated {0}"), Pattern("Time spent {0}"),
        Pattern("Text scaling: {0}%", true)
    };

    private static readonly string[] ActionPrefixes =
    {
        "Applying", "Restoring", "Verifying", "Reading", "Collecting", "Downloading", "Installing",
        "Repairing", "Disabling", "Enabling", "Ending", "Suspending", "Resuming", "Decrypting", "Analyzing",
        "Completed", "Failed", "Removing", "Installed"
    };

    private static TextPattern Pattern(string key, bool numeric = false, bool translateArguments = false)
    {
        StringBuilder expression = new("\\A");
        int offset = 0, arguments = 0;
        foreach (Match placeholder in Regex.Matches(key, @"\{(\d+)\}"))
        {
            expression.Append(Regex.Escape(key[offset..placeholder.Index]));
            expression.Append("(?<p").Append(placeholder.Groups[1].Value).Append('>')
                .Append(numeric ? @"\d+(?:[.,]\d+)?" : @"[^\r\n]+?").Append(')');
            offset = placeholder.Index + placeholder.Length;
            arguments++;
        }
        expression.Append(Regex.Escape(key[offset..])).Append("\\z");
        return new(key, new Regex(expression.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)), arguments, translateArguments);
    }

    private static string? TranslateDisplayText(string text, string code, Dictionary<string, string> table, int depth)
    {
        if (depth > 6 || text.Length > 8000) return null;
        // Callers append a leading paragraph break or trailing spaces to an
        // otherwise exact caption. Preserve layout while looking up the caption.
        string trimmed = text.Trim();
        if (trimmed.Length != text.Length && trimmed.Length != 0)
        {
            int start = text.IndexOf(trimmed, StringComparison.Ordinal);
            return text[..start] + TranslateCore(trimmed, code, depth + 1) +
                text[(start + trimmed.Length)..];
        }
        // Multi-line confirmations retain their original paragraph boundaries.
        if (text.Contains('\n'))
        {
            string[] parts = Regex.Split(text, "(\\r?\\n)");
            for (int i = 0; i < parts.Length; i += 2) parts[i] = TranslateCore(parts[i], code, depth + 1);
            return string.Concat(parts);
        }
        foreach (TextPattern pattern in DisplayPatterns)
        {
            Match match = pattern.Pattern.Match(text);
            if (!match.Success || !table.TryGetValue(pattern.Key, out string? translated)) continue;
            object[] values = Enumerable.Range(0, pattern.Arguments)
                .Select(index => match.Groups["p" + index].Value)
                .Select(value => (object)Isolate(pattern.TranslateArguments ? TranslateCore(value, code, depth + 1) : value, code)).ToArray();
            return string.Format(CultureInfo.InvariantCulture, translated, values);
        }
        // Split only the grammar authored by CatalogVerificationReport and
        // CatalogAvailability, not arbitrary diagnostics or command lines.
        Match state = Regex.Match(text, @"\A(ON|OFF|PARTIAL(?: / saved restore state)?) — (.+)\z",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (state.Success)
            return TranslateCore(state.Groups[1].Value, code, depth + 1) + " — " +
                TranslateCore(state.Groups[2].Value, code, depth + 1);
        if (Regex.IsMatch(text, @"\AApplied \d+/\d+(?:; Not applicable \d+)?(?:; Verification failed \d+)?\z",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) && text.Contains("; ", StringComparison.Ordinal))
            return string.Join("; ", text.Split("; ").Select(part => TranslateCore(part, code, depth + 1)));
        // Staged repair labels: keep the ordinal outside the localized caption.
        Match numbered = Regex.Match(text, @"\A(\d+\. )(.+)\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (numbered.Success)
            return Isolate(numbered.Groups[1].Value, code) + TranslateCore(numbered.Groups[2].Value, code, depth + 1);
        if (text.StartsWith("TASKS: ", StringComparison.Ordinal))
            return TranslateCore("Tasks", code, depth + 1) + ": " + TranslateCore(text[7..], code, depth + 1);
        if (text.StartsWith("Task Monitoring: ", StringComparison.Ordinal))
            return TranslateCore("Task Monitoring", code, depth + 1) + ": " + TranslateCore(text[17..], code, depth + 1);
        if (text.StartsWith("🔴 ", StringComparison.Ordinal))
            return "🔴 " + TranslateCore(text[3..], code, depth + 1);
        if (text.Length > 8 && text.StartsWith("=== ", StringComparison.Ordinal) && text.EndsWith(" ===", StringComparison.Ordinal))
            return "=== " + TranslateCore(text[4..^4], code, depth + 1) + " ===";
        // Category/risk badges and summaries contain separated, independently
        // meaningful fields. Translate each field, not arbitrary words within it.
        foreach (string separator in new[] { " | ", " / ", " • " })
        {
            if (!text.Contains(separator, StringComparison.Ordinal)) continue;
            return string.Join(separator, text.Split(separator).Select(part => TranslateCore(part, code, depth + 1)));
        }
        foreach (string action in ActionPrefixes)
        {
            if (text == action + " completed") return TranslateCore("Completed", code, depth + 1);
            if (text == action + " failed") return TranslateCore("Failed", code, depth + 1);
            string? subject = text.StartsWith(action + ": ", StringComparison.OrdinalIgnoreCase) ? text[(action.Length + 2)..]
                : text.StartsWith(action + " ", StringComparison.OrdinalIgnoreCase) ? text[(action.Length + 1)..] : null;
            if (subject is not null)
                return TranslateCore(action, code, depth + 1) + ": " + Isolate(TranslateCore(subject, code, depth + 1), code);
        }
        foreach (string suffix in new[] { " — Verified", " - VERIFIED", " failed", "...", ":" })
        {
            if (!text.EndsWith(suffix, StringComparison.Ordinal) || text.Length == suffix.Length) continue;
            string stem = text[..^suffix.Length];
            string translatedStem = TranslateCore(stem, code, depth + 1);
            if (translatedStem == stem) continue;
            return suffix switch
            {
                " — Verified" or " - VERIFIED" => translatedStem + " — " + TranslateCore("VERIFIED", code, depth + 1),
                " failed" => TranslateCore("Failed", code, depth + 1) + ": " + translatedStem,
                _ => translatedStem + suffix
            };
        }
        return null;
    }

    private static string Isolate(string value, string code) => IsRightToLeft(code) ? "\u2068" + value + "\u2069" : value;
}
