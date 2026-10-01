using Naufal_Windows_Tech_s_Powertoys;

// Live test flags are never sufficient authority to mutate a physical host.
internal static class GuestAuditIdentity
{
    internal static bool Matches(Guid expected, IReadOnlyList<string?> ids, IReadOnlyList<string?> models) =>
        expected != Guid.Empty && ids.Count == 1 && models.Count == 1 &&
        Guid.TryParse(ids[0], out var actual) && actual == expected && models[0] == "VirtualBox";

    internal static Guid Require(string[] args)
    {
        int index = Array.IndexOf(args, "--expected-vm-uuid");
        if (index < 0 || index + 1 >= args.Length || !Guid.TryParse(args[index + 1], out var expected) || expected == Guid.Empty)
            throw new InvalidOperationException("An explicit disposable VM hardware UUID is required. No settings changed.");
        var products = NativeHardwareData.Query(@"ROOT\CIMV2", "Win32_ComputerSystemProduct", "UUID");
        var systems = NativeHardwareData.Query(@"ROOT\CIMV2", "Win32_ComputerSystem", "Model");
        if (!Matches(expected, products.Select(p => p.GetValueOrDefault("UUID")).ToArray(),
            systems.Select(s => s.GetValueOrDefault("Model")).ToArray()))
            throw new InvalidOperationException("This is not the explicitly identified disposable VirtualBox guest. No settings changed.");
        return expected;
    }
}
