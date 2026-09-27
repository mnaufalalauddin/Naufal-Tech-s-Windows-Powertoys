namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct SystemReportEntry(
    string Property, string Value, bool IsSection, bool IsNetworkAddress = false,
    SmartAttribute? Smart = null);
