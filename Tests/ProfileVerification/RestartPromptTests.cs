using Naufal_Windows_Tech_s_Powertoys;

internal static class RestartPromptTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        ToolToggleDefinition d = new("HAGS", "GPU", "GPU scheduling", "", true, true);
        ToolToggleOperationResult good = new(true, true, "", new(true, true, "Applied"));
        check(RestartPromptPolicy.ForToggle(d, good), "verified restart-sensitive tweak prompts");
        check(!RestartPromptPolicy.ForToggle(d, good with { Success = false }), "failed tweak does not prompt");
        check(!RestartPromptPolicy.ForToggle(d, good with { Verified = false }), "unverified tweak does not prompt");
        check(!RestartPromptPolicy.ForToggle(d, good with { SkippedUnavailable = true }), "skipped tweak does not prompt");
        check(!RestartPromptPolicy.ForToggle(d, good with { State = ToolToggleState.Unavailable("absent") }), "absent component does not prompt");
        check(!RestartPromptPolicy.ForToggle(d with { RestartRecommended = false }, good), "immediate setting does not prompt");
        foreach (string id in new[] { "IconCache", "EndTask", "ClassicContext", "PhotoViewer", "Widgets", "ExplorerHomeGallery", "Hibernation" })
            check(!RestartPromptPolicy.RequiresWindowsRestart(id, true), id + " needs no OS restart prompt");
        check(RestartPromptPolicy.RequiresWindowsRestart("ModernStandbyOverride", false), "standby override remains restart-sensitive");
        check(RestartPromptPolicy.RequiresWindowsRestart("Service", false, "RUNNING UNTIL STOP/REBOOT"), "deferred service stop prompts");
        ToolActionDefinition action = new("USB", "", "USB power", "", "Apply", true, true, RestartRecommended: true);
        check(RestartPromptPolicy.ForAction(action, new(true, "")), "successful action prompts in either direction");
        check(!RestartPromptPolicy.ForAction(action, new(false, "")), "failed action does not prompt");
        check(!RestartPromptPolicy.ForAction(action, new(true, "", true)), "unavailable action does not prompt");
        check(!RestartPromptPolicy.ForAction(action with { RestartRecommended = false }, new(true, "")), "ordinary action does not prompt");

        RestartPromptCoordinator queue = new();
        int prompts = 0, restarts = 0;
        bool busy = true;
        Task<bool> Later(string[] names) { prompts++; check(names.Length == 1, "deduplicated batch"); return Task.FromResult(false); }
        Task Restart() { restarts++; return Task.CompletedTask; } // Never calls the operating system.
        queue.Add("HAGS"); queue.Add("hags"); queue.Add("");
        await queue.OfferAsync(() => busy, Later, Restart);
        check(prompts == 0 && queue.PendingCount == 1, "busy tasks defer prompt");
        busy = false;
        await queue.OfferAsync(() => busy, Later, Restart);
        await queue.OfferAsync(() => busy, Later, Restart);
        check(prompts == 1 && restarts == 0 && queue.PendingCount == 0, "Later never reboots or nags");
        queue.Add("NTFS");
        await queue.OfferAsync(() => busy, _ => { busy = true; return Task.FromResult(true); }, Restart);
        check(restarts == 0 && queue.PendingCount == 1, "new task during consent defers restart");
        busy = false;
        var confirmation = new TaskCompletionSource<bool>();
        Task pending = queue.OfferAsync(() => busy, _ => confirmation.Task, Restart);
        await queue.OfferAsync(() => busy, Later, Restart);
        check(queue.IsPromptActive && prompts == 1, "only one confirmation can be open");
        confirmation.SetResult(true);
        await pending;
        check(restarts == 1 && queue.RestartRequested && !queue.IsPromptActive, "explicit acceptance calls injected restart once");
        queue.Add("Another change");
        await queue.OfferAsync(() => false, _ => Task.FromResult(true), Restart);
        check(restarts == 1, "accepted restart cannot dispatch twice");
        RestartPromptCoordinator failing = new();
        failing.Add("BCD");
        try { await failing.OfferAsync(() => false, _ => Task.FromResult(true), () => throw new InvalidOperationException("fake failure")); }
        catch (InvalidOperationException) { }
        check(!failing.IsPromptActive && !failing.RestartRequested, "restart failure releases guard without false success");
        await failing.OfferAsync(() => false, Later, Restart);
        check(restarts == 1, "restart failure is not retried automatically");
    }
}
