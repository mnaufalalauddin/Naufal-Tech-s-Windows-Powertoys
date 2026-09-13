using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// Only drops elevation for the current account. No passwords, shell commands,
// scheduled tasks, cross-account execution, or administrator fallback.
internal sealed class SameUserProcessRunner : IDisposable
{
    private readonly SafeAccessTokenHandle _token;
    private readonly bool _alreadyStandardUser;

    internal SameUserProcessRunner()
    {
        Check(OpenProcessToken(GetCurrentProcess(), 0x000B, out var current), "Read process token");
        using (current)
        {
            var parent = Identity(current);
            _alreadyStandardUser = !parent.Elevated;
            SafeAccessTokenHandle candidate;
            if (parent.Elevated)
            {
                // TokenLinkedToken can be identification-only without SeTcbPrivilege
                // (ERROR_BAD_IMPERSONATION_LEVEL). Use the desktop shell's primary
                // token ONLY after matching its account, session and integrity.
                IntPtr shell = GetShellWindow();
                if (shell == IntPtr.Zero || GetWindowThreadProcessId(shell, out uint processId) == 0)
                    throw new InvalidOperationException("The standard-user Windows desktop is unavailable. No per-user OneDrive change was started.");
                using SafeProcessHandle shellProcess = OpenProcess(0x1000, false, processId);
                if (shellProcess.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Read Windows desktop process failed.");
                Check(OpenProcessToken(shellProcess.DangerousGetHandle(), 0x000A, out candidate), "Read Windows desktop token");
            }
            else
            {
                Check(OpenProcessToken(GetCurrentProcess(), 0x000B, out candidate), "Read standard-user token");
            }
            using (candidate)
            {
                UserExecutionPolicy.Validate(parent, Identity(candidate));
                // Duplicate only after validating identity, and check the result
                // again. Never launch in a different desktop user's account.
                // MAXIMUM_ALLOWED concerns access to this handle, not added user
                // privileges. The child's medium-integrity token remains unchanged.
                // Narrow query-only rights caused ERROR_ACCESS_DENIED in the native
                // secondary-logon launch probe, despite valid identity checks.
                Check(DuplicateTokenEx(candidate, 0x02000000, IntPtr.Zero, 2, 1, out var primary), "Create same-user primary token");
                try { UserExecutionPolicy.Validate(parent, Identity(primary)); }
                catch { primary.Dispose(); throw; }
                _token = primary;
            }
        }
    }

    internal static UserExecutionIdentity CurrentIdentity()
    {
        Check(OpenProcessToken(GetCurrentProcess(), 8, out var token), "Read process identity");
        using (token) return Identity(token);
    }

    private static UserExecutionIdentity Identity(SafeAccessTokenHandle token)
    {
        using WindowsIdentity identity = new(token.DangerousGetHandle());
        IntPtr label = TokenInformation(token, 25); // TokenIntegrityLevel
        int integrity;
        try
        {
            IntPtr sid = Marshal.ReadIntPtr(label);
            int count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
            if (count < 1) throw new InvalidOperationException("The Windows integrity label is invalid.");
            integrity = Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
        }
        finally { Marshal.FreeHGlobal(label); }
        return new(identity.User?.Value, TokenInt(token, 12), TokenInt(token, 20) != 0, integrity);
    }

    private static int TokenInt(SafeAccessTokenHandle token, int informationClass)
    {
        IntPtr buffer = TokenInformation(token, informationClass);
        try { return Marshal.ReadInt32(buffer); }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IntPtr TokenInformation(SafeAccessTokenHandle token, int informationClass)
    {
        GetTokenInformation(token, informationClass, IntPtr.Zero, 0, out int size);
        if (size <= 0) throw new Win32Exception(Marshal.GetLastWin32Error(),
            "A same-account standard-user token is unavailable. Per-user OneDrive changes were not started.");
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Check(GetTokenInformation(token, informationClass, buffer, size, out _), "Read token information");
            return buffer;
        }
        catch { Marshal.FreeHGlobal(buffer); throw; }
    }

    internal async Task<NativeCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        if (!Path.IsPathFullyQualified(executable)) throw new ArgumentException("An absolute executable path is required.");
        if (timeout != Timeout.InfiniteTimeSpan && timeout.TotalMilliseconds < 1)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        string commandLine = UserExecutionPolicy.CommandLine(executable, arguments);
        if (_alreadyStandardUser)
            return await new NativeCommandRunner().RunAsync(executable, arguments, timeout, outputEncoding: Encoding.UTF8);
        using var input = new OutputFile();
        using var output = new OutputFile();
        using var error = new OutputFile();
        Stopwatch elapsed = Stopwatch.StartNew();
        using SafeWaitHandle process = Launch(executable, commandLine, input.Child, output.Child, error.Child);
        // Parent-side inheritable duplicates are no longer needed after launch.
        input.Child.Dispose(); output.Child.Dispose(); error.Child.Dispose();
        bool timedOut = false;
        while (true)
        {
            uint wait = WaitForSingleObject(process, 0);
            if (wait == 0) break;
            if (wait != 258) throw new Win32Exception(Marshal.GetLastWin32Error(), "Wait for standard-user process failed.");
            if (timeout != Timeout.InfiniteTimeSpan && elapsed.Elapsed >= timeout)
            {
                // Production only gives read-only source export a finite timeout.
                // Deployments use Infinite under the existing shared operation gate.
                Check(TerminateProcess(process, 1), "Stop timed-out read-only process");
                if (WaitForSingleObject(process, 5000) != 0)
                    throw new InvalidOperationException("The timed-out process has not confirmed exit.");
                timedOut = true;
                break;
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
        Check(GetExitCodeProcess(process, out uint code), "Read standard-user process result");
        return new(timedOut ? -1 : unchecked((int)code), output.Read(),
            error.Read() + (timedOut ? "\nThe operation timed out." : ""), timedOut, elapsed.Elapsed);
    }

    private SafeWaitHandle Launch(string executable, string commandLine, SafeFileHandle input, SafeFileHandle output, SafeFileHandle error)
    {
        IntPtr environment = IntPtr.Zero;
        try
        {
            Check(CreateEnvironmentBlock(out environment, _token, false), "Create same-user environment");
            StartupInfo startup = new()
            {
                Size = Marshal.SizeOf<StartupInfo>(), Flags = 0x101,
                Input = input.DangerousGetHandle(), Output = output.DangerousGetHandle(), Error = error.DangerousGetHandle()
            };
            // This API uses the ordinary administrator impersonation privilege,
            // not SeAssignPrimaryToken/SeTcb. It does not enable general handle
            // inheritance; the standard IO handles are passed explicitly.
            if (commandLine.Length >= 1024) throw new ArgumentException("The standard-user command line is too long.");
            // The validated desktop account is logged on and its HKCU hive is
            // already loaded. Do not ask secondary logon to reload that profile.
            Check(CreateProcessWithTokenW(_token, 0, executable, new StringBuilder(commandLine),
                0x08000400, environment, Environment.GetFolderPath(Environment.SpecialFolder.System),
                ref startup, out ProcessInformation created), "Start OneDrive command without administrator privileges");
            using var thread = new SafeWaitHandle(created.Thread, true);
            return new SafeWaitHandle(created.Process, true);
        }
        finally
        {
            if (environment != IntPtr.Zero) DestroyEnvironmentBlock(environment);
        }
    }

    // Redirect to private, nonce-named temporary handles instead of pipes: an
    // installer descendant retaining stdout cannot deadlock completion. Delete on
    // last handle close; read by offset so no descendant's file position is changed.
    private sealed class OutputFile : IDisposable
    {
        private readonly FileStream _file;
        internal SafeFileHandle Child { get; }
        internal OutputFile()
        {
            _file = new(Path.Combine(Path.GetTempPath(), "wpt-command-" + Guid.NewGuid().ToString("N") + ".tmp"),
                FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.DeleteOnClose);
            try
            {
                Check(DuplicateHandle(GetCurrentProcess(), _file.SafeFileHandle, GetCurrentProcess(), out var child, 0, true, 2), "Duplicate command output handle");
                Child = child;
            }
            catch { _file.Dispose(); throw; }
        }
        internal string Read()
        {
            const int limit = 1024 * 1024;
            long length = RandomAccess.GetLength(_file.SafeFileHandle);
            byte[] bytes = new byte[(int)Math.Min(length, limit)];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = RandomAccess.Read(_file.SafeFileHandle, bytes.AsSpan(offset), offset);
                if (count == 0) break;
                offset += count;
            }
            return Encoding.UTF8.GetString(bytes, 0, offset).TrimStart('\uFEFF') +
                (length > limit ? "\n[Command output truncated at 1 MiB.]" : "");
        }
        public void Dispose() { Child.Dispose(); _file.Dispose(); }
    }

    public void Dispose() => _token.Dispose();
    private static void Check(bool success, string action)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error(), action + " failed; no elevated retry will be attempted.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        internal int Size;
        internal IntPtr Reserved, Desktop, Title;
        internal uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        internal ushort Show, ReservedSize;
        internal IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }

    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr buffer, int length, out int required);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(SafeAccessTokenHandle existing, uint access, IntPtr security, int impersonationLevel, int tokenType, out SafeAccessTokenHandle primary);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("userenv.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeAccessTokenHandle token, [MarshalAs(UnmanagedType.Bool)] bool inherit);
    [DllImport("userenv.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyEnvironmentBlock(IntPtr environment);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(IntPtr sourceProcess, SafeFileHandle source, IntPtr targetProcess, out SafeFileHandle target, uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint options);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithTokenW(SafeAccessTokenHandle token, uint logonFlags, string application, StringBuilder command,
        uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation created);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeWaitHandle process, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(SafeWaitHandle process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(SafeWaitHandle process, uint code);
}
