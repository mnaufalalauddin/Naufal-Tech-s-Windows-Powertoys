using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Windows inventory calls can block synchronously. Bound callers' wait without
// accumulating another blocked worker on every Analyze/retry. Never use for writes.
internal sealed class BoundedReadProbe<T>
{
    private readonly object _sync = new();
    private Task<T>? _pending;

    public Task<T> ReadAsync(Func<T> readOnlyProbe, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(readOnlyProbe);
        return ReadTaskAsync(() => Task.FromResult(readOnlyProbe()), timeout);
    }

    public Task<T> ReadTaskAsync(Func<Task<T>> readOnlyProbe, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(readOnlyProbe);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        lock (_sync)
        {
            if (_pending is null || _pending.IsCompleted)
            {
                _pending = Task.Run(readOnlyProbe);
                _ = _pending.ContinueWith(task => { _ = task.Exception; },
                    System.Threading.CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            return _pending.WaitAsync(timeout);
        }
    }
}
