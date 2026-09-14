using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogExpansionTests
{
    internal static void Run(Action<bool, string> check)
    {
        var options = PrivacyPolicyCatalog.Options;
        check(options.Count == 11, "eleven privacy suboptions without duplicate catalog rows");
        check(options.Select(p => p.Id).Distinct().Count() == options.Count, "unique privacy IDs");
        var all = options.SelectMany(o => o.Settings).ToArray();
        check(all.Select(s => (s.Hive, s.Path.ToUpperInvariant(), s.Name.ToUpperInvariant())).Distinct().Count() == all.Length,
            "new policy bundles do not overwrite one another");
        foreach (var option in options)
        {
            check(option.Settings.Count > 0 && option.Description.Length > 20, "described executable policy " + option.Id);
            check(option.Settings.All(s => s.Path.Length > 12 && s.Name.Length > 0 && s.Value is >= 0 and <= 2), "bounded explicit policy " + option.Id);
        }
        foreach (int build in new[] { 17763, 19045, 22000, 22621, 26100 })
        {
            check(!PrivacyPolicyCatalog.EditionSupports("Enterprise11", build, "Core"), "Home never certified for Enterprise ad policy");
            check(!PrivacyPolicyCatalog.EditionSupports("Enterprise11", build, "Professional"), "Pro never certified for Enterprise ad policy");
            check(PrivacyPolicyCatalog.EditionSupports("Enterprise11", build, "Enterprise") == (build >= 22000), "Enterprise build gate");
            check(!PrivacyPolicyCatalog.EditionSupports("Pro", build, "Core"), "Home not certified for Pro-only policies");
            check(PrivacyPolicyCatalog.EditionSupports("PaintAI", build, "Professional") == (build >= 22621), "Paint build gate");
        }
        check(!PrivacyPolicyCatalog.EditionSupports("PhoneStart", 19045, "Professional"), "Windows 10 has no Phone Link Start companion");
        var encryption = options.Single(p => p.Id == "PreventDeviceEncryption");
        check(encryption.Settings.Count == 1 && encryption.Settings[0] == new PrivacyPolicySetting(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\BitLocker", "PreventDeviceEncryption", 1), "BitLocker changes only future automatic-encryption policy");
        check(encryption.Warning.Contains("Existing encrypted drives stay encrypted"), "encryption warning distinguishes decrypt");
        check(options.Single(p => p.Id == "PaintAI").Settings.All(s => s.Path.EndsWith(@"Policies\Paint")), "documented Paint registry path");
        var target = BuiltInAppsCatalog.Targets.Single(t => t.Id == "Copilot");
        var copilot = CopilotStorePolicy.Package();
        check(BuiltInAppsCatalog.Matches(target, copilot), "exact Store-product identity supports Copilot desktop");
        check(!BuiltInAppsCatalog.Matches(target, copilot with { Scope = "machine" }), "Copilot cannot widen to other users");
        check(!BuiltInAppsCatalog.Matches(target, copilot with { FullName = "other:user" }), "no substituted Store ID");
        check(!BuiltInAppsCatalog.Matches(BuiltInAppsCatalog.Targets[0], copilot), "Copilot marker cannot match another app");
        check(CopilotStorePolicy.Package("offline").InventoryError == "offline", "unknown remains unknown");
        NativeCommandResult Result(int exit, string text = "", bool timeout = false) => new(exit, text, "", timeout, TimeSpan.Zero);
        check(CopilotStorePolicy.Installed(Result(0, "Microsoft Copilot  XP9CXNGPPJ97XX  1.0 msstore")) == true, "exact WinGet table identity accepted");
        check(CopilotStorePolicy.Installed(Result(CopilotStorePolicy.NoApplications)) == false, "documented absent return code accepted");
        foreach (var result in new[] { Result(0), Result(1, "No installed package"), Result(0, "XP9CXNGPPJ97XXevil"),
            Result(0, "Microsoft Copilot"), Result(CopilotStorePolicy.NoApplications, "", true), Result(-1) })
            check(CopilotStorePolicy.Installed(result) is null, "failed/ambiguous Copilot lookup never means absent");
        foreach (var command in new[] { CopilotStorePolicy.ListArguments(), CopilotStorePolicy.RemoveArguments(),
            CopilotStorePolicy.ListArguments(true), CopilotStorePolicy.RemoveArguments(true) })
        {
            check(command.Contains("--exact") && command[Array.IndexOf(command, "--id") + 1] == CopilotStorePolicy.ProductId, "exact user-supplied Store ID");
            check(command[Array.IndexOf(command, "--scope") + 1] == "user", "Copilot current-user scope");
            check(!command.Any(a => a is "--force" or "--name" or "--all" or "--purge"), "no force/fuzzy/global Copilot removal");
        }
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string Read(string file) => File.ReadAllText(Path.Combine(root, file));
        string lab = Read("DebloatRegistryLabService.cs");
        check(lab.Contains("VerifyWindowsDefaults(lab)"), "restore verifies every default, not partial OFF state");
        check(lab.IndexOf("foreach (RegistryLabSetting setting in lab.Settings) Capture") < lab.IndexOf("key.SetValue(setting.Name, setting.Value"), "capture whole policy plan before writes");
        string performance = Read("PerformanceLabService.cs");
        check(performance.Contains("restoreOriginal: true") && performance.Contains("explicitOff") && performance.Contains("value == 0"), "Fast Startup OFF distinct from exact restore");
        string ai = Read("WindowsAiService.cs");
        check(!ai.Contains("DeprovisionPackageForAllUsersAsync") && !ai.Contains("RemovalOptions.RemoveForAllUsers"), "AI cleanup current user only");
        check(!ai.Contains("Contains(\"Copilot\"") && !ai.Contains("\"--force\""), "AI cleanup exact identity, no force");
        check(ai.Contains("RecallState.Unknown") && ai.Contains("0x800F080C"), "Recall failures not reported as absence");
        string service = Read("BuiltInAppsService.cs");
        check(service.Contains("using var userRunner = new SameUserProcessRunner()"), "Store install never silently runs elevated");
        string main = Read("MainWindow.xaml.cs");
        check(main.Contains("Disable BitLocker automatic device encryption") && main.Contains("PreventDeviceEncryption"), "BitLocker entry routed to policy catalog");
        Console.WriteLine("Catalog recommendations: " + string.Join(", ", BuiltInAppsCatalog.Targets.GroupBy(t => t.Recommendation).Select(g => g.Key + "=" + g.Count())));
    }
}
