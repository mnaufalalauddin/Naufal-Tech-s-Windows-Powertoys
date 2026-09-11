using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Informational UI must never delay admission or strand an already granted lease.
// Confirmation happens before requesting admission, not in these callbacks.
internal static class TaskAdmission
{
    internal static async Task<T> WaitAsync<T>(Task<T> request, Action showWaiting, Action dismissWaiting)
    {
        bool queued = !request.IsCompleted;
        if (queued) Notify(showWaiting);
        try { return await request; }
        finally { if (queued) Notify(dismissWaiting); }
    }

    private static void Notify(Action notification)
    {
        try { notification(); }
        catch (Exception exception) { Trace.TraceWarning("Task notification failed: {0}", exception); }
    }
}
