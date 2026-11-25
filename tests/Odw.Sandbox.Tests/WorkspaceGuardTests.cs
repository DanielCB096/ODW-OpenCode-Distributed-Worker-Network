using System.Diagnostics;
using Odw.Sandbox.Workspaces;
using Xunit;

namespace Odw.Sandbox.Tests;

public class WorkspaceGuardTests
{
    [Fact]
    public void Accepts_a_clean_tree()
    {
        var root = CreateTemp();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            File.WriteAllText(Path.Combine(root, "src", "a.txt"), "hola");

            WorkspaceGuard.EnsureNoReparsePoints(root);
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public void Rejects_a_junction()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTemp();
        var link = Path.Combine(root, "link");

        try
        {
            var target = Path.Combine(root, "target");
            Directory.CreateDirectory(target);
            Assert.Equal(0, Run("mklink", "/J", link, target));

            var error = Assert.Throws<WorkspaceGuardException>(
                () => WorkspaceGuard.EnsureNoReparsePoints(root));
            Assert.Contains("REPARSE_DETECTED", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Run("rmdir", link);
            Delete(root);
        }
    }

    [Fact]
    public void Rejects_a_hard_link()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var root = CreateTemp();
        var source = Path.Combine(root, "source.txt");
        var link = Path.Combine(root, "link.txt");

        try
        {
            File.WriteAllText(source, "hola");
            Assert.Equal(0, Run("mklink", "/H", link, source));

            var error = Assert.Throws<WorkspaceGuardException>(
                () => WorkspaceGuard.EnsureNoReparsePoints(root));
            Assert.Contains("HARD_LINK_DETECTED", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(link);
            Delete(root);
        }
    }

    private static int Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("/c");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
    }

    private static string CreateTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "odw-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
