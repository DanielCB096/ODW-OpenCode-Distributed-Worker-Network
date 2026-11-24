using System.Diagnostics;
using Odw.Sandbox.Workspaces;
using Xunit;

namespace Odw.Sandbox.Tests;

public class WorkspaceManagerTests
{
    private const string TaskId = "01JAZ5Z8Q4XK3V9C2M1N7P6R8T";

    private static string CreateTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "odw-sandbox-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateRepo()
    {
        var repo = CreateTemp();
        Git(repo, "init", "-q");
        Git(repo, "config", "user.email", "test@example.com");
        Git(repo, "config", "user.name", "Test");
        File.WriteAllText(Path.Combine(repo, "readme.txt"), "hola");
        Git(repo, "add", ".");
        Git(repo, "commit", "-q", "-m", "init");
        return repo;
    }

    private static string CurrentBranch(string repo)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("--abbrev-ref");
        startInfo.ArgumentList.Add("HEAD");

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    private static void Git(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(" ", arguments)} falló ({process.ExitCode}): {error}");
        }
    }

    [Fact]
    public async Task Read_only_workspace_is_detached_without_remote()
    {
        var repo = CreateRepo();
        var branch = CurrentBranch(repo);
        var root = CreateTemp();

        var manager = new WorkspaceManager(new WorkspaceOptions
        {
            WorkspaceRoot = root,
            Repos = new Dictionary<string, string> { ["SistemaMaestro"] = repo },
        });

        var lease = await manager.PrepareAsync(
            new WorkspaceRequest(TaskId, "SistemaMaestro", branch, ReadOnly: true),
            CancellationToken.None);

        Assert.True(Directory.Exists(lease.Path));
        Assert.True(File.Exists(Path.Combine(lease.Path, "readme.txt")));
        Assert.Equal($"detached:{branch}", lease.Branch);
        Assert.Equal("HEAD", CurrentBranch(lease.Path));

        using var remotes = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "remote",
            WorkingDirectory = lease.Path,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        Assert.Equal(string.Empty, remotes.StandardOutput.ReadToEnd().Trim());

        await lease.DisposeAsync();
        Assert.False(Directory.Exists(lease.Path));
    }

    [Fact]
    public async Task Write_workspace_creates_odw_branch()
    {
        var repo = CreateRepo();
        var branch = CurrentBranch(repo);
        var root = CreateTemp();

        var manager = new WorkspaceManager(new WorkspaceOptions
        {
            WorkspaceRoot = root,
            Repos = new Dictionary<string, string> { ["SistemaMaestro"] = repo },
        });

        await using var lease = await manager.PrepareAsync(
            new WorkspaceRequest(TaskId, "SistemaMaestro", branch, ReadOnly: false),
            CancellationToken.None);

        Assert.Equal($"odw/{TaskId}", lease.Branch);
        Assert.Equal(lease.Branch, CurrentBranch(lease.Path));
    }

    [Fact]
    public async Task Unknown_repo_is_rejected()
    {
        var root = CreateTemp();
        var manager = new WorkspaceManager(new WorkspaceOptions
        {
            WorkspaceRoot = root,
            Repos = new Dictionary<string, string>(),
        });

        await Assert.ThrowsAsync<WorkspaceException>(() => manager.PrepareAsync(
            new WorkspaceRequest(TaskId, "NoExiste", "main", ReadOnly: true),
            CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_task_id_is_rejected()
    {
        var repo = CreateRepo();
        var root = CreateTemp();
        var manager = new WorkspaceManager(new WorkspaceOptions
        {
            WorkspaceRoot = root,
            Repos = new Dictionary<string, string> { ["SistemaMaestro"] = repo },
        });

        await Assert.ThrowsAsync<WorkspaceException>(() => manager.PrepareAsync(
            new WorkspaceRequest("../escape", "SistemaMaestro", "main", ReadOnly: true),
            CancellationToken.None));
    }

    [Fact]
    public async Task Existing_workspace_is_not_reused()
    {
        var repo = CreateRepo();
        var branch = CurrentBranch(repo);
        var root = CreateTemp();
        Directory.CreateDirectory(Path.Combine(root, TaskId));

        var manager = new WorkspaceManager(new WorkspaceOptions
        {
            WorkspaceRoot = root,
            Repos = new Dictionary<string, string> { ["SistemaMaestro"] = repo },
        });

        await Assert.ThrowsAsync<WorkspaceException>(() => manager.PrepareAsync(
            new WorkspaceRequest(TaskId, "SistemaMaestro", branch, ReadOnly: true),
            CancellationToken.None));
    }
}
