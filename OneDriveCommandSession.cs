using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class OneDriveCommandSession : IDisposable
{
    internal delegate Task<NativeCommandResult> Command(string image, IReadOnlyList<string> arguments, TimeSpan timeout);
    private readonly Command _run;
    private readonly IDisposable? _owner;

    internal OneDriveCommandSession(string scope) : this(scope, () =>
    {
        var runner = new SameUserProcessRunner();
        return (runner.RunAsync, runner);
    }, (image, arguments, timeout) => new NativeCommandRunner().RunAsync(image, arguments, timeout)) { }

    internal OneDriveCommandSession(string scope, Func<(Command Run, IDisposable Owner)> userFactory, Command machine)
    {
        if (!OneDriveAppPolicy.IsScope(scope)) throw new ArgumentException("Unknown OneDrive installation scope.", nameof(scope));
        // Preflight before source export/scope save. Never retry user commands elevated.
        if (scope == "user") (_run, _owner) = userFactory();
        else _run = machine;
    }

    internal Task<NativeCommandResult> RunAsync(string image, IReadOnlyList<string> arguments, TimeSpan timeout) => _run(image, arguments, timeout);
    public void Dispose() => _owner?.Dispose();
}
