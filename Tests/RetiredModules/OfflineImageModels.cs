using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record OfflineImageItem(string Kind, string Name, string State)
{
    public override string ToString() => $"{Kind}: {Name} — {State}";
}

internal static class OfflineImagePolicy
{
    // A deliberately conservative, reviewable list. Servicing support is not a promise that a feature is unnecessary.
    private static readonly HashSet<string> Features = new(StringComparer.OrdinalIgnoreCase)
    { "Printing-XPSServices-Features", "WorkFolders-Client", "TelnetClient", "TFTP", "WindowsMediaPlayer", "MSRDC-Infrastructure" };
    private static readonly string[] Capabilities = { "App.StepsRecorder~~~~", "MathRecognizer~~~~", "Microsoft.Windows.WordPad~~~~", "Print.Fax.Scan~~~~", "Microsoft.Windows.PowerShell.ISE~~~~" };
    private static readonly string[] Apps = { "Microsoft.BingNews", "Microsoft.BingWeather", "Microsoft.Getstarted", "Microsoft.MicrosoftSolitaireCollection", "Microsoft.WindowsFeedbackHub", "Clipchamp.Clipchamp", "Microsoft.Microsoft3DViewer", "Microsoft.3DBuilder" };

    public static bool CanRemove(OfflineImageItem item) => item.Kind switch
    {
        "Feature" => Features.Contains(item.Name) && item.State.Equals("Enabled", StringComparison.OrdinalIgnoreCase),
        "Capability" => Capabilities.Any(p => item.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) && item.State.Equals("Installed", StringComparison.OrdinalIgnoreCase),
        "Provisioned app" => Apps.Any(p => item.Name.StartsWith(p + "_", StringComparison.OrdinalIgnoreCase)),
        _ => false
    };

