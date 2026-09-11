using System;
using System.Threading;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct CatalogProgressUpdate(string Detail, double? Percent);

internal static class CatalogOperationProgress
{
    private static readonly AsyncLocal<IProgress<CatalogProgressUpdate>?> Reporter = new();
    public static IProgress<CatalogProgressUpdate>? Current => Reporter.Value;
    public static IDisposable Begin(IProgress<CatalogProgressUpdate>? reporter)
    {
        var previous = Reporter.Value;
        Reporter.Value = reporter ?? previous;
        return new Scope(previous);
    }
    private sealed class Scope(IProgress<CatalogProgressUpdate>? previous) : IDisposable
    {
        public void Dispose() => Reporter.Value = previous;
    }
}
