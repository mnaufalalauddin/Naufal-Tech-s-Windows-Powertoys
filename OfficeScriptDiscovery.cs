using System;
using System.Collections.Generic;
using System.IO;

namespace Naufal_Windows_Tech_s_Powertoys;

// Enumerate known install layouts only; never recursively search or run a
// script from the working directory/network because a known path was absent.
internal static class OfficeScriptDiscovery
{
    internal static IReadOnlyList<string> Candidates(IEnumerable<string> programDirectories, IEnumerable<string> registeredRoots)
    {
        List<string> paths = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        void Add(string root, params string[] suffix)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            root = root.Trim().Trim('"');
            if (root.Length < 3 || !char.IsAsciiLetter(root[0]) || root[1] != ':' ||
                (root[2] != '\\' && root[2] != '/')) return;
            try
            {
                string path = root;
                foreach (string part in suffix) path = Path.Combine(path, part);
                path = Path.GetFullPath(path);
                if (seen.Add(path)) paths.Add(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            { /* Invalid registration must not hide other installations. */ }
        }
        foreach (string root in registeredRoots)
        {
            Add(root, "ospp.vbs");
            Add(root, "root", "Office16", "ospp.vbs");
            Add(root, "Office16", "ospp.vbs");
        }
        foreach (string directory in programDirectories)
            foreach (string version in new[] { "Office16", "Office15", "Office14", "Office19" })
            {
                Add(directory, "Microsoft Office", "root", version, "ospp.vbs");
                Add(directory, "Microsoft Office", version, "ospp.vbs");
            }
        return paths;
    }
}
