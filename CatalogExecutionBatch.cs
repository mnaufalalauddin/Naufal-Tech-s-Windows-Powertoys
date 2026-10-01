using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// A batch owns execution results, not persistent Windows state or snapshots.
// The same canonical leaf and operation are executed once, including Restore.
internal sealed class CatalogExecutionBatch : IDisposable
{
    private static readonly AsyncLocal<CatalogExecutionBatch?> Ambient = new();
    private readonly CatalogExecutionBatch? _previous;
    private readonly Dictionary<string, (ToolToggleDefinition Definition, Task<ToolToggleOperationResult> Task)> _results = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    private bool _disposed;
    private CatalogExecutionBatch() { _previous = Ambient.Value; Ambient.Value = this; }
    internal static CatalogExecutionBatch Begin() => new();

    internal static Task<ToolToggleOperationResult> ExecuteAsync(IToolToggleService service, ToolToggleDefinition definition,
        CatalogOperation operation, Func<Task<ToolToggleOperationResult>> execute)
    {
        var batch = Ambient.Value;
        if (batch is null) return execute();
        string key = new CatalogPlanAction(service, definition).Key + ":" + operation;
        lock (batch._sync)
        {
            if (batch._disposed) throw new ObjectDisposedException(nameof(CatalogExecutionBatch));
            if (batch._results.TryGetValue(key, out var prior))
            {
                if (prior.Definition != definition) throw new InvalidOperationException("Incompatible canonical definitions in execution batch: " + key);
                return prior.Task;
            }
            // Register before executing so concurrent references share the in-flight result.
            var completion = new TaskCompletionSource<ToolToggleOperationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            batch._results.Add(key, (definition, completion.Task));
            _ = CompleteAsync(execute, completion);
            return completion.Task;
        }
    }

    private static async Task CompleteAsync(Func<Task<ToolToggleOperationResult>> execute, TaskCompletionSource<ToolToggleOperationResult> completion)
    {
        try { completion.TrySetResult(await execute().ConfigureAwait(false)); }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Ambient.Value = _previous;
    }
}
