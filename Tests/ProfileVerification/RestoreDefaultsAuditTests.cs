using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class RestoreDefaultsAuditTests
{
    internal static async Task RunAsync(Action<bool, string> assert)
    {
        void Throws(Action action, string label)
        {
            bool failed = false;
            try { action(); } catch (Exception) { failed = true; }
            assert(failed, label);
        }
        var target = new RestoreRegistryTarget(RegistryHive.CurrentUser, @"Software\Test", "Example");
        Dictionary<string, object?> Valid() => new()
        {
            ["Snapshot.Captured"] = 1, ["Snapshot.Count"] = 1,
            ["Item.0.Hive"] = "CurrentUser", ["Item.0.Path"] = @"Software\Test", ["Item.0.Name"] = "Example",
            ["Item.0.Exists"] = 1, ["Item.0.Kind"] = "DWord", ["Item.0.Value"] = "1"
        };
        IReadOnlyList<RestoreRegistryValue> Parse(Dictionary<string, object?> data) => RegistryRestorePlan.ReadIndexed(
            key => data.GetValueOrDefault(key), new[] { target }, (value, kind) => kind == RegistryValueKind.DWord ? int.Parse(value) : value);
        var valid = Valid();
        var plan = Parse(valid);
        assert(plan[0].Value is int savedValue && savedValue == 1, "exact saved active value remains a valid Restore target");
        foreach (string key in valid.Keys)
        {
            var missing = Valid(); missing.Remove(key);
            Throws(() => Parse(missing), "missing snapshot field fails preflight: " + key);
        }
        foreach (var (key, value) in new (string, object?)[] {
            ("Snapshot.Captured", "1"), ("Snapshot.Count", 0), ("Snapshot.Count", 2),
            ("Item.0.Exists", 2), ("Item.0.Hive", "LocalMachine"), ("Item.0.Path", @"SYSTEM\Unrelated"),
            ("Item.0.Name", "Other"), ("Item.0.Kind", "Unknown"), ("Item.0.Value", "broken") })
        {
            var corrupt = Valid(); corrupt[key] = value;
            Throws(() => Parse(corrupt), "corrupt snapshot cannot use guessed defaults: " + key);
        }
        var absent = Valid(); absent["Item.0.Exists"] = 0; absent.Remove("Item.0.Kind"); absent.Remove("Item.0.Value");
        assert(Parse(absent)[0].Value is null, "captured absence is valid");
        int writes = 0;
        RegistryRestorePlan.Execute(plan, _ => (1, RegistryValueKind.DWord), _ => writes++);
        assert(writes == 0, "matching saved value never requests protected-key write access");
        RegistryRestorePlan.Execute(Parse(absent), _ => (null, null), _ => writes++);
        assert(writes == 0, "already absent restore doesn't create/delete keys");
        Throws(() => RegistryRestorePlan.Execute(plan, _ => throw new UnauthorizedAccessException(), _ => writes++), "read denied not absent");
        assert(writes == 0, "failed read did not write");
        Throws(() => RegistryRestorePlan.Execute(plan, _ => (0, RegistryValueKind.DWord), _ => writes++), "failed readback retains backup via exception");
        object? current = 0;
        RegistryRestorePlan.Execute(plan, _ => (current, RegistryValueKind.DWord), item => current = item.Value);
        assert(Equals(current, 1), "exact Restore writes and verifies saved value even if still applied");
        assert(RegistryRestorePlan.Matches(new byte[] { 1, 2 }, new byte[] { 1, 2 }), "binary structural match");
        assert(!RegistryRestorePlan.Matches(new byte[] { 1, 2 }, new byte[] { 2, 1 }), "binary order matters");
        assert(RegistryRestorePlan.Matches(new[] { "a", "b" }, new[] { "a", "b" }), "multi-string structural match");
        var tags = new Dictionary<string, object?> { ["a.Captured"] = 1, ["a.Exists"] = 1, ["a.Kind"] = "DWord", ["a.Value"] = "1" };
        Throws(() => RegistryRestorePlan.RequireTags(k => tags.GetValueOrDefault(k), new[] { "a", "b" }, (v, _) => int.Parse(v)), "full tag preflight rejects partially captured bundle");
        tags["a.Value"] = "bad";
        Throws(() => RegistryRestorePlan.RequireTags(k => tags.GetValueOrDefault(k), new[] { "a" }, (v, _) => int.Parse(v)), "preflight decodes values before writes");
        var uac = new RestoreRegistryTarget(RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Policies\System", "PromptOnSecureDesktop");
        assert(Equals(DocumentedRestoreDefaults.PerformanceLab("UacSecureDesktopDimOff", new[] { uac })[0].Value, 1), "documented UAC default is explicit enabled, not deletion");
        var tdr = new RestoreRegistryTarget(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay");
        assert(DocumentedRestoreDefaults.PerformanceLab("NvTdr10", new[] { tdr })[0].Value is null, "remove TDR test override to driver default");
        Throws(() => DocumentedRestoreDefaults.PerformanceLab("UacSecureDesktopDimOff", new[] { target }), "default allowlist also checks exact target");
        Throws(() => DocumentedRestoreDefaults.PerformanceLab("IntelPpmDisabled", new[] { new RestoreRegistryTarget(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\intelppm", "Start") }), "never blindly delete driver Start to reset default");
        foreach (string option in new[] { "disabledynamictick", "useplatformclock", "debug", "bootdebug", "sos", "highestmode" })
        {
            assert(BcdRestoreVerification.Matches(option, "yes", "on"), "BCD boolean aliases " + option);
            assert(!BcdRestoreVerification.Matches(option, "no", "yes"), "BCD opposite value mismatch " + option);
            assert(BcdRestoreVerification.Matches(option, null, null), "absent BCD override already default " + option);
        }
        int commands = 0;
        await ServiceRestoreRuntime.EnsureAsync("fake", true, () => "Running", _ => { commands++; return Task.FromResult((0, "")); }, () => Task.CompletedTask);
        assert(commands == 0, "service already Running skips command");
        string runtime = "Stopped";
        await ServiceRestoreRuntime.EnsureAsync("fake", true, () => runtime, _ => { commands++; runtime = "Running"; return Task.FromResult((0, "")); }, () => Task.CompletedTask);
        assert(runtime == "Running" && commands == 1, "restore actually starts service");
        await ServiceRestoreRuntime.EnsureAsync("fake", false, () => runtime, action => { assert(action == "stop", "saved Stopped sends stop"); runtime = "Stopped"; return Task.FromResult((0, "")); }, () => Task.CompletedTask);
        bool failedRuntime = false;
        try { await ServiceRestoreRuntime.EnsureAsync("fake", true, () => "Stopped", _ => Task.FromResult((0, "success without running")), () => Task.CompletedTask); }
        catch (InvalidOperationException) { failedRuntime = true; }
        assert(failedRuntime, "Automatic/exit zero but not Running remains failure");
        const string devices = @"Software\Naufal Windows Tech\Powertoys\Backups\GamingDeviceNetwork\";
        const string dnsPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{d7923919-abda-47b0-a6b7-b305cd4201a9}";
        const string nic = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}\0001";
        assert(GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "DnsOriginal", dnsPath, "NameServer"), "DNS exact interface target");
        assert(!GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "DnsOriginal", dnsPath + @"\Other", "NameServer"), "DNS nested path rejected");
        assert(!GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "DnsOriginal", dnsPath, "Other"), "DNS unrelated value rejected");
        assert(GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "EthernetLatency", nic, "*EEE"), "Ethernet driver scope");
        assert(!GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "EthernetLatency", nic, "Service"), "Ethernet cannot replace driver service association");
        assert(!GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "WifiLatency", nic, "PnPCapabilities"), "Wi-Fi snapshot cannot inject Ethernet-only target");
        assert(GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "UsbLatencyPower", @"SYSTEM\CurrentControlSet\Enum\USB\VID_X\Instance\Device Parameters", "AllowIdleIrpInD3"), "USB scoped target");
        assert(!GamingDeviceNetworkService.IsAllowedSnapshotTarget(devices + "UsbLatencyPower", @"SYSTEM\CurrentControlSet\Services\intelppm", "Start"), "USB cannot target unrelated service");
        assert(!GamingDeviceNetworkService.DnsMatches(new[] { "1.1.1.1", "1.0.0.1" }, new[] { "1.0.0.1", "1.1.1.1" }), "DNS priority order verified");
        assert(!GamingDeviceNetworkService.DnsMatches(new[] { "1.1.1.1" }, new[] { "1.1.1.1", "8.8.8.8" }), "DNS extra servers rejected");
        assert(!GamingDeviceNetworkService.DnsMatches(Array.Empty<string>(), new[] { "1.1.1.1" }), "DHCP cannot pass with static override");
        assert(GamingDeviceNetworkService.DnsMatches(Array.Empty<string>(), Array.Empty<string>()), "DHCP configuration absence verified");
        var dns = new Dictionary<string, object?> { ["Captured"] = 1, ["Count"] = 1,
            ["Item.0.Path"] = dnsPath, ["Item.0.Name"] = "NameServer", ["Item.0.Exists"] = 1,
            ["Item.0.Kind"] = (int)RegistryValueKind.String, ["Item.0.Value"] = "1.1.1.1,1.0.0.1" };
        var decoded = GamingDeviceNetworkService.ParseSnapshot(devices + "DnsOriginal", k => dns.GetValueOrDefault(k));
        assert(decoded.Count == 1 && decoded[0].Exists, "DNS snapshot preflight decodes full target");
        foreach (string key in dns.Keys)
        {
            var incomplete = new Dictionary<string, object?>(dns); incomplete.Remove(key);
            Throws(() => GamingDeviceNetworkService.ParseSnapshot(devices + "DnsOriginal", k => incomplete.GetValueOrDefault(k)), "DNS missing field rejected before mutation " + key);
        }
        dns["Item.0.Value"] = "invalid DNS";
        Throws(() => GamingDeviceNetworkService.ParseSnapshot(devices + "DnsOriginal", k => dns.GetValueOrDefault(k)), "malformed DNS backup not silently converted to DHCP");
        dns["Item.0.Value"] = "::1";
        Throws(() => GamingDeviceNetworkService.ParseSnapshot(devices + "DnsOriginal", k => dns.GetValueOrDefault(k)), "IPv6 cannot enter IPv4 restore command");
        assert(new ToolActionResult(false, "Absent", SkippedUnavailable: true).SkippedUnavailable, "action unavailable is not success or failure");
    }
}
