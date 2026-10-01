using System;
using System.Threading;

namespace Naufal_Windows_Tech_s_Powertoys;

// In-process invalidation, not a persistent representation of Windows state.
internal static class CatalogStateEpoch
{
    private static long _version;
    internal static long Version => Interlocked.Read(ref _version);
    internal static event Action? Changed;
    internal static void Invalidate()
    {
        Interlocked.Increment(ref _version);
        var subscribers = Changed;
        if (subscribers is null) return;
        foreach (Action subscriber in subscribers.GetInvocationList())
        {
            // A closed UI listener must never turn a successful Windows operation
            // into a failure. Version checks still catch a missed notification.
            try { subscriber(); } catch { }
        }
    }
}
