using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class StoreDaclSnapshot
{
    internal static string AccessSddl(string saved)
    {
        RawSecurityDescriptor descriptor = new(saved);
        // A missing DACL must not become an unrestricted file through a corrupt
        // snapshot. Existing snapshots from this feature contain a real DACL.
        if (!descriptor.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclPresent) ||
            descriptor.DiscretionaryAcl is null)
            throw new InvalidDataException("The saved Store permissions have no DACL. Backup retained.");
        return descriptor.GetSddlForm(AccessControlSections.Access);
    }

    internal static bool Matches(string saved, string actual)
    {
        RawSecurityDescriptor expected = new(AccessSddl(saved));
        RawSecurityDescriptor current = new(AccessSddl(actual));
        if (expected.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected) !=
            current.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected)) return false;
        RawAcl left = expected.DiscretionaryAcl!, right = current.DiscretionaryAcl!;
        if (left.Revision != right.Revision || left.Count != right.Count) return false;
        for (int index = 0; index < left.Count; index++)
        {
            byte[] a = new byte[left[index].BinaryLength], b = new byte[right[index].BinaryLength];
            left[index].GetBinaryForm(a, 0);
            right[index].GetBinaryForm(b, 0);
            if (!a.SequenceEqual(b)) return false;
        }
        // Owner, group, audit ACL and auto-inherit bookkeeping are not the
        // permissions changed by this feature. ACE order/masks/SIDs still match exactly.
        return true;
    }
}
