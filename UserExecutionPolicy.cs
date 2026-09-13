using System;
using System.Collections.Generic;
using System.Text;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct UserExecutionIdentity(string? Sid, int Session, bool Elevated, int Integrity);

internal static class UserExecutionPolicy
{
    internal static void Validate(UserExecutionIdentity parent, UserExecutionIdentity child)
    {
        // Never substitute Explorer's account, a different session, SYSTEM, or an
        // elevated token when a per-user installer requires a standard user.
        if (string.IsNullOrEmpty(parent.Sid) || parent.Sid != child.Sid || parent.Session != child.Session ||
            child.Elevated || child.Integrity is < 0x2000 or >= 0x3000)
            throw new InvalidOperationException("A non-administrator process for this same Windows account could not be verified. No per-user OneDrive change was started. Use Installed apps from this account without administrator privileges.");
    }

    internal static string CommandLine(string executable, IReadOnlyList<string> arguments)
    {
        StringBuilder result = new(Quote(executable));
        foreach (string argument in arguments) result.Append(' ').Append(Quote(argument));
        if (result.Length >= 32767) throw new ArgumentException("The Windows command line is too long.");
        return result.ToString();
    }

    // Windows argv quoting, not cmd.exe/PowerShell syntax. Every argument remains
    // a single literal argument, including quotes, spaces and trailing slashes.
    internal static string Quote(string value)
    {
        if (value.Contains('\0')) throw new ArgumentException("A process argument contains a null character.");
        StringBuilder result = new("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
            result.Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }
}
