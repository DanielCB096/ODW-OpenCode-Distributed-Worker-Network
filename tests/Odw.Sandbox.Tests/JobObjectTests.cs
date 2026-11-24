using System.Diagnostics;
using Odw.Sandbox.Windows;
using Xunit;

namespace Odw.Sandbox.Tests;

public class JobObjectTests
{
    [Fact]
    public void Closing_the_job_kills_the_process_tree()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var job = JobObject.Create(new JobLimits
        {
            ProcessMemoryBytes = 512L * 1024 * 1024,
            JobMemoryBytes = 512L * 1024 * 1024,
            ActiveProcessLimit = 4,
            CpuTime = TimeSpan.FromMinutes(1),
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -n 30 127.0.0.1 >nul",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)!;

        try
        {
            job.Assign(process);
            Assert.False(process.WaitForExit(200), "El proceso no debería haber terminado.");

            job.Dispose();

            Assert.True(process.WaitForExit(5000), "El Job Object no mató el proceso.");
        }
        finally
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    [Fact]
    public void Creating_a_job_outside_windows_throws()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Throws<PlatformNotSupportedException>(() => JobObject.Create(new JobLimits()));
    }
}
