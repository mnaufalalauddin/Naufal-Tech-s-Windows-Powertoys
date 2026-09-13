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
