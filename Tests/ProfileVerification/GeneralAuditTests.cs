using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class GeneralAuditTests
{
    public static async Task RunAsync(Action<bool, string> assert)
    {
        await DownloadsAsync(assert);
        await SchedulerAsync(assert);
        Gates(assert);
        Snapshots(assert);
        NativeCommandRunner runner = new();
        await ThrowsAsync<OperationCanceledException>(() => runner.RunAsync("nonexistent-audit-command.exe", Array.Empty<string>(),
            TimeSpan.FromSeconds(1), new CancellationToken(true)), assert, "cancelled command never launches");
        await ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync("nonexistent-audit-command.exe", Array.Empty<string>(),
            TimeSpan.FromMilliseconds(-2)), assert, "invalid timeout rejected before process launch");
        foreach (TimeSpan timeout in new[] { TimeSpan.Zero, TimeSpan.FromTicks(1), TimeSpan.FromTicks(-1) })
            await ThrowsAsync<ArgumentOutOfRangeException>(() => runner.RunAsync("nonexistent-audit-command.exe", Array.Empty<string>(),
                timeout), assert, "expired or sub-millisecond timeout rejected before process launch: " + timeout);
    }

    private static async Task DownloadsAsync(Action<bool, string> assert)
    {
        string directory = Path.Combine(Path.GetTempPath(), "Powertoys-Download-Audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "package.bin");
        string unrelated = Path.Combine(directory, "other.download");
        byte[] bytes = Enumerable.Range(0, 2048).Select(i => (byte)(i % 251)).ToArray();
        await File.WriteAllTextAsync(unrelated, "unrelated partial");
        try
        {
            long lastProgress = 0;
            using MemoryStream source = new(bytes);
            long count = await AtomicDownloadFile.SaveAsync(source, target, bytes.Length, 1024, 4096,
                received => lastProgress = received, default);
            assert(count == bytes.Length && lastProgress == bytes.Length, "download reports all bytes");
            assert((await File.ReadAllBytesAsync(target)).SequenceEqual(bytes), "exclusive download handle closed before rename");
            assert(source.CanRead, "download does not dispose caller's input");

            async Task BadDownload(byte[] payload, long? expected, long minimum, long maximum, string label)
            {
                using MemoryStream input = new(payload);
                await ThrowsAsync<InvalidDataException>(() => AtomicDownloadFile.SaveAsync(input, target, expected,
                    minimum, maximum, null, default), assert, label);
                assert((await File.ReadAllBytesAsync(target)).SequenceEqual(bytes), label + " preserves existing package");
                assert(Directory.GetFiles(directory).Length == 2, label + " removes only own temporary file");
            }
            await BadDownload(bytes[..100], 2048, 1, 4096, "truncated download");
            await BadDownload(bytes, 100, 1, 4096, "longer than declared");
            await BadDownload(Array.Empty<byte>(), null, 1, 4096, "empty unknown-length download");
            await BadDownload(bytes, null, 1, 1024, "unknown-length exceeds limit");
            await BadDownload(bytes, 5000, 1, 4096, "declared size exceeds limit");

            using CancellationTokenSource cancelled = new();
            using MemoryStream cancelInput = new(bytes);
            await ThrowsAsync<OperationCanceledException>(() => AtomicDownloadFile.SaveAsync(cancelInput, target, bytes.Length,
                1, 4096, _ => cancelled.Cancel(), cancelled.Token), assert, "mid-download cancellation");
            using MemoryStream preCancelInput = new(bytes);
            await ThrowsAsync<OperationCanceledException>(() => AtomicDownloadFile.SaveAsync(preCancelInput, target, bytes.Length,
                1, 4096, null, new CancellationToken(true)), assert, "pre-download cancellation");
            using MemoryStream callbackInput = new(bytes);
            await ThrowsAsync<InvalidOperationException>(() => AtomicDownloadFile.SaveAsync(callbackInput, target, bytes.Length,
                1, 4096, _ => throw new InvalidOperationException("synthetic callback failure"), default), assert, "callback failure cleanup");
            assert((await File.ReadAllBytesAsync(target)).SequenceEqual(bytes), "cancellation/fault preserve cached package");
            assert(Directory.GetFiles(directory).Length == 2 && await File.ReadAllTextAsync(unrelated) == "unrelated partial", "unrelated partial untouched");

            byte[] replacement = bytes.Reverse().ToArray();
            using MemoryStream replaceInput = new(replacement);
            await AtomicDownloadFile.SaveAsync(replaceInput, target, null, 1024, 4096, null, default);
            assert((await File.ReadAllBytesAsync(target)).SequenceEqual(replacement), "complete unknown-length replacement succeeds");
        }
        finally
        {
            // Only files created inside this test's unique, explicitly resolved directory.
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory, recursive: false);
        }
    }

    private static async Task SchedulerAsync(Action<bool, string> assert)
    {
        TaskActivityService service = new();
        using var first = await service.AcquireAsync("first", "First", new[] { " X ", "x" });
        assert(first is not null, "first resource granted");
        assert(service.Snapshot().Single(x => x.Title == "First").Detail == "Starting: First",
            "new task detail starts with an active verb");
        first!.UpdateDetail("Applying synthetic setting.");
        assert(service.Snapshot().Single(x => x.Title == "First").Detail == "Applying: synthetic setting.",
            "running task detail normalizes the action verb");
        first.UpdateDetail("Applying and verifying synthetic setting.");
        assert(service.Snapshot().Single(x => x.Title == "First").Detail == "Applying and verifying: synthetic setting.",
            "compound action verb remains grammatically correct");
        var secondTask = service.AcquireAsync("second", "Second", new[] { "x", "Y" });
        var thirdTask = service.AcquireAsync("third", "Third", new[] { "y" });
        assert(!secondTask.IsCompleted && !thirdTask.IsCompleted, "new task cannot overtake earlier conflicting queue");
        using var independent = await service.AcquireAsync("independent", "Independent", new[] { "z" });
        assert(independent is not null, "independent resource is not blocked by queue");
        assert(await service.AcquireAsync("SECOND", "Duplicate", null) is null, "duplicate queued id rejected case-insensitively");
        assert(service.Snapshot().Single(x => x.Title == "Third").Detail.Contains("Second"), "queue detail names older conflicting task");
        first!.Complete("COMPLETED", "done");
        assert(service.Snapshot().Single(x => x.Title == "First").Detail == "Completed: done",
            "completed task detail starts with a terminal verb");
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(3));
        assert(second is not null && !thirdTask.IsCompleted, "FIFO grants earlier task only");
        second!.Complete("FAILED", "synthetic failure");
        assert(service.Snapshot().Single(x => x.Title == "Second").Detail == "Failed: synthetic failure",
            "failed task detail starts with a terminal verb");
        using var third = await thirdTask.WaitAsync(TimeSpan.FromSeconds(3));
        assert(third is not null, "failure releases logical resources");
        third!.Dispose();
        assert(service.Snapshot().Single(x => x.Title == "Third").State == "INTERRUPTED", "abandoned task is not reported completed");
        first.Dispose();
        assert(service.Snapshot().Single(x => x.Title == "First").State == "COMPLETED", "dispose preserves explicit result");
        independent!.Complete("COMPLETED", "done");
        assert(!service.HasActiveTask && service.HeaderStatus == "TASKS: IDLE", "scheduler returns idle");

        List<TaskActivityService.TaskActivityLease> active = new();
        for (int i = 0; i < 40; i++) active.Add((await service.AcquireAsync("active-" + i, "Active " + i, null))!);
        for (int i = 0; i < 40; i++)
        {
            using var done = await service.AcquireAsync("done-" + i, "Done " + i, null);
            done!.Complete("COMPLETED", "done");
        }
        assert(service.Snapshot().Count(x => x.State == "RUNNING") == 40, "history never hides active tasks beyond thirty rows");
        assert(service.Snapshot().Count(x => x.State == "COMPLETED") <= 30, "completed history remains bounded");
        foreach (var lease in active) lease.Dispose();
        assert(!service.HasActiveTask, "all synthetic leases released");
    }

    private static void Gates(Action<bool, string> assert)
    {
        OperationGate gate = new();
        assert(!gate.IsBusy, "operation gate initially idle");
        using var first = gate.TryEnter();
        assert(first is not null && gate.IsBusy && gate.TryEnter() is null, "operation gate rejects rapid second click");
        first!.Dispose();
        using var second = gate.TryEnter();
        first.Dispose();
        assert(second is not null && gate.IsBusy, "old lease cannot release a newer operation");
        second!.Dispose();
        var winners = new System.Collections.Concurrent.ConcurrentBag<IDisposable>();
        Parallel.For(0, 32, _ => { var lease = gate.TryEnter(); if (lease is not null) winners.Add(lease); });
        assert(winners.Count == 1, "only one concurrent operation enters");
        foreach (var lease in winners) lease.Dispose();
        assert(!gate.IsBusy, "gate unlocks after operation ends");
    }

    private static void Snapshots(Action<bool, string> assert)
    {
        Dictionary<string, object> data = new();
        object? Read(string key) => data.GetValueOrDefault(key);
        void Write(string key, object value, RegistryValueKind _) => data[key] = value;
        assert(!RegistrySnapshotCommit.IsCaptured(Read, "test"), "missing marker isn't a saved snapshot");
        for (int failAt = 1; failAt <= 4; failAt++)
        {
            data.Clear();
            int step = 0;
            void Step() { if (++step == failAt) throw new IOException("synthetic write failure"); }
            Throws<IOException>(() => RegistrySnapshotCommit.Write((key, value, _) => { Step(); data[key] = value; },
                Step, "test", true, RegistryValueKind.DWord, "2"), assert, "interrupted snapshot step " + failAt);
            assert(!RegistrySnapshotCommit.IsCaptured(Read, "test"), "uncommitted partial snapshot isn't accepted " + failAt);
        }
        data.Clear();
        RegistrySnapshotCommit.Write(Write, () => { }, "test", true, RegistryValueKind.DWord, "2");
        assert(RegistrySnapshotCommit.IsCaptured(Read, "test"), "complete snapshot accepted");
        string Serialize(object value, RegistryValueKind _) => value.ToString()!;
        RegistrySnapshotCommit.RequireRestored(Read, "test", 2, RegistryValueKind.DWord, Serialize);
        assert(true, "exact restore read-back accepted");
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.RequireRestored(Read, "test", 3, RegistryValueKind.DWord, Serialize), assert, "wrong restored data rejected");
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.RequireRestored(Read, "test", "2", RegistryValueKind.String, Serialize), assert, "wrong restored registry kind rejected");
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.RequireRestored(Read, "test", null, null, Serialize), assert, "missing restored value rejected");
        data.Remove("test.Value");
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.IsCaptured(Read, "test"), assert, "legacy marker missing value rejected");
        data["test.Value"] = "2";
        data["test.Kind"] = "invalid-kind";
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.IsCaptured(Read, "test"), assert, "invalid kind rejected");
        data.Clear();
        RegistrySnapshotCommit.Write(Write, () => { }, "test", false, RegistryValueKind.String, "");
        assert(RegistrySnapshotCommit.IsCaptured(Read, "test"), "originally absent value is valid backup");
        assert(!data.ContainsKey("test.Value"), "absent snapshot requires no fabricated value");
        RegistrySnapshotCommit.RequireRestored(Read, "test", null, null, Serialize);
        assert(true, "restored absence verified");
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.RequireRestored(Read, "test", 0, RegistryValueKind.DWord, Serialize), assert, "zero is not absence");

        data.Clear();
        assert(!RegistrySnapshotCommit.HasCompleteSet(Read, new[] { "first", "second" }, "test group"),
            "fully absent snapshot set uses the documented fallback");
        RegistrySnapshotCommit.Write(Write, () => { }, "first", true, RegistryValueKind.DWord, "1");
        Throws<InvalidDataException>(() => RegistrySnapshotCommit.HasCompleteSet(Read, new[] { "first", "second" }, "test group"), assert,
            "partial multi-value snapshot is blocked before restore");
        RegistrySnapshotCommit.Write(Write, () => { }, "second", false, RegistryValueKind.String, "");
        assert(RegistrySnapshotCommit.HasCompleteSet(Read, new[] { "first", "second" }, "test group"),
            "complete multi-value snapshot accepted");

        data.Clear();
        assert(!ServiceRestoreSnapshot.Validate(Read, "svc", false), "never-installed service may be skipped");
        Throws<InvalidDataException>(() => ServiceRestoreSnapshot.Validate(Read, "svc", true), assert, "installed service missing backup blocked");
        RegistrySnapshotCommit.Write(Write, () => { }, "svc.Start", true, RegistryValueKind.DWord, "2");
        Throws<InvalidDataException>(() => ServiceRestoreSnapshot.Validate(Read, "svc", true), assert, "missing delayed-start capture blocked");
        RegistrySnapshotCommit.Write(Write, () => { }, "svc.Delayed", false, RegistryValueKind.DWord, "");
        Throws<InvalidDataException>(() => ServiceRestoreSnapshot.Validate(Read, "svc", true), assert, "missing running state cannot silently become stopped");
        data["svc.Running"] = 1;
        assert(ServiceRestoreSnapshot.Validate(Read, "svc", true), "complete service restore snapshot accepted");
        assert(ServiceRestoreSnapshot.ReadRunningState(0, "svc") == false && ServiceRestoreSnapshot.ReadRunningState(1, "svc"), "captured running states round trip");
        data["svc.Running"] = 2;
        Throws<InvalidDataException>(() => ServiceRestoreSnapshot.Validate(Read, "svc", true), assert, "invalid running state blocked");
        data["svc.Running"] = 0;
        data["svc.Start.Value"] = "99";
        Throws<InvalidDataException>(() => ServiceRestoreSnapshot.Validate(Read, "svc", true), assert, "invalid startup mode blocked");

        data["svc.Start.Value"] = "1";
        Throws<InvalidDataException>(() => ServiceRestoreSnapshot.Validate(Read, "svc", true), assert, "driver-only startup mode blocked for service restore");

        ServiceStartupRestorePlan sysMain = ServiceRestoreSnapshot.CreatePlan(
            savedStart: 3,
            delayedAutoStart: null,
            wasRunning: false,
            documentedWindowsDefault: 2);
        assert(sysMain.TargetStart == 2 && sysMain.ShouldStart &&
            !sysMain.ShouldStop && sysMain.VerifyRuntime &&
            sysMain.UsesDocumentedDefault,
            "SysMain smart restore returns to documented Automatic and Running");
        assert(ServiceRestoreSnapshot.ToScStartMode(sysMain.TargetStart, sysMain.DelayedAutoStart) == "auto",
            "SysMain Automatic maps to sc.exe auto");
        sysMain = ServiceRestoreSnapshot.ApplyDocumentedRuntimePolicy("SysMain", sysMain);
        assert(sysMain.ShouldStart && sysMain.VerifyRuntime,
            "SysMain is started and must verify Running after restore");
        ServiceStartupRestorePlan retailDemo = ServiceRestoreSnapshot.ApplyDocumentedRuntimePolicy(
            "RetailDemo",
            ServiceRestoreSnapshot.CreateWindowsDefaultPlan(2, null));
        assert(retailDemo.ShouldStart && !retailDemo.VerifyRuntime,
            "Automatic trigger-start service is started without false failure after it self-stops");

        ServiceStartupRestorePlan delayedAutomatic = ServiceRestoreSnapshot.CreatePlan(
            savedStart: 2,
            delayedAutoStart: 1,
            wasRunning: true,
            documentedWindowsDefault: 2);
        assert(ServiceRestoreSnapshot.ToScStartMode(delayedAutomatic.TargetStart, delayedAutomatic.DelayedAutoStart) == "delayed-auto",
            "captured delayed Automatic is preserved");

        ServiceStartupRestorePlan vendorExact = ServiceRestoreSnapshot.CreatePlan(
            savedStart: 3,
            delayedAutoStart: 0,
            wasRunning: true,
            documentedWindowsDefault: null);
        assert(vendorExact.TargetStart == 3 && vendorExact.ShouldStart &&
            vendorExact.VerifyRuntime && !vendorExact.UsesDocumentedDefault,
            "unknown vendor service replays exact saved startup and runtime state");

        ServiceStartupRestorePlan windowsManual = ServiceRestoreSnapshot.CreateWindowsDefaultPlan(3, null);
        assert(windowsManual.TargetStart == 3 && !windowsManual.ShouldStart &&
            !windowsManual.ShouldStop && !windowsManual.VerifyRuntime,
            "documented Manual default remains demand-driven");
        ServiceStartupRestorePlan windowsDisabled = ServiceRestoreSnapshot.CreateWindowsDefaultPlan(4, null);
        assert(windowsDisabled.ShouldStop && !windowsDisabled.VerifyRuntime,
            "documented Disabled default is durable even when a busy service stops only after reboot");
        Throws<ArgumentOutOfRangeException>(() => ServiceRestoreSnapshot.CreateWindowsDefaultPlan(1, null), assert,
            "invalid documented service default rejected");
    }

    private static async Task ThrowsAsync<T>(Func<Task> action, Action<bool, string> assert, string label) where T : Exception
    {
        bool caught = false;
        try { await action(); } catch (T) { caught = true; }
        assert(caught, label);
    }
    private static void Throws<T>(Action action, Action<bool, string> assert, string label) where T : Exception
    {
        bool caught = false;
        try { action(); } catch (T) { caught = true; }
        assert(caught, label);
    }
}
