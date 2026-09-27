using Naufal_Windows_Tech_s_Powertoys;

internal static class GamingOwnershipTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var core = new Recorder("DynamicTick", "HPET", "GameMode", "GameDVR", "HAGS", "MPO", "WindowedOptimizations");
        var boot = new Recorder("BcdTscSyncEnhanced", "BcdHypervisorOff");
        var registry = new Recorder("CpuPowerLatencyOptimization", "KernelTimerLatency");
        IToolToggleService catalog = GamingCatalogOwnership.Create(core, boot, registry);
        var definitions = catalog.GetDefinitions();
        check(definitions.Select(item => item.Id).SequenceEqual(new[]
        {
            "GameMode", "GameDVR", "HAGS", "MPO", "WindowedOptimizations",
            "BcdTscSyncEnhanced", "BcdHypervisorOff", "CpuPowerLatencyOptimization", "KernelTimerLatency"
        }), "Gaming ownership removes only the two profile-controlled timer entries");
        check(core.GetDefinitions().Count == 7, "UI filtering does not delete backend definitions");
        foreach (var definition in definitions)
        {
            Recorder owner = core.GetDefinitions().Any(item => item.Id == definition.Id) ? core :
                boot.GetDefinitions().Any(item => item.Id == definition.Id) ? boot : registry;
            await catalog.ReadStateAsync(definition);
            check(owner.Last == "read:" + definition.Id, "Independent read routes to original backend " + definition.Id);
            foreach (bool on in new[] { false, true })
            {
                var result = await catalog.SetStateAsync(definition, on);
                check(owner.Last == $"set:{definition.Id}:{on}" && result.State.IsOn == on,
                    "Independent ON/OFF direction preserved " + definition.Id);
            }
            await catalog.RestoreOriginalAsync(definition);
            check(owner.Last == "original:" + definition.Id, "Exact Restore preserved " + definition.Id);
            await catalog.RestoreWindowsDefaultAsync(definition);
            check(owner.Last == "default:" + definition.Id, "Windows Default remains separate " + definition.Id);
            owner.Result = new(false, false, "fixture mismatch", new(false, true, "mismatch"));
            check(!(await catalog.SetStateAsync(definition, true)).Verified, "Ownership cannot mask verification failure " + definition.Id);
            owner.Result = null;
        }
        foreach (string id in new[] { "DynamicTick", "HPET", "Unregistered" })
        {
            ToolToggleDefinition hidden = new(id, "", "", "", false, false);
            Func<Task>[] attempts =
            [
                async () => { await catalog.ReadStateAsync(hidden); },
                async () => { await catalog.SetStateAsync(hidden, true); },
                async () => { await catalog.SetStateAsync(hidden, false); },
                async () => { await catalog.RestoreOriginalAsync(hidden); },
                async () => { await catalog.RestoreWindowsDefaultAsync(hidden); }
            ];
            foreach (var attempt in attempts)
            {
                bool rejected = false;
                try { await attempt(); } catch (KeyNotFoundException) { rejected = true; }
                check(rejected, "Hidden profile setting cannot bypass catalog routing " + id);
            }
        }
        var states = definitions.ToDictionary(item => item.Id, _ => new ToolToggleState(true, true, "fixture"));
        var plan = CatalogSelectionPlan.Create(definitions, states, _ => true);
        check(plan.ToRestore.Count == definitions.Count && !plan.Selected.Any(item => item.Id is "DynamicTick" or "HPET"),
            "Bulk selection/restore cannot include removed profile controls");
        foreach (string id in new[] { "GameMode", "GameDVR", "Hags", "Mpo", "WindowedOptimization" })
            check(!typeof(ProfileVerificationInput).GetProperties().Any(property => property.Name.Equals(id, StringComparison.OrdinalIgnoreCase)),
                "Independent state is not part of profile identity input " + id);

        CaptureSwitch(check);
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string main = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        check(main.Contains("_gamingCatalog ??= GamingCatalogOwnership.Create("), "Main UI uses audited ownership factory");
        check(main.Contains("GamingCatalogOwnership.Notice +"), "Gaming informational ownership note is wired");
        string gaming = File.ReadAllText(Path.Combine(root, "GamingTweaksService.cs"));
        check(gaming.Contains("GameDvrSetting.Apply(targetOn,"), "Explicit capture switch uses tested writer");
        check(gaming.Contains("if (targetOn && verified && !definition.IsFeatureSwitch) DeleteBackup"),
            "Feature switch does not discard the original baseline on ON");
        check(gaming.Contains("RestoreGameDvrDefaults();") && gaming.Contains("case \"DynamicTick\":") && gaming.Contains("case \"HPET\":"),
            "Capture defaults and legacy timer backends remain intact");
    }

    private static void CaptureSwitch(Action<bool, string> check)
    {
        string[] names = ["GameDVR_Enabled", "AppCaptureEnabled", "HistoricalCaptureEnabled", "AllowGameDVR"];
        foreach (int? initial in new int?[] { null, 0, 1 })
        {
            var values = names.ToDictionary(name => name, _ => initial);
            Dictionary<string, int?>? original = null;
            foreach (bool enabled in new[] { false, true, false, true })
            {
                var writes = new List<string>();
                GameDvrSetting.Apply(enabled, () => original ??= new(values), (name, value) =>
                {
                    check(original is not null && original.Count == 4, "Capture entire baseline before writing any value");
                    values[name] = value;
                    writes.Add(name);
                });
                foreach (string name in names.Where(name => name != "HistoricalCaptureEnabled"))
                    check(values[name] == (enabled ? 1 : 0), "Capture switch writes explicit state " + name);
                check(writes.Contains("HistoricalCaptureEnabled") == !enabled, "ON does not opt into background recording");
                check(original!.Values.All(value => value == initial), "Repeated capture switches preserve the first original baseline");
            }
            values = new(original!);
            check(values.Values.All(value => value == initial), "Saved baseline can restore absent, disabled, and enabled values");
        }
        bool wrote = false;
        try { GameDvrSetting.Apply(true, () => throw new IOException("snapshot denied"), (_, _) => wrote = true); }
        catch (IOException) { }
        check(!wrote, "Failed capture snapshot prevents all writes");
        var definition = new ToolToggleDefinition("GameDVR", "", "", "", true, false, IsFeatureSwitch: true);
        check(CatalogTogglePolicy.FromSwitch(definition, false) == CatalogOperation.SetOff, "Capture OFF does not mean Restore");
        check(CatalogTogglePolicy.FromSwitch(definition, true) == CatalogOperation.Apply, "Capture ON means explicit enable");
    }

    private sealed class Recorder(params string[] ids) : IToolToggleService
    {
        internal string Last = "";
        internal ToolToggleOperationResult? Result;
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => ids.Select(id =>
            new ToolToggleDefinition(id, "Fixture", id, "", false, false,
                IsFeatureSwitch: id is "GameMode" or "GameDVR")).ToArray();
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
        { Last = "read:" + definition.Id; return Task.FromResult(new ToolToggleState(true, true, "fixture")); }
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool on) => Complete($"set:{definition.Id}:{on}", on);
        public Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition definition) => Complete("original:" + definition.Id, false);
        public Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(ToolToggleDefinition definition) => Complete("default:" + definition.Id, false);
        private Task<ToolToggleOperationResult> Complete(string action, bool on)
        { Last = action; return Task.FromResult(Result ?? new(true, true, "fixture", new(on, true, "fixture"))); }
    }
}
