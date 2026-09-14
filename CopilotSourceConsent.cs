using System;
using System.Threading;

namespace Naufal_Windows_Tech_s_Powertoys;

// UI callers create this scope only AFTER the source-specific confirmation.
// AsyncLocal isolates concurrent operations; disposal also revokes inherited
// copies so a delayed child cannot reuse consent after its operation ends.
internal static class CopilotSourceConsent
{
    private static readonly AsyncLocal<Grant?> Current = new();
    internal static bool IsGranted => Current.Value?.IsActive == true;

    internal static IDisposable BeginConfirmedOperation()
    {
        Grant grant = new(Current.Value);
        Current.Value = grant;
        return grant;
    }

    private sealed class Grant(Grant? previous) : IDisposable
    {
        private int active = 1;
        internal bool IsActive => Volatile.Read(ref active) == 1;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref active, 0) == 0) return;
            if (ReferenceEquals(Current.Value, this)) Current.Value = previous;
        }
    }
}
