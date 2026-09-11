using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class PendingDeploymentException(string message) : InvalidOperationException(message);

// A timeout stops waiting, not necessarily the underlying Windows mutation.
// Retain ownership until completion is observed, including after cancellation.
internal sealed class BoundedOperationGate
{
    private readonly object _sync = new();
    private bool _busy;

    public async Task<T> RunAsync<T>(Func<Task<T>> start, Action cancel, Action close,
        string name, TimeSpan timeout, TimeSpan cancellationGrace)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(cancel);
        ArgumentNullException.ThrowIfNull(close);
        if (timeout <= TimeSpan.Zero || cancellationGrace <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        lock (_sync)
        {
            if (_busy)
                throw new PendingDeploymentException(
                    $"{name} was not started: a previous Windows deployment is still pending. " +
                    "Wait for it to finish; do not start another package operation. Restart Windows if it remains stuck.");
            _busy = true;
        }
        Task<T>? task = null;
        try
        {
            task = start();
            try { return await task.WaitAsync(timeout); }
            catch (TimeoutException) when (!task.IsCompleted)
            {
                string cancellationError = "";
                try { cancel(); }
                catch (Exception exception) { cancellationError = " Cancellation request failed: " + exception.Message; }
                try { return await task.WaitAsync(cancellationGrace); }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"{name} exceeded {timeout.TotalSeconds:0} seconds. " +
                        "Windows acknowledged cancellation. Partial changes may remain; analyze again before retrying.");
                }
                catch (TimeoutException) when (!task.IsCompleted)
                {
                    throw new TimeoutException($"{name} exceeded {timeout.TotalSeconds:0} seconds. " +
                        "Cancellation is not confirmed; Windows may still be changing this package. " +
                        "Further package operations are blocked until it finishes." + cancellationError);
                }
            }
        }
        finally
        {
            if (task is null || task.IsCompleted) Release(close);
            else _ = ReleaseWhenFinishedAsync(task, close);
        }
    }

    private async Task ReleaseWhenFinishedAsync(Task task, Action close)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception) { /* Observe late faults; the timeout has already been reported. */ }
        finally { Release(close); }
    }

    private void Release(Action close)
    {
        lock (_sync)
        {
            try { close(); }
            catch (Exception) { /* Cleanup must not mask the deployment result. */ }
            finally { _busy = false; }
        }
    }
}
