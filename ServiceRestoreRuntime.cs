using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class ServiceRestoreRuntime
{
    internal static bool ReadStable(string name)
    {
        if (!GamingLiveStatusService.TryReadServiceState(name, out string state) || state is not ("Running" or "Stopped"))
            throw new InvalidOperationException($"Cannot read a stable state for {name}: {state}. Backup retained.");
        return state == "Running";
    }

    internal static Task EnsureAsync(string name, bool running, NativeCommandRunner runner) => EnsureAsync(name, running,
        () => GamingLiveStatusService.TryReadServiceState(name, out string state) ? state : "Unknown",
        async action =>
        {
            var result = await runner.RunAsync("sc.exe", new[] { action, name }, TimeSpan.FromSeconds(25));
            return (result.ExitCode, result.CombinedOutput);
        }, () => Task.Delay(250));

    internal static async Task EnsureAsync(string name, bool running, Func<string> read,
        Func<string, Task<(int ExitCode, string Output)>> command, Func<Task> delay)
    {
        string expected = running ? "Running" : "Stopped";
        if (read() == expected) return;
        var result = await command(running ? "start" : "stop");
        for (int attempt = 0; attempt < 80; attempt++)
        {
            if (read() == expected) return;
            if (result.ExitCode is not (0 or 1056 or 1062)) break;
            await delay();
        }
        throw new InvalidOperationException($"{name} did not reach {expected} (exit {result.ExitCode}). Backup retained. {result.Output}");
    }
}