    public static string LocalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) || value.StartsWith(@"\\", StringComparison.Ordinal) || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
            throw new InvalidOperationException("Select a fully qualified local path; network and device paths are not supported.");
        string result = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar);
        if (result.Length < 4 || result.IndexOf(':', 2) >= 0) throw new InvalidOperationException("Drive roots and alternate streams are not supported.");
        if (new DriveInfo(Path.GetPathRoot(result)!).DriveType == DriveType.Network)
            throw new InvalidOperationException("Mapped network drives are not supported for offline workspaces.");
        return result;
    }

    public static bool Same(string a, string b) => string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    public static bool Beneath(string child, string parent) => Path.GetFullPath(child).StartsWith(Path.GetFullPath(parent).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public static void NoReparseAncestors(string path)
    {
        for (string? part = Path.GetFullPath(path); part is not null; part = Path.GetDirectoryName(part))
            if ((Directory.Exists(part) || File.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Reparse points, junctions and symbolic links are not accepted: " + part);
    }

    public static void SafeWorkspace(string workspace)
    {
        LocalPath(workspace); NoReparseAncestors(workspace);
        foreach (string system in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            if (!string.IsNullOrEmpty(system) && (Same(workspace, system) || Beneath(workspace, system)))
                throw new InvalidOperationException("Use a dedicated data folder outside Windows and Program Files.");
    }

    public static IReadOnlyList<Dictionary<string, string>> Records(string text, string firstKey)
    {
        List<Dictionary<string, string>> records = new(); Dictionary<string, string>? current = null;
        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            int split = line.IndexOf(':'); if (split < 0) continue;
            string key = line[..split].Trim(), value = line[(split + 1)..].Trim();
            if (key.Equals(firstKey, StringComparison.OrdinalIgnoreCase)) { current = new(StringComparer.OrdinalIgnoreCase); records.Add(current); }
            if (current is not null) current[key] = value;
        }
        return records;
    }

    public static IReadOnlyList<OfflineImageItem> Items(string text, string kind, string key) => Records(text, key)
        .Select(r => new OfflineImageItem(kind, r[key], r.GetValueOrDefault("State", kind == "Provisioned app" ? "Provisioned" : "Unknown"))).ToArray();

    public static string ImageIdentity(string text, int selectedIndex)
    {
        var records = Records(text, "Index");
        if (records.Count != 1 || !int.TryParse(records[0]["Index"], out int index) || index != selectedIndex)
            throw new InvalidOperationException("Detailed image metadata did not report the requested index.");
        var image = records[0];
        string architecture = image.GetValueOrDefault("Architecture", "").ToLowerInvariant();
        string edition = image.GetValueOrDefault("Edition", "");
        string installation = image.GetValueOrDefault("Installation", "");
        string version = image.GetValueOrDefault("Version", "");
        if (architecture is not ("x64" or "x86" or "arm64") || string.IsNullOrWhiteSpace(edition) ||
            !installation.Equals("Client", StringComparison.OrdinalIgnoreCase) ||
            !Version.TryParse(version, out var parsed) || parsed.Major != 10 || parsed.Build < 19041)
            throw new InvalidOperationException("Only identified Windows client images, version 10.0 build 19041 or later, with a reported edition and x64/x86/ARM64 architecture are supported. Unknown and Server/WinPE images are blocked.");
        return $"Index={index}; Version={version}; Edition={edition}; Architecture={architecture}; Installation={installation}";
    }
}

internal sealed class OfflineImageSession
{
    public string DirectoryPath { get; }
    public string Source { get; }
    public string Clone => Path.Combine(DirectoryPath, "working.wim");
    public string Mount => Path.Combine(DirectoryPath, "mount");
    public string Manifest => Path.Combine(DirectoryPath, "session.json");
    public int Index { get; }
    public string SourceSha256 { get; set; } = "";
    public string ImageIdentity { get; set; } = "";
    public string State { get; set; } = "Created";
    public string LastOperation { get; set; } = "";
    public int ProcessId { get; set; }
    public long ProcessStartTicks { get; set; }
    public bool VerificationFailed { get; set; }
    public List<string> Changes { get; } = new();
    public void RecordChange(string change)
    {
        if (Changes.Count >= 200 || Changes.Sum(c => c.Length) + change.Length > 24000)
            throw new InvalidOperationException("Session change history limit reached. Finish or discard this session before further operations.");
        Changes.Add(change);
    }

    public OfflineImageSession(string directory, string source, int index)
    { DirectoryPath = OfflineImagePolicy.LocalPath(directory); Source = OfflineImagePolicy.LocalPath(source); Index = index; Validate(); }
    public void Validate()
    {
        OfflineImagePolicy.SafeWorkspace(DirectoryPath);
        if (!Path.GetFileName(DirectoryPath).StartsWith("NWU-Offline-", StringComparison.Ordinal) || !Guid.TryParseExact(Path.GetFileName(DirectoryPath)[12..], "N", out _))
            throw new InvalidOperationException("The session directory is not a managed offline image workspace.");
        if (Index < 1 || !Path.GetExtension(Source).Equals(".wim", StringComparison.OrdinalIgnoreCase) || OfflineImagePolicy.Same(Source, Clone) || OfflineImagePolicy.Beneath(Source, DirectoryPath))
            throw new InvalidOperationException("A source WIM outside the session and a positive image index are required.");
        OfflineImagePolicy.NoReparseAncestors(Clone);
        // Do not traverse inside a mounted image. Only the owned mount root is validated.
        OfflineImagePolicy.NoReparseAncestors(Mount);
    }
    public void Save()
    {
        Validate(); OfflineImagePolicy.NoReparseAncestors(Manifest);
        string pending = Manifest + "." + Guid.NewGuid().ToString("N") + ".new";
        using (FileStream file = new(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using Utf8JsonWriter json = new(file, new() { Indented = true }); json.WriteStartObject();
            json.WriteNumber("Schema", 1); json.WriteString("Source", Source); json.WriteNumber("Index", Index);
            json.WriteString("SourceSha256", SourceSha256); json.WriteString("State", State); json.WriteString("LastOperation", LastOperation);
            json.WriteString("ImageIdentity", ImageIdentity);
            json.WriteNumber("ProcessId", ProcessId); json.WriteNumber("ProcessStartTicks", ProcessStartTicks);
            json.WriteBoolean("VerificationFailed", VerificationFailed); json.WriteString("UpdatedUtc", DateTimeOffset.UtcNow);
            json.WriteStartArray("Changes"); foreach (string change in Changes) json.WriteStringValue(change); json.WriteEndArray();
            json.WriteEndObject(); json.Flush(); file.Flush(true);
        }
        File.Move(pending, Manifest, true);
    }
    public static OfflineImageSession Load(string manifest)
    {
        string full = OfflineImagePolicy.LocalPath(manifest); OfflineImagePolicy.NoReparseAncestors(full);
        if (!Path.GetFileName(full).Equals("session.json", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select session.json from an owned workspace.");
        if (new FileInfo(full).Length > 65536) throw new InvalidOperationException("Offline session manifest exceeds its size limit.");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(full)); JsonElement root = doc.RootElement;
        if (root.GetProperty("Schema").GetInt32() != 1) throw new InvalidOperationException("Unsupported session schema.");
        var session = new OfflineImageSession(Path.GetDirectoryName(full)!, root.GetProperty("Source").GetString()!, root.GetProperty("Index").GetInt32())
        {
            SourceSha256 = root.GetProperty("SourceSha256").GetString()!, State = root.GetProperty("State").GetString()!,
            ImageIdentity = root.TryGetProperty("ImageIdentity", out var identity) ? identity.GetString() ?? "" : "",
            LastOperation = root.GetProperty("LastOperation").GetString()!, ProcessId = root.GetProperty("ProcessId").GetInt32(),
            ProcessStartTicks = root.GetProperty("ProcessStartTicks").GetInt64(), VerificationFailed = root.GetProperty("VerificationFailed").GetBoolean()
        };
        if (root.TryGetProperty("Changes", out var changes))
            foreach (var change in changes.EnumerateArray()) session.RecordChange(change.GetString() ?? "");
        return session;
    }
}
