using System;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Both directions must carry the same progress context, including synchronous
// portions of a backend. Never run those portions on the XAML dispatcher.
internal static class CatalogActionRunner
{
    internal static Task<ToolActionResult> ExecuteAsync(
        IToolActionService service,
        ToolActionDefinition definition,
        bool restore,
        IProgress<CatalogProgressUpdate>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (restore && !definition.SupportsRestore)
            return Task.FromResult(new ToolActionResult(false, "This action does not have a restore operation."));

        return Task.Run(async () =>
        {
            using var scope = CatalogOperationProgress.Begin(progress);
            return restore
                ? await service.RestoreAsync(definition)
                : await service.RunAsync(definition);
        });
    }
}
