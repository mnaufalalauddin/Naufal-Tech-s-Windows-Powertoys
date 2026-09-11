using Naufal_Windows_Tech_s_Powertoys;

internal static class BuiltInAppsTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var targets = BuiltInAppsCatalog.Targets;
        check(targets.Count == 31, "built-in apps exactly 31 rows");
        check(targets.SelectMany(t => t.Families).Distinct().Count() == 32, "31 apps / 32 exact families including Teams variants");
        check(targets.Select(t => t.Id).Distinct().Count() == 31, "stable unique app IDs");
        foreach (var target in targets)
        foreach (string family in target.Families)
        {
            var package = Package(target, family);
            check(BuiltInAppsCatalog.Matches(target, package), "approved " + family);
            check(BuiltInAppsCatalog.Matches(target, package with { Healthy = false }), "damaged app still removable " + family);
            check(!BuiltInAppsCatalog.Matches(target, package with { IsFramework = true }), "framework excluded " + family);
            check(!BuiltInAppsCatalog.Matches(target, package with { IsResource = true }), "resource excluded " + family);
            check(!BuiltInAppsCatalog.Matches(target, package with { Family = family + "evil" }), "publisher identity exact " + family);
            check(!BuiltInAppsCatalog.Matches(target, package with { FullName = "C:\\" + package.FullName }), "no arbitrary path " + family);
            check(!BuiltInAppsCatalog.Matches(target, package with { Name = "Microsoft.WindowsStore" }), "name must match family " + family);
            check(!BuiltInAppsCatalog.Matches(target, package with { FullName = package.FullName + "_wrongpublisher" }), "full name publisher exact " + family);
        }
        foreach (string forbidden in new[] { "NotepadPlusPlus_2247w0b46hfww", "Microsoft.WindowsStore_8wekyb3d8bbwe",
            "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe", "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
            "MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "Microsoft.HEVCVideoExtensions_8wekyb3d8bbwe" })
            check(!targets.Any(t => BuiltInAppsCatalog.Matches(t, Package(t, forbidden))), "excluded system/paid/third-party " + forbidden);
        check(BuiltInAppsCatalog.ResolveSelection(["Clock", "AV1", "Clock"]).Select(t => t.Id).SequenceEqual(["AV1", "Clock"]), "selection canonical ordering and distinct IDs");
        foreach (string[] invalid in new[] { Array.Empty<string>(), new[] { "Clock", "WindowsStore" }, new[] { "*" }, new[] { "clock" } })
            check(await ThrowsAsync(() => Task.Run(() => BuiltInAppsCatalog.ResolveSelection(invalid))), "reject invalid selection");
        foreach (var target in targets)
        {
            var uri = BuiltInAppsCatalog.StoreUri(target);
            check(uri.Scheme == "ms-windows-store" && uri.Host == "pdp", "official Store link " + target.Id);
            if (BuiltInAppsCatalog.StoreProductId(target.Id) is string product)
            {
                var args = BuiltInAppsCatalog.InstallArguments(target.Id);
                check(product.Length == 12 && product.All(char.IsAsciiLetterOrDigit), "fixed product ID " + target.Id);
                check(args[Array.IndexOf(args, "--id") + 1] == product && args.Contains("--exact"), "exact restore ID " + target.Id);
                check(args[Array.IndexOf(args, "--scope") + 1] == "user", "current-user Store install " + target.Id);
                check(args[Array.IndexOf(args, "--source") + 1] == "msstore", "Store source only " + target.Id);
                check(!args.Any(a => a is "--force" or "--ignore-security-hash" or "--allow-reboot" or "--override"), "no force bypass " + target.Id);
            }
            else check(await ThrowsAsync(() => Task.Run(() => BuiltInAppsCatalog.InstallArguments(target.Id))), "no guessed ID " + target.Id);
        }
        const string trusted = """{"Name":"msstore","Type":"Microsoft.Rest","Arg":"https://storeedgefd.dsx.mp.microsoft.com/v9.0"}""";
        check(BuiltInAppsCatalog.IsMicrosoftStoreSource(trusted), "official Store source accepted");
        foreach (string invalid in new[] { "", "{}", "[]", "null", "corrupt", trusted.Replace("https:", "http:"),
            trusted.Replace(".com/", ".com.attacker.test/"), trusted.Replace("msstore", "other"),
            trusted.Replace("Microsoft.Rest", "Microsoft.PreIndexed.Package"), trusted.Replace("/v9.0", "/v9.0?redirect=evil") })
            check(!BuiltInAppsCatalog.IsMicrosoftStoreSource(invalid), "untrusted/malformed source rejected");

        Fake backend = new() { Packages = targets.Select(t => Package(t)).ToList() };
        var updates = new Updates();
        var removed = await BuiltInAppsRemoval.RunAsync(backend, targets.Select(t => t.Id), updates);
        check(removed.Success && removed.Succeeded == 31 && backend.Packages.Count == 0, "all 31 uninstall verified");
        check(backend.Saved && !backend.SavedRestore && backend.Log.Count == 31, "plan before removal and 31 result logs");
        check(updates.Items.Count(u => u.State == "COMPLETED") == 31 && updates.Items.Any(u => u.State == "PROGRESS"), "per-app success and live progress");
        var restored = await BuiltInAppsRemoval.RunAsync(backend, targets.Select(t => t.Id), updates, restore: true);
        check(restored.Success && restored.Succeeded == 31 && backend.Packages.Count == 31, "restore all 31 missing apps verifies each family");
        check(backend.SavedRestore, "restore saves audit before mutations");
        backend = new(); updates = new();
        removed = await BuiltInAppsRemoval.RunAsync(backend, ["Clock"], updates);
        check(removed.Success && removed.Unavailable == 1 && backend.RemoveCalls == 0 && updates.Items.Single().State == "UNAVAILABLE", "absent uninstall neutral, no mutation");
        backend = new() { Packages = [Package(targets[2])], RemoveFailure = new UnauthorizedAccessException("denied") };
        removed = await BuiltInAppsRemoval.RunAsync(backend, ["Clock"], new Updates());
        check(!removed.Success && removed.Failed == 1 && removed.Unavailable == 0, "access denied not unavailable");
        backend = new() { ReadFailure = new UnauthorizedAccessException("inventory denied") };
        check(await ThrowsAsync(() => BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null)) && backend.RemoveCalls == 0 && !backend.Saved, "failed inventory cannot uninstall or create empty success plan");
        check(await ThrowsAsync(() => BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null, true)) && backend.RestoreCalls == 0, "failed inventory cannot restore");
        backend = new() { SaveFailure = true, Packages = [Package(targets[2])] };
        check(await ThrowsAsync(() => BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null)) && backend.RemoveCalls == 0, "failed audit aborts before uninstall");
        check(await ThrowsAsync(() => BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null, true)) && backend.RestoreCalls == 0, "failed audit aborts before restore");
        backend = new() { Packages = [Package(targets[2])], KeepAfterRemove = true };
        removed = await BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null);
        check(!removed.Success && removed.Failed == 1, "uninstall return alone not success");
        backend = new() { NoRestore = true };
        restored = await BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null, true);
        check(!restored.Success && restored.Failed == 1 && restored.Unavailable == 0, "restore missing readback not false success/unavailable");
        backend = new() { RestoreUnhealthy = true };
        restored = await BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null, true);
        check(!restored.Success, "damaged registration not verified restore");
        backend = new() { FailReadback = true };
        restored = await BuiltInAppsRemoval.RunAsync(backend, ["Clock"], null, true);
        check(!restored.Success && restored.Unavailable == 0, "readback failure not absent");
        foreach (Exception failure in new Exception[] { new TimeoutException("Windows still busy"), new PendingDeploymentException("busy") })
        {
            backend = new() { Packages = targets.Select(t => Package(t)).ToList(), RemoveFailure = failure };
            removed = await BuiltInAppsRemoval.RunAsync(backend, ["AV1", "Clock"], null);
            check(!removed.Success && removed.Failed == 1 && removed.NotStarted == 1 && backend.RemoveCalls == 1, "timeout/pending removal stops later apps");
            backend = new() { RestoreFailure = failure };
            restored = await BuiltInAppsRemoval.RunAsync(backend, ["AV1", "Clock"], null, true);
            check(!restored.Success && restored.NotStarted == 1 && backend.RestoreCalls == 1, "timeout/pending restore stops later apps");
        }
        backend = new() { Packages = targets.Select(t => Package(t)).ToList(), AppendFailure = true };
        removed = await BuiltInAppsRemoval.RunAsync(backend, ["AV1", "Clock"], null);
        check(!removed.Success && removed.Succeeded == 1 && removed.NotStarted == 1 && backend.RemoveCalls == 1, "lost audit stops batch without lying about completed app");
        var teams = targets.Single(t => t.Id == "Teams");
        backend = new() { Packages = teams.Families.Select(f => Package(teams, f)).ToList() };
        removed = await BuiltInAppsRemoval.RunAsync(backend, ["Teams"], null);
        check(removed.Success && removed.Succeeded == 1 && backend.RemoveCalls == 2, "both Teams variants removed as one requested app");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string service = File.ReadAllText(Path.Combine(root, "BuiltInAppsService.cs"));
        string ui = File.ReadAllText(Path.Combine(root, "MainWindow.BuiltInApps.cs"));
        check(service.Contains("FindPackagesForUser(string.Empty)") && service.Contains("RemovalOptions.None"), "production current-user enumeration/removal");
        check(!service.Contains("RemovePackageForUserAsync") && !service.Contains("DeprovisionPackageForAllUsersAsync") && !service.Contains("RegistryKey"), "no all-user removal / deprovision / ACL bypass");
        check(service.Contains("DeploymentOperationTimeout.AwaitAsync") && service.Contains("Timeout.InfiniteTimeSpan"), "deployment gate and no premature external kill");
        check(service.Contains("IsMicrosoftStoreSource(source.StandardOutput)"), "production verifies Store endpoint");
        check(ui.Contains("primaryButtonText: \"Uninstall selected\"") && ui.Contains("secondaryButtonText: \"Restore selected\""), "both requested buttons wired");
        check(ui.Contains("new CatalogProgressWindow(window, verb") && ui.Contains("progressWindow.UnavailableItem"), "separate per-app progress and neutral absence");
        check(ui.Contains("IsChecked = false") && ui.Contains("CloseOnPrimary = true") && ui.IndexOf("confirmWindow.ShowAsync()") < ui.IndexOf("BuiltInAppsRemoval.RunAsync"), "no preselection / confirm before mutation");
    }

    private static BuiltInAppPackage Package(BuiltInAppTarget target, string? family = null)
    {
        family ??= target.Families[0];
        int split = family.LastIndexOf('_');
        string name = family[..split];
        return new(name, family, name + "_1.0.0.0_x64_" + family[split..], false, false);
    }
    private static async Task<bool> ThrowsAsync(Func<Task> action)
    { try { await action(); return false; } catch { return true; } }
    private sealed class Updates : IProgress<BuiltInAppUpdate>
    {
        internal List<BuiltInAppUpdate> Items = new();
        public void Report(BuiltInAppUpdate update) => Items.Add(update);
    }
    private sealed class Fake : IBuiltInAppsBackend
    {
        internal List<BuiltInAppPackage> Packages = new();
        internal List<string> Log = new();
        internal Exception? ReadFailure, RemoveFailure, RestoreFailure;
        internal bool SaveFailure, AppendFailure, Saved, SavedRestore, KeepAfterRemove, NoRestore, RestoreUnhealthy, FailReadback;
        internal int RemoveCalls, RestoreCalls, Reads;
        public Task<IReadOnlyList<BuiltInAppPackage>> ReadAsync()
        {
            Reads++;
            if (ReadFailure is not null) throw ReadFailure;
            if (FailReadback && Reads > 1) throw new UnauthorizedAccessException("readback denied");
            return Task.FromResult<IReadOnlyList<BuiltInAppPackage>>(Packages.ToArray());
        }
        public Task SavePlanAsync(bool restore, IReadOnlyList<BuiltInAppTarget> targets, IReadOnlyList<BuiltInAppPackage> packages)
        { if (SaveFailure) throw new IOException("audit denied"); Saved = true; SavedRestore = restore; return Task.CompletedTask; }
        public Task AppendResultAsync(string text)
        { if (AppendFailure) throw new IOException("disk full"); Log.Add(text); return Task.CompletedTask; }
        public Task RemoveAsync(BuiltInAppPackage package, IProgress<CatalogProgressUpdate>? progress)
        {
            if (!Saved) throw new InvalidOperationException("mutation before audit");
            RemoveCalls++; if (RemoveFailure is not null) throw RemoveFailure;
            progress?.Report(new("Removing package", 40));
            if (!KeepAfterRemove) Packages.RemoveAll(p => p.FullName == package.FullName);
            return Task.CompletedTask;
        }
        public Task RestoreAppAsync(BuiltInAppTarget target, IProgress<CatalogProgressUpdate>? progress)
        {
            if (!Saved) throw new InvalidOperationException("restore before audit");
            RestoreCalls++; if (RestoreFailure is not null) throw RestoreFailure;
            if (!NoRestore && !Packages.Any(p => BuiltInAppsCatalog.Matches(target, p)))
                Packages.Add(Package(target) with { Healthy = !RestoreUnhealthy });
            return Task.CompletedTask;
        }
    }
}
