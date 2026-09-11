using System;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record StoreRegistrationState(
    string FamilyName, string FullName, bool ManifestExists, bool Healthy);

internal static class StoreRegistrationVerification
{
    internal const string StoreFamily = "Microsoft.WindowsStore_8wekyb3d8bbwe";
    internal const int MaximumAttempts = 6;

    // A reset can temporarily remove registration; only the final repair stage
    // judges it. A Store update may change the full name, but not this family.
    internal static async Task<StoreRegistrationState> VerifyAsync(
        Func<StoreRegistrationState?> read,
        Action<string> log,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? delay = null)
    {
        delay ??= token => Task.Delay(TimeSpan.FromSeconds(1), token);
        string problem = "Microsoft Store registration has not been verified.";
        for (int attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                StoreRegistrationState? state = read();
                problem = GetProblem(state);
                if (problem.Length == 0) return state!;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                problem = $"Cannot read current-user Store registration: {exception.Message}";
            }
            if (attempt < MaximumAttempts)
            {
                log($"Verifying Store registration ({attempt}/{MaximumAttempts}): {problem} Retrying...");
                await delay(cancellationToken);
            }
        }
        throw new InvalidOperationException($"Store verification failed after {MaximumAttempts} checks. {problem}");
    }

    private static string GetProblem(StoreRegistrationState? state)
    {
        if (state is null) return "Microsoft Store is not registered for the current user.";
        if (!string.Equals(state.FamilyName, StoreFamily, StringComparison.OrdinalIgnoreCase))
            return "The registered package does not match the Microsoft Store family.";
        if (!state.ManifestExists) return "Store AppXManifest.xml could not be verified.";
        if (!state.Healthy) return "Windows reports that the Store package is not in a healthy, usable state.";
        return string.Empty;
    }
}
