using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class SecurityMitigationNative : ISecurityMitigationPlatform, ILsaProtectionPlatform
{
    private const string DeviceGuard = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
    private const string Hvci = DeviceGuard + @"\Scenarios\HypervisorEnforcedCodeIntegrity";
    private const string Lsa = @"SYSTEM\CurrentControlSet\Control\Lsa";

    public SecurityMitigationSnapshot Read()
    {
        List<string> errors = [];
        int? enabled = null, hvciLocked = null, vbsLocked = null, vbs = null, ci = null;
        int[]? configured = null, running = null, hardware = null;
        string machine = "";
        bool? managed = null, policy = null;
        bool client = false;
        int build = 0;
        try
        {
            using var machineHive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var version = machineHive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            client = string.Equals(version?.GetValue("InstallationType") as string, "Client", StringComparison.OrdinalIgnoreCase);
            int.TryParse(version?.GetValue("CurrentBuildNumber") as string, NumberStyles.None, CultureInfo.InvariantCulture, out build);
            using var identity = machineHive.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            machine = identity?.GetValue("MachineGuid") as string ?? "";
            enabled = Dword(machineHive, Hvci, "Enabled");
            hvciLocked = Dword(machineHive, Hvci, "Locked");
            vbsLocked = Dword(machineHive, DeviceGuard, "Locked");
            policy = HasContent(machineHive, @"SOFTWARE\Policies\Microsoft\Windows\DeviceGuard") ||
                HasContent(machineHive, @"SOFTWARE\Microsoft\PolicyManager\current\device\DeviceGuard") ||
                HasContent(machineHive, @"SOFTWARE\Microsoft\PolicyManager\current\device\VirtualizationBasedTechnology");
            bool enrolled = HasContent(machineHive, @"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo") ||
                HasContent(machineHive, @"SOFTWARE\Microsoft\Provisioning\OMADM\Accounts") ||
                HasContent(machineHive, @"SOFTWARE\Microsoft\Enrollments");
            int computers = 0;
            bool? domain = null;
            NativeRscReader.Visit((_, instance) => { domain = bool.TryParse(NativeRscReader.ReadValue(instance, "PartOfDomain"), out bool joined) ? joined : null; computers++; },
                @"ROOT\CIMV2", "SELECT PartOfDomain FROM Win32_ComputerSystem");
            managed = computers == 1 && domain.HasValue ? enrolled || domain.Value : null;
        }
        catch (Exception ex) { errors.Add("Registry/management evidence: " + ex.Message); }
        try
        {
            int instances = 0;
            NativeRscReader.Visit((_, instance) =>
            {
                instances++;
                configured = SecurityMitigationWmiArray.Read(instance, "SecurityServicesConfigured");
                running = SecurityMitigationWmiArray.Read(instance, "SecurityServicesRunning");
                hardware = SecurityMitigationWmiArray.Read(instance, "AvailableSecurityProperties");
                if (int.TryParse(NativeRscReader.ReadValue(instance, "VirtualizationBasedSecurityStatus"), out int value) && value is >= 0 and <= 2) vbs = value;
                if (int.TryParse(NativeRscReader.ReadValue(instance, "CodeIntegrityPolicyEnforcementStatus"), out int policyValue) && policyValue is >= 0 and <= 2) ci = policyValue;
            }, @"ROOT\Microsoft\Windows\DeviceGuard", "SELECT SecurityServicesConfigured, SecurityServicesRunning, AvailableSecurityProperties, VirtualizationBasedSecurityStatus, CodeIntegrityPolicyEnforcementStatus FROM Win32_DeviceGuard");
            if (instances != 1)
            {
                configured = running = hardware = null; vbs = ci = null;
                errors.Add("Win32_DeviceGuard did not return exactly one instance.");
            }
        }
        catch (Exception ex) { configured = running = hardware = null; vbs = ci = null; errors.Add("Win32_DeviceGuard: " + ex.Message); }
        return new(build, client, WindowsPrivilegeService.IsAdministrator(), machine, enabled, hvciLocked, vbsLocked,
            vbs, configured, running, hardware, managed, policy, ci, string.Join("; ", errors)) { Lsa = ReadLsaEvidence() };
    }

    private static LsaProtectionSnapshot ReadLsaEvidence()
    {
        int? configured = null;
        bool? policy = null;
        uint? effective = null;
        List<string> errors = [];
        try
        {
            using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            configured = Dword(hive, Lsa, "RunAsPPL");
            // Deliberately conservative: an unrelated system policy can block, never silently override policy.
            policy = HasContent(hive, @"SOFTWARE\Policies\Microsoft\Windows\System") ||
                HasContent(hive, @"SOFTWARE\Policies\Microsoft\Windows\LocalSecurityAuthority") ||
                HasContent(hive, @"SOFTWARE\Microsoft\PolicyManager\current\device\LocalSecurityAuthority");
        }
        catch (Exception ex) { errors.Add("LSA registry/policy: " + ex.Message); }
        try
        {
            Process[] candidates = Process.GetProcessesByName("lsass");
            try
            {
                if (candidates.Length != 1) throw new InvalidDataException("Expected exactly one LSASS process.");
                // Query limited information only. No process-memory, token, debug privilege or bypass access.
                nint handle = OpenProcess(0x1000, false, candidates[0].Id);
                if (handle == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    StringBuilder image = new(32768); uint length = (uint)image.Capacity;
                    if (!QueryFullProcessImageName(handle, 0, image, ref length)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    if (!StringComparer.OrdinalIgnoreCase.Equals(image.ToString(), Path.Combine(Environment.SystemDirectory, "lsass.exe")))
                        throw new InvalidDataException("LSASS image path is not the Windows System32 image.");
                    if (!GetProcessInformation(handle, 7, out uint protection, sizeof(uint))) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    effective = protection;
                }
                finally { CloseHandle(handle); }
            }
            finally { foreach (Process process in candidates) process.Dispose(); }
        }
        catch (Exception ex) { errors.Add("LSASS protection query: " + ex.Message); }
        return new(configured, policy, effective, string.Join("; ", errors));
    }

    public int? ReadLsaProtection()
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        return Dword(hive, Lsa, "RunAsPPL");
    }

    public void EnableLsaProtectionWithoutFirmwareLock()
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = hive.CreateSubKey(Lsa, writable: true);
        key.SetValue("RunAsPPL", 2, RegistryValueKind.DWord); key.Flush();
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder image, ref uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessInformation(nint process, int informationClass, out uint protection, uint size);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);

    public int? ReadMemoryIntegrity()
    {
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        return Dword(hive, Hvci, "Enabled");
    }
    public void WriteMemoryIntegrity(int? value)
    {
        if (value is not (null or 0 or 1)) throw new ArgumentOutOfRangeException(nameof(value));
        using var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        if (value is null)
        {
            // Restore value absence only; never remove a key shared with Windows or other settings.
            using var key = hive.OpenSubKey(Hvci, writable: true);
            key?.DeleteValue("Enabled", throwOnMissingValue: false); key?.Flush();
        }
        else
        {
            using var key = hive.CreateSubKey(Hvci, writable: true);
            key.SetValue("Enabled", value.Value, RegistryValueKind.DWord); key.Flush();
        }
    }
    private static int? Dword(RegistryKey hive, string path, string name)
    {
        using var key = hive.OpenSubKey(path);
        object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return null;
        if (key!.GetValueKind(name) != RegistryValueKind.DWord || value is not int number)
            throw new InvalidDataException("Unexpected registry kind at " + path + " / " + name);
        return number;
    }
    private static bool HasContent(RegistryKey hive, string path)
    {
        using var key = hive.OpenSubKey(path);
        return key is not null && (key.ValueCount > 0 || key.SubKeyCount > 0);
    }
}

