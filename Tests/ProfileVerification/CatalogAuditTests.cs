using Naufal_Windows_Tech_s_Powertoys;

internal static class CatalogAuditTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        var widget = new ToolToggleDefinition("Widgets", "SYSTEM / HIGH RISK", "Widgets - Remove", "Removes Widgets; restore reinstalls it.", true, true);
        assert(widget.Name == "Taskbar Widgets", "Widgets title is simplified at the model boundary");
        assert(widget.Id == "Widgets" && widget.Description.StartsWith("Removes"), "Renaming preserves snapshot identity and effect description");
        assert((widget with { Category = "COMPONENTS" }).Name == "Taskbar Widgets", "Composites retain simplified names");
        assert(CatalogDisplayNames.Simplify("Taskbar Widgets") == "Taskbar Widgets", "Name normalization is idempotent");
        assert(CatalogDisplayNames.Simplify("Unknown Vendor Service") == "Unknown Vendor Service", "Unknown titles are never guessed");
        foreach (string code in new[] { "en", "id", "de", "fr", "ar", "tl", "vi", "zh-CN", "zh-TW", "th", "ru", "uk", "pt", "ja", "ko", "ur", "ta", "hi", "ms", "jv", "ban", "sv", "es" })
            assert(!string.IsNullOrWhiteSpace(CatalogDisplayNames.LocalizedTitle("Taskbar Widgets", code)), "Simplified Widgets label exists: " + code);
        var action = new ToolActionDefinition("Temp", "SYSTEM", "Temporary Files - Remove", "Deletes temporary files.", "Remove", true, false);
        assert(action.Name == "Temporary Files" && action.RunLabel == "Remove", "Only the title changes, not the command button");

        foreach (bool selected in new[] { false, true })
        foreach (bool available in new[] { false, true })
        foreach (bool applied in new[] { false, true })
        foreach (bool partial in new[] { false, true })
        {
            var states = new Dictionary<string, ToolToggleState> { [widget.Id] = new(applied, available, "synthetic", HasAppliedParts: partial) };
            var plan = CatalogSelectionPlan.Create(new[] { widget }, states, _ => selected);
            assert(plan.ToApply.Count == (selected && available && !applied ? 1 : 0), "Partial-state Apply truth table");
            assert(plan.ToRestore.Count == (selected && available && (applied || partial) ? 1 : 0), "Partial-state Restore truth table");
        }

        var progress = new CatalogProgressState(new[] { "done", "active", "pending" });
        progress.Update("done", "COMPLETED", 100, "verified");
        progress.Update("active", "RUNNING", null, "Windows is working");
        assert(progress.Items["active"].Percent is null, "Unknown duration has no fabricated 15 percent");
        progress.Update("active", "RUNNING", 42, "Windows package step");
        assert(progress.Items["active"].Percent == 42 && progress.Settled == 1, "Backend percent is not a completed catalog item");
        progress.Update("active", "VERIFYING", null, "Verifying");
        assert(progress.Items["active"].Percent is null, "Verification does not fabricate 85 percent");
        progress.Finish(false, "Deployment timed out");
        assert(progress.Items["done"].Phase == "COMPLETED", "Batch failure preserves completed rows");
        assert(progress.Items["active"].Phase == "FAILED", "Aborted active row becomes failed");
        assert(progress.Items["pending"].Phase == "SKIPPED", "Unstarted row becomes skipped");
        assert(progress.Settled == 3 && !progress.AllVerified, "Settled does not mean successful");
        assert(!progress.Update("active", "RUNNING", 90, "late") && progress.Items["active"].Phase == "FAILED", "Late Windows callbacks cannot revive finished tasks");
        var missing = new CatalogProgressState(new[] { "missing" });
        missing.Finish(true, "claimed success");
        assert(!missing.AllVerified && missing.Items["missing"].Phase == "NOT VERIFIED", "Success requires item verification, not merely a batch result");
        var verified = new CatalogProgressState(new[] { "a" });
        verified.Update("a", "COMPLETED", 100, "verified");
        verified.Finish(true, "done");
        assert(verified.AllVerified, "Verified completion stays successful");

        assert(FirstRunWizardPolicy.ShouldShow("{\"Schema\":2,\"Completed\":false,\"Suppress\":false}", 2), "Skip shows wizard next startup");
        assert(!FirstRunWizardPolicy.ShouldShow("{\"Schema\":1,\"Suppress\":true}", 2), "Don't show again survives schema upgrade");
        assert(!FirstRunWizardPolicy.ShouldShow("{\"Schema\":2,\"Completed\":true}", 2), "Completed wizard does not recur");
        assert(FirstRunWizardPolicy.ShouldShow("{\"Schema\":1,\"Completed\":true}", 2), "Incomplete upgraded prerequisites are offered");
        assert(FirstRunWizardPolicy.ShouldShow("broken", 2), "Damaged wizard state is recoverable");

        var gate = new BoundedOperationGate();
        var probe = new BoundedReadProbe<int>();
        using var readRelease = new ManualResetEventSlim(false);
        int readStarts = 0;
        int ReadOnly() { Interlocked.Increment(ref readStarts); readRelease.Wait(); return 23; }
        for (int i = 0; i < 2; i++)
        {
            bool timedOut = false;
            try { await probe.ReadAsync(ReadOnly, TimeSpan.FromMilliseconds(25)); }
            catch (TimeoutException) { timedOut = true; }
            assert(timedOut, "Read-only inventory wait is bounded");
        }
        assert(readStarts == 1, "Repeated inventory timeouts do not accumulate blocked workers");
        readRelease.Set();
        assert(await probe.ReadAsync(() => 23, TimeSpan.FromSeconds(2)) == 23, "Inventory can finish after the caller timeout");
        assert(BcdRestoreVerification.Matches("debug", "No", "Off"), "BCD boolean aliases verify consistently");
        assert(BcdRestoreVerification.Matches("timeout", "1", "1"), "Saved applied value may be a valid restore target");
        assert(!BcdRestoreVerification.Matches("timeout", "30", "1"), "BCD default verification checks the actual target, not inverse ON");
        assert(!BcdRestoreVerification.Matches("debug", null, "No"), "Absent BCD setting differs from explicit OFF");
        assert(BcdRestoreVerification.Matches("debug", null, null), "Absent BCD default verifies only after successful read");
        var timeout = TimeSpan.FromMilliseconds(30);
        var grace = TimeSpan.FromMilliseconds(20);
        int closes = 0, cancels = 0;
        assert(await gate.RunAsync(() => Task.FromResult(7), () => cancels++, () => closes++, "test", timeout, grace) == 7,
            "Bounded deployment preserves success");
        assert(closes == 1 && cancels == 0, "Successful deployment closes exactly once without cancel");

        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        string message = "";
        try { await gate.RunAsync(() => pending.Task, () => cancels++, () => closes++, "test", timeout, grace); }
        catch (TimeoutException ex) { message = ex.Message; }
        assert(message.Contains("not confirmed") && closes == 1 && cancels == 1,
            "Timeout does not claim cancellation or close a pending operation");
        bool started = false, blocked = false;
        try { await gate.RunAsync(() => { started = true; return Task.FromResult(2); }, () => { }, () => { }, "next", timeout, grace); }
        catch (InvalidOperationException) { blocked = true; }
        assert(blocked && !started, "Pending cancellation blocks the next factory BEFORE mutation starts");
        pending.SetException(new InvalidOperationException("late failure"));
        for (int i = 0; i < 100 && Volatile.Read(ref closes) < 2; i++) await Task.Delay(5);
        assert(closes == 2, "Late fault is observed and releases operation resources");
        assert(await gate.RunAsync(() => Task.FromResult(9), () => { }, () => { }, "next", timeout, grace) == 9,
            "Gate reopens only after the previous operation finishes");

        var canceled = new TaskCompletionSource<int>();
        message = "";
        try { await gate.RunAsync(() => canceled.Task, () => canceled.SetCanceled(), () => { }, "cancel", timeout, grace); }
        catch (TimeoutException ex) { message = ex.Message; }
        assert(message.Contains("acknowledged cancellation"), "Acknowledged cancellation has distinct truthful diagnostic");
        var lateSuccess = new TaskCompletionSource<int>();
        assert(await gate.RunAsync(() => lateSuccess.Task, () => lateSuccess.SetResult(11), () => { }, "late", timeout, grace) == 11,
            "Completion during grace returns the actual successful result");
        bool originalFault = false;
        try { await gate.RunAsync<int>(() => throw new InvalidOperationException("factory failed"), () => { }, () => throw new Exception("cleanup"), "factory", timeout, grace); }
        catch (InvalidOperationException ex) { originalFault = ex.Message == "factory failed"; }
        assert(originalFault, "Cleanup cannot mask a factory failure");
        assert(await gate.RunAsync(() => Task.FromResult(12), () => { }, () => { }, "next", timeout, grace) == 12,
            "Factory exception releases the gate");
    }
}
