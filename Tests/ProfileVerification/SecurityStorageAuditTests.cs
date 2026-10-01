using Naufal_Windows_Tech_s_Powertoys;

internal static class SecurityStorageAuditTests
{
    internal static void Run(Action<bool, string> check)
    {
        var original = new SecurityStorageLiveAudit.Identity(Guid.NewGuid(), "S-1-5-21-test", "machine", 26300, "boot-one");
        SecurityMitigationSnapshot security = new(26300, true, true, "machine", 0, 0, 0, 2,
            [], [], [1], false, false, 0, "");
        var manifest = new SecurityStorageLiveAudit.Manifest(1, original, "AwaitAppliedBoot", security,
            "Enabled", "ConfiguredRoundTripVerified", true, null, "boot-one", "");
        var afterBoot = original with { Boot = "boot-two" };
        var skipped = manifest with { SecurityChanged = false, StorageOutcome = "PostBootRoundTripVerified" };
        check(SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped), "Old no-change waiting manifest can finish read-only without another reboot");
        check(SecurityStorageLiveAudit.ResumeBlock(skipped, original) == "", "No-change completion accepts same boot after storage roundtrip");
        check(SecurityStorageLiveAudit.ResumeBlock(skipped, original with { Vm = Guid.NewGuid() }).Length > 0, "No-change completion still rejects another VM");
        check(SecurityStorageLiveAudit.ResumeBlock(skipped, original with { Sid = "other" }).Length > 0, "No-change completion still rejects another user");
        check(SecurityStorageLiveAudit.ResumeBlock(skipped, original with { Boot = "" }).Length > 0, "No-change completion needs known boot identity");
        check(SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped with { StorageOutcome = "NotExercised" }), "Fully skipped run does not request artificial reboot");
        check(!SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped with { StorageOriginal = "Disabled" }), "Inconsistent completed storage baseline cannot skip reboot");
        check(!SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped with { SecurityChanged = true }), "Actual HVCI change still requires reboot");
        check(!SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped with { AppliedEffective = true }), "Inconsistent applied evidence cannot use no-change route");
        foreach (string outcome in new[] { "DisableAwaitingBoot", "RestoreAwaitingBoot", "Unknown", "ConfiguredRoundTripVerified" })
            check(!SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped with { StorageOutcome = outcome }), "Incomplete storage cannot skip boot: " + outcome);
        foreach (string phase in new[] { "AwaitStorageDisabledBoot", "AwaitStorageRestoredBoot", "AwaitRestoredBoot", "SecurityNeedsReview", "CompletedWithSkippedControls" })
            check(!SecurityStorageLiveAudit.CanFinalizeWithoutBoot(skipped with { Phase = phase }), "Only prior no-change security wait can finalize: " + phase);
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, afterBoot) == "", "Guest audit permits matching identity after a different boot");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, original).Length > 0, "Guest audit refuses same-boot effective verification");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, afterBoot with { Vm = Guid.NewGuid() }).Length > 0, "Guest audit refuses different VM");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, afterBoot with { Sid = "other" }).Length > 0, "Guest audit refuses different SID");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, afterBoot with { Build = 26301 }).Length > 0, "Guest audit refuses changed Windows build");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, afterBoot with { Machine = "other" }).Length > 0, "Guest audit refuses different machine identity");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest, afterBoot with { Boot = "" }).Length > 0, "Guest audit refuses unknown boot");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest with { Version = 2 }, afterBoot).Length > 0, "Guest audit refuses unknown manifest schema");
        foreach (string phase in new[] { "Prepared", "StorageDisableStarted", "StorageRestoreStarted", "SecurityEnableStarted", "SecurityRestoreStarted", "SecurityNeedsReview", "PostBootNeedsReview", "CompletedScopedEvidence", "invalid" })
            check(SecurityStorageLiveAudit.ResumeBlock(manifest with { Phase = phase }, afterBoot).Length > 0, "Guest audit never silently resets interrupted phase " + phase);
        check(SecurityStorageLiveAudit.ResumeBlock(manifest with { Phase = "AwaitRestoredBoot" }, afterBoot) == "", "Guest audit permits explicit second-boot verification phase");
        foreach (string phase in new[] { "AwaitStorageDisabledBoot", "AwaitStorageRestoredBoot" })
        {
            var storageManifest = manifest with { Phase = phase, SecurityChanged = false };
            check(SecurityStorageLiveAudit.ResumeBlock(storageManifest, afterBoot) == "", "Storage resume requires exact identity and a new boot: " + phase);
            check(SecurityStorageLiveAudit.ResumeBlock(storageManifest, original).Length > 0, "Storage same-boot resume denied");
            check(SecurityStorageLiveAudit.ResumeBlock(storageManifest with { StorageOriginal = "Disabled" }, afterBoot).Length > 0, "Storage altered original rejected");
            check(SecurityStorageLiveAudit.ResumeBlock(storageManifest with { SecurityChanged = true }, afterBoot).Length > 0, "Storage phase cannot hide a security change");
        }
        check(SecurityStorageLiveAudit.AcceptedStorageChange("Verified", "Disabled", false), "Verified disable may await post-boot evidence");
        check(SecurityStorageLiveAudit.AcceptedStorageChange("RebootRequired", "Disable Pending", false), "Pending disable needs a separate boot before restore");
        check(SecurityStorageLiveAudit.AcceptedStorageChange("RebootRequired", "Enable Pending", true), "Pending restore needs a separate boot");
        check(!SecurityStorageLiveAudit.AcceptedStorageChange("Failed", "Disabled", false), "Failure is not accepted despite desired readback");
        check(!SecurityStorageLiveAudit.AcceptedStorageChange("RebootRequired", "Enabled", false), "Opposite pending state rejected");
        check(!SecurityStorageLiveAudit.AcceptedStorageChange("VerificationPending", "Disabled", false), "Unknown verification state does not start resume path");
        check(SecurityStorageLiveAudit.ResumeBlock(manifest with { Security = security with { HvciEnabled = 1 } }, afterBoot).Length > 0, "Guest enable audit cannot claim original already enabled as a changed test");
        check(SecurityStorageLiveAudit.RuntimeMatches(security, security), "Known identical effective state matches baseline");
        check(!SecurityStorageLiveAudit.RuntimeMatches(security, security with { Running = null }), "Unknown running protection is not a match");
        check(!SecurityStorageLiveAudit.RuntimeMatches(security, security with { Configured = null }), "Unknown configured protection is not a match");
        check(!SecurityStorageLiveAudit.RuntimeMatches(security, security with { VbsStatus = null }), "Unknown VBS is not a match");
        check(!SecurityStorageLiveAudit.RuntimeMatches(security, security with { Running = [2] }), "Different effective service set is detected");
        check(!SecurityStorageLiveAudit.RuntimeMatches(security, security with { EvidenceError = "read failed" }), "Incomplete security evidence does not pass");

        var fake = new SecurityPlatform(security);
        var store = new BackupStore();
        var applied = SecurityMitigationPolicy.Execute(SecurityMitigationAction.EnableMemoryIntegrity, fake, store);
        check(applied.Verified && fake.Value == 1 && store.Value?.OriginalValue == 0, "Eligible HVCI enable keeps exact isolated original");
        check(SecurityMitigationPolicy.Execute(SecurityMitigationAction.RestoreMemoryIntegrity, fake, store).Verified && fake.Value == 0, "HVCI rollback returns exact original through production policy");
        foreach (var denied in new[] { security with { Managed = true }, security with { PolicyPresent = true }, security with { HvciLocked = null }, security with { VbsLocked = 1 }, security with { CodeIntegrityPolicy = 2 } })
        {
            fake = new(denied); store = new();
            var blocked = SecurityMitigationPolicy.Execute(SecurityMitigationAction.EnableMemoryIntegrity, fake, store);
            check(!blocked.Verified && fake.Writes == 0 && store.Value is null, "Guest audit inherits strict production gate without backup or writes");
        }
        string logRoot = Path.Combine(Path.GetTempPath(), "NWU-SecurityStorage-Unit-" + Guid.NewGuid().ToString("N"));
        var fakeStorage = new StorageRunner();
        var service = new StorageServicing(fakeStorage, logRoot);
        var disable = service.ChangeAsync(new(StorageItemKind.Feature, SecurityStorageLiveAudit.Feature, "Enabled"), false).GetAwaiter().GetResult();
        check(disable.Outcome == "Verified" && fakeStorage.State == "Disabled", "Production storage disable verifies with fake DISM");
        var restore = service.ChangeAsync(new(StorageItemKind.Feature, SecurityStorageLiveAudit.Feature, "Disabled"), true).GetAwaiter().GetResult();
        check(restore.Outcome == "Verified" && fakeStorage.State == "Enabled", "Production storage restore verifies exact fake Enabled baseline");
        var mutations = fakeStorage.Commands.Where(c => c.Mutation).ToArray();
        check(mutations.Length == 2 && mutations.All(c => !c.Arguments.Contains("/Remove") && !c.Arguments.Contains("/All") && c.Arguments.Contains("/NoRestart")), "Production Print-to-PDF roundtrip retains payload and never requests automatic restart");
        fakeStorage.Pending = true;
        var pending = service.ChangeAsync(new(StorageItemKind.Feature, SecurityStorageLiveAudit.Feature, "Enabled"), false).GetAwaiter().GetResult();
        check(pending.Outcome == "RebootRequired" && pending.RestartRequired, "Pending storage result is never counted as verified");
        int writes = fakeStorage.Commands.Count(c => c.Mutation);
        var pendingRestore = service.ChangeAsync(new(StorageItemKind.Feature, SecurityStorageLiveAudit.Feature, "Disable Pending"), true).GetAwaiter().GetResult();
        check(pendingRestore.Outcome == "RebootRequired" && fakeStorage.Commands.Count(c => c.Mutation) == writes, "Pending storage restore makes no new mutation");
    }

    private sealed class SecurityPlatform(SecurityMitigationSnapshot snapshot) : ISecurityMitigationPlatform
    {
        internal int? Value = snapshot.HvciEnabled;
        internal int Writes;
        public SecurityMitigationSnapshot Read() => snapshot with { HvciEnabled = Value };
        public int? ReadMemoryIntegrity() => Value;
        public void WriteMemoryIntegrity(int? value) { Value = value; Writes++; }
    }
    private sealed class BackupStore : ISecurityMitigationBackupStore
    {
        internal SecurityMitigationBackup? Value;
        public SecurityMitigationBackup? Load() => Value;
        public void Save(SecurityMitigationBackup backup) => Value = backup;
    }
    private sealed class StorageRunner : IStorageCommandRunner
    {
        internal string State = "Enabled";
        internal bool Pending;
        internal List<StorageCommand> Commands = [];
        public Task<NativeCommandResult> RunAsync(StorageCommand command)
        {
            Commands.Add(command);
            if (!command.Mutation) return Task.FromResult(new NativeCommandResult(0, "State : " + State, "", false, TimeSpan.Zero));
            State = Pending ? "Disable Pending" : command.Arguments.Contains("/Disable-Feature") ? "Disabled" : "Enabled";
            return Task.FromResult(new NativeCommandResult(Pending ? 3010 : 0, "Fake DISM only", "", false, TimeSpan.Zero));
        }
    }
}
