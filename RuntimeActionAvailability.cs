namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct RuntimeActionAvailability(bool Install, bool Repair, bool Enable)
{
    internal static RuntimeActionAvailability Resolve(bool busy, bool installable, bool repairable, bool enableable) =>
        new(!busy && installable && !repairable, !busy && repairable, !busy && enableable);
}
