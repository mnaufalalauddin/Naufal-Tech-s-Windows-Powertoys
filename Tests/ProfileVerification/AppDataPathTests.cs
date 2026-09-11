using Naufal_Windows_Tech_s_Powertoys;

internal static class AppDataPathTests
{
    internal static void Run(Action<bool, string> assert)
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "Powertoys-AppData-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            string canonical = Path.Combine(sandbox, AppDataPaths.FolderName);
            string settings = Path.Combine(canonical, "Settings");
            string legacy = Path.Combine(sandbox, "WindowsPowerToysV78");
            string older = Path.Combine(sandbox, "WindowsPowerToysV77");
            assert(AppDataPaths.FolderName == "Naufal Windows Powertoys", "AppData canonical folder name");
            assert(AppDataPaths.MigrateKnownFiles(sandbox).Count == 0, "Fresh install has no migration warnings");
            assert(!Directory.Exists(canonical), "Read-only empty migration creates no folders");
            string scratch = AppDataPaths.GetTemporaryDirectory(sandbox);
            assert(scratch == Path.Combine(canonical, "Temp") && Directory.Exists(scratch), "Owned scratch files use the canonical app directory");
            assert(AppDataPaths.GetTemporaryDirectory(sandbox) == scratch, "Temporary directory creation is idempotent");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(older);
            Directory.CreateDirectory(canonical);
            File.WriteAllText(Path.Combine(legacy, "ui-language.txt"), "id");
            File.WriteAllText(Path.Combine(canonical, "ui-language.txt"), "de");
            File.WriteAllText(Path.Combine(older, "ui-language.txt"), "fr");
            File.WriteAllText(Path.Combine(canonical, "ui-theme.txt"), "Dark");
            File.WriteAllText(Path.Combine(older, "ui-font-scale.txt"), "150");
            const string wizard = "{\"Schema\":2,\"Suppress\":true}";
            File.WriteAllText(Path.Combine(legacy, "first-run-prerequisites.json"), wizard);
            byte[] backup = [0xEF, 0xBB, 0xBF, 0x7B, 0x7D];
            File.WriteAllBytes(Path.Combine(legacy, "performance-lab-original.json"), backup);
            File.WriteAllText(Path.Combine(legacy, "Debloat_LowRisk_Original.json"), "{\"saved\":true}");
            File.WriteAllText(Path.Combine(legacy, "service-lock-test.cmd"), "never execute or migrate this script");
            assert(AppDataPaths.MigrateKnownFiles(sandbox).Count == 0, "Known AppData files import successfully");
            assert(File.ReadAllText(Path.Combine(settings, "ui-language.txt")) == "id", "Most recent native V78 preference has precedence");
            assert(File.ReadAllText(Path.Combine(settings, "ui-theme.txt")) == "Dark", "Previous canonical-root preference imports");
            assert(File.ReadAllText(Path.Combine(settings, "ui-font-scale.txt")) == "150", "V77 remains last fallback");
            assert(File.ReadAllText(Path.Combine(settings, "first-run-prerequisites.json")) == wizard, "Wizard suppression state preserved byte-for-byte");
            assert(!FirstRunWizardPolicy.ShouldShow(File.ReadAllText(Path.Combine(settings, "first-run-prerequisites.json")), 2), "Migrated Don't show again still suppresses wizard");
            string resolved = AppDataPaths.ResolveLegacyBackup("performance-lab-original.json", sandbox);
            assert(resolved == Path.Combine(canonical, "Backups", "LegacyV78", "performance-lab-original.json"), "Restore resolves imported backup under canonical root");
            assert(File.ReadAllBytes(resolved).SequenceEqual(backup), "Backup contents are unchanged");
            assert(File.ReadAllBytes(Path.Combine(legacy, "performance-lab-original.json")).SequenceEqual(backup), "Original backup is retained");
            assert(File.ReadAllText(AppDataPaths.ResolveLegacyBackup("Debloat_LowRisk_Original.json", sandbox)) == "{\"saved\":true}", "Debloat import routes correctly");
            assert(Directory.GetFiles(canonical, "*.cmd", SearchOption.AllDirectories).Length == 0, "Legacy executable service-lock scripts are not imported");
            File.WriteAllText(Path.Combine(legacy, "ui-language.txt"), "ru");
            File.WriteAllText(Path.Combine(legacy, "performance-lab-original.json"), "changed source");
            assert(AppDataPaths.MigrateKnownFiles(sandbox).Count == 0, "Migration is idempotent");
            assert(File.ReadAllText(Path.Combine(settings, "ui-language.txt")) == "id", "New canonical setting is not overwritten");
            assert(File.ReadAllBytes(resolved).SequenceEqual(backup), "Captured backup is not overwritten by later legacy changes");
            File.WriteAllText(resolved, "corrupt canonical snapshot");
            assert(AppDataPaths.ResolveLegacyBackup("performance-lab-original.json", sandbox) == resolved, "Corruption is not silently replaced by legacy snapshot");
            File.Delete(resolved);
            assert(AppDataPaths.ResolveLegacyBackup("performance-lab-original.json", sandbox) == Path.Combine(legacy, "performance-lab-original.json"), "Missing imported backup retains legacy read compatibility");
            Directory.CreateDirectory(resolved);
            bool rejected = false;
            try { AppDataPaths.ResolveLegacyBackup("performance-lab-original.json", sandbox); }
            catch (IOException) { rejected = true; }
            assert(rejected, "Directory collision is not missing backup");
            assert(AppDataPaths.MigrateKnownFiles(sandbox).Count == 1, "Import failure is reported without crashing startup");
            rejected = false;
            try { AppDataPaths.ResolveLegacyBackup("..\\outside.json", sandbox); }
            catch (ArgumentException) { rejected = true; }
            assert(rejected, "Legacy backup names cannot escape the allowlist");
            assert(Directory.GetFiles(sandbox, "*.tmp", SearchOption.AllDirectories).Length == 0, "No partial import is left behind");

            string lockedCase = Path.Combine(sandbox, "locked-case");
            string lockedSource = Path.Combine(lockedCase, "WindowsPowerToysV78", "ui-language.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(lockedSource)!);
            Directory.CreateDirectory(Path.Combine(lockedCase, AppDataPaths.FolderName));
            File.WriteAllText(lockedSource, "id");
            File.WriteAllText(Path.Combine(lockedCase, AppDataPaths.FolderName, "ui-language.txt"), "de");
            using (var held = new FileStream(lockedSource, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                assert(AppDataPaths.MigrateKnownFiles(lockedCase).Count == 1, "Unreadable current settings emit migration warning");
                assert(!File.Exists(Path.Combine(lockedCase, AppDataPaths.FolderName, "Settings", "ui-language.txt")), "Unreadable current source does not fall through to stale settings");
                assert(Directory.GetFiles(lockedCase, "*.tmp", SearchOption.AllDirectories).Length == 0, "Failed copy cleans only its temporary import");
            }
            assert(AppDataPaths.MigrateKnownFiles(lockedCase).Count == 0, "Failed import retries on a later launch");
            assert(File.ReadAllText(Path.Combine(lockedCase, AppDataPaths.FolderName, "Settings", "ui-language.txt")) == "id", "Retry preserves the authoritative source");
        }
        finally
        {
            string full = Path.GetFullPath(sandbox);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("Powertoys-AppData-Test-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected test cleanup path.");
            Directory.Delete(full, recursive: true);
        }
    }
}
