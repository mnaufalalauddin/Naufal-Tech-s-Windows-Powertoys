using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// WinGet product IDs are not AppX identity names. Keep this exact Store product
// route separate, current-user, and non-elevated even when the app is elevated.
internal static class CopilotStoreService
{
    private static string Winget => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
    private static async Task ValidateSourceAsync(SameUserProcessRunner runner)
    {
        if (!File.Exists(Winget)) throw new InvalidOperationException("WinGet is unavailable; Copilot Store-product detection is not verified.");
        var result = await runner.RunAsync(Winget, ["source", "export", "--name", "msstore", "--disable-interactivity"], TimeSpan.FromSeconds(20));
        if (result.TimedOut || result.ExitCode != 0 || !BuiltInAppsCatalog.IsMicrosoftStoreSource(result.StandardOutput))
            throw new InvalidOperationException("The official Microsoft Store source could not be verified. No source was reset or changed.");
    }
    private static async Task<bool> ReadAsync(SameUserProcessRunner runner)
    {
        var result = await runner.RunAsync(Winget, CopilotStorePolicy.ListArguments(CopilotSourceConsent.IsGranted), TimeSpan.FromSeconds(30));
        return CopilotStorePolicy.Installed(result) ?? throw new InvalidOperationException(
            CopilotStorePolicy.InventoryFailure(result));
    }
    internal static async Task<BuiltInAppPackage?> ProbeAsync()
    {
        try
        {
            using var runner = new SameUserProcessRunner();
            await ValidateSourceAsync(runner);
            return await ReadAsync(runner) ? CopilotStorePolicy.Package() : null;
        }
        catch (Exception exception) { return CopilotStorePolicy.Package(exception.Message); }
    }
    internal static async Task RemoveAsync(IProgress<CatalogProgressUpdate>? progress, Func<string, Task> log)
    {
        using var runner = new SameUserProcessRunner();
        await ValidateSourceAsync(runner);
        if (!await ReadAsync(runner)) return;
        progress?.Report(new("Removing: Microsoft Copilot (Microsoft Store)", null));
        await DeploymentOperationTimeout.AwaitExternalAsync(async () =>
        {
            var result = await runner.RunAsync(Winget, CopilotStorePolicy.RemoveArguments(CopilotSourceConsent.IsGranted), Timeout.InfiniteTimeSpan);
            await log("Microsoft Copilot: exit " + result.ExitCode + Environment.NewLine + result.CombinedOutput);
            if (result.TimedOut || result.ExitCode != 0 && result.ExitCode != CopilotStorePolicy.NoApplications)
                throw new InvalidOperationException("Copilot removal did not complete. " + result.CombinedOutput);
            if (await ReadAsync(runner)) throw new InvalidOperationException("Copilot is still installed for this account.");
            return true;
        }, "Removing: Microsoft Copilot");
    }
}
