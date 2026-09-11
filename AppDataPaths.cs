using System;
using System.Collections.Generic;
using System.IO;

namespace Naufal_Windows_Tech_s_Powertoys;

// Application-owned per-user files only. Windows/vendor caches, machine-wide
// ProgramData state and registry snapshot identities must not be renamed here.
internal static class AppDataPaths
{
    internal const string FolderName = "Naufal Windows Powertoys";
    internal static string LocalBase => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    internal static string LocalRoot => Path.Combine(LocalBase, FolderName);
    internal static string SettingsDirectory => Path.Combine(LocalRoot, "Settings");
    internal static string RuntimeCacheDirectory => Path.Combine(LocalRoot, "RuntimeCache");

    internal static string GetTemporaryDirectory(string? localBase = null)
    {
        string directory = Path.Combine(localBase ?? LocalBase, FolderName, "Temp");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static readonly string[] SettingsFiles =
        ["ui-language.txt", "ui-theme.txt", "ui-font-scale.txt", "first-run-prerequisites.json"];
    private static readonly string[] BackupFiles =
        ["Debloat_LowRisk_Original.json", "performance-lab-original.json"];

    internal static IReadOnlyList<string> MigrateKnownFiles(string localBase)
    {
        List<string> warnings = [];
        string root = Path.Combine(localBase, FolderName);
        foreach (string file in SettingsFiles)
            TryImport(Path.Combine(root, "Settings", file),
                [Path.Combine(localBase, "WindowsPowerToysV78", file),
                 Path.Combine(root, file),
                 Path.Combine(localBase, "WindowsPowerToysV77", file)], warnings);
        foreach (string file in BackupFiles)
            TryImport(Path.Combine(root, "Backups", "LegacyV78", file),
                [Path.Combine(localBase, "WindowsPowerToysV78", file)], warnings);
        return warnings;
    }

    private static void TryImport(string destination, string[] sources, List<string> warnings)
    {
        string? temporary = null;
        try
        {
            if (IsPresent(destination)) return;
            foreach (string source in sources)
            {
                if (!IsPresent(source)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                temporary = destination + ".import-" + Guid.NewGuid().ToString("N") + ".tmp";
                File.Copy(source, temporary, overwrite: false);
                File.Move(temporary, destination, overwrite: false);
                temporary = null;
                return;
            }
        }
        catch (Exception exception)
        {
            // Do not choose an older source after a newer source could not be
            // read, overwrite a destination, or erase the only original backup.
            warnings.Add($"Could not import {Path.GetFileName(destination)}: {exception.Message}");
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception exception) { warnings.Add($"Could not remove temporary import file: {exception.Message}"); }
            }
        }
    }

    internal static string ResolveLegacyBackup(string fileName, string? localBase = null)
    {
        if (Array.IndexOf(BackupFiles, fileName) < 0)
            throw new ArgumentException("Unknown legacy snapshot file.", nameof(fileName));
        localBase ??= LocalBase;
        string canonical = Path.Combine(localBase, FolderName, "Backups", "LegacyV78", fileName);
        return IsPresent(canonical) ? canonical : Path.Combine(localBase, "WindowsPowerToysV78", fileName);
    }

    private static bool IsPresent(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
                throw new IOException("A directory occupies an application data file path: " + path);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        // Access failure/corruption is not absence, especially for Restore.
    }
}
