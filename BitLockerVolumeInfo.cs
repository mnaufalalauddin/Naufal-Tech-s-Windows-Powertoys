using System;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct BitLockerVolumeInfo(string MountPoint, string VolumeStatus,
    int? EncryptionPercentage, string ProtectionStatus, string LockStatus)
{
    public bool IsFullyDecrypted => VolumeStatus.Equals("FullyDecrypted", StringComparison.OrdinalIgnoreCase) ||
        VolumeStatus.Equals("Fully Decrypted", StringComparison.OrdinalIgnoreCase);
}

internal static class BitLockerVerification
{
    internal static bool ProtectionSucceeded(int exitCode, BitLockerVolumeInfo? after, string expected) =>
        exitCode == 0 && after.HasValue &&
        string.Equals(after.Value.ProtectionStatus, expected, StringComparison.OrdinalIgnoreCase) &&
        expected is "On" or "Off";
    internal static bool DecryptionAccepted(int exitCode, BitLockerVolumeInfo? after) =>
        exitCode == 0 && after.HasValue &&
        (after.Value.IsFullyDecrypted || after.Value.VolumeStatus == "DecryptionInProgress");
}
