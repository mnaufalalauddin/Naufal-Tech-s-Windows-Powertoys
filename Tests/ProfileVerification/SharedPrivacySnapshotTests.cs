using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class SharedPrivacySnapshotTests
{
    internal static void Run(Action<bool, string> check)
    {
        var productionStore = new SharedPrivacySnapshotStore();
        var isolatedStore = SharedPrivacySnapshotStore.CreateIsolatedAudit(Guid.NewGuid());
        var secondStore = SharedPrivacySnapshotStore.CreateIsolatedAudit(Guid.NewGuid());
        check(productionStore.AuditBackupRoot == SharedPrivacySnapshot.ProductionRoot, "Production snapshot root remains unchanged");
        check(!isolatedStore.AuditBackupRoot.StartsWith(SharedPrivacySnapshot.ProductionRoot + "\\", StringComparison.OrdinalIgnoreCase), "Audit root is outside production backups");
        check(isolatedStore.AuditBackupRoot != secondStore.AuditBackupRoot, "Independent runs use independent snapshot storage");
        check(productionStore.AuditBackupRoot == SharedPrivacySnapshot.ProductionRoot, "Creating audit stores cannot redirect production storage");
        bool emptyRunRejected = false;
        try { SharedPrivacySnapshotStore.CreateIsolatedAudit(Guid.Empty); } catch (ArgumentException) { emptyRunRejected = true; }
        check(emptyRunRejected, "Empty audit identity rejected before registry access");
        Guid guestId = Guid.Parse("0db4178e-aeca-4e6a-a900-2a9365fa4431");
        check(GuestAuditIdentity.Matches(guestId, [guestId.ToString()], ["VirtualBox"]), "Exact disposable guest identity is eligible");
        check(!GuestAuditIdentity.Matches(Guid.Empty, [guestId.ToString()], ["VirtualBox"]), "Empty expected VM identity denied");
        check(!GuestAuditIdentity.Matches(guestId, [guestId.ToString()], ["PhysicalPC"]), "Physical host denied despite matching identifier");
        check(!GuestAuditIdentity.Matches(guestId, [Guid.NewGuid().ToString()], ["VirtualBox"]), "Different VM denied");
        check(!GuestAuditIdentity.Matches(guestId, [], ["VirtualBox"]), "Missing identity denied");
        check(!GuestAuditIdentity.Matches(guestId, [guestId.ToString(), guestId.ToString()], ["VirtualBox"]), "Ambiguous identity denied");
        check(!GuestAuditIdentity.Matches(guestId, [guestId.ToString()], []), "Missing guest model denied");
        check(!GuestAuditIdentity.Matches(guestId, ["invalid"], ["VirtualBox"]), "Malformed guest identity denied");
        var absent = new SharedRegistryOriginal(false, RegistryValueKind.String, "");
        var one = new SharedRegistryOriginal(true, RegistryValueKind.DWord, "1");
        var zero = new SharedRegistryOriginal(true, RegistryValueKind.DWord, "0");
        int captures = 0;
        SharedRegistryOriginal Capture() { captures++; return zero; }
        void Reject(Action action, string label)
        {
            bool rejected = false;
            try { action(); } catch (Exception e) when (e is InvalidDataException or FormatException or OverflowException) { rejected = true; }
            check(rejected, label);
        }
        check(SharedRegistryOriginal.Resolve([null, null], null) is null, "Restore without backup never invents an original");
        check(SharedRegistryOriginal.Resolve([null, null], Capture) == zero && captures == 1, "First Apply captures actual state once");
        check(SharedRegistryOriginal.Resolve([null, one], Capture) == one && captures == 1, "Legacy original wins over currently applied state");
        check(SharedRegistryOriginal.Resolve([one, one, null], Capture) == one && captures == 1, "Matching aliases preserve their original");
        check(SharedRegistryOriginal.Resolve([absent, absent], Capture) == absent, "Original absence remains distinct from zero");
        Reject(() => SharedRegistryOriginal.Resolve([one, zero], Capture), "Conflicting legacy owners stop before capture");
        Reject(() => SharedRegistryOriginal.Resolve([absent, zero], Capture), "Absent and zero originals cannot be merged");
        Reject(() => SharedRegistryOriginal.Resolve([one, one with { Kind = RegistryValueKind.String }], Capture), "Equal text with different registry kinds conflicts");
        check(captures == 1, "Conflict paths never recapture a modified value");

        var targetIdentity = SharedPrivacySnapshot.Targets[0];
        var canonicalFields = new Dictionary<string, object>();
        object? ReadCanonical(string key) => canonicalFields.GetValueOrDefault(key);
        Reject(() => SharedRegistryOriginal.ReadCanonical(ReadCanonical, targetIdentity), "Existing empty canonical key fails closed");
        canonicalFields["Path"] = targetIdentity.Path;
        canonicalFields["Name"] = targetIdentity.Name;
        Reject(() => SharedRegistryOriginal.ReadCanonical(ReadCanonical, targetIdentity), "Identity-only interrupted commit never recaptures live state");
        canonicalFields["Original.Exists"] = 0;
        Reject(() => SharedRegistryOriginal.ReadCanonical(ReadCanonical, targetIdentity), "Uncommitted absent original is not trusted");
        canonicalFields["Original.Captured"] = 1;
        check(SharedRegistryOriginal.ReadCanonical(ReadCanonical, targetIdentity) == absent, "Committed canonical absence retains exact semantics");
        canonicalFields["Name"] = "OtherValue";
        Reject(() => SharedRegistryOriginal.ReadCanonical(ReadCanonical, targetIdentity), "Canonical identity mismatch blocks even valid committed original");
        foreach (var t in SharedPrivacySnapshot.Targets)
        {
            var effect = CatalogEffect.Registry("HKCU", t.Path, t.Name, "DWord", 0);
            check(SharedPrivacySnapshot.Describe("Telemetry", effect).SharedOriginalOwner ==
                SharedPrivacySnapshot.Describe(t.Id, effect).SharedOriginalOwner, "Migrated aliases advertise one original owner: " + t.Id);
            check(SharedPrivacySnapshot.Describe("Unrelated", effect).SharedOriginalOwner is null, "Unregistered owner cannot opt in by target alone");
            check(SharedPrivacySnapshot.Describe("Telemetry", CatalogEffect.Registry("HKLM", t.Path, t.Name, "DWord", 0)).SharedOriginalOwner is null,
                "Different registry hive cannot acquire shared user ownership");
        }

        var fields = new Dictionary<string, object>();
        object? Read(string key) => fields.GetValueOrDefault(key);
        check(SharedRegistryOriginal.Read(Read, "T") is null, "No tag is a missing snapshot");
        fields["T.Exists"] = 1;
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Interrupted uncommitted snapshot fails closed");
        fields["T.Captured"] = 1;
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Missing type and payload rejected");
        fields["T.Kind"] = "DWord"; fields["T.Value"] = "1";
        check(SharedRegistryOriginal.Read(Read, "T") == one, "Committed original round-trips");
        fields["T.Value"] = "bad";
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Malformed integer rejected before write");
        fields["T.Kind"] = "Binary";
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Malformed binary rejected before write");
        fields["T.Kind"] = "MultiString";
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Ambiguous legacy multi-string codec not silently accepted");
        fields["T.Kind"] = "None";
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Lossy legacy REG_NONE codec rejected");
        fields["T.Exists"] = 0;
        check(SharedRegistryOriginal.Read(Read, "T") == absent, "Absence ignores irrelevant legacy payload fields");
        fields["T.Captured"] = "1";
        Reject(() => SharedRegistryOriginal.Read(Read, "T"), "Wrong marker registry type rejected");

        // Exercise sequential aliases and retirement policy with synthetic values, not host registry.
        SharedRegistryOriginal current = one;
        SharedRegistryOriginal? canonical = null, essential = null, debloat = null;
        canonical = SharedRegistryOriginal.Resolve([canonical, essential, debloat], () => current);
        essential = canonical; current = zero;
        canonical = SharedRegistryOriginal.Resolve([canonical, essential, debloat], () => current);
        debloat = canonical;
        check(essential == one && debloat == one, "Second catalog does not back up the first catalog's applied value");
        current = essential!; essential = null;
        check(debloat == one && canonical == one && current == one, "One owner restores without retiring another owner's original");
        current = debloat!; debloat = null; canonical = null;
        check(current == one, "Last owner restores the same original");
        current = absent;
        canonical = SharedRegistryOriginal.Resolve([canonical, essential, debloat], () => current);
        check(canonical == absent, "A completed generation allows a new original");

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string essentialSource = File.ReadAllText(Path.Combine(root, "EssentialTweaksService.cs"));
        string debloatSource = File.ReadAllText(Path.Combine(root, "DebloatService.cs"));
        foreach (var mode in new[] { "Capture", "Restore", "Validate" })
        {
            check(essentialSource.Contains("SharedSnapshotPreparation." + mode), "Essential integrates shared " + mode + " preflight");
            check(debloatSource.Contains("SharedSnapshotPreparation." + mode), "Debloat integrates shared " + mode + " preflight");
        }
        check(essentialSource.Contains("SharedPrivacySnapshot.RetireUnused(id)") && debloatSource.Contains("SharedPrivacySnapshot.RetireUnused(id)"), "Both catalogs retire originals only after their backup deletion");
        check(SharedPrivacySnapshot.Targets.Count == 2 && SharedPrivacySnapshot.Targets.Select(t => t.EssentialTag).Distinct().Count() == 2,
            "Only two explicitly audited privacy targets have shared ownership");
        foreach (var target in SharedPrivacySnapshot.Targets)
        {
            check(essentialSource.Contains(target.Path) && essentialSource.Contains(target.Name), "Shared alias exists in Essential metadata: " + target.Id);
            check(debloatSource.Contains(target.Path) && debloatSource.Contains(target.Name), "Shared alias exists in Debloat metadata: " + target.Id);
        }
    }
}
