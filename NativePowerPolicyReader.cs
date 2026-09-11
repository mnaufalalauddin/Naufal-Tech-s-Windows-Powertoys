using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct PowerPolicyPair(uint Ac, uint Dc);
internal readonly record struct PowerIndexReadResult(uint Error, uint Value);

internal static class NativePowerPolicyReader
{
    private static readonly Guid ProcessorSubgroup = new("54533251-82be-4824-96c1-47b60b740d00");

    // The per-scheme registry tree contains overrides, not necessarily every
    // effective value. Use the power API for snapshots, apply verification and
    // live verification alike. Missing/denied API reads must never become zero.
    public static PowerPolicyPair ReadRequiredPair(string scheme, string setting,
        Func<Guid, Guid, Guid, bool, PowerIndexReadResult>? readIndex = null) =>
        ReadRequiredPairInSubgroup(scheme, ProcessorSubgroup.ToString(), setting, readIndex);

    public static PowerPolicyPair ReadRequiredPairInSubgroup(string scheme, string subgroup, string setting,
        Func<Guid, Guid, Guid, bool, PowerIndexReadResult>? readIndex = null)
    {
        if (!Guid.TryParse(scheme, out Guid plan))
            throw new ArgumentException("The power scheme GUID is invalid.", nameof(scheme));
        if (!Guid.TryParse(setting, out Guid settingId))
            throw new ArgumentException("The power setting GUID is invalid.", nameof(setting));
        if (!Guid.TryParse(subgroup, out Guid subgroupId))
            throw new ArgumentException("The power subgroup GUID is invalid.", nameof(subgroup));
        readIndex ??= ReadNativeIndex;
        PowerIndexReadResult ac = readIndex(plan, subgroupId, settingId, true);
        PowerIndexReadResult dc = readIndex(plan, subgroupId, settingId, false);
        if (ac.Error != 0 || dc.Error != 0)
        {
            uint error = ac.Error != 0 ? ac.Error : dc.Error;
            throw new Win32Exception(unchecked((int)error),
                $"Windows could not read power setting {setting} in subgroup {subgroup} for scheme {scheme}. " +
                $"PowerReadACValueIndex error={ac.Error}; PowerReadDCValueIndex error={dc.Error}. " +
                new Win32Exception(unchecked((int)error)).Message);
        }
        return new(ac.Value, dc.Value);
    }

    private static PowerIndexReadResult ReadNativeIndex(Guid scheme, Guid subgroup, Guid setting, bool ac)
    {
        uint value;
        uint error = ac
            ? PowerReadACValueIndex(0, in scheme, in subgroup, in setting, out value)
            : PowerReadDCValueIndex(0, in scheme, in subgroup, in setting, out value);
        return new(error, value);
    }

    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerReadACValueIndex(nint root, in Guid scheme, in Guid subgroup, in Guid setting, out uint value);
    [DllImport("powrprof.dll", ExactSpelling = true)]
    private static extern uint PowerReadDCValueIndex(nint root, in Guid scheme, in Guid subgroup, in Guid setting, out uint value);
}
