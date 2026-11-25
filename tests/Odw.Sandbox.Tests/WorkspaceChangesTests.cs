using System.Diagnostics;
using Odw.Sandbox.Workspaces;
using Xunit;

namespace Odw.Sandbox.Tests;

public class WorkspaceChangesTests
{
    [Fact]
    public async Task Commits_and_reports_changes()
    {
        var repo = CreateRepo();

        try
        {
            File.WriteAllText(Path.Combine(repo, "src", "a.txt"), "cambiado");
            File.WriteAllText(Path.Combine(repo, "nuevo.txt"), "nuevo");

            var changes = await WorkspaceChanges.CaptureAsync(
                repo, commitChanges: true, "01JAZ5Z8Q4XK3V9C2M1N7P6R8T", CancellationToken.None);

            Assert.Contains("src/a.txt", changes.Paths);
            Assert.Contains("nuevo.txt", changes.Paths);
            Assert.NotNull(changes.Commit);
            Assert.Equal(40, changes.Commit!.Length);
            Assert.Contains("nuevo.txt", changes.Diff, StringComparison.Ordinal);
            Assert.False(changes.DiffTruncated);

            var head = Run(repo, "rev-parse", "HEAD");
            Assert.Equal(changes.Commit, head.Trim());
        }
        finally
        {
            Delete(repo);
        }
    }

    [Fact]
    public async Task Reports_read_only_changes_without_committing()
    {
        var repo = CreateRepo();

        try
        {
            File.WriteAllText(Path.Combine(repo, "src", "a.txt"), "cambiado");

            var changes = await WorkspaceChanges.CaptureAsync(
                repo, commitChanges: false, "01JAZ5Z8Q4XK3V9C2M1N7P6R8T", CancellationToken.None);

            Assert.Contains("src/a.txt", changes.Paths);
            Assert.Null(changes.Commit);
            Assert.Contains("src/a.txt", changes.Diff, StringComparison.Ordinal);
        }
        finally
        {
            Delete(repo);
        }
    }

    [Fact]
    public async Task Clean_tree_has_no_changes()
    {
        var repo = CreateRepo();

        try
        {
            var changes = await WorkspaceChanges.CaptureAsync(
                repo, commitChanges: true, "01JAZ5Z8Q4XK3V9C2M1N7P6R8T", CancellationToken.None);

            Assert.Empty(changes.Paths);
            Assert.Null(changes.Commit);
            Assert.Null(changes.Diff);
        }
        finally
        {
            Delete(repo);
        }
    }

    private static string CreateRepo()
    {
        var repo = Path.Combine(Path.GetTempPath(), "odw-changes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "a.txt"), "inicial");
        Run(repo, "init", "-q");
        Run(repo, "config", "user.email", "test@example.com");
        Run(repo, "config", "user.name", "Test");
        Run(repo, "add", ".");
        Run(repo, "commit", "-qm", "init");
        return repo;
    }

    private static string Run(string repo, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(error);
        }

        return output;
    }

    private static void Delete(string repo)
    {
        try
        {
            if (!Directory.Exists(repo))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(repo, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                }
            }

            Directory.Delete(repo, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
