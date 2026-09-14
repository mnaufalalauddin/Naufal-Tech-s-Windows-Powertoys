using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed partial class DebloatRegistryLabService
{
    private static IEnumerable<RegistryLab> CreatePrivacyCatalog() => PrivacyPolicyCatalog.Options.Select(option =>
        new RegistryLab(new ToolToggleDefinition(option.Id, "Privacy / Advanced", option.Name,
            option.Description + " " + PrivacyPolicyCatalog.VerificationNotice, true, true,
            option.Id == "PreventDeviceEncryption" ? ToolToggleTier.VeryAggressive : ToolToggleTier.Advanced, option.Warning),
            option.Settings.Select(s => Dword(s.Hive, s.Path, s.Name, s.Value)).ToArray()));

    private static string? PrivacyUnavailable(string id)
    {
        var option = PrivacyPolicyCatalog.Options.SingleOrDefault(o => o.Id == id);
        if (option is null) return null;
        using var version = OpenKey(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false);
        string edition = version?.GetValue("EditionID") as string ?? throw new InvalidOperationException("Windows edition could not be read.");
        if (!PrivacyPolicyCatalog.EditionSupports(option.Support, Environment.OSVersion.Version.Build, edition))
            return "This policy is not supported on this Windows edition or build.";
        if (option.Support is "Edge" or "Brave")
        {
            string relative = option.Support == "Edge" ? @"Microsoft\Edge\Application\msedge.exe" : @"BraveSoftware\Brave-Browser\Application\brave.exe";
            bool installed = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData }
                .Any(folder => CatalogAvailability.FileIsPresent(Path.Combine(Environment.GetFolderPath(folder), relative)));
            if (!installed) return "The related browser is not installed on this PC.";
        }
        if (option.Support == "PhoneStart")
        {
            using var companion = OpenKey(RegistryHive.CurrentUser, option.Settings[0].Path, false);
            if (companion is null) return "The Phone Link Start companion is not available for this account.";
        }
        if (option.Support == "PaintAI")
        {
            int build = Environment.OSVersion.Version.Build;
            int ubr = version?.GetValue("UBR") is int revision ? revision : 0;
            if ((build == 22621 || build == 22631) && ubr < 4870 || build == 26100 && ubr < 3360)
                return "This Windows build needs a newer update for the Paint AI policies.";
            if (!new Windows.Management.Deployment.PackageManager().FindPackagesForUser(string.Empty)
                    .Any(p => p.Id.Name.Equals("Microsoft.Paint", StringComparison.OrdinalIgnoreCase)))
                return "The related application is not installed on this PC.";
        }
        return null;
    }
}
