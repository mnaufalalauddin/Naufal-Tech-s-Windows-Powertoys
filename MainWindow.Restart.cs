using System;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private readonly RestartPromptCoordinator _pendingRestarts = new();

    private async Task OfferPendingRestartAsync()
    {
        if (_isClosed || !RootLayout.IsLoaded) return;
        try
        {
            await _pendingRestarts.OfferAsync(
                () => _isClosed || HasPendingWork,
                names => ShowConfirmationWindowAsync(
                    "Restart Windows to finish changes",
                    "The following completed changes are marked as restart-sensitive:" +
                    Environment.NewLine + Environment.NewLine +
                    string.Join(Environment.NewLine, names.Select(name => "• " + name)) +
                    Environment.NewLine + Environment.NewLine +
                    "Save your work and close other applications before restarting. " +
                    "Choose Later to keep working and restart from Windows when convenient.",
                    "Restart now", "Later"),
                async () =>
                {
                    NativeCommandResult result = await _commandRunner.RunAsync(
                        "shutdown.exe", new[] { "/r", "/t", "0" }, TimeSpan.FromSeconds(10));
                    if (result.ExitCode != 0)
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.CombinedOutput)
                            ? $"Restart request returned exit code {result.ExitCode}." : result.CombinedOutput);
                    TaskStatusMessage = "TASKS: RESTARTING";
                });
        }
        catch (Exception exception)
        {
            if (!_isClosed)
                await ShowMessageDialogAsync("Restart request failed",
                    exception.Message + Environment.NewLine + "You can restart later from the Windows Start menu.");
        }
    }
}
