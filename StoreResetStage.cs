using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class StoreResetStage
{
    // Empty string = real reset completed. A warning is returned for unsupported/failed reset.
    internal static async Task<string> RunAsync(string? currentUserPackage, Func<string, Task> reset)
    {
        if (string.IsNullOrWhiteSpace(currentUserPackage))
            return "Reset app data: Store is not registered for the current user; continuing with registration.";
        try { await reset(currentUserPackage); return ""; }
        catch (OperationCanceledException) { throw; }
        catch (TimeoutException) { throw; }
        catch (PendingDeploymentException) { throw; }
        catch (Exception exception) { return "Reset app data: " + exception.Message; }
    }
}
