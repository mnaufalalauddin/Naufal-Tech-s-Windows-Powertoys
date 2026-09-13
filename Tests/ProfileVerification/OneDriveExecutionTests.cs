using Naufal_Windows_Tech_s_Powertoys;
using System.Text;

internal static class OneDriveExecutionTests
{
    internal static async Task RunAsync(Action<bool, string> check)
    {
        var elevated = new UserExecutionIdentity("S-1-5-21-fixture", 1, true, 0x3000);
        var standard = elevated with { Elevated = false, Integrity = 0x2000 };
        UserExecutionPolicy.Validate(elevated, standard);
        UserExecutionPolicy.Validate(standard, standard);
        check(true, "same-account medium integrity accepted");
        foreach (var invalid in new[] { standard with { Sid = "other" }, standard with { Sid = null },
            standard with { Session = 2 }, standard with { Elevated = true }, standard with { Integrity = 0x3000 },
            standard with { Integrity = 0x4000 }, standard with { Integrity = 0x1000 } })
        {
            bool rejected = false;
            try { UserExecutionPolicy.Validate(elevated, invalid); } catch (InvalidOperationException) { rejected = true; }
            check(rejected, "reject wrong user/session/elevation/integrity");
        }
        foreach (string scope in new[] { "user", "machine" })
        foreach (bool restore in new[] { false, true })
        {
            int user = 0, machine = 0;
            var owner = new Owner();
            Task<NativeCommandResult> User(string _, IReadOnlyList<string> __, TimeSpan ___)
            { user++; return Task.FromResult(new NativeCommandResult(0, "ok", "", false, TimeSpan.Zero)); }
            Task<NativeCommandResult> Machine(string _, IReadOnlyList<string> __, TimeSpan ___)
            { machine++; return Task.FromResult(new NativeCommandResult(0, "ok", "", false, TimeSpan.Zero)); }
            using (var session = new OneDriveCommandSession(scope, () => (User, owner), Machine))
            {
                await session.RunAsync("winget.exe", ["source", "export"], TimeSpan.FromSeconds(30));
                await session.RunAsync("winget.exe", OneDriveAppPolicy.Arguments(restore, scope), Timeout.InfiniteTimeSpan);
            }
            check(user == (scope == "user" ? 2 : 0) && machine == (scope == "machine" ? 2 : 0), "source and change use same scope " + scope);
            check(owner.Disposed == (scope == "user"), "dispose only created token owner");
        }
        int fallback = 0;
        bool failed = false;
        try
        {
            using var session = new OneDriveCommandSession("user", () => throw new InvalidOperationException("No linked token"),
                (_, _, _) => { fallback++; return Task.FromResult(default(NativeCommandResult)); });
        }
        catch (InvalidOperationException) { failed = true; }
        check(failed && fallback == 0, "missing user token never retries elevated");
        check(UserExecutionPolicy.Quote("") == "\"\"", "quote empty argument");
        check(UserExecutionPolicy.Quote("a b") == "\"a b\"", "quote spaces");
        check(UserExecutionPolicy.Quote("a\"b") == "\"a\\\"b\"", "quote embedded quote");
        check(UserExecutionPolicy.Quote("a\\") == "\"a\\\\\"", "quote trailing backslash");
        check(UserExecutionPolicy.Quote("&|$()") == "\"&|$()\"", "shell metacharacters remain literal");
        bool nullRejected = false;
        try { UserExecutionPolicy.Quote("a\0b"); } catch (ArgumentException) { nullRejected = true; }
        check(nullRejected, "reject NUL argument");
    }

    // Opt-in read-only native probe. The child only reports its token and argv.
    // Never calls WinGet, reads sync folders, or changes Windows configuration.
    internal static bool Child(string[] args)
    {
        if (!args.Contains("--onedrive-scope-child")) return false;
        Console.OutputEncoding = Encoding.UTF8;
        var identity = SameUserProcessRunner.CurrentIdentity();
        Console.WriteLine($"TOKEN|{identity.Sid}|{identity.Session}|{identity.Elevated}|{identity.Integrity}");
        foreach (string arg in args.SkipWhile(a => a != "--onedrive-scope-child").Skip(1))
            Console.WriteLine("ARG|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(arg)));
        Console.Error.WriteLine("STDERR-PROBE");
        Environment.ExitCode = 41;
        return true;
    }

    internal static async Task ProbeAsync(string host, bool requireElevated)
    {
        var parent = SameUserProcessRunner.CurrentIdentity();
        if (requireElevated && !parent.Elevated) throw new Exception("Probe must start elevated to test elevation dropping.");
        using var runner = new SameUserProcessRunner();
        string[] literals = ["", "a b", "quote\"inside", "C:\\with space\\", "&|$()", "Bahasa Indonesia"];
        string[] args = [typeof(OneDriveExecutionTests).Assembly.Location, "--onedrive-scope-child", .. literals];
        var result = await runner.RunAsync(host, args, TimeSpan.FromSeconds(30));
        if (result.ExitCode != 41 || result.TimedOut || !result.StandardError.Contains("STDERR-PROBE"))
            throw new Exception("Native output/exit probe failed: " + result.ExitCode + " " + result.CombinedOutput);
        if (!result.StandardOutput.Contains($"TOKEN|{parent.Sid}|{parent.Session}|False|8192"))
            throw new Exception("Child is not the same account/session at medium integrity.");
        foreach (string literal in literals)
            if (!result.StandardOutput.Contains("ARG|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(literal))))
                throw new Exception("Native argv quoting failed.");
        Console.WriteLine($"PASS: native same-user process; parent elevated={parent.Elevated}; child standard user; stdout, stderr, exit code and six literal arguments verified.");
        string winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        if (File.Exists(winget))
        {
            var source = await runner.RunAsync(winget, ["source", "export", "--name", "winget", "--disable-interactivity"], TimeSpan.FromSeconds(30));
            if (source.TimedOut || source.ExitCode != 0 || !OneDriveAppPolicy.IsOfficialSource(source.StandardOutput))
                throw new Exception("Read-only WinGet source export failed: " + source.ExitCode + " " + source.CombinedOutput);
            Console.WriteLine("PASS: actual WinGet execution alias and official source export in standard-user context. No install/uninstall invoked.");
        }
        else Console.WriteLine("SKIP: actual WinGet alias is unavailable; native process identity probe passed.");
    }

    private sealed class Owner : IDisposable
    {
        internal bool Disposed;
        public void Dispose() => Disposed = true;
    }
}
