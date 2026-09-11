using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed record MsiDeviceConfiguration(
        string DeviceId,
        string Name,
        string DeviceClass,
        string Driver,
        bool MsiSupported,
        int? MessageNumberLimit,
        int DevicePriority,
        string RegistryPath,
        string Irq,
        int IrqCount,
        int MaxLimit,
        string SupportedModes,
        string Details);

    internal readonly record struct MsiDeviceApplyRequest(
        string DeviceId,
        bool MsiSupported,
        int? MessageNumberLimit,
        int DevicePriority);

    internal readonly record struct MsiDeviceApplyResult(
        bool Success,
        string Message,
        MsiDeviceConfiguration? VerifiedConfiguration);

    internal sealed class MsiModeService
    {
        private const uint CrSuccess = 0;
        private const uint CrBufferSmall = 0x0000001A;
        private const uint DnHasProblem = 0x00000400;
        private const uint ResourceTypeIrq = 4;
        private const uint AllocLogConfig = 2;
        private const uint BootLogConfig = 3;
        private const uint ForcedLogConfig = 4;
        private const uint InfStyleWin4 = 2;
        private const string PciRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
        private const string DriverClassRoot = @"SYSTEM\CurrentControlSet\Control\Class";
        private const string InterruptPath =
            @"Device Parameters\Interrupt Management";
        private const string MsiPath =
            @"Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
        private const string AffinityPath =
            @"Device Parameters\Interrupt Management\Affinity Policy";
        private static readonly IntPtr InvalidHandleValue = new(-1);

        [Flags]
        private enum InterruptType : uint
        {
            Unknown = 0,
            LineBased = 1,
            Msi = 2,
            MsiX = 4
        }

        private enum PciDeviceType : uint
        {
            PciConventional = 0,
            PciX = 1,
            PciExpressEndpoint = 2,
            PciExpressLegacyEndpoint = 3,
            PciExpressRootComplexIntegratedEndpoint = 4,
            PciExpressTreatedAsPci = 5,
            BridgeTypePciConventional = 6,
            BridgeTypePciX = 7,
            BridgeTypePciExpressRootPort = 8,
            BridgeTypePciExpressUpstreamSwitchPort = 9,
            BridgeTypePciExpressDownstreamSwitchPort = 10,
            BridgeTypePciExpressToPciXBridge = 11,
            BridgeTypePciXToExpressBridge = 12,
            BridgeTypePciExpressTreatedAsPci = 13
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DevPropKey
        {
            public Guid FormatId;
            public uint PropertyId;

            public DevPropKey(string formatId, uint propertyId)
            {
                FormatId = new Guid(formatId);
                PropertyId = propertyId;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InfContext
        {
            public IntPtr Inf;
            public IntPtr CurrentInf;
            public uint Section;
            public uint Line;
        }

        private static readonly DevPropKey DeviceName =
            new("B725F130-47EF-101A-A5F1-02608C9EEBAC", 10);
        private static readonly DevPropKey DeviceManufacturer =
            new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 13);
        private static readonly DevPropKey DeviceLocation =
            new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 15);
        private static readonly DevPropKey DevicePdoName =
            new("A45C254E-DF1C-4EFD-8020-67D146A850E0", 16);
        private static readonly DevPropKey DeviceInstanceId =
            new("78C34FC8-104A-4ACA-9EA4-524D52996E57", 256);
        private static readonly DevPropKey DeviceDriverVersion =
            new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 3);
        private static readonly DevPropKey DeviceDriverInfPath =
            new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 5);
        private static readonly DevPropKey DeviceDriverInfSection =
            new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 6);
        private static readonly DevPropKey DeviceDriverInfSectionExtension =
            new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 7);
        private static readonly DevPropKey DeviceDriverProvider =
            new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 9);
        private static readonly DevPropKey DeviceDriverRank =
            new("A8B865DD-2E3D-4094-AD97-E593A70C75D6", 14);
        private static readonly DevPropKey PciDeviceDeviceType =
            new("3AB22E31-8264-4B4E-9AF5-A8D2D8E33E62", 1);
        private static readonly DevPropKey PciDeviceMaxLinkSpeed =
            new("3AB22E31-8264-4B4E-9AF5-A8D2D8E33E62", 11);
        private static readonly DevPropKey PciDeviceMaxLinkWidth =
            new("3AB22E31-8264-4B4E-9AF5-A8D2D8E33E62", 12);
        private static readonly DevPropKey PciDeviceInterruptSupport =
            new("3AB22E31-8264-4B4E-9AF5-A8D2D8E33E62", 14);
        private static readonly DevPropKey PciDeviceInterruptMessageMaximum =
            new("3AB22E31-8264-4B4E-9AF5-A8D2D8E33E62", 15);
        private static readonly Regex MsiInfPattern = new(
            @"HKR\s*,\s*""?Interrupt Management\\MessageSignaledInterruptProperties""?\s*,\s*MSISupported\s*,\s*[^,]+\s*,\s*(?<v>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex LimitInfPattern = new(
            @"HKR\s*,\s*""?Interrupt Management\\MessageSignaledInterruptProperties""?\s*,\s*MessageNumberLimit\s*,\s*[^,]+\s*,\s*(?<v>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex PriorityInfPattern = new(
            @"HKR\s*,\s*""?Interrupt Management\\Affinity Policy""?\s*,\s*DevicePriority\s*,\s*[^,]+\s*,\s*(?<v>\d+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public Task<IReadOnlyList<MsiDeviceConfiguration>> ReadDevicesAsync()
        {
            return Task.Run<IReadOnlyList<MsiDeviceConfiguration>>(() => ReadDevices());
        }

        public Task<string> ReadDetailsAsync(MsiDeviceConfiguration original) => Task.Run(() =>
        {
            MsiDeviceConfiguration? fresh = ReadDevices(original.DeviceId).FirstOrDefault();
            string details = fresh?.Details ?? original.Details;
            StringBuilder report = new(details);
            report.AppendLine();
            report.AppendLine(fresh is null ? "Runtime refresh unavailable; using the last Refresh snapshot." : $"Runtime refreshed: {DateTimeOffset.Now:O}");
            if (fresh is not null && fresh.IrqCount == 0 && original.IrqCount > 0)
                report.AppendLine($"Live IRQ list is empty; last Refresh snapshot: {original.Irq} ({original.IrqCount} resources).");
            report.AppendLine(ReadNdisDetails(fresh ?? original));
            return report.ToString().TrimEnd();
        });

        private static string ReadNdisDetails(MsiDeviceConfiguration device)
        {
            string[] fields = { "Name", "InterfaceDescription", "LocationInformationString", "PNPDeviceID",
                "BusNumber", "DeviceNumber", "FunctionNumber", "MaxInterruptMessages", "NumMsixTableEntries",
                "MsiSupported", "MsiXSupported", "MsiXInterruptSupported" };
            string unavailable = "NDIS hardware max: <not reported>\nNDIS MSI-X entries: <not reported>\nNDIS match source: <not matched>";
            if (!device.DeviceClass.Equals("Net", StringComparison.OrdinalIgnoreCase)) return unavailable + " (not a network adapter)";
            try
            {
                string location = "";
                if (TryGetLiveDevice(device.DeviceId, out uint instance))
                    location = ReadDeviceStringProperty(instance, DeviceLocation);
                var rows = NativeHardwareData.Query(@"ROOT\StandardCimv2", "MSFT_NetAdapterHardwareInfoSettingData", fields);
                MsiNdisMatch identity = MsiNdisIdentity.Match(rows, device.DeviceId, location, device.Name);
                if (identity.Row is null) return unavailable + " (" + identity.Source + ")";
                var match = identity.Row;
                string Value(string key) => string.IsNullOrWhiteSpace(match[key]) ? "<not reported>" : match[key];
                return $"NDIS hardware max: {Value("MaxInterruptMessages")}\nNDIS MSI-X entries: {Value("NumMsixTableEntries")}\n" +
                    $"NDIS match source: {identity.Source}\nNDIS location: {Value("LocationInformationString")}\n" +
                    $"NDIS PCI B/D/F: {Value("BusNumber")}/{Value("DeviceNumber")}/{Value("FunctionNumber")}\n" +
                    $"NDIS MSI support: {Value("MsiSupported")}\nNDIS MSI-X support: {Value("MsiXSupported")}\nNDIS MSI-X interrupt support: {Value("MsiXInterruptSupported")}";
            }
            catch (Exception exception) { return unavailable + "\nNDIS query unavailable: " + exception.Message; }
        }

        public Task<MsiDeviceApplyResult> ApplyAsync(MsiDeviceApplyRequest request)
        {
            return Task.Run(() => Apply(request));
        }

        public static string PriorityToString(int value) => value switch
        {
            0 => "Undefined",
            1 => "Low",
            2 => "Normal",
            3 => "High",
            _ => $"Unknown {value.ToString(CultureInfo.InvariantCulture)}"
        };

        public static SecuritySettingsLaunchResult OpenRegistry(
            MsiDeviceConfiguration device)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit",
                    writable: true);
                key.SetValue("Lastkey", device.RegistryPath, RegistryValueKind.String);
                Process.Start(new ProcessStartInfo
                {
                    FileName = "regedit.exe",
                    UseShellExecute = true
                });
                return new SecuritySettingsLaunchResult(
                    true,
                    $"Opened Registry Editor at {device.RegistryPath}.");
            }
            catch (Exception exception)
            {
                return new SecuritySettingsLaunchResult(
                    false,
                    $"Unable to open Registry Editor. {exception.Message}");
            }
        }

        private static IReadOnlyList<MsiDeviceConfiguration> ReadDevices(string? onlyDeviceId = null)
        {
            List<MsiDeviceConfiguration> devices = new();
            List<string> enumerationErrors = new();
            using RegistryKey? pci = OpenLocalMachineKey(PciRoot, writable: false);
            if (pci is null)
            {
                return devices;
            }

            foreach (string hardwareId in pci.GetSubKeyNames())
            {
                using RegistryKey? hardware = pci.OpenSubKey(hardwareId, writable: false);
                if (hardware is null)
                {
                    continue;
                }

                foreach (string instance in hardware.GetSubKeyNames())
                {
                    string relativeDevicePath = $@"{PciRoot}\{hardwareId}\{instance}";
                    string deviceId = $@"PCI\{hardwareId}\{instance}";
                    if (onlyDeviceId is not null && !string.Equals(onlyDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        if (!TryGetLiveDevice(deviceId, out uint deviceInstance))
                        {
                            continue;
                        }

                        using RegistryKey? device = OpenLocalMachineKey(
                            relativeDevicePath,
                            writable: false);
                        using RegistryKey? interrupt = device?.OpenSubKey(
                            InterruptPath,
                            writable: false);
                        if (device is null || interrupt is null)
                        {
                            continue;
                        }

                        using RegistryKey? msi = device.OpenSubKey(
                            MsiPath,
                            writable: false);
                        using RegistryKey? affinity = device.OpenSubKey(
                            AffinityPath,
                            writable: false);

                        string name = ReadDisplayName(device, instance);
                        string deviceClass = Convert.ToString(
                            device.GetValue("Class"),
                            CultureInfo.InvariantCulture) ?? "Unknown";
                        string driver = Convert.ToString(
                            device.GetValue("Driver"),
                            CultureInfo.InvariantCulture) ?? string.Empty;
                        using RegistryKey? driverKey = string.IsNullOrWhiteSpace(driver)
                            ? null
                            : OpenLocalMachineKey($@"{DriverClassRoot}\{driver}", writable: false);
                        bool msiSupported = ConvertToNullableInt(
                            msi?.GetValue("MSISupported")) == 1;
                        int? limit = ConvertToNullableInt(
                            msi?.GetValue("MessageNumberLimit"));
                        int priority = ConvertToNullableInt(
                            affinity?.GetValue("DevicePriority")) ?? 0;
                        if (priority is < 0 or > 3)
                        {
                            priority = 0;
                        }

                        string resolvedName = ReadDeviceStringProperty(deviceInstance, DeviceName);
                        if (!string.IsNullOrWhiteSpace(resolvedName))
                        {
                            name = resolvedName;
                        }
                        string infFile = FirstNotEmpty(
                            ReadDeviceStringProperty(deviceInstance, DeviceDriverInfPath),
                            ReadString(driverKey, "InfPath"));
                        string infSection = FirstNotEmpty(
                            ReadDeviceStringProperty(deviceInstance, DeviceDriverInfSection),
                            ReadString(driverKey, "InfSection"));
                        string infSectionExtension = FirstNotEmpty(
                            ReadDeviceStringProperty(deviceInstance, DeviceDriverInfSectionExtension),
                            ReadString(driverKey, "InfSectionExt"));
                        if (!string.IsNullOrWhiteSpace(infSectionExtension) &&
                            !infSection.EndsWith(infSectionExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            infSection += infSectionExtension;
                        }

                        uint? deviceType = ReadDeviceUInt32Property(deviceInstance, PciDeviceDeviceType);
                        uint? maxLinkSpeed = ReadDeviceUInt32Property(deviceInstance, PciDeviceMaxLinkSpeed);
                        uint? maxLinkWidth = ReadDeviceUInt32Property(deviceInstance, PciDeviceMaxLinkWidth);
                        uint? interruptSupport = ReadDeviceUInt32Property(deviceInstance, PciDeviceInterruptSupport);
                        uint? maximum = ReadDeviceUInt32Property(deviceInstance, PciDeviceInterruptMessageMaximum);
                        string supportedModes = interruptSupport.HasValue
                            ? FormatInterruptTypes(interruptSupport.Value)
                            : InterruptType.Unknown.ToString();
                        int maxLimit = maximum.HasValue
                            ? unchecked((int)maximum.Value)
                            : 0;
                        IReadOnlyList<int> irqValues = ReadDeviceIrqs(deviceInstance);
                        string irq = FormatIrqs(irqValues);
                        IReadOnlyDictionary<string, string> infValues =
                            ReadInfMsiProperties(infFile, infSection);
                        string details = BuildDetails(
                            deviceInstance,
                            deviceId,
                            name,
                            driverKey,
                            infFile,
                            infSection,
                            deviceType,
                            maxLinkSpeed,
                            maxLinkWidth,
                            supportedModes,
                            maxLimit,
                            irqValues.Count,
                            infValues);

                        devices.Add(new MsiDeviceConfiguration(
                            deviceId,
                            name,
                            deviceClass,
                            driver,
                            msiSupported,
                            limit,
                            priority,
                            $@"HKEY_LOCAL_MACHINE\{relativeDevicePath}",
                            irq,
                            irqValues.Count,
                            maxLimit,
                            supportedModes,
                            details));
                    }
                    catch (Exception exception)
                    {
                        // Keep enumerating other devices, but do not silently turn a
                        // systemic parser/interoperability failure into an empty table.
                        if (enumerationErrors.Count < 8)
                        {
                            enumerationErrors.Add(
                                $"{deviceId}: {exception.GetType().Name}: {exception.Message}");
                        }
                    }
                }
            }

            if (devices.Count == 0 && enumerationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "PCI devices were found, but their interrupt data could not be parsed. " +
                    string.Join(" | ", enumerationErrors));
            }

            return devices
                .OrderBy(item => item.DeviceClass, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static MsiDeviceApplyResult Apply(MsiDeviceApplyRequest request)
        {
            if (!WindowsPrivilegeService.IsAdministrator())
            {
                return new MsiDeviceApplyResult(
                    false,
                    "Administrator rights are required.",
                    null);
            }
            if (request.MessageNumberLimit is < 1 or > 2048)
            {
                return new MsiDeviceApplyResult(
                    false,
                    "MessageNumberLimit must be empty or between 1 and 2048.",
                    null);
            }
            if (request.DevicePriority is < 0 or > 3)
            {
                return new MsiDeviceApplyResult(
                    false,
                    "DevicePriority must be Undefined, Low, Normal, or High.",
                    null);
            }
            if (!request.DeviceId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase) ||
                !TryGetLiveDevice(request.DeviceId, out _))
            {
                return new MsiDeviceApplyResult(
                    false,
                    "The PCI device is no longer active. Refresh the list before applying changes.",
                    null);
            }

            string relativeDevicePath = $@"{PciRoot}\{request.DeviceId[4..]}";
            try
            {
                using RegistryKey? device = OpenLocalMachineKey(
                    relativeDevicePath,
                    writable: true);
                if (device is null)
                {
                    return new MsiDeviceApplyResult(
                        false,
                        "The device registry key could not be opened for writing.",
                        null);
                }

                using RegistryKey interrupt = device.CreateSubKey(
                    InterruptPath,
                    writable: true);
                using RegistryKey msi = interrupt.CreateSubKey(
                    "MessageSignaledInterruptProperties",
                    writable: true);
                using RegistryKey affinity = interrupt.CreateSubKey(
                    "Affinity Policy",
                    writable: true);

                msi.SetValue(
                    "MSISupported",
                    request.MsiSupported ? 1 : 0,
                    RegistryValueKind.DWord);
                if (request.MessageNumberLimit.HasValue)
                {
                    msi.SetValue(
                        "MessageNumberLimit",
                        request.MessageNumberLimit.Value,
                        RegistryValueKind.DWord);
                }
                else
                {
                    msi.DeleteValue(
                        "MessageNumberLimit",
                        throwOnMissingValue: false);
                }

                if (request.DevicePriority == 0)
                {
                    affinity.DeleteValue(
                        "DevicePriority",
                        throwOnMissingValue: false);
                }
                else
                {
                    affinity.SetValue(
                        "DevicePriority",
                        request.DevicePriority,
                        RegistryValueKind.DWord);
                }

                MsiDeviceConfiguration? verified = ReadDevices()
                    .FirstOrDefault(item => string.Equals(
                        item.DeviceId,
                        request.DeviceId,
                        StringComparison.OrdinalIgnoreCase));
                bool success = verified is not null &&
                               verified.MsiSupported == request.MsiSupported &&
                               verified.MessageNumberLimit == request.MessageNumberLimit &&
                               verified.DevicePriority == request.DevicePriority;
                return new MsiDeviceApplyResult(
                    success,
                    success
                        ? "Registry values were written and verified. Restart Windows before evaluating device behavior."
                        : "The device configuration did not match after writing.",
                    verified);
            }
            catch (Exception exception)
            {
                return new MsiDeviceApplyResult(
                    false,
                    exception.Message,
                    null);
            }
        }

        private static bool TryGetLiveDevice(
            string deviceId,
            out uint deviceInstance)
        {
            uint locate = CM_Locate_DevNodeW(
                out deviceInstance,
                deviceId,
                0);
            if (locate != CrSuccess)
            {
                return false;
            }

            uint statusResult = CM_Get_DevNode_Status(
                out uint status,
                out _,
                deviceInstance,
                0);
            return statusResult == CrSuccess &&
                   (status & DnHasProblem) == 0;
        }

        private static string BuildDetails(
            uint deviceInstance,
            string deviceId,
            string name,
            RegistryKey? driverKey,
            string infFile,
            string infSection,
            uint? deviceType,
            uint? maxLinkSpeed,
            uint? maxLinkWidth,
            string supportedModes,
            int maxLimit,
            int irqCount,
            IReadOnlyDictionary<string, string> infValues)
        {
            StringBuilder builder = new();
            builder.AppendLine("DEVICE PNP PROPERTIES");
            AppendDetail(builder, "Display name", name);
            AppendDetail(
                builder,
                "Manufacturer",
                FirstNotEmpty(
                    ReadDeviceStringProperty(deviceInstance, DeviceManufacturer),
                    ReadString(driverKey, "ProviderName")));
            AppendDetail(builder, "Location", ReadDeviceStringProperty(deviceInstance, DeviceLocation));
            AppendDetail(
                builder,
                "Instance ID",
                FirstNotEmpty(
                    ReadDeviceStringProperty(deviceInstance, DeviceInstanceId),
                    deviceId));
            AppendDetail(builder, "PDO name", ReadDeviceStringProperty(deviceInstance, DevicePdoName));
            AppendDetail(
                builder,
                "Driver version",
                FirstNotEmpty(
                    ReadDeviceStringProperty(deviceInstance, DeviceDriverVersion),
                    ReadString(driverKey, "DriverVersion")));
            string infDescription = string.IsNullOrWhiteSpace(infSection)
                ? infFile
                : $"{infFile} (section [{infSection}])";
            AppendDetail(builder, "Driver inf-file", infDescription);
            AppendDetail(
                builder,
                "Driver provider",
                FirstNotEmpty(
                    ReadDeviceStringProperty(deviceInstance, DeviceDriverProvider),
                    ReadString(driverKey, "ProviderName")));
            uint? rank = ReadDeviceUInt32Property(deviceInstance, DeviceDriverRank);
            if (rank.HasValue)
            {
                AppendDetail(builder, "Driver rank", rank.Value.ToString(CultureInfo.InvariantCulture));
            }

            builder.AppendLine();
            builder.AppendLine("DEVICE PCI PROPERTIES");
            AppendDetail(
                builder,
                "PCI device type",
                deviceType.HasValue
                    ? ((PciDeviceType)deviceType.Value).ToString()
                    : string.Empty);
            AppendDetail(
                builder,
                "PCI-E max speed",
                maxLinkSpeed.HasValue
                    ? $"v.{maxLinkSpeed.Value.ToString(CultureInfo.InvariantCulture)}"
                    : string.Empty);
            AppendDetail(
                builder,
                "PCI-E max lanes",
                maxLinkWidth?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            IReadOnlyList<int> currentIrqs = ReadDeviceIrqs(deviceInstance, out string interruptSource);
            AppendDetail(builder, "Current IRQ / MSI", currentIrqs.Count > 0 ? FormatIrqs(currentIrqs) : "<not reported>");
            AppendDetail(builder, "Interrupt source", interruptSource);
            AppendDetail(builder, "Interrupt detail", currentIrqs.Count > 0 ? "Signed IRQ allocation values; negative values indicate message-signaled resources." : "Resource list unavailable or empty.");
            AppendDetail(builder, "Runtime message resources", currentIrqs.Count > 0 ? currentIrqs.Count(value => value < 0).ToString(CultureInfo.InvariantCulture) : "<not reported>");
            AppendDetail(builder, "Hardware max limit", maxLimit > 0 ? maxLimit.ToString(CultureInfo.InvariantCulture) : "<not reported>");
            AppendDetail(builder, "Interrupt modes", supportedModes);
            AppendDetail(
                builder,
                "Max MSI limit",
                maxLimit > 0 ? maxLimit.ToString(CultureInfo.InvariantCulture) : string.Empty);

            builder.AppendLine();
            AppendDetail(builder, "Actual number of IRQs", currentIrqs.Count > 0 ? currentIrqs.Count.ToString(CultureInfo.InvariantCulture) : "<not reported>");
            builder.AppendLine();
            if (infValues.TryGetValue("failed to open inf-file", out string? error))
            {
                builder.AppendLine("MSI VALUES SPECIFIED IN THE INF-FILE");
                AppendDetail(builder, "failed to open inf-file", error);
            }
            else if (infValues.Count > 0)
            {
                builder.AppendLine("MSI VALUES SPECIFIED IN THE INF-FILE");
                if (infValues.TryGetValue("msi", out string? msi))
                {
                    AppendDetail(builder, "msi", msi);
                }
                if (infValues.TryGetValue("limit", out string? limit))
                {
                    AppendDetail(builder, "limit", limit);
                }
                if (infValues.TryGetValue("priority", out string? priority))
                {
                    AppendDetail(builder, "priority", priority);
                }
            }
            else
            {
                builder.AppendLine("NO MSI VALUES SPECIFIED IN THE INF-FILE");
            }
            return builder.ToString().TrimEnd();
        }

        private static void AppendDetail(
            StringBuilder builder,
            string name,
            string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.Append('\t')
                    .Append(name.PadRight(24, ' '))
                    .Append(": ")
                    .AppendLine(value);
            }
        }

        private static string ReadDeviceStringProperty(
            uint deviceInstance,
            DevPropKey propertyKey)
        {
            uint size = 0;
            DevPropKey key = propertyKey;
            uint result = CM_Get_DevNode_PropertyW(
                deviceInstance,
                ref key,
                out _,
                null,
                ref size,
                0);
            if (result != CrBufferSmall || size < 2)
            {
                return string.Empty;
            }
            byte[] bytes = new byte[size];
            result = CM_Get_DevNode_PropertyW(
                deviceInstance,
                ref key,
                out _,
                bytes,
                ref size,
                0);
            if (result != CrSuccess)
            {
                return string.Empty;
            }
            return Encoding.Unicode.GetString(bytes, 0, checked((int)size)).TrimEnd('\0').Trim();
        }

        private static uint? ReadDeviceUInt32Property(
            uint deviceInstance,
            DevPropKey propertyKey)
        {
            byte[] bytes = new byte[4];
            uint size = 4;
            DevPropKey key = propertyKey;
            uint result = CM_Get_DevNode_PropertyW(
                deviceInstance,
                ref key,
                out _,
                bytes,
                ref size,
                0);
            return result == CrSuccess && size >= 4
                ? BitConverter.ToUInt32(bytes, 0)
                : null;
        }

        private static IReadOnlyList<int> ReadDeviceIrqs(uint deviceInstance) => ReadDeviceIrqs(deviceInstance, out _);

        private static IReadOnlyList<int> ReadDeviceIrqs(uint deviceInstance, out string source)
        {
            source = "Configuration Manager: allocated resources";
            List<int> values = new(128);
            IntPtr log = IntPtr.Zero;
            uint result = CM_Get_First_Log_Conf(out log, deviceInstance, AllocLogConfig);
            if (result != CrSuccess || log == IntPtr.Zero)
            {
                source = "Configuration Manager: forced resources (allocated configuration unavailable)";
                result = CM_Get_First_Log_Conf(out log, deviceInstance, ForcedLogConfig);
                if (result != CrSuccess || log == IntPtr.Zero)
                {
                    source = "Configuration Manager: boot resources (allocated/forced configuration unavailable)";
                    result = CM_Get_First_Log_Conf(out log, deviceInstance, BootLogConfig);
                }
            }
            if (result != CrSuccess || log == IntPtr.Zero)
            {
                source = $"Configuration Manager unavailable (CR 0x{result:X8})";
                return values;
            }

            IntPtr cursor = log;
            bool cursorIsLog = true;
            try
            {
                while (true)
                {
                    IntPtr descriptor = IntPtr.Zero;
                    result = CM_Get_Next_Res_Des(
                        out descriptor,
                        cursor,
                        ResourceTypeIrq,
                        IntPtr.Zero,
                        0);
                    if (!cursorIsLog && cursor != IntPtr.Zero)
                    {
                        CM_Free_Res_Des_Handle(cursor);
                        cursor = IntPtr.Zero;
                    }
                    if (result != CrSuccess || descriptor == IntPtr.Zero)
                    {
                        break;
                    }

                    if (CM_Get_Res_Des_Data_Size(out uint size, descriptor, 0) == CrSuccess &&
                        size >= 16)
                    {
                        IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
                        try
                        {
                            if (CM_Get_Res_Des_Data(descriptor, buffer, size, 0) == CrSuccess)
                            {
                                values.Add(Marshal.ReadInt32(buffer, 12));
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                    cursor = descriptor;
                    cursorIsLog = false;
                }
            }
            finally
            {
                if (!cursorIsLog && cursor != IntPtr.Zero)
                {
                    CM_Free_Res_Des_Handle(cursor);
                }
                CM_Free_Log_Conf_Handle(log);
            }
            return values;
        }

        private static string FormatIrqs(IReadOnlyList<int> values)
        {
            if (values.Count == 0)
            {
                return string.Empty;
            }
            if (values.Count == 1)
            {
                return values[0].ToString(CultureInfo.InvariantCulture);
            }

            StringBuilder builder = new();
            int index = 0;
            while (index < values.Count)
            {
                int start = index;
                int step = 0;
                if (index + 1 < values.Count)
                {
                    int delta = values[index + 1] - values[index];
                    if (delta is 1 or -1)
                    {
                        step = delta;
                    }
                }
                int end = index;
                if (step != 0)
                {
                    while (end + 1 < values.Count &&
                           values[end + 1] - values[end] == step)
                    {
                        end++;
                    }
                }
                if (builder.Length > 0)
                {
                    builder.Append(',');
                }
                builder.Append(values[start].ToString(CultureInfo.InvariantCulture));
                int length = end - start + 1;
                if (length >= 3)
                {
                    builder.Append('.', length - 2);
                    builder.Append(values[end].ToString(CultureInfo.InvariantCulture));
                }
                else if (length == 2)
                {
                    builder.Append(',');
                    builder.Append(values[end].ToString(CultureInfo.InvariantCulture));
                }
                index = end + 1;
            }
            return builder.ToString();
        }

        private static string FormatInterruptTypes(uint value)
        {
            InterruptType types = (InterruptType)value;
            return types == InterruptType.Unknown
                ? InterruptType.Unknown.ToString()
                : string.Join(", ", types.ToString()
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        private static IReadOnlyDictionary<string, string> ReadInfMsiProperties(
            string infFile,
            string section)
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(infFile) || string.IsNullOrWhiteSpace(section))
            {
                return values;
            }
            string path = Path.IsPathRooted(infFile)
                ? infFile
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", infFile);
            IntPtr inf = SetupOpenInfFileW(path, null, InfStyleWin4, IntPtr.Zero);
            if (inf == InvalidHandleValue)
            {
                values["failed to open inf-file"] =
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()).Message;
                return values;
            }
            try
            {
                ParseInfSection(inf, section + ".HW", values);
                if (values.ContainsKey("msi") ||
                    values.ContainsKey("limit") ||
                    values.ContainsKey("priority"))
                {
                    return values;
                }
                values.Clear();
                ParseInfSection(inf, section, values);
                return values;
            }
            finally
            {
                SetupCloseInfFile(inf);
            }
        }

        private static void ParseInfSection(
            IntPtr inf,
            string section,
            IDictionary<string, string> values)
        {
            List<string> addRegSections = new();
            if (SetupFindFirstLineW(inf, section, "AddReg", out InfContext context))
            {
                while (true)
                {
                    string line = GetInfLineText(ref context);
                    foreach (string item in line.Split(','))
                    {
                        string value = item.Trim().Trim('"');
                        if (!string.IsNullOrWhiteSpace(value) &&
                            !value.Equals("AddReg", StringComparison.OrdinalIgnoreCase))
                        {
                            addRegSections.Add(value);
                        }
                    }
                    if (!SetupFindNextMatchLineW(ref context, "AddReg", out InfContext next))
                    {
                        break;
                    }
                    context = next;
                }
            }

            foreach (string addRegSection in addRegSections.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!SetupFindFirstLineW(inf, addRegSection, null, out InfContext lineContext))
                {
                    continue;
                }
                while (true)
                {
                    ParseInfRegistryLine(GetInfLineText(ref lineContext), values);
                    if (!SetupFindNextLine(ref lineContext, out InfContext next))
                    {
                        break;
                    }
                    lineContext = next;
                }
            }
        }

        private static string GetInfLineText(ref InfContext context)
        {
            StringBuilder buffer = new(4096);
            return SetupGetLineTextW(
                ref context,
                IntPtr.Zero,
                null,
                null,
                buffer,
                checked((uint)buffer.Capacity),
                out _)
                ? buffer.ToString()
                : string.Empty;
        }

        private static void ParseInfRegistryLine(
            string line,
            IDictionary<string, string> values)
        {
            Match match = MsiInfPattern.Match(line);
            if (match.Success)
            {
                values["msi"] = match.Groups["v"].Value switch
                {
                    "0" => bool.FalseString.ToLowerInvariant(),
                    "1" => bool.TrueString.ToLowerInvariant(),
                    string value => value
                };
            }
            match = LimitInfPattern.Match(line);
            if (match.Success)
            {
                values["limit"] = match.Groups["v"].Value;
            }
            match = PriorityInfPattern.Match(line);
            if (match.Success)
            {
                values["priority"] = int.TryParse(
                    match.Groups["v"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int priority)
                    ? PriorityToString(priority)
                    : match.Groups["v"].Value;
            }
        }

        private static string ReadString(RegistryKey? key, string valueName)
        {
            return Convert.ToString(
                key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        private static string FirstNotEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string ReadDisplayName(
            RegistryKey device,
            string fallback)
        {
            string value = Convert.ToString(
                device.GetValue("FriendlyName") ?? device.GetValue("DeviceDesc"),
                CultureInfo.InvariantCulture) ?? fallback;
            int separator = value.LastIndexOf(';');
            if (separator >= 0 && separator + 1 < value.Length)
            {
                value = value[(separator + 1)..];
            }
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int? ConvertToNullableInt(object? value)
        {
            if (value is null)
            {
                return null;
            }
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static RegistryKey? OpenLocalMachineKey(
            string path,
            bool writable)
        {
            RegistryView view = Environment.Is64BitOperatingSystem
                ? RegistryView.Registry64
                : RegistryView.Default;
            RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                view);
            RegistryKey? key = baseKey.OpenSubKey(path, writable);
            baseKey.Dispose();
            return key;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Locate_DevNodeW(
            out uint pdnDevInst,
            string pDeviceId,
            uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_DevNode_Status(
            out uint pulStatus,
            out uint pulProblemNumber,
            uint dnDevInst,
            uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern uint CM_Get_DevNode_PropertyW(
            uint deviceInstance,
            ref DevPropKey propertyKey,
            out uint propertyType,
            [Out] byte[]? buffer,
            ref uint bufferSize,
            uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_First_Log_Conf(
            out IntPtr logConfiguration,
            uint deviceInstance,
            uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Next_Res_Des(
            out IntPtr resourceDescriptor,
            IntPtr currentDescriptor,
            uint resourceType,
            IntPtr resourceId,
            uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Res_Des_Data_Size(
            out uint size,
            IntPtr resourceDescriptor,
            uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Get_Res_Des_Data(
            IntPtr resourceDescriptor,
            IntPtr buffer,
            uint bufferLength,
            uint flags);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Free_Res_Des_Handle(IntPtr resourceDescriptor);

        [DllImport("cfgmgr32.dll")]
        private static extern uint CM_Free_Log_Conf_Handle(IntPtr logConfiguration);

        [DllImport("setupapi.dll", EntryPoint = "SetupOpenInfFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupOpenInfFileW(
            string fileName,
            string? infClass,
            uint infStyle,
            IntPtr errorLine);

        [DllImport("setupapi.dll", EntryPoint = "SetupFindFirstLineW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupFindFirstLineW(
            IntPtr infHandle,
            string section,
            string? key,
            out InfContext context);

        [DllImport("setupapi.dll", EntryPoint = "SetupFindNextMatchLineW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupFindNextMatchLineW(
            ref InfContext contextIn,
            string keyToMatch,
            out InfContext contextOut);

        // SetupFindNextLine has no ANSI/Unicode suffix because neither argument
        // contains a string. Importing SetupFindNextLineW fails at runtime.
        [DllImport("setupapi.dll", EntryPoint = "SetupFindNextLine", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupFindNextLine(
            ref InfContext contextIn,
            out InfContext contextOut);

        [DllImport("setupapi.dll", EntryPoint = "SetupGetLineTextW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupGetLineTextW(
            ref InfContext context,
            IntPtr infHandle,
            string? section,
            string? key,
            StringBuilder returnBuffer,
            uint returnBufferSize,
            out uint requiredSize);

        [DllImport("setupapi.dll")]
        private static extern void SetupCloseInfFile(IntPtr infHandle);
    }
}
