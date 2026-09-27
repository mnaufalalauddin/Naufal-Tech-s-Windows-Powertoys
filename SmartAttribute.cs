namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record SmartAttribute(byte Id, byte Current, byte Worst, byte? Threshold, string Raw, ushort Flags = 0)
{
    internal string Name => Id switch
    {
        0x05 => "Reallocated sectors", 0x09 => "Power-on hours", 0x0C => "Power cycle count",
        0xC2 => "Temperature", 0xC5 => "Pending sectors", 0xC6 => "Uncorrectable sectors",
        0xC7 => "Interface CRC errors", _ => "Vendor-defined attribute"
    };
}
