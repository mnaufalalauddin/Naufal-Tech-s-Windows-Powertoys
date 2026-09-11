using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct SystemSnapshot(
        double CpuPercent,
        double? GpuPercent,
        double MemoryPercent,
        double MemoryUsedGigabytes,
        double MemoryTotalGigabytes,
        double ReceiveMegabitsPerSecond,
        double SendMegabitsPerSecond,
        int ProcessCount,
        TimeSpan Uptime);

    internal sealed class SystemMonitorService : IDisposable
    {
        private const double BytesPerGigabyte = 1024d * 1024d * 1024d;
        private const double BitsPerMegabit = 1_000_000d;

        private ulong _previousIdleTime;
        private ulong _previousKernelTime;
        private ulong _previousUserTime;
        private long _previousReceivedBytes;
        private long _previousSentBytes;
        private long _previousNetworkTimestamp;
        private bool _hasCpuSample;
        private bool _hasNetworkSample;
        private IntPtr _gpuQuery;
        private IntPtr _gpuCounter;

        public SystemMonitorService()
        {
            InitializeGpuCounter();
        }

        public SystemSnapshot ReadSnapshot()
        {
            double cpuPercent = ReadCpuPercent();
            double? gpuPercent = ReadGpuPercent();
            (double memoryPercent, double usedGigabytes, double totalGigabytes) = ReadMemory();
            (double receiveMbps, double sendMbps) = ReadNetworkThroughput();
            int processCount = ReadProcessCount();
            TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            return new SystemSnapshot(
                cpuPercent,
                gpuPercent,
                memoryPercent,
                usedGigabytes,
                totalGigabytes,
                receiveMbps,
                sendMbps,
                processCount,
                uptime);
        }

        public void Dispose()
        {
            if (_gpuQuery != IntPtr.Zero)
            {
                PdhCloseQuery(_gpuQuery);
                _gpuQuery = IntPtr.Zero;
                _gpuCounter = IntPtr.Zero;
            }
        }

        private void InitializeGpuCounter()
        {
            uint status = PdhOpenQueryW(null, IntPtr.Zero, out _gpuQuery);
            if (status != PdhSuccess)
            {
                _gpuQuery = IntPtr.Zero;
                return;
            }

            status = PdhAddEnglishCounterW(
                _gpuQuery,
                @"\GPU Engine(*engtype_3D)\Utilization Percentage",
                IntPtr.Zero,
                out _gpuCounter);

            if (status != PdhSuccess)
            {
                PdhCloseQuery(_gpuQuery);
                _gpuQuery = IntPtr.Zero;
                _gpuCounter = IntPtr.Zero;
                return;
            }

            PdhCollectQueryData(_gpuQuery);
        }

        private double? ReadGpuPercent()
        {
            if (_gpuQuery == IntPtr.Zero || _gpuCounter == IntPtr.Zero || IntPtr.Size != 8)
            {
                return null;
            }

            if (PdhCollectQueryData(_gpuQuery) != PdhSuccess)
            {
                return null;
            }

            uint bufferSize = 0;
            uint itemCount = 0;
            uint status = PdhGetFormattedCounterArrayW(
                _gpuCounter,
                PdhFormatDouble,
                ref bufferSize,
                ref itemCount,
                IntPtr.Zero);

            if (status != PdhMoreData || bufferSize == 0 || itemCount == 0)
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal(checked((int)bufferSize));

            try
            {
                status = PdhGetFormattedCounterArrayW(
                    _gpuCounter,
                    PdhFormatDouble,
                    ref bufferSize,
                    ref itemCount,
                    buffer);

                if (status != PdhSuccess)
                {
                    return null;
                }

                const int itemSizeX64 = 24;
                const int counterStatusOffsetX64 = 8;
                const int doubleValueOffsetX64 = 16;
                byte[] doubleBytes = new byte[sizeof(double)];
                double total = 0;
                bool foundValidValue = false;

                for (uint index = 0; index < itemCount; index++)
                {
                    IntPtr item = IntPtr.Add(buffer, checked((int)index * itemSizeX64));
                    uint counterStatus = unchecked((uint)Marshal.ReadInt32(item, counterStatusOffsetX64));

                    if (counterStatus > PdhNewData)
                    {
                        continue;
                    }

                    Marshal.Copy(IntPtr.Add(item, doubleValueOffsetX64), doubleBytes, 0, doubleBytes.Length);
                    double value = BitConverter.ToDouble(doubleBytes, 0);

                    if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                    {
                        continue;
                    }

                    foundValidValue = true;
                    total += value;
                }

                return foundValidValue ? Math.Clamp(total, 0d, 100d) : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private double ReadCpuPercent()
        {
            if (!GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user))
            {
                return 0;
            }

            ulong idleTime = idle.ToUInt64();
            ulong kernelTime = kernel.ToUInt64();
            ulong userTime = user.ToUInt64();

            if (!_hasCpuSample)
            {
                _previousIdleTime = idleTime;
                _previousKernelTime = kernelTime;
                _previousUserTime = userTime;
                _hasCpuSample = true;
                return 0;
            }

            ulong idleDelta = idleTime - _previousIdleTime;
            ulong kernelDelta = kernelTime - _previousKernelTime;
            ulong userDelta = userTime - _previousUserTime;
            ulong totalDelta = kernelDelta + userDelta;

            _previousIdleTime = idleTime;
            _previousKernelTime = kernelTime;
            _previousUserTime = userTime;

            if (totalDelta == 0)
            {
                return 0;
            }

            ulong busyDelta = totalDelta > idleDelta ? totalDelta - idleDelta : 0;
            double busyPercent = 100d * busyDelta / totalDelta;
            return Math.Clamp(busyPercent, 0d, 100d);
        }

        private static (double Percent, double UsedGigabytes, double TotalGigabytes) ReadMemory()
        {
            MemoryStatusEx memory = new()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
            };

            if (!GlobalMemoryStatusEx(ref memory) || memory.TotalPhysical == 0)
            {
                return (0, 0, 0);
            }

            ulong usedBytes = memory.TotalPhysical - memory.AvailablePhysical;
            double totalGigabytes = memory.TotalPhysical / BytesPerGigabyte;
            double usedGigabytes = usedBytes / BytesPerGigabyte;
            double percent = 100d * usedBytes / memory.TotalPhysical;

            return (percent, usedGigabytes, totalGigabytes);
        }

        private (double ReceiveMbps, double SendMbps) ReadNetworkThroughput()
        {
            long receivedBytes = 0;
            long sentBytes = 0;

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                try
                {
                    IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
                    receivedBytes += statistics.BytesReceived;
                    sentBytes += statistics.BytesSent;
                }
                catch
                {
                    // Ignore an adapter that disappears while the snapshot is collected.
                }
            }

            long timestamp = Stopwatch.GetTimestamp();

            if (!_hasNetworkSample)
            {
                _previousReceivedBytes = receivedBytes;
                _previousSentBytes = sentBytes;
                _previousNetworkTimestamp = timestamp;
                _hasNetworkSample = true;
                return (0, 0);
            }

            double elapsedSeconds =
                (timestamp - _previousNetworkTimestamp) / (double)Stopwatch.Frequency;

            long receivedDelta = Math.Max(0, receivedBytes - _previousReceivedBytes);
            long sentDelta = Math.Max(0, sentBytes - _previousSentBytes);

            _previousReceivedBytes = receivedBytes;
            _previousSentBytes = sentBytes;
            _previousNetworkTimestamp = timestamp;

            if (elapsedSeconds <= 0)
            {
                return (0, 0);
            }

            double receiveMbps = receivedDelta * 8d / elapsedSeconds / BitsPerMegabit;
            double sendMbps = sentDelta * 8d / elapsedSeconds / BitsPerMegabit;
            return (receiveMbps, sendMbps);
        }

        private static int ReadProcessCount()
        {
            Process[] processes = Process.GetProcesses();

            try
            {
                return processes.Length;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint Low;
            public uint High;

            public readonly ulong ToUInt64()
            {
                return ((ulong)High << 32) | Low;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        private const uint PdhSuccess = 0x00000000;
        private const uint PdhNewData = 0x00000001;
        private const uint PdhMoreData = 0x800007D2;
        private const uint PdhFormatDouble = 0x00000200;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(
            out FileTime idleTime,
            out FileTime kernelTime,
            out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatus);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQueryW(
            string? dataSource,
            IntPtr userData,
            out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounterW(
            IntPtr query,
            string fullCounterPath,
            IntPtr userData,
            out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhGetFormattedCounterArrayW(
            IntPtr counter,
            uint format,
            ref uint bufferSize,
            ref uint itemCount,
            IntPtr itemBuffer);

        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);
    }
}