// Native SAFEARRAY decoding, without reflection/COM marshalling: compatible with Native AOT.
internal static unsafe class SecurityMitigationWmiArray
{
    internal static int[] Read(nint instance, string name)
    {
        Variant value = default;
        bool locked = false;
        try
        {
            var get = (delegate* unmanaged[Stdcall]<nint, char*, int, Variant*, nint, nint, int>)(*(nint**)instance)[4];
            fixed (char* property = name) Marshal.ThrowExceptionForHR(get(instance, property, 0, &value, 0, 0));
            if (value.Type is not (0x2003 or 0x2013) || value.Pointer == 0 || SafeArrayGetDim(value.Pointer) != 1 || SafeArrayGetElemsize(value.Pointer) != 4)
                throw new InvalidDataException("Expected a one-dimensional WMI 32-bit integer array for " + name);
            Marshal.ThrowExceptionForHR(SafeArrayGetLBound(value.Pointer, 1, out int lower));
            Marshal.ThrowExceptionForHR(SafeArrayGetUBound(value.Pointer, 1, out int upper));
            long length = (long)upper - lower + 1;
            if (length <= 0 || length > 64) throw new InvalidDataException("Missing or excessive DeviceGuard list.");
            Marshal.ThrowExceptionForHR(SafeArrayAccessData(value.Pointer, out nint data)); locked = true;
            int[] result = new int[(int)length]; Marshal.Copy(data, result, 0, result.Length);
            if (result.Any(item => item < 0)) throw new InvalidDataException("Invalid DeviceGuard property identifier.");
            return result;
        }
        finally { if (locked) SafeArrayUnaccessData(value.Pointer); VariantClear(ref value); }
    }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Pointer; }
    [DllImport("oleaut32.dll")] private static extern uint SafeArrayGetDim(nint array);
    [DllImport("oleaut32.dll")] private static extern uint SafeArrayGetElemsize(nint array);
    [DllImport("oleaut32.dll")] private static extern int SafeArrayGetLBound(nint array, uint dimension, out int lower);
    [DllImport("oleaut32.dll")] private static extern int SafeArrayGetUBound(nint array, uint dimension, out int upper);
    [DllImport("oleaut32.dll")] private static extern int SafeArrayAccessData(nint array, out nint data);
    [DllImport("oleaut32.dll")] private static extern int SafeArrayUnaccessData(nint array);
    [DllImport("oleaut32.dll")] private static extern int VariantClear(ref Variant variant);
}

