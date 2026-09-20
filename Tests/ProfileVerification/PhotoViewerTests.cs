using Microsoft.Win32;
using Naufal_Windows_Tech_s_Powertoys;

internal static class PhotoViewerTests
{
    internal static void Run(Action<bool, string> check)
    {
        var plan = PhotoViewerRegistration.CreatePlan(@"C:\Program Files\Windows Photo Viewer\PhotoViewer.dll", @"C:\Windows\System32");
        check(plan.Length == 37 && plan.Select(e => e.Tag).Distinct().Count() == 37, "Photo Viewer complete unique plan");
        check(plan.Count(e => e.Legacy) == 15, "Photo Viewer preserves all legacy snapshot tags");
        check(PhotoViewerRegistration.Extensions.Length == 13, "Photo Viewer preserves 13 image extensions");
        foreach (var ext in PhotoViewerRegistration.Extensions)
        {
            check(plan.Any(e => e.Tag == "Association" + ext && e.Value == PhotoViewerRegistration.ProgId),
                "Photo Viewer real shared ProgID for " + ext);
            check(plan.Any(e => e.Path == @"SOFTWARE\Classes\" + ext + @"\OpenWithProgids" &&
                e.Name == PhotoViewerRegistration.ProgId && e.Value == ""), "Photo Viewer Open with for " + ext);
        }
        check(plan.All(e => !e.Path.Contains("UserChoice") && !e.Path.StartsWith(@"SOFTWARE\Classes\PhotoViewer.FileAssoc.")),
            "Photo Viewer does not overwrite protected user choice or Windows handlers");
        check(plan.Where(e => e.Path.StartsWith(@"SOFTWARE\Classes\.")).All(e => e.Name.Length > 0),
            "Photo Viewer never replaces extension default or third-party Open with entries");
        string command = plan.Single(e => e.Tag == "HandlerCommand").Value;
        check(command == "\"C:\\Windows\\System32\\rundll32.exe\" \"C:\\Program Files\\Windows Photo Viewer\\PhotoViewer.dll\", ImageView_Fullscreen %1",
            "Photo Viewer preserves documented raw image argument and quotes executable/DLL paths");
        check(plan.Single(e => e.Tag == "HandlerDropTarget").Value == PhotoViewerRegistration.DropTargetClsid,
            "Photo Viewer registers documented Explorer DropTarget");
        var state = plan.ToDictionary(e => e.Tag, e => ((object?)e.Value, (RegistryValueKind?)RegistryValueKind.String));
        var ready = PhotoViewerRegistration.Inspect(plan, e => state[e.Tag]);
        check(ready.Ready && ready.Detail.Contains("Default Apps") && ready.Detail.Contains("PNG/JPG"),
            "Photo Viewer ready does not imply changed default");
        foreach (var entry in plan)
        {
            var saved = state[entry.Tag];
            state[entry.Tag] = (null, null);
            check(!PhotoViewerRegistration.Inspect(plan, e => state[e.Tag]).Ready, "Photo Viewer missing value " + entry.Tag);
            state[entry.Tag] = ("incorrect", RegistryValueKind.String);
            check(!PhotoViewerRegistration.Inspect(plan, e => state[e.Tag]).Ready, "Photo Viewer incorrect value " + entry.Tag);
            state[entry.Tag] = saved;
        }
        // Reproduces the reported failure: all 13 associations but no handlers.
        check(!PhotoViewerRegistration.Inspect(plan, e => e.Legacy ? state[e.Tag] : (null, null)).Ready,
            "Photo Viewer old extension-only registration is not ON");
        state["HandlerCommand"] = (command, RegistryValueKind.ExpandString);
        check(!PhotoViewerRegistration.Inspect(plan, e => state[e.Tag]).Ready, "Photo Viewer rejects wrong registry type");
        state["HandlerCommand"] = (command, RegistryValueKind.String);

        var events = new List<string>();
        PhotoViewerRegistration.Apply(plan, e => events.Add("capture:" + e.Tag),
            () => events.Add("schema"), e => events.Add("write:" + e.Tag), () => events.Add("notify"));
        check(events.Take(plan.Length).All(e => e.StartsWith("capture:")) &&
            events[plan.Length] == "schema" && events[^1] == "notify", "Photo Viewer snapshots entire plan before writing");
        events.Clear();
        try
        {
            PhotoViewerRegistration.Apply(plan,
                e => { if (e.Tag == "HandlerCommand") throw new IOException("capture denied"); },
                () => events.Add("schema"), _ => events.Add("write"), () => events.Add("notify"));
            check(false, "Photo Viewer capture error propagated");
        }
        catch (IOException) { check(events.Count == 0, "Photo Viewer failed capture causes no registration writes"); }
        try
        {
            PhotoViewerRegistration.Apply(plan, _ => { }, () => { },
                _ => throw new IOException("write denied"), () => events.Add("notify"));
            check(false, "Photo Viewer write error propagated");
        }
        catch (IOException) { check(events.SequenceEqual(["notify"]), "Photo Viewer partial apply still notifies Shell"); }

        var backup = new Dictionary<string, object?>();
        void Capture(PhotoViewerEntry e)
        {
            // First-change semantics including binary and expand-string originals.
            if (RegistrySnapshotCommit.IsCaptured(k => backup.GetValueOrDefault(k), e.Tag)) return;
            RegistrySnapshotCommit.Write((k, v, _) => backup[k] = v, () => { }, e.Tag,
                e.Tag == "ApplicationName", RegistryValueKind.ExpandString, "%OldPhotoViewer%");
        }
        foreach (var e in plan.Where(e => e.Legacy)) Capture(e);
        var old = PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k));
        check(old.Length == 15 && old.All(e => e.Legacy), "Photo Viewer old backup restores only values old app changed");
        // A capture interruption before writing the schema must not assume
        // uncaptured new registry values were absent.
        Capture(plan.Single(e => e.Tag == "HandlerName"));
        check(PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k)).Length == 16,
            "Photo Viewer incomplete upgrade preserves untouched new entries");
        backup[PhotoViewerRegistration.SchemaKey] = 2;
        check(!PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k)).Any(e => e.Tag == "HandlerDropTarget"),
            "Photo Viewer v2 restore does not delete a DropTarget it never captured");
        foreach (var e in plan) Capture(e);
        backup[PhotoViewerRegistration.SchemaKey] = 2;
        var restoredEntries = PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k));
        check(restoredEntries.Length == plan.Length, "Photo Viewer upgraded backup restores full plan");
        check((string)backup["ApplicationName.Value"]! == "%OldPhotoViewer%",
            "Photo Viewer upgraded snapshot retains original legacy value");
        RegistryRestorePlan.RequireTags(k => backup.GetValueOrDefault(k), restoredEntries.Select(e => e.Tag), (s, _) => s);
        backup[PhotoViewerRegistration.SchemaKey] = PhotoViewerRegistration.CurrentSchema;
        var currentEntries = PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k));
        check(currentEntries.Length == 37 && currentEntries.Any(e => e.Tag == "HandlerDropTarget"),
            "Photo Viewer schema 3 requires the complete DropTarget registration snapshot");
        RegistryRestorePlan.RequireTags(k => backup.GetValueOrDefault(k), currentEntries.Select(e => e.Tag), (s, _) => s);
        foreach (object? marker in new object?[] { null, 0, "1" })
        {
            if (marker is null) backup.Remove("HandlerDropTarget.Captured");
            else backup["HandlerDropTarget.Captured"] = marker;
            bool rejected = false;
            try
            {
                RegistryRestorePlan.RequireTags(k => backup.GetValueOrDefault(k),
                    PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k)).Select(e => e.Tag), (s, _) => s);
            }
            catch (InvalidOperationException) { rejected = true; }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "Photo Viewer schema 3 rejects absent or corrupt DropTarget capture before restore");
        }
        backup["HandlerDropTarget.Captured"] = 1;
        backup[PhotoViewerRegistration.SchemaKey] = 2;
        // Exercise exact value/kind restoration with an in-memory registry only.
        var targets = plan.ToDictionary(e => new RestoreRegistryTarget(RegistryHive.LocalMachine, e.Path, e.Name),
            e => ((object?)e.Value, (RegistryValueKind?)RegistryValueKind.String));
        var restorePlan = restoredEntries.Select(e => new RestoreRegistryValue(
            new(RegistryHive.LocalMachine, e.Path, e.Name),
            (int)backup[e.Tag + ".Exists"]! == 1 ? backup[e.Tag + ".Value"] : null,
            (int)backup[e.Tag + ".Exists"]! == 1 ? RegistryValueKind.ExpandString : null)).ToArray();
        RegistryRestorePlan.Execute(restorePlan, t => targets[t], e => targets[e.Target] = (e.Value, e.Kind));
        check(targets.Values.Count(v => v.Item1 is not null) == 1 &&
            targets[restorePlan[0].Target].Item2 == RegistryValueKind.ExpandString, "Photo Viewer exact restore, no guessed defaults");
        backup.Remove("HandlerCommand.Captured");
        try
        {
            RegistryRestorePlan.RequireTags(k => backup.GetValueOrDefault(k),
                PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k)).Select(e => e.Tag), (s, _) => s);
            check(false, "Photo Viewer corrupt new snapshot rejected");
        }
        catch (InvalidOperationException) { check(true, "Photo Viewer missing v2 snapshot field cannot be silently skipped"); }
        backup[PhotoViewerRegistration.SchemaKey] = 99;
        try
        {
            PhotoViewerRegistration.RestoreEntries(plan, k => backup.GetValueOrDefault(k));
            check(false, "Photo Viewer unknown schema rejected");
        }
        catch (InvalidDataException) { check(true, "Photo Viewer unknown schema keeps backup"); }

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string service = File.ReadAllText(Path.Combine(root, "EssentialTweaksService.cs"));
        check(service.Contains("PhotoViewerRegistration.Apply(PhotoViewerPlan()") &&
            service.Contains("PhotoViewerRegistration.Inspect(PhotoViewerPlan(), PhotoViewerRegistration.ReadNative)"),
            "Photo Viewer Apply and readback use tested shared plan");
        check(!service.Contains("bool markedApplied") && !service.Contains("PhotoViewer.FileAssoc.Png"),
            "Photo Viewer no stale marker or dangling Windows PNG ProgID");
        check(service.Contains("PhotoViewerRegistration.NotifyShell") && service.Contains("ms-settings:defaultapps"),
            "Photo Viewer Shell refresh and user-confirmed Default Apps wired");
        check(service.Contains("PhotoViewerRegistration.RestoreEntries(PhotoViewerPlan(), key => backup.GetValue(key)).Select"),
            "Photo Viewer restores preflight entire snapshot");
    }

    internal static void ProbeReadOnly()
    {
        var plan = PhotoViewerRegistration.CreatePlan(PhotoViewerRegistration.ViewerDll, Environment.SystemDirectory);
        Console.WriteLine("PhotoViewer.dll present: " + File.Exists(PhotoViewerRegistration.ViewerDll));
        Console.WriteLine(PhotoViewerRegistration.Inspect(plan, PhotoViewerRegistration.ReadNative).Detail);
        foreach (string progId in new[] { "PhotoViewer.FileAssoc.Tiff", "PhotoViewer.FileAssoc.Jpeg", "PhotoViewer.FileAssoc.Png" })
        {
            using var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64);
            using var key = root.OpenSubKey(progId + @"\shell\open\command");
            Console.WriteLine(progId + " command: " + (key?.GetValue("") ?? "<missing>"));
        }
        Console.WriteLine("Read-only: no registration, settings, snapshots or defaults changed.");
    }
}
