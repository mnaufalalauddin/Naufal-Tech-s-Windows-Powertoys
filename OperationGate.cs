using System;
using System.Threading;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class OperationGate
{
    private int _busy;
    public bool IsBusy => Volatile.Read(ref _busy) != 0;
    public IDisposable? TryEnter() => Interlocked.CompareExchange(ref _busy, 1, 0) == 0
        ? new Lease(this) : null;

    private sealed class Lease(OperationGate owner) : IDisposable
    {
        private OperationGate? _owner = owner;
        public void Dispose()
        {
            OperationGate? current = Interlocked.Exchange(ref _owner, null);
            if (current is not null) Volatile.Write(ref current._busy, 0);
        }
    }
}
