using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys
{
    /// <summary>Explicit per-adapter RSC changes; called only from confirmed profile transactions.</summary>
    internal static unsafe class NativeRscService
    {
        public static void SetStates(IReadOnlyList<ProfileRscAdapter> targets)
        {
            if (targets.Count == 0) return;
            Dictionary<string, ProfileRscAdapter> remaining = targets.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> outcomes = new(StringComparer.OrdinalIgnoreCase);
            NativeRscReader.Visit((services, instance) =>
            {
                string name = NativeRscReader.ReadString(instance, "Name");
                if (!remaining.TryGetValue(name, out ProfileRscAdapter target)) return;
                string objectPath = NativeRscReader.ReadString(instance, "__PATH");
                List<string> results = new();
                if (target.Ipv4Enabled || target.Ipv6Enabled)
                    results.Add(Invoke(services, objectPath, "Enable", target.Ipv4Enabled, target.Ipv6Enabled));
                if (!target.Ipv4Enabled || !target.Ipv6Enabled)
                    results.Add(Invoke(services, objectPath, "Disable", !target.Ipv4Enabled, !target.Ipv6Enabled));
                outcomes[name] = string.Join("; ", results);
                remaining.Remove(name);
            });
            if (remaining.Count > 0)
                throw new InvalidOperationException("RSC adapters disappeared: " + string.Join(", ", remaining.Keys));

            // Neither HRESULT success nor a zero/missing ReturnValue proves that
            // the requested state was applied. This also gates rollback success.
            VerifyStates(targets, NativeRscReader.Read(), outcomes);
        }

        // The metadata-only path is testable without invoking an adapter method.
        internal static IReadOnlyList<string> ValidateMethodMetadata()
        {
            List<string> results = new();
            bool checkedOnce = false;
            NativeRscReader.Visit((services, _) =>
            {
                if (checkedOnce) return;
                results.Add(Invoke(services, string.Empty, "Enable", true, true, metadataOnly: true));
                results.Add(Invoke(services, string.Empty, "Disable", true, true, metadataOnly: true));
                checkedOnce = true;
            });
            return results;
        }

        private static string Invoke(nint services, string objectPath, string method, bool ipv4, bool ipv6, bool metadataOnly = false)
        {
            nint cls = 0, signature = 0, outputSignature = 0, parameters = 0, result = 0;
            nint className = Marshal.StringToBSTR("MSFT_NetAdapterRscSettingData");
            nint path = 0, methodName = 0;
            try
            {
                var getObject = (delegate* unmanaged[Stdcall]<nint, nint, int, nint, nint*, nint, int>)(*(nint**)services)[6];
                Marshal.ThrowExceptionForHR(getObject(services, className, 0, 0, &cls, 0));
                RequireObject(cls, "RSC class");
                var getMethod = (delegate* unmanaged[Stdcall]<nint, char*, int, nint*, nint*, int>)(*(nint**)cls)[19];
                fixed (char* name = method)
                    Marshal.ThrowExceptionForHR(getMethod(cls, name, 0, &signature, &outputSignature));
                RequireObject(signature, "RSC method signature");
                RequireObject(outputSignature, "RSC method output signature");
                var spawn = (delegate* unmanaged[Stdcall]<nint, int, nint*, int>)(*(nint**)signature)[15];
                Marshal.ThrowExceptionForHR(spawn(signature, 0, &parameters));
                RequireObject(parameters, "RSC method parameters");
                PutBoolean(parameters, "IPv4", ipv4);
                PutBoolean(parameters, "IPv6", ipv6);
                if (metadataOnly) return ReadMethodReturn(outputSignature, method);

                path = Marshal.StringToBSTR(objectPath);
                methodName = Marshal.StringToBSTR(method);
                var execMethod = (delegate* unmanaged[Stdcall]<nint, nint, nint, int, nint, nint, nint*, nint, int>)(*(nint**)services)[24];
                Marshal.ThrowExceptionForHR(execMethod(services, path, methodName, 0, 0, parameters, &result, 0));
                RequireObject(result, "RSC method result");
                return ReadMethodReturn(result, method);
            }
            finally
            {
                Release(result); Release(parameters); Release(outputSignature); Release(signature); Release(cls);
                if (methodName != 0) Marshal.FreeBSTR(methodName);
                if (path != 0) Marshal.FreeBSTR(path);
                Marshal.FreeBSTR(className);
            }
        }

        private static string ReadMethodReturn(nint result, string method)
        {
            Variant value = default;
            int cimType = 0;
            try
            {
                var get = (delegate* unmanaged[Stdcall]<nint, char*, int, Variant*, int*, nint, int>)(*(nint**)result)[4];
                fixed (char* returnName = "ReturnValue")
                    Marshal.ThrowExceptionForHR(get(result, returnName, 0, &value, &cimType, 0));
                return ValidateMethodReturn(method, value.Type, value.Unsigned, cimType);
            }
            finally { VariantClear(ref value); }
        }

        internal static string ValidateMethodReturn(string method, ushort variantType, uint rawValue, int cimType)
        {
            string context = $"RSC {method} (VARIANT={variantType}, CIM={cimType})";
            if (cimType != 19) // CIM_UINT32: the Enable/Disable return schema.
                throw new InvalidOperationException($"{context}: unexpected ReturnValue schema; the operation is not verified.");
            // Some providers leave the declared output unset. Do not reinterpret
            // the unused union bits as result 0 or claim success: SetStates must
            // still query and compare every requested adapter/protocol below.
            if (variantType is 0 or 1)
                return $"{context}: ReturnValue is {(variantType == 0 ? "EMPTY" : "NULL")}; state read-back required";
            long code = variantType switch
            {
                2 => unchecked((short)rawValue), // VT_I2
                3 or 22 => unchecked((int)rawValue), // VT_I4 / VT_INT
                17 => (byte)rawValue, // VT_UI1
                18 => (ushort)rawValue, // VT_UI2
                19 or 23 => rawValue, // VT_UI4 / VT_UINT
                _ => throw new InvalidOperationException($"{context}: unsupported ReturnValue type; the operation is not verified.")
            };
            if (code != 0)
                throw new InvalidOperationException($"{context} failed with result {code} (0x{unchecked((uint)code):X8}).");
            return $"{context}: ReturnValue=0; state read-back required";
        }

        internal static void VerifyStates(IReadOnlyList<ProfileRscAdapter> targets,
            IReadOnlyList<ProfileRscAdapter> current, IReadOnlyDictionary<string, string> outcomes)
        {
            Dictionary<string, ProfileRscAdapter> actual = current.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
            List<string> failures = new();
            foreach (ProfileRscAdapter expected in targets)
            {
                bool found = actual.TryGetValue(expected.Name, out ProfileRscAdapter row);
                if (found && row.Ipv4Enabled == expected.Ipv4Enabled && row.Ipv6Enabled == expected.Ipv6Enabled) continue;
                string observed = found ? $"IPv4={row.Ipv4Enabled}, IPv6={row.Ipv6Enabled}" : "adapter missing";
                outcomes.TryGetValue(expected.Name, out string? outcome);
                failures.Add($"{expected.Name}: expected IPv4={expected.Ipv4Enabled}, IPv6={expected.Ipv6Enabled}; actual {observed}. {outcome}");
            }
            if (failures.Count > 0)
                throw new InvalidOperationException("RSC state read-back failed: " + string.Join(" | ", failures));
        }

        private static void RequireObject(nint instance, string name)
        {
            if (instance == 0) throw new InvalidOperationException($"{name} is unavailable.");
        }

        private static void PutBoolean(nint instance, string name, bool enabled)
        {
            Variant value = new() { Type = 11, Boolean = enabled ? (short)-1 : (short)0 };
            var put = (delegate* unmanaged[Stdcall]<nint, char*, int, Variant*, int, int>)(*(nint**)instance)[5];
            fixed (char* property = name)
                Marshal.ThrowExceptionForHR(put(instance, property, 0, &value, 0));
        }

        private static void Release(nint instance)
        {
            if (instance != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)instance)[2])(instance);
        }
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct Variant
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(8)] public short Boolean;
            [FieldOffset(8)] public uint Unsigned;
        }
        [DllImport("oleaut32.dll")] private static extern int VariantClear(ref Variant variant);
    }
}
