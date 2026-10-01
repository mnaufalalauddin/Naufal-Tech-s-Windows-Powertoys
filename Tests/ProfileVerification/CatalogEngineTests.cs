using System.Text.Json;
using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogEngineTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        ToolToggleDefinition Row(string id) => new(id, "TEST", id, "Synthetic", false, false);
        var a = Row("a"); var b = Row("b");
        var effect = CatalogEffect.Registry("LocalMachine", @"Software\Example", "Enabled", "DWord", 0);
        check(effect == CatalogEffect.Registry("HKLM:", "/software/example/", "enabled", "DWord", 0), "Effect identity normalizes registry roots, paths and names");
        check(effect != CatalogEffect.Registry("HKCU", @"Software\Example", "Enabled", "DWord", 0), "Effect scope separates current user from machine");
        check(effect.Desired != CatalogEffect.Registry("HKLM", @"Software\Example", "Enabled", "DWord", null, true).Desired, "Absent is distinct from zero");
        var service = new Fake([a, b], new() { ["a"] = [effect], ["b"] = [effect] });
        var plan = CatalogEffectPlan.Create(service, [a, a]);
        check(plan.Actions.Count == 1 && plan.RepeatedReferences == 1 && plan.Conflicts.Count == 0, "Canonical references deduplicate without a false conflict");
        plan = CatalogEffectPlan.Create(service, [a, b]);
        check(plan.SharedEffects.Count == 1 && plan.Conflicts.Count == 1, "Shared effects with separate snapshots are blocked until restore ownership is consolidated");
        service.Effects["a"] = [effect with { SharedOriginalOwner = "AuditedOwner" }];
        service.Effects["b"] = [effect with { SharedOriginalOwner = "AuditedOwner" }];
        plan = CatalogEffectPlan.Create(service, [a, b]);
        check(plan.SharedEffects.Count == 1 && plan.Conflicts.Count == 0, "Equal effects with explicitly consolidated originals can coexist");
        service.Effects["b"] = [effect with { SharedOriginalOwner = "DifferentOwner" }];
        check(CatalogEffectPlan.Create(service, [a, b]).Conflicts.Count == 1, "Different canonical originals remain a conflict");
        service.Effects["b"] = [effect with { SharedOriginalOwner = "AuditedOwner", Desired = "DWord:1" }];
        check(CatalogEffectPlan.Create(service, [a, b]).Conflicts.Count == 1, "Shared originals do not authorize conflicting desired values");
        service.Effects["b"] = [effect];
        check(CatalogEffectPlan.Create(service, [a, b]).Conflicts.Count == 1, "Unmigrated participant cannot inherit another owner's opt-in");
        service.Effects["a"] = [effect];
        service.Effects["b"] = [effect with { Desired = "DWord:1" }];
        plan = CatalogEffectPlan.Create(service, [a, b]);
        check(plan.Conflicts.Count == 1 && plan.SharedEffects.Count == 0, "Opposing values are explicit conflicts");
        service.Effects["b"] = [];
        check(CatalogEffectPlan.Create(service, [b]).UndeclaredActions == 1, "Undeclared actions are not presented as fully audited");
        check(CatalogEffectPlan.Create(service, [a, a with { Description = "different" }]).Conflicts.Count == 1, "Conflicting canonical definitions are rejected");
        var second = new OtherFake([a], new() { ["a"] = [effect] });
        var wrapper = new Group(service, second, a);
        check(CatalogEffectPlan.Create(wrapper, [a]).SharedEffects.Count == 1, "Same ID in different backends remains a distinct snapshot owner");
        check(service.Writes == 0 && second.Writes == 0, "Planning never executes writes");
        check(CatalogPlanSafety.ApplyConflicts([Row("securitymitigationsperformance")]).Single().Contains("Unsupported Apply"), "Legacy mitigation bundle rejected before bulk mutation, case-insensitive stable ID");
        check(CatalogPlanSafety.ApplyConflicts([a]).Count == 0, "Unrelated actions retain Apply");
        var inventory = CatalogInventoryReport.Build([("Synthetic catalog", service)]);
        check(inventory.Any(r => r.Property == "Stable ID / UI owner" && r.Value == "a / Synthetic catalog"), "Inventory retains stable ID and UI ownership");
        check(inventory.Any(r => r.Property == "Target coverage" && r.Value.Contains("Not yet declared")), "Inventory exposes unimplemented target metadata");
        check(inventory.Any(r => r.Property == effect.Target && r.Value == effect.Desired), "Inventory reports declared target value");
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string lab = File.ReadAllText(Path.Combine(root, "PerformanceLabService.cs"));
        check(lab.Contains("targetOn && LegacyMitigationPolicy.IsBlocked(definition.Id)") && lab.Contains("ChangeStateAsync(definition, targetOn: false, restoreOriginal: true)"), "Backend gate blocks Apply only; original restore path remains callable");

        string directory = Path.Combine(Path.GetTempPath(), "NWU-journal-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var result = new ToolToggleOperationResult(true, true, "test", new(true, true, "ON"), BeforeState: new(false, true, "OFF"));
            long scanEpoch = CatalogStateEpoch.Version;
            int invalidations = 0;
            void Changed() => invalidations++;
            CatalogStateEpoch.Changed += Changed;
            var saved = await CatalogOperationJournal.RunAsync(directory, a, CatalogOperation.Apply, () =>
            {
                using var started = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()));
                check(started.RootElement.GetProperty("Phase").GetString() == "Started" && started.RootElement.GetProperty("Outcome").GetString() == "Unknown", "Durable intent exists before mutation, without inferred success");
                return Task.FromResult(result);
            });
            CatalogStateEpoch.Changed -= Changed;
            check(CatalogStateEpoch.Version == scanEpoch + 2 && invalidations == 2, "Journal invalidates older catalog scans before and after execution");
            CatalogStateEpoch.Invalidate();
            check(invalidations == 2, "Closed catalog listener can unsubscribe without leaks");
            using (var document = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single())))
            {
                check(document.RootElement.GetProperty("Outcome").GetString() == "Applied", "Completed operation persists actual outcome");
                check(document.RootElement.GetProperty("Before").GetString() == "OFF" && document.RootElement.GetProperty("After").GetString() == "ON", "Journal records before/after evidence");
            }
            check(saved.Success && saved.Message.Contains("Journal:"), "Journal path is attached without changing success");
            bool threw = false;
            try { await CatalogOperationJournal.RunAsync(directory, b, CatalogOperation.Apply, () => throw new InvalidOperationException("simulated interruption")); }
            catch (InvalidOperationException) { threw = true; }
            check(threw && Directory.GetFiles(directory, "*.json").Length == 2, "Operation exception is retained and logged separately");
            var failedPath = Directory.GetFiles(directory, "*.json").Single(p => File.ReadAllText(p).Contains("simulated interruption"));
            using (var failed = JsonDocument.Parse(File.ReadAllText(failedPath)))
                check(failed.RootElement.GetProperty("Outcome").GetString() == "Unknown", "Exception does not invent rollback or successful completion");
            bool executed = false;
            long beforeRejected = CatalogStateEpoch.Version;
            try { await CatalogOperationJournal.RunAsync(failedPath, a, CatalogOperation.Apply, () => { executed = true; return Task.FromResult(result); }); }
            catch (IOException) { }
            check(!executed, "Initial journal write failure prevents mutation");
            check(CatalogStateEpoch.Version == beforeRejected, "Initial journal failure does not invalidate state without an execution attempt");
            FileStream? heldJournal = null;
            try
            {
                var existing = Directory.GetFiles(directory, "*.json").ToHashSet();
                var logged = await CatalogOperationJournal.RunAsync(directory, Row("locked"), CatalogOperation.Apply, () =>
                {
                    string startedPath = Directory.GetFiles(directory, "*.json").Single(p => !existing.Contains(p));
                    heldJournal = new FileStream(startedPath, FileMode.Open, FileAccess.Read, FileShare.None);
                    return Task.FromResult(result);
                });
                check(logged.Success && logged.Verified && logged.Message.Contains("Completion journal could not be saved"), "Completion journal failure preserves successful mutation result and reports warning");
            }
            finally { heldJournal?.Dispose(); }
            check(CatalogOperationJournal.Outcome(a with { RestartRecommended = true }, result) == "RebootRequired", "Configuration verification is not post-reboot verification");
            check(CatalogOperationJournal.Outcome(a, result with { AlreadyApplied = true }) == "AlreadyApplied", "Already-applied is distinct");
            check(CatalogOperationJournal.Outcome(a, result with { Success = false, Verified = false, State = new(false, false, "Unknown") }) == "Unknown", "Unreadable state is not falsely classified as unapplied");
            check(CatalogOperationJournal.Outcome(a, result with { Success = false, Verified = false, SkippedUnavailable = true, State = ToolToggleState.Unavailable("absent") }) == "NotApplicable", "Confirmed absence is not a failure");
            check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Atomic journal writes leave no temporary files on success");
            string main = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
            check(main.Split("if (loadedAtEpoch != CatalogStateEpoch.Version)").Length - 1 == 2, "Apply and Restore both recheck catalog epoch after queue admission");
            check(main.Contains("statesLoaded = loadedAtEpoch == CatalogStateEpoch.Version;") && main.Contains("finally { CatalogStateEpoch.Changed -= OnCatalogStateChanged; }"), "Concurrent scan invalidation and window listener cleanup are wired");
        }
        finally
        {
            // Only files in this exact test-created directory; no recursive deletion.
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private class Fake(IReadOnlyList<ToolToggleDefinition> definitions, Dictionary<string, IReadOnlyList<CatalogEffect>> effects) : IToolToggleService, ICatalogEffectSource
    {
        internal Dictionary<string, IReadOnlyList<CatalogEffect>> Effects = effects;
        internal int Writes;
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => definitions;
        public IReadOnlyList<CatalogEffect> GetEffects(ToolToggleDefinition definition) => Effects[definition.Id];
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition) => Task.FromResult(new ToolToggleState(false, true, "OFF"));
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn) { Writes++; throw new InvalidOperationException("Unexpected mutation"); }
    }
    private sealed class OtherFake(IReadOnlyList<ToolToggleDefinition> definitions, Dictionary<string, IReadOnlyList<CatalogEffect>> effects) : Fake(definitions, effects);
    private sealed class Group(Fake first, Fake second, ToolToggleDefinition row) : Fake([row], new()), ICatalogPlanSource
    {
        public IReadOnlyList<CatalogPlanAction> GetPlanActions(ToolToggleDefinition definition) => [new(first, row), new(second, row)];
    }
}
