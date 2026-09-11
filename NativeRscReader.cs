using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys
{
    /// <summary>
    /// Read-only local WMI query with explicit COM ABI calls (no reflection,
    /// PowerShell host, WMIC dependency, or runtime COM marshalling for AOT).
    /// Slots/signatures follow the Windows SDK WbemCli.h interfaces.
    /// </summary>
    internal static unsafe class NativeRscReader
    {
        public static IReadOnlyList<ProfileRscAdapter> Read()
        {
            List<ProfileRscAdapter> rows = new();
            Visit((_, instance) => rows.Add(new(ReadString(instance, "Name"),
                ReadBoolean(instance, "IPv4Enabled"), ReadBoolean(instance, "IPv6Enabled"))));
            return rows;
        }

        internal static void Visit(Action<nint, nint> visitor,
            string namespacePath = @"ROOT\StandardCimv2",
            string queryText = "SELECT * FROM MSFT_NetAdapterRscSettingData")
        {
            int initialized = CoInitializeEx(0, 0);
            if (initialized < 0 && initialized != unchecked((int)0x80010106))
                Marshal.ThrowExceptionForHR(initialized);
            nint locator = 0, services = 0, enumerator = 0;
            nint ns = 0, language = 0, query = 0;
            try
            {
                Guid clsid = new("4590F811-1D3A-11D0-891F-00AA004B2E24");
                Guid iid = new("DC12A687-737F-11CF-884D-00AA004B2E24");
                Marshal.ThrowExceptionForHR(CoCreateInstance(in clsid, 0, 1, in iid, out locator));
                ns = Marshal.StringToBSTR(namespacePath);
                var connect = (delegate* unmanaged[Stdcall]<nint, nint, nint, nint, nint, int, nint, nint, nint*, int>)(*(nint**)locator)[3];
                // WBEM_FLAG_CONNECT_USE_MAX_WAIT bounds the connection attempt.
                Marshal.ThrowExceptionForHR(connect(locator, ns, 0, 0, 0, 0x80, 0, 0, &services));
                Marshal.ThrowExceptionForHR(CoSetProxyBlanket(services, 10, 0, 0, 6, 3, 0, 0));
                language = Marshal.StringToBSTR("WQL");
                query = Marshal.StringToBSTR(queryText);
                var exec = (delegate* unmanaged[Stdcall]<nint, nint, nint, int, nint, nint*, int>)(*(nint**)services)[20];
                Marshal.ThrowExceptionForHR(exec(services, language, query, 0x30, 0, &enumerator));
                int count = 0;
                var next = (delegate* unmanaged[Stdcall]<nint, int, uint, nint*, uint*, int>)(*(nint**)enumerator)[4];
                while (true)
                {
                    nint instance = 0;
                    uint returned = 0;
                    int hr = next(enumerator, 5000, 1, &instance, &returned);
                    try
                    {
                        Marshal.ThrowExceptionForHR(hr);
                        if (returned == 0)
                        {
                            if (hr == 1) break; // WBEM_S_FALSE: enumeration complete.
                            throw new TimeoutException("WMI provider enumeration did not finish.");
                        }
                        if (++count > 4096) throw new InvalidOperationException("WMI provider returned too many objects.");
                        visitor(services, instance);
                    }
                    finally { Release(instance); }
                }
            }
            finally
            {
                Release(enumerator);
                Release(services);
                Release(locator);
                if (query != 0) Marshal.FreeBSTR(query);
                if (language != 0) Marshal.FreeBSTR(language);
                if (ns != 0) Marshal.FreeBSTR(ns);
                if (initialized >= 0) CoUninitialize();
            }
        }

        internal static string ReadString(nint instance, string name)
        {
            Variant value = default;
            try
            {
                Get(instance, name, &value);
                if (value.Type != 8) throw new InvalidOperationException($"RSC {name} is not a string.");
                return Marshal.PtrToStringBSTR(value.Pointer);
            }
            finally { VariantClear(ref value); }
        }

        internal static bool ReadBoolean(nint instance, string name)
        {
            Variant value = default;
            try
            {
                Get(instance, name, &value);
                if (value.Type != 11) throw new InvalidOperationException($"RSC {name} is not a Boolean.");
                return value.Boolean != 0;
            }
            finally { VariantClear(ref value); }
        }

        internal static string ReadValue(nint instance, string name)
        {
            Variant value = default;
            try
            {
                Get(instance, name, &value);
                return value.Type switch
                {
                    0 or 1 => string.Empty,
                    8 => Marshal.PtrToStringBSTR(value.Pointer),
                    2 => value.Boolean.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    3 => value.Signed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    17 => unchecked((byte)value.Unsigned).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    19 => value.Unsigned.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    18 => unchecked((ushort)value.Boolean).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    11 => value.Boolean != 0 ? "True" : "False",
                    _ => throw new InvalidOperationException($"Unsupported WMI type for {name}: {value.Type}")
                };
            }
            finally { VariantClear(ref value); }
        }

        // Only read-only BitLocker getter methods are accepted here.
        internal static Dictionary<string, string> ReadVolumeMethod(nint services, nint instance,
            string method, params string[] properties)
        {
            if (method is not ("GetConversionStatus" or "GetProtectionStatus" or "GetLockStatus"))
                throw new ArgumentException("Only volume status getters are allowed.", nameof(method));
            nint path = Marshal.StringToBSTR(ReadString(instance, "__PATH"));
            nint name = Marshal.StringToBSTR(method), result = 0;
            try
            {
                var exec = (delegate* unmanaged[Stdcall]<nint, nint, nint, int, nint, nint, nint*, nint, int>)(*(nint**)services)[24];
                Marshal.ThrowExceptionForHR(exec(services, path, name, 0, 0, 0, &result, 0));
                if (result == 0 || ReadValue(result, "ReturnValue") != "0")
                    throw new InvalidOperationException($"BitLocker {method} did not return success.");
                Dictionary<string, string> values = new(StringComparer.Ordinal);
                foreach (string property in properties) values[property] = ReadValue(result, property);
                return values;
            }
            finally { Release(result); Marshal.FreeBSTR(name); Marshal.FreeBSTR(path); }
        }

        private static void Get(nint instance, string name, Variant* value)
        {
            var get = (delegate* unmanaged[Stdcall]<nint, char*, int, Variant*, nint, nint, int>)(*(nint**)instance)[4];
            fixed (char* property = name)
                Marshal.ThrowExceptionForHR(get(instance, property, 0, value, 0, 0));
        }

        private static void Release(nint instance)
        {
            if (instance != 0)
                ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)instance)[2])(instance);
        }

        // Native VARIANT is 24 bytes on x64 (16 on x86; the extra tail is unused).
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct Variant
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public nint Pointer;
            [FieldOffset(8)] public short Boolean;
            [FieldOffset(8)] public int Signed;
            [FieldOffset(8)] public uint Unsigned;
        }

        [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
        [DllImport("ole32.dll")] private static extern void CoUninitialize();
        [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, nint outer, uint context, in Guid iid, out nint instance);
        [DllImport("ole32.dll")] private static extern int CoSetProxyBlanket(nint proxy, uint authentication, uint authorization, nint principal, uint level, uint impersonation, nint identity, uint capabilities);
        [DllImport("oleaut32.dll")] private static extern int VariantClear(ref Variant variant);
    }
}
