using Naufal_Windows_Tech_s_Powertoys;

// Read-only by construction: no switches can invoke removal, restore, or SavePlan.
try
{
    var service = new BuiltInAppsService();
    var packages = await service.ReadAsync();
    foreach (var target in BuiltInAppsCatalog.Targets)
    {
        var found = packages.Where(p => BuiltInAppsCatalog.Matches(target, p)).ToArray();
        Console.WriteLine($"{target.Name}: {found.Length} package(s), healthy={found.Count(p => p.Healthy)}");
    }
    if (service.AuditPath is not null) throw new InvalidOperationException("Read created a mutation audit.");
    Console.WriteLine("PASS: production current-user inventory completed. No app installed, removed, or re-registered.");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