internal sealed class SecurityMitigationFileStore(string path, string control = "MemoryIntegrity") : ISecurityMitigationBackupStore
{
    public SecurityMitigationBackup? Load()
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Security snapshot exceeds its size limit.");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        if (root.GetProperty("Schema").GetInt32() != 1) throw new InvalidDataException("Unsupported security snapshot version.");
        string savedControl = root.TryGetProperty("Control", out var identity) ? identity.GetString() ?? "" : "MemoryIntegrity";
        if (!StringComparer.Ordinal.Equals(savedControl, control)) throw new InvalidDataException("Security snapshot belongs to another protection control.");
        var original = root.GetProperty("OriginalValue");
        int? number = original.ValueKind == JsonValueKind.Null ? null : original.GetInt32();
        ValidateOriginal(number);
        return new(root.GetProperty("MachineIdentity").GetString() ?? "", number,
            root.GetProperty("Outcome").GetString() ?? "Unknown", root.GetProperty("CreatedAt").GetDateTimeOffset());
    }
    public void Save(SecurityMitigationBackup backup)
    {
        ValidateOriginal(backup.OriginalValue);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject(); writer.WriteNumber("Schema", 1);
                    writer.WriteString("Control", control);
                    writer.WriteString("MachineIdentity", backup.MachineIdentity);
                    if (backup.OriginalValue is int value) writer.WriteNumber("OriginalValue", value); else writer.WriteNull("OriginalValue");
                    writer.WriteString("Outcome", backup.Outcome); writer.WriteString("CreatedAt", backup.CreatedAt);
                    writer.WriteString("UpdatedAt", DateTimeOffset.UtcNow); writer.WriteEndObject(); writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { /* A cleanup failure must not rewrite result evidence. */ } }
    }
    private void ValidateOriginal(int? value)
    {
        bool valid = control switch { "MemoryIntegrity" => value is null or 0 or 1, "LsaProtection" => value is null or 0 or 2, _ => false };
        if (!valid) throw new InvalidDataException("Unsupported security control or original value.");
    }
}
