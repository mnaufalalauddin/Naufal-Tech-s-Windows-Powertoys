using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys;

// No process enumeration, WMI, services, registry writes or background worker.
// Each on-demand capture owns and disposes its PDH query and Process handle.
internal sealed class NativeResourceProbe : IResourceProbe
{
    private readonly Process _self = Process.GetCurrentProcess();
    private IntPtr _query;
    private IntPtr _cpu, _read, _write;
    private long _timestamp;
    private double? _previousOwnCpu;
    private readonly uint _processors = GetActiveProcessorCount(0xffff);
    public string Windows { get; }
    public string Volume { get; } = Path.GetPathRoot(Environment.SystemDirectory) ?? "";

    internal NativeResourceProbe()
    {
        Windows = RuntimeInformation.OSDescription + " / " + RuntimeInformation.OSArchitecture;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            Windows += $" / Edition={key?.GetValue("EditionID") ?? "Unknown"} / Build={key?.GetValue("CurrentBuildNumber") ?? "Unknown"}.{key?.GetValue("UBR") ?? "Unknown"}";
        }
        catch { Windows += " / Edition/build details unavailable"; }
        if (PdhOpenQueryW(null, IntPtr.Zero, out _query) == 0)
        {
            Add(@"\Processor Information(_Total)\% Processor Time", out _cpu);
            Add(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec", out _read);
            Add(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec", out _write);
        }
        else _query = IntPtr.Zero;
    }

    private void Add(string path, out IntPtr counter)
    {
        if (PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out counter) != 0) counter = IntPtr.Zero;
    }

    public ResourceSample Read()
    {
        long now = Stopwatch.GetTimestamp();
        double elapsed = _timestamp == 0 ? 0 : (now - _timestamp) / (double)Stopwatch.Frequency;
        _timestamp = now;
        Dictionary<string, double?> values = new();
        foreach (var metric in ResourceMeasurements.Metrics) values[metric.Id] = null;
        PerformanceInformation info = new() { Size = (uint)Marshal.SizeOf<PerformanceInformation>() };
        if (GetPerformanceInfo(out info, info.Size))
        {
            double page = info.PageSize;
            if (page > 0 && info.PhysicalAvailable <= info.PhysicalTotal)
            {
                values["ram"] = (info.PhysicalTotal - info.PhysicalAvailable) * page;
                values["ramTotal"] = info.PhysicalTotal * page;
                values["commit"] = info.CommitTotal * page;
                values["commitLimit"] = info.CommitLimit * page;
            }
            values["processes"] = info.ProcessCount;
            values["threads"] = info.ThreadCount;
            values["handles"] = info.HandleCount;
        }
        if (_query != IntPtr.Zero && PdhCollectQueryData(_query) == 0 && elapsed > 0)
        {
            values["cpu"] = Counter(_cpu, percent: true);
            values["diskRead"] = Counter(_read);
            values["diskWrite"] = Counter(_write);
        }
        try { values["free"] = new DriveInfo(Volume).TotalFreeSpace; } catch { }
        _self.Refresh();
        ReadOwn("ownPrivate", () => _self.PrivateMemorySize64);
        ReadOwn("ownWorking", () => _self.WorkingSet64);
        ReadOwn("ownThreads", () => _self.Threads.Count);
        ReadOwn("ownHandles", () => _self.HandleCount);
        try
        {
            double ownCpu = _self.TotalProcessorTime.TotalSeconds;
            if (_previousOwnCpu is double previous && ownCpu >= previous && elapsed > 0 && _processors > 0)
                values["ownCpu"] = Math.Clamp(100 * (ownCpu - previous) / elapsed / _processors, 0, 100);
            _previousOwnCpu = ownCpu;
        }
        catch { _previousOwnCpu = null; }
        return new(DateTimeOffset.UtcNow, elapsed, values);

        void ReadOwn(string id, Func<double> read)
        {
            try { values[id] = read(); } catch { values[id] = null; }
        }
    }

    private static double? Counter(IntPtr counter, bool percent = false)
    {
        if (counter == IntPtr.Zero || PdhGetFormattedCounterValue(counter, 0x200, out _, out CounterValue value) != 0 ||
            value.Status > 1 || !double.IsFinite(value.Value) || value.Value < 0) return null;
        return percent ? Math.Clamp(value.Value, 0, 100) : value.Value;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; }
        _self.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public nuint CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable,
            SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct CounterValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double Value;
    }
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PerformanceInformation info, uint size);
    [DllImport("kernel32.dll")]
    private static extern uint GetActiveProcessorCount(ushort group);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out CounterValue value);
    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);
}
