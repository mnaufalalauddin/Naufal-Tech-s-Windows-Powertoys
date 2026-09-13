using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record PhotoViewerEntry(string Tag, string Path, string Name, string Value, bool Legacy = false);
internal sealed record PhotoViewerStatus(int Matching, int Total)
{
    internal bool Ready => Matching == Total;
    internal string Detail => $"Registration={Matching}/{Total}. " +
        (Ready ? "Ready in Open with; choose Windows Photo Viewer in Default Apps for PNG/JPG and other image types."
               : "Registration is incomplete. Apply to repair image handlers; ON does not mean the default app was changed.");
}

// Register our own handler rather than advertising absent Windows ProgIDs or
// replacing Windows' TIFF handler. No extension default or UserChoice is written.
internal static class PhotoViewerRegistration
{
    internal const string ProgId = "NaufalTechs.PhotoViewer.Image";
    internal const string Capabilities = @"SOFTWARE\Microsoft\Windows Photo Viewer\Capabilities";
    internal const string Associations = Capabilities + @"\FileAssociations";
    internal const string RegisteredApps = @"SOFTWARE\RegisteredApplications";
    internal const string SchemaKey = "RegistrationSchema";
    internal static readonly string[] Extensions =
        [".cr2", ".jpg", ".wdp", ".jfif", ".dib", ".png", ".jxr", ".bmp", ".jpe", ".jpeg", ".gif", ".tif", ".tiff"];
    internal static string ViewerDll => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Photo Viewer", "PhotoViewer.dll");

    internal static PhotoViewerEntry[] CreatePlan(string viewerDll, string systemDirectory)
    {
        string command = $"\"{Path.Combine(systemDirectory, "rundll32.exe")}\" \"{viewerDll}\", ImageView_Fullscreen \"%1\"";
        string icon = $"\"{viewerDll}\",0";
        string handler = @"SOFTWARE\Classes\" + ProgId;
        var entries = new List<PhotoViewerEntry>
        {
            new("ApplicationName", Capabilities, "ApplicationName", "Windows Photo Viewer", true),
            new("RegisteredApplication", RegisteredApps, "Windows Photo Viewer", Capabilities, true),
            new("ApplicationDescription", Capabilities, "ApplicationDescription", "View pictures with Windows Photo Viewer."),
            new("ApplicationIcon", Capabilities, "ApplicationIcon", icon),
            new("HandlerName", handler, "", "Windows Photo Viewer"),
            new("HandlerIcon", handler + @"\DefaultIcon", "", icon),
            new("HandlerVerb", handler + @"\shell", "", "open"),
            new("HandlerCommand", handler + @"\shell\open\command", "", command),
            new("HandlerApplicationName", handler + @"\Application", "ApplicationName", "Windows Photo Viewer"),
            new("HandlerApplicationIcon", handler + @"\Application", "ApplicationIcon", icon)
        };
        foreach (string extension in Extensions)
        {
            entries.Add(new("Association" + extension, Associations, extension, ProgId, true));
            entries.Add(new("OpenWith" + extension, @"SOFTWARE\Classes\" + extension + @"\OpenWithProgids", ProgId, ""));
        }
        return entries.ToArray();
    }

    internal static PhotoViewerStatus Inspect(IReadOnlyList<PhotoViewerEntry> plan,
        Func<PhotoViewerEntry, (object? Value, RegistryValueKind? Kind)> read) =>
        new(plan.Count(entry =>
        {
            var actual = read(entry);
            return actual.Kind == RegistryValueKind.String && actual.Value is string value &&
                string.Equals(value, entry.Value, StringComparison.OrdinalIgnoreCase);
        }), plan.Count);

    // Capture the whole plan BEFORE any target write. Captures are first-change
    // only, so re-applying from an old/incomplete registration preserves history.
    internal static void Apply(IReadOnlyList<PhotoViewerEntry> plan, Action<PhotoViewerEntry> capture,
        Action markSchema, Action<PhotoViewerEntry> write, Action notify)
    {
        foreach (var entry in plan) capture(entry);
        markSchema();
        try { foreach (var entry in plan) write(entry); }
        finally { notify(); }
    }

    internal static PhotoViewerEntry[] RestoreEntries(IReadOnlyList<PhotoViewerEntry> plan,
        Func<string, object?> read)
    {
        object? schema = read(SchemaKey);
        if (schema is not null && (schema is not int version || version != 2))
            throw new InvalidDataException("The Photo Viewer registration snapshot version is unsupported. Backup retained.");
        // Pre-fix snapshots contain only the original 15 tags. Never guess that
        // newer, uncaptured keys were absent. Include partial pre-write captures
        // so an interrupted capture can still be restored without leaking edits.
        return plan.Where(entry => schema is not null || entry.Legacy ||
            read(entry.Tag + ".Captured") is not null).ToArray();
    }

    internal static (object? Value, RegistryValueKind? Kind) ReadNative(PhotoViewerEntry entry)
    {
        // Check the effective merged class registration, not just HKLM: a user
        // override must not produce a false successful machine-level readback.
        bool classes = entry.Path.StartsWith(@"SOFTWARE\Classes\", StringComparison.OrdinalIgnoreCase);
        using var root = RegistryKey.OpenBaseKey(classes ? RegistryHive.ClassesRoot : RegistryHive.LocalMachine,
            Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
        using var key = root.OpenSubKey(classes ? entry.Path[@"SOFTWARE\Classes\".Length..] : entry.Path);
        object? value = key?.GetValue(entry.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return (value, value is null ? null : key!.GetValueKind(entry.Name));
    }

    internal static void NotifyShell() => SHChangeNotify(0x08000000, 0x2000, IntPtr.Zero, IntPtr.Zero);
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
}

