using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class NtfsPerformanceTests
{
    internal static void Run(Action<bool, string> check)
    {
        const RegistryValueKind dword = RegistryValueKind.DWord;
        var ok = new NativeCommandResult(0, "", "", false, TimeSpan.Zero);
        foreach (uint raw in new uint[] { 0, 1, 2, 3, 0x80000000, 0x80000001, 0x80000002, 0x80000003 })
        {
            int signed = unchecked((int)raw);
            int mode = (int)(raw & 3);
            var state = new NtfsPerformanceState(signed, dword, 1, dword);
            check(NtfsPerformanceVerification.LastAccessMode(signed, dword) == mode, "NTFS known encoding " + raw.ToString("X8"));
            check(state.IsApplied == (mode == 1), "NTFS exact user-managed preset " + raw.ToString("X8"));
            check(NtfsPerformanceVerification.ApplySucceeded(ok, ok, state) == (mode == 1), "NTFS apply uses same decoder " + raw.ToString("X8"));
            check(state.ActualValue.Contains("DWORD=0x" + raw.ToString("X8")), "NTFS keeps hex diagnostic");
            check(!state.ActualValue.Contains(signed < 0 ? signed.ToString() : "NEVER PRESENT"), "NTFS no misleading signed decimal");
            foreach (object? shortNames in new object?[] { null, 0, 2, 3, "1", 1L, unchecked((int)0x80000001) })
                check(!(state with { ShortNames = shortNames }).IsApplied, "NTFS 8dot3 remains strict");
            // Restore stores/replays the original DWORD rather than the decoder's
            // logical mode, including all sign/management bits.
            var snapshot = new Dictionary<string, object?> { ["Captured"] = 1, ["Last.Exists"] = 1, ["Last.Value"] = signed };
            var saved = EssentialSnapshotValidation.ReadNtfs(k => snapshot.GetValueOrDefault(k), _ => dword, ["Last"])["Last"];
            check(saved.Value is int exact && exact == signed && saved.Matches(signed, dword), "NTFS exact snapshot preserved");
            if (raw >= 0x80000000)
                check(!saved.Matches(mode, dword), "NTFS restore must not discard encoding bit");
        }
        var applied = new NtfsPerformanceState(unchecked((int)0x80000001), dword, 1, dword);
        foreach (var failed in new[] { ok with { ExitCode = 5 }, ok with { ExitCode = -1 }, ok with { TimedOut = true } })
        {
            check(!NtfsPerformanceVerification.ApplySucceeded(failed, ok, applied), "NTFS failed last-access command is not success");
            check(!NtfsPerformanceVerification.ApplySucceeded(ok, failed, applied), "NTFS failed 8dot3 command is not success");
        }
        foreach (object? invalid in new object?[] { null, "1", 1L, 1U, true, 4, 5, -1, int.MaxValue,
            unchecked((int)0x80000004), unchecked((int)0xC0000001), unchecked((int)0x80010001) })
        {
            check(NtfsPerformanceVerification.LastAccessMode(invalid, dword) is null, "NTFS unknown bits and invalid types rejected");
            check(!(applied with { LastAccess = invalid }).IsApplied, "NTFS no false applied state");
        }
        foreach (RegistryValueKind? kind in new RegistryValueKind?[] { null, RegistryValueKind.String, RegistryValueKind.QWord, RegistryValueKind.Binary })
        {
            check(!(applied with { LastAccessKind = kind }).IsApplied, "NTFS last-access requires real DWORD");
            check(!(applied with { ShortNamesKind = kind }).IsApplied, "NTFS 8dot3 requires real DWORD");
        }
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string service = File.ReadAllText(Path.Combine(root, "EssentialActionsService.cs"));
        check(service.Contains("var ntfs = ReadNtfsPerformanceState()") && service.Contains("new(ntfs.IsApplied"), "NTFS bulk/UI status wired to decoder");
        check(service.Contains("NtfsPerformanceVerification.ApplySucceeded(lastAccess, shortNames, state)"), "NTFS Apply wired to decoder");
        check(service.Contains("savedValues[name].Matches(actual"), "NTFS Restore retains exact comparison");
        check(service.Contains("lastAccess.CombinedOutput") && service.Contains("shortNames.CombinedOutput"), "NTFS failure includes command evidence");
    }

    // Explicit opt-in probe. Reads only two machine-level configuration values;
    // never runs Apply, fsutil set, Restore, or snapshot capture/deletion.
    internal static void ProbeReadOnly()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = machine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem", writable: false);
        object? last = key?.GetValue("NtfsDisableLastAccessUpdate");
        object? shortNames = key?.GetValue("NtfsDisable8dot3NameCreation");
        var state = new NtfsPerformanceState(last, last is null ? null : key!.GetValueKind("NtfsDisableLastAccessUpdate"),
            shortNames, shortNames is null ? null : key!.GetValueKind("NtfsDisable8dot3NameCreation"));
        Console.WriteLine(state.ActualValue);
        Console.WriteLine("Applied preset readback: " + state.IsApplied);
        Console.WriteLine("Read-only probe; no Windows settings or backups changed.");
    }
}
