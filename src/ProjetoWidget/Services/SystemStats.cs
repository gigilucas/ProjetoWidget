using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ProjetoWidget.Services;

/// <summary>Live CPU and memory numbers for this PC.</summary>
public sealed class SystemStats : IDisposable
{
    private readonly PerformanceCounter? _utility;
    private readonly PerformanceCounter? _performance;

    private SystemStats()
    {
        try
        {
            // Same counters Task Manager uses for "Utilization" and "Speed".
            _utility = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", readOnly: true);
            _performance = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", readOnly: true);
            _utility.NextValue();
            _performance.NextValue();
        }
        catch (Exception)
        {
            _utility = _performance = null; // counters unavailable: CPU shows as 0%
        }
    }

    /// <summary>Creating performance counters can take a moment, so do it off the UI thread.</summary>
    public static Task<SystemStats> CreateAsync() => Task.Run(() => new SystemStats());

    public static string ProcessorName { get; } = ReadProcessorName();

    public static int CoreCount => Environment.ProcessorCount;

    private static readonly double BaseMHz = ReadBaseMHz();

    public SystemSnapshot Sample()
    {
        double cpu = 0, ghz = 0;
        try
        {
            if (_utility is not null && _performance is not null)
            {
                cpu = Math.Clamp(_utility.NextValue(), 0, 100);
                ghz = BaseMHz * _performance.NextValue() / 100 / 1000;
            }
        }
        catch (Exception)
        {
            // keep zeros
        }

        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        GlobalMemoryStatusEx(ref memory);
        const double gb = 1024d * 1024 * 1024;
        var totalGb = memory.TotalPhys / gb;
        var usedGb = (memory.TotalPhys - memory.AvailPhys) / gb;

        return new SystemSnapshot(cpu, ghz, memory.MemoryLoad, usedGb, totalGb);
    }

    public void Dispose()
    {
        _utility?.Dispose();
        _performance?.Dispose();
    }

    private static string ReadProcessorName() =>
        (Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string)?.Trim()
        ?? "Unknown processor";

    private static double ReadBaseMHz() =>
        Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz", null) is int mhz ? mhz : 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}

/// <param name="CpuPercent">CPU utilization, 0-100.</param>
/// <param name="CpuGHz">Current CPU speed.</param>
/// <param name="MemoryPercent">Memory in use, 0-100.</param>
public readonly record struct SystemSnapshot(double CpuPercent, double CpuGHz, double MemoryPercent, double UsedGb, double TotalGb);
