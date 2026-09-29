using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogSelectionTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        ToolToggleDefinition Row(string id) => new(id, "TEST", id, "Synthetic only", false, false);
        var definitions = Enumerable.Range(0, 6).Select(i => Row("item" + i)).ToArray();
        var states = definitions.ToDictionary(d => d.Id, _ => new ToolToggleState(false, true, "OFF"));
        var selected = new HashSet<string>(definitions.Take(5).Select(d => d.Id));
        var plan = CatalogSelectionPlan.Create(definitions, states, d => selected.Contains(d.Id));
        assert(plan.Selected.Count == 5 && plan.ToApply.Count == 5 && plan.ToRestore.Count == 0,
            "Catalog screenshot: five selected OFF rows produce five apply operations, without toggling");

        // Exhaust every combination for a row: selection, availability, applied state.
        foreach (bool chosen in new[] { false, true })
        foreach (bool available in new[] { false, true })
        foreach (bool on in new[] { false, true })
        {
            var row = definitions[0];
            var rowStates = new Dictionary<string, ToolToggleState> { [row.Id] = new(on, available, "test") };
            var result = CatalogSelectionPlan.Create(new[] { row }, rowStates, _ => chosen);
            assert(result.Selected.Count == (chosen && available ? 1 : 0), "Catalog selected scope truth table");
            assert(result.ToApply.Count == (chosen && available && !on ? 1 : 0), "Catalog Apply truth table");
            assert(result.ToRestore.Count == (chosen && available && on ? 1 : 0), "Catalog Restore truth table");
            assert(result.AlreadyAppliedCount == (chosen && available && on ? 1 : 0), "Catalog already-applied count");
            assert(result.AlreadyRestoredCount == (chosen && available && !on ? 1 : 0), "Catalog already-restored count");
        }

        states["item0"] = new(true, true, "ON");
        states["item1"] = new(false, false, "Unavailable");
        states.Remove("item2");
        plan = CatalogSelectionPlan.Create(definitions, states, d => selected.Contains(d.Id));
        assert(plan.Selected.Select(d => d.Id).SequenceEqual(new[] { "item0", "item3", "item4" }),
            "Catalog Restore scope includes selected ON and OFF, excludes unavailable and missing");
        assert(plan.ToApply.Select(d => d.Id).SequenceEqual(new[] { "item3", "item4" }),
            "Catalog Apply skips already-applied and unselected items");
        assert(plan.AlreadyAppliedCount == 1, "Catalog skip count");
        selected.Clear();
        assert(plan.Selected.Count == 3, "Catalog operation snapshot is unaffected by later selection edits");
        assert(CatalogSelectionPlan.Create(definitions, states, _ => false).ToApply.Count == 0,
            "Catalog de-select all has no apply scope");
        assert(CatalogSelectionPlan.Create(definitions, states, _ => true).ToApply.Count == 3,
            "Catalog Select all prepares OFF rows without changing their states");
        assert(!states["item3"].IsOn, "Catalog planning is non-mutating");

        var canonicalBase = Row("canonical") with
        {
            CanonicalActionId = "runtime.telemetry",
            Impact = ToolActionImpact.RuntimeOptimization,
            Evidence = ToolActionEvidence.MechanismUnmeasured
        };
        var duplicateAlias = canonicalBase with { Name = "Telemetry alias", Description = "Same canonical operation" };
        var canonicalStates = new Dictionary<string, ToolToggleState> { ["canonical"] = new(false, true, "OFF") };
        var deduplicated = CatalogSelectionPlan.Create(
            new[] { canonicalBase, duplicateAlias },
            canonicalStates,
            _ => true);
        assert(deduplicated.Selected.Count == 1 && deduplicated.ToApply.Count == 1,
            "Canonical action selected through two UI/preset references executes once");

        var dependency = Row("dependency") with { CanonicalActionId = "runtime.dependency" };
        var dependant = Row("dependant") with
        {
            CanonicalActionId = "runtime.dependant",
            DependsOn = new[] { "runtime.dependency" }
        };
        var dependencyStates = new Dictionary<string, ToolToggleState>
        {
            ["dependency"] = new(false, true, "OFF"),
            ["dependant"] = new(false, true, "OFF")
        };
        var dependencyPlan = CatalogSelectionPlan.Create(
            new[] { dependant, dependency },
            dependencyStates,
            definition => definition.Id == "dependant");
        assert(dependencyPlan.ToApply.Select(definition => definition.Id)
            .SequenceEqual(new[] { "dependency", "dependant" }),
            "Dependencies are included once and ordered before dependant actions");

        bool conflictRejected = false;
        try
        {
            var left = Row("left") with
            {
                CanonicalActionId = "service.mode.disabled",
                ConflictsWith = new[] { "service.mode.manual" }
            };
            var right = Row("right") with { CanonicalActionId = "service.mode.manual" };
            var conflictStates = new Dictionary<string, ToolToggleState>
            {
                ["left"] = new(false, true, "OFF"),
                ["right"] = new(false, true, "OFF")
            };
            CatalogSelectionPlan.Create(new[] { left, right }, conflictStates, _ => true);
        }
        catch (InvalidOperationException) { conflictRejected = true; }
        assert(conflictRejected, "Conflicting canonical actions are rejected before execution");

        bool cycleRejected = false;
        try
        {
            var first = Row("first") with { CanonicalActionId = "cycle.first", DependsOn = new[] { "cycle.second" } };
            var second = Row("second") with { CanonicalActionId = "cycle.second", DependsOn = new[] { "cycle.first" } };
            var cycleStates = new Dictionary<string, ToolToggleState>
            {
                ["first"] = new(false, true, "OFF"),
                ["second"] = new(false, true, "OFF")
            };
            CatalogSelectionPlan.Create(new[] { first, second }, cycleStates, definition => definition.Id == "first");
        }
        catch (InvalidOperationException) { cycleRejected = true; }
        assert(cycleRejected, "Dependency cycles are rejected before execution");

        var analyzerSummary = OptimizationAnalyzer.Summarize(new[]
        {
            new OptimizationSample(DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(10), 100, 200, 1000, 10, 20, 30, 4000, 50, 500, 5000),
            new OptimizationSample(DateTimeOffset.UnixEpoch.AddSeconds(1), TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)), 300, 400, 1000, 30, 40, 50, 4500, 70, 700, 7000)
        });
        assert(analyzerSummary.SampleCount == 2 &&
               analyzerSummary.AveragePhysicalUsedBytes == 200 &&
               analyzerSummary.AverageCommitUsedBytes == 300 &&
               analyzerSummary.AverageCpuPercent == 20 &&
               analyzerSummary.AverageProcesses == 60 &&
               analyzerSummary.AverageThreads == 600 &&
               analyzerSummary.AverageHandles == 6000 &&
               analyzerSummary.SystemDriveFreeBytes == 4500,
            "Optimization analyzer reports averages and final free space without inventing a boost score");

        var firewallProfiles = WindowsSecurityControlsService.ParseFirewallProfiles(
            "Domain|True\r\nPrivate|False\r\nPublic|True\r\n");
        assert(firewallProfiles.Count == 3 &&
               firewallProfiles["domainprofile"] &&
               !firewallProfiles["privateprofile"] &&
               firewallProfiles["publicprofile"],
            "Firewall parser keeps Domain, Private and Public states distinct");

        var service = new RecordingService();
        foreach (var definition in plan.ToApply)
        {
            var result = await CatalogOperationRunner.ExecuteAsync(service, definition, CatalogOperation.Apply);
            assert(result.Success && result.Verified && result.State.IsOn, "Catalog Apply forwards verified result");
        }
        assert(service.Calls.SequenceEqual(new[] { "apply:item3:True", "apply:item4:True" }),
            "Catalog Apply sends true to the backend for exactly the selected pending rows");
        service.Calls.Clear();
        foreach (var definition in plan.ToRestore)
        {
            await CatalogOperationRunner.ExecuteAsync(service, definition, CatalogOperation.RestoreSavedState);
        }
        assert(service.Calls.SequenceEqual(new[] { "restore:item0" }),
            "Catalog Restore skips OFF rows and uses the snapshot API only for applied rows");
        service.Calls.Clear();
        await CatalogOperationRunner.ExecuteAsync(service, definitions[0], CatalogOperation.RestoreWindowsDefaults);
        assert(service.Calls.SequenceEqual(new[] { "default:item0" }), "Catalog Windows defaults remains distinct");
        service.Fail = true;
        var failure = await CatalogOperationRunner.ExecuteAsync(service, definitions[0], CatalogOperation.Apply);
        assert(!failure.Success && !failure.Verified && !failure.State.IsOn, "Catalog failures remain failures");
        service.Fail = false;
        service.MissingSnapshot = true;
        service.Calls.Clear();
        var fallback = await CatalogOperationRunner.ExecuteAsync(
            service,
            definitions[0],
            CatalogOperation.RestoreSavedState);
        assert(fallback.Success && fallback.Verified &&
            service.Calls.SequenceEqual(new[] { "restore:item0", "default:item0" }),
            "Catalog Restore falls back to a documented default when an original snapshot is unavailable");
        service.MissingSnapshot = false;
        service.DefaultFallbackHandled = true;
        service.MissingSnapshot = true;
        service.Calls.Clear();
        await CatalogOperationRunner.ExecuteAsync(service, definitions[0], CatalogOperation.RestoreSavedState);
        assert(service.Calls.SequenceEqual(new[] { "restore:item0" }),
            "Composite child fallback is never repeated against already-restored siblings");
        service.DefaultFallbackHandled = false;
        service.ConfirmMissingSnapshot = false;
        service.Calls.Clear();
        var ambiguous = await CatalogOperationRunner.ExecuteAsync(service, definitions[0], CatalogOperation.RestoreSavedState);
        assert(!ambiguous.Success && service.Calls.SequenceEqual(new[] { "restore:item0" }),
            "English missing-snapshot message alone never authorizes a default reset");
        service.ConfirmMissingSnapshot = true;
        service.MissingSnapshot = false;
        service.Calls.Clear();
        bool invalidRejected = false;
        try { await CatalogOperationRunner.ExecuteAsync(service, definitions[0], (CatalogOperation)99); }
        catch (ArgumentOutOfRangeException) { invalidRejected = true; }
        assert(invalidRejected && service.Calls.Count == 0, "Catalog invalid command cannot call the backend");
    }

    private sealed class RecordingService : IToolToggleService
    {
        public List<string> Calls { get; } = new();
        public bool Fail { get; set; }
        public bool MissingSnapshot { get; set; }
        public bool ConfirmMissingSnapshot { get; set; } = true;
        public bool DefaultFallbackHandled { get; set; }
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => Array.Empty<ToolToggleDefinition>();
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition) =>
            Task.FromResult(new ToolToggleState(false, true, "fake"));
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
        {
            Calls.Add($"apply:{definition.Id}:{targetOn}");
            return Task.FromResult(new ToolToggleOperationResult(!Fail, !Fail, "fake", new(targetOn && !Fail, true, "fake")));
        }
        public Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition definition)
        {
            Calls.Add("restore:" + definition.Id);
            if (MissingSnapshot)
            {
                return Task.FromResult(new ToolToggleOperationResult(
                    false,
                    false,
                    "No original snapshot exists. Use Restore Windows defaults explicitly if that is intended.",
                    new ToolToggleState(true, true, "ON"), DefaultFallbackHandled, OriginalBackupMissing: ConfirmMissingSnapshot));
            }
            // A captured state may itself be ON. Restore must not force OFF.
            return Task.FromResult(new ToolToggleOperationResult(true, true, "fake snapshot", new(true, true, "fake")));
        }
        public Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(ToolToggleDefinition definition)
        {
            Calls.Add("default:" + definition.Id);
            return Task.FromResult(new ToolToggleOperationResult(true, true, "fake default", new(false, true, "fake")));
        }
    }
}
