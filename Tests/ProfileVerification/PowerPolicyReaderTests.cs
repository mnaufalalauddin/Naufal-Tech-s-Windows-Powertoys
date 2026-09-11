using System.ComponentModel;
using Naufal_Windows_Tech_s_Powertoys;

internal static class PowerPolicyReaderTests
{
    internal static void Run(Action<bool, string> assert)
    {
        const string scheme = "381b4222-f694-41f0-9685-ff5bb260df2e";
        const string setting = "893dee8e-2bef-41e0-89c6-b55d0929964c";
        var calls = new List<bool>();
        PowerIndexReadResult Reader(Guid plan, Guid subgroup, Guid option, bool ac)
        {
            assert(plan == new Guid(scheme) && option == new Guid(setting), "Power API receives requested scheme and setting");
            assert(subgroup == new Guid("54533251-82be-4824-96c1-47b60b740d00"), "Power API uses processor subgroup");
            calls.Add(ac);
            return new(0, ac ? 5u : 10u);
        }
        var pair = NativePowerPolicyReader.ReadRequiredPair(scheme, setting, Reader);
        assert(pair == new PowerPolicyPair(5, 10), "Effective values are accepted without any explicit registry override");
        assert(calls.SequenceEqual(new[] { true, false }), "AC and DC are read independently, including battery/DC on desktop");
        var zero = NativePowerPolicyReader.ReadRequiredPair(scheme, setting, (_, _, _, _) => new(0, 0));
        assert(zero == new PowerPolicyPair(0, 0), "Successful zero indexes are valid values");
        const string disk = "0012ee47-9041-4b5d-9b77-535fba8b1442";
        foreach (string diskSetting in new[] { "0b2d69d7-a2a1-449c-9680-f91c70521c60", "d639518a-e56d-4345-8af2-b9f32fb26109", "d3d55efd-c1ff-424e-9dc3-441be7833010" })
        {
            var diskPair = NativePowerPolicyReader.ReadRequiredPairInSubgroup(scheme, disk, diskSetting,
                (plan, group, option, ac) =>
                {
                    assert(plan == new Guid(scheme) && group == new Guid(disk) && option == new Guid(diskSetting),
                        "AHCI/NVMe reads use disk subgroup and correct setting, not processor defaults");
                    return new(0, ac ? 0u : 100u);
                });
            assert(diskPair == new PowerPolicyPair(0, 100), "Storage AC/DC effective values stay distinct");
        }
        bool invalidSubgroupRejected = false;
        try { NativePowerPolicyReader.ReadRequiredPairInSubgroup(scheme, "bad", setting,
            (_, _, _, _) => throw new Exception("Invalid subgroup reached native code")); }
        catch (ArgumentException) { invalidSubgroupRejected = true; }
        assert(invalidSubgroupRejected, "Invalid subgroup rejected before native call");
        foreach (uint acError in new uint[] { 0, 2, 5, 50 })
        foreach (uint dcError in new uint[] { 0, 2, 5, 50 })
        {
            if (acError == 0 && dcError == 0) continue;
            bool denied = false;
            try
            {
                NativePowerPolicyReader.ReadRequiredPair(scheme, setting,
                    (_, _, _, ac) => new(ac ? acError : dcError, 0));
            }
            catch (Win32Exception exception)
            {
                denied = exception.NativeErrorCode == (acError == 0 ? dcError : acError) &&
                    exception.Message.Contains($"error={acError}") && exception.Message.Contains($"error={dcError}");
            }
            assert(denied, "Unavailable/denied/unsupported AC or DC fails closed with native diagnostic");
        }
        foreach (var inputs in new[] { ("bad", setting), (scheme, "bad") })
        {
            bool called = false, rejected = false;
            try { NativePowerPolicyReader.ReadRequiredPair(inputs.Item1, inputs.Item2, (_, _, _, _) => { called = true; return new(0, 5); }); }
            catch (ArgumentException) { rejected = true; }
            assert(rejected && !called, "Invalid GUID is rejected before native access");
        }
    }

    internal static void ProbeBalanced()
    {
        const string scheme = "381b4222-f694-41f0-9685-ff5bb260df2e";
        string[] settings = { "893dee8e-2bef-41e0-89c6-b55d0929964c", "bc5038f7-23e0-4960-96da-33abaf5935ec", "465e1f50-b610-473a-ab58-00d1077dc418", "0cc5b647-c1df-4637-891a-dec35c318583", "ea062031-0e34-4ff1-9b6d-eb1059334028" };
        Console.WriteLine("READ-ONLY Balanced power API probe (no profile applied):");
        for (int i = 0; i < settings.Length; i++)
        {
            var pair = NativePowerPolicyReader.ReadRequiredPair(scheme, settings[i]);
            Console.WriteLine($"{PerformanceProfileVerification.PowerAliases[i]} AC={pair.Ac}, DC={pair.Dc}");
        }
    }
}
