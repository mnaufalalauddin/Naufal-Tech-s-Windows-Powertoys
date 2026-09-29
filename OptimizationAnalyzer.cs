using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal readonly record struct OptimizationSample(
    DateTimeOffset Timestamp,
    TimeSpan Uptime,
    ulong PhysicalUsedBytes,
    ulong CommitUsedBytes,
    ulong CommitLimitBytes,
    double? CpuPercent,
    long DiskReadBytesPerSecond,
    long DiskWriteBytesPerSecond,
    long SystemDriveFreeBytes,
    int Processes,
    long Threads,
    long Handles);

internal readonly record struct OptimizationSummary(
    int SampleCount,
    double AveragePhysicalUsedBytes,
    double AverageCommitUsedBytes,
    double AverageCpuPercent,
    double AverageDiskReadBytesPerSecond,
    double AverageDiskWriteBytesPerSecond,
    double AverageProcesses,
    double AverageThreads,
    double AverageHandles,
    long SystemDriveFreeBytes,
    TimeSpan Uptime);

internal readonly record struct OptimizationComparison(
    OptimizationSummary Baseline,
    OptimizationSummary After,
    double PhysicalUsedBytesDelta,
    double CommitUsedBytesDelta,
    double CpuPercentDelta,
    double DiskReadBytesPerSecondDelta,
    double DiskWriteBytesPerSecondDelta,
    double ProcessesDelta,
    double ThreadsDelta,
    double HandlesDelta,
    long FreeSpaceDelta);

internal sealed class OptimizationAnalyzer
{
    internal async Task<IReadOnlyList<OptimizationSample>> CaptureAsync(
        TimeSpan duration,
        TimeSpan interval,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (interval < TimeSpan.FromMilliseconds(250)) throw new ArgumentOutOfRangeException(nameof(interval));

        List<OptimizationSample> samples = new();
        TimeSpan previousCpu = TotalCpu();
        DateTimeOffset previousTime = DateTimeOffset.UtcNow;
        IoCounters previousIo = ReadSystemIo();

        Stopwatch elapsed = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan cpu = TotalCpu();
            IoCounters io = ReadSystemIo();
            double seconds = Math.Max(0.001, (now - previousTime).TotalSeconds);
            double? cpuPercent = Math.Clamp(
                (cpu - previousCpu).TotalSeconds / seconds / Math.Max(1, Environment.ProcessorCount) * 100d,
                0d, 100d);

            MemoryStatusEx memory = ReadMemory();
            (int processes, long threads, long handles) = ReadProcessCounts();
            long free = ReadSystemDriveFreeBytes();

            samples.Add(new OptimizationSample(
                now,
                TimeSpan.FromMilliseconds(Environment.TickCount64),
                memory.TotalPhysical - memory.AvailablePhysical,
                memory.TotalPageFile - memory.AvailablePageFile,
                memory.TotalPageFile,
                cpuPercent,
                Math.Max(0, io.ReadBytes - previousIo.ReadBytes) / (long)Math.Max(1, seconds),
                Math.Max(0, io.WriteBytes - previousIo.WriteBytes) / (long)Math.Max(1, seconds),
                free,
                processes,
                threads,
                handles));

            if (elapsed.Elapsed >= duration) break;
            previousCpu = cpu;
            previousTime = now;
            previousIo = io;
            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
        return samples;
    }

    internal static OptimizationSummary Summarize(IReadOnlyList<OptimizationSample> samples)
    {
        if (samples.Count == 0) throw new ArgumentException("At least one sample is required.", nameof(samples));
        return new OptimizationSummary(
            samples.Count,
            samples.Average(sample => (double)sample.PhysicalUsedBytes),
            samples.Average(sample => (double)sample.CommitUsedBytes),
            samples.Where(sample => sample.CpuPercent.HasValue).Select(sample => sample.CpuPercent!.Value).DefaultIfEmpty().Average(),
            samples.Average(sample => (double)sample.DiskReadBytesPerSecond),
            samples.Average(sample => (double)sample.DiskWriteBytesPerSecond),
            samples.Average(sample => sample.Processes),
            samples.Average(sample => (double)sample.Threads),
            samples.Average(sample => (double)sample.Handles),
            samples[^1].SystemDriveFreeBytes,
            samples[^1].Uptime);
    }

    internal static OptimizationComparison Compare(OptimizationSummary baseline, OptimizationSummary after) =>
        new(baseline, after,
            after.AveragePhysicalUsedBytes - baseline.AveragePhysicalUsedBytes,
            after.AverageCommitUsedBytes - baseline.AverageCommitUsedBytes,
            after.AverageCpuPercent - baseline.AverageCpuPercent,
            after.AverageDiskReadBytesPerSecond - baseline.AverageDiskReadBytesPerSecond,
            after.AverageDiskWriteBytesPerSecond - baseline.AverageDiskWriteBytesPerSecond,
            after.AverageProcesses - baseline.AverageProcesses,
            after.AverageThreads - baseline.AverageThreads,
            after.AverageHandles - baseline.AverageHandles,
            after.SystemDriveFreeBytes >= 0 && baseline.SystemDriveFreeBytes >= 0
                ? after.SystemDriveFreeBytes - baseline.SystemDriveFreeBytes
                : 0);

    private static TimeSpan TotalCpu()
    {
        TimeSpan total = TimeSpan.Zero;
        foreach (Process process in Process.GetProcesses())
        {
            try { total += process.TotalProcessorTime; }
            catch { }
            finally { process.Dispose(); }
        }
        return total;
    }

    private static (int Processes, long Threads, long Handles) ReadProcessCounts()
    {
        int count = 0;
        long threads = 0, handles = 0;
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                count++;
                threads += process.Threads.Count;
                handles += process.HandleCount;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return (count, threads, handles);
    }

    private static IoCounters ReadSystemIo()
    {
        IoCounters total = default;
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (GetProcessIoCounters(process.Handle, out ProcessIoCounters io))
                {
                    total.ReadBytes += checked((long)io.ReadTransferCount);
                    total.WriteBytes += checked((long)io.WriteTransferCount);
                }
            }
            catch { }
            finally { process.Dispose(); }
        }
        return total;
    }

    private static long ReadSystemDriveFreeBytes()
    {
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        try { return new DriveInfo(root).AvailableFreeSpace; }
        catch { return -1; }
    }

    private static MemoryStatusEx ReadMemory()
    {
        MemoryStatusEx status = new() { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) throw new InvalidOperationException("GlobalMemoryStatusEx failed.");
        return status;
    }

    private struct IoCounters { internal long ReadBytes; internal long WriteBytes; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessIoCounters
    {
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
        public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile;
        public ulong TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessIoCounters(nint process, out ProcessIoCounters counters);
}
