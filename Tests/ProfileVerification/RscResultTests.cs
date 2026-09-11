using Naufal_Windows_Tech_s_Powertoys;

internal static class RscResultTests
{
    internal static void Run(Action<bool, string> assert)
    {
        foreach (string method in new[] { "Enable", "Disable" })
        {
            foreach (ushort type in new ushort[] { 2, 3, 17, 18, 19, 22, 23 })
            {
                string result = NativeRscService.ValidateMethodReturn(method, type, 0, 19);
                assert(result.Contains("ReturnValue=0") && result.Contains("read-back required"),
                    "RSC numeric zero does not skip state verification");
                assert(Throws(() => NativeRscService.ValidateMethodReturn(method, type, 5, 19), "result 5"),
                    "RSC nonzero method errors remain errors");
            }
            foreach (ushort type in new ushort[] { 0, 1 })
            {
                string result = NativeRscService.ValidateMethodReturn(method, type, 12345, 19);
                assert(result.Contains(type == 0 ? "EMPTY" : "NULL") && !result.Contains("result 0"),
                    "RSC unset output is distinct from a numerical error/success and ignores union bits");
                bool enabled = method == "Enable";
                var expected = new[] { new ProfileRscAdapter("Ethernet", enabled, enabled) };
                var outcomes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Ethernet"] = result };
                NativeRscService.VerifyStates(expected, expected, outcomes);
                assert(true, "RSC unset output can finish only after matching state read-back");
                foreach (var actual in new[]
                {
                    Array.Empty<ProfileRscAdapter>(),
                    new[] { new ProfileRscAdapter("Ethernet", !enabled, enabled) },
                    new[] { new ProfileRscAdapter("Ethernet", enabled, !enabled) },
                    new[] { new ProfileRscAdapter("Other adapter", enabled, enabled) }
                })
                    assert(Throws(() => NativeRscService.VerifyStates(expected, actual, outcomes), "read-back failed"),
                        "Missing or mismatched RSC state fails Apply and rollback even with an unset return");
            }
            foreach (ushort type in new ushort[] { 8, 11, 5, 0x4003, 0x2013, ushort.MaxValue })
                assert(Throws(() => NativeRscService.ValidateMethodReturn(method, type, 0, 19), $"VARIANT={type}"),
                    "Unsupported RSC return types fail with diagnostic, never bogus result 0");
            foreach (int schema in new[] { 0, 3, 8, 0x2013 })
                assert(Throws(() => NativeRscService.ValidateMethodReturn(method, 1, 0, schema), "unexpected ReturnValue schema"),
                    "Null RSC output does not bypass return schema validation");
            assert(Throws(() => NativeRscService.ValidateMethodReturn(method, 3, uint.MaxValue, 19), "result -1"),
                "Signed RSC failures retain sign and hex code");
            assert(Throws(() => NativeRscService.ValidateMethodReturn(method, 19, uint.MaxValue, 19), "result 4294967295"),
                "Unsigned RSC failure preserves full width");
        }
        var both = new[] { new ProfileRscAdapter("Ethernet", false, false), new ProfileRscAdapter("Wi-Fi", true, false) };
        var zeroOutcomes = new Dictionary<string, string> { ["Wi-Fi"] = "ReturnValue=0" };
        NativeRscService.VerifyStates(both, new[] { new ProfileRscAdapter("ETHERNET", false, false), both[1] }, zeroOutcomes);
        assert(true, "RSC exact read-back supports mixed protocol targets and case-insensitive adapter names");
        assert(Throws(() => NativeRscService.VerifyStates(both,
            new[] { both[0], new ProfileRscAdapter("Wi-Fi", true, true) }, zeroOutcomes), "Wi-Fi: expected"),
            "RSC numerical zero cannot hide a second-adapter mismatch");
    }

    private static bool Throws(Action action, string message)
    {
        try { action(); return false; }
        catch (InvalidOperationException exception) { return exception.Message.Contains(message); }
    }

    internal static async Task ProbeAsync()
    {
        Console.WriteLine("READ-ONLY native RSC probe; no Enable/Disable method executed:");
        var rows = await Task.Run(NativeRscReader.Read);
        foreach (var row in rows) Console.WriteLine($"{row.Name}: IPv4={row.Ipv4Enabled}, IPv6={row.Ipv6Enabled}");
        foreach (string result in await Task.Run(NativeRscService.ValidateMethodMetadata))
            Console.WriteLine("OUTPUT SIGNATURE ONLY (not an execution result): " + result);
        NativeRscService.VerifyStates(rows, rows, new Dictionary<string, string>());
        Console.WriteLine($"Native input/output metadata validated; {rows.Count} read-only adapter records verified.");
    }
}
