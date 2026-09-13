using Naufal_Windows_Tech_s_Powertoys;

internal static class GameModeToggleTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var gameMode = new ToolToggleDefinition("GameMode", "Gaming", "Game Mode", "", true, false, IsFeatureSwitch: true);
        var ordinary = gameMode with { Id = "Other", IsFeatureSwitch = false };
        check(CatalogTogglePolicy.FromSwitch(gameMode, false) == CatalogOperation.SetOff, "Game Mode OFF is not Restore");
        check(CatalogTogglePolicy.FromSwitch(gameMode, true) == CatalogOperation.Apply, "Game Mode ON applies explicit state");
        check(CatalogTogglePolicy.FromSwitch(ordinary, false) == CatalogOperation.RestoreSavedState, "other catalogs retain snapshot semantics");
        foreach (int? original in new int?[] { null, 0, 1 })
        {
            int? value = original, backup = null;
            bool captured = false;
            foreach (bool on in new[] { false, true, false, true })
            {
                GameModeSetting.Apply(on, () => { if (!captured) { backup = value; captured = true; } }, number => value = number);
                check(value == (on ? 1 : 0), "Game Mode writes requested ON/OFF independent of backup");
                check(captured && backup == original, "original Game Mode state retained across toggles");
            }
        }
        bool wrote = false;
        try { GameModeSetting.Apply(false, () => throw new IOException("snapshot denied"), _ => wrote = true); } catch (IOException) { }
        check(!wrote, "snapshot failure prevents Game Mode mutation");
        foreach (bool on in new[] { false, true })
        {
            var plan = CatalogSelectionPlan.Create([gameMode], new Dictionary<string, ToolToggleState> { [gameMode.Id] = new(on, true, "fixture") }, _ => true);
            check(plan.ToRestore.Count == 1, "explicit Restore remains possible when Game Mode is OFF or ON");
        }
        var fake = new Fake();
        var off = await CatalogOperationRunner.ExecuteAsync(fake, gameMode, CatalogTogglePolicy.FromSwitch(gameMode, false));
        check(fake.Target == false && fake.Restores == 0 && off.Verified, "OFF routes to SetState(false) without Restore");
        await CatalogOperationRunner.ExecuteAsync(fake, gameMode, CatalogOperation.RestoreSavedState);
        check(fake.Restores == 1, "Restore button still routes to saved-state restore");
    }
    private sealed class Fake : IToolToggleService
    {
        internal bool? Target;
        internal int Restores;
        public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => [];
        public Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition _) => Task.FromResult(new ToolToggleState(true, true, "fixture"));
        public Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition _, bool targetOn)
        { Target = targetOn; return Task.FromResult(new ToolToggleOperationResult(true, true, "verified", new(targetOn, true, "fixture"))); }
        public Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition _)
        { Restores++; return Task.FromResult(new ToolToggleOperationResult(true, true, "restored", new(true, true, "fixture"))); }
    }
}
