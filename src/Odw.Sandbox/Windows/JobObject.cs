using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Odw.Sandbox.Windows;

public sealed record JobLimits
{
    public long? ProcessMemoryBytes { get; init; }

    public long? JobMemoryBytes { get; init; }

    public int? ActiveProcessLimit { get; init; }

    public TimeSpan? CpuTime { get; init; }

    public static JobLimits FromTask(int memoryGb, int processes, int minutes)
        => new()
        {
            ProcessMemoryBytes = memoryGb * 1024L * 1024L * 1024L,
            JobMemoryBytes = memoryGb * 1024L * 1024L * 1024L,
            ActiveProcessLimit = processes,
            CpuTime = TimeSpan.FromMinutes(minutes),
        };
}

/// <summary>
/// Job Object de Windows: lÃ­mites de memoria/procesos/CPU y muerte en cascada al cerrar.
/// No sustituye al usuario restringido ni a las ACL (H3); es el control de recursos.
/// </summary>
public sealed class JobObject : IDisposable
{
    private const int ExtendedLimitInformationClass = 9;
    private const uint JobObjectLimitProcessTime = 0x2;
    private const uint JobObjectLimitActiveProcess = 0x8;
    private const uint JobObjectLimitProcessMemory = 0x100;
    private const uint JobObjectLimitJobMemory = 0x200;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private IntPtr _handle;
    private bool _disposed;

    private JobObject(IntPtr handle)
    {
        _handle = handle;
    }

    public static JobObject Create(JobLimits limits, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Los Job Objects solo existen en Windows.");
        }

        var handle = CreateJobObject(IntPtr.Zero, name);
        if (handle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject fallÃ³.");
        }

        try
        {
            ApplyLimits(handle, limits);
            return new JobObject(handle);
        }
        catch
        {
            CloseHandle(handle);
            throw;
        }
    }

    public void Assign(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!AssignProcessToJobObject(_handle, process.Handle))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"AssignProcessToJobObject fallÃ³ para el proceso {process.Id}.");
        }
    }

    public void Terminate(uint exitCode = 1)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TerminateJobObject(_handle, exitCode);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    private static void ApplyLimits(IntPtr handle, JobLimits limits)
    {
        var flags = JobObjectLimitKillOnJobClose;
        var basic = default(JobObjectBasicLimitInformation);

        if (limits.ProcessMemoryBytes is { } processMemory)
        {
            flags |= JobObjectLimitProcessMemory;
        }

        if (limits.JobMemoryBytes is { } jobMemory)
        {
            flags |= JobObjectLimitJobMemory;
        }

        if (limits.ActiveProcessLimit is { } activeProcesses)
        {
            flags |= JobObjectLimitActiveProcess;
            basic.ActiveProcessLimit = (uint)activeProcesses;
        }

        if (limits.CpuTime is { } cpuTime)
        {
            flags |= JobObjectLimitProcessTime;
            basic.PerProcessUserTimeLimit = cpuTime.Ticks;
        }

        basic.LimitFlags = flags;

        var info = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = basic,
            ProcessMemoryLimit = (UIntPtr)(ulong)(limits.ProcessMemoryBytes ?? 0),
            JobMemoryLimit = (UIntPtr)(ulong)(limits.JobMemoryBytes ?? 0),
        };

        var size = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
        var pointer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(info, pointer, fDeleteOld: false);

            if (!SetInformationJobObject(handle, ExtendedLimitInformationClass, pointer, (uint)size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject fallÃ³.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr job,
        int informationClass,
        IntPtr information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
