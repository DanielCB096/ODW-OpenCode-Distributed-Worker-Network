using System.Diagnostics;

namespace Odw.Sandbox.Workspaces;

public sealed record WorkspaceChangeSet(
    IReadOnlyList<string> Paths,
    string? Commit,
    string? Diff,
    bool DiffTruncated);

/// <summary>
/// Calcula los cambios de una tarea antes de borrar el workspace: rutas, commit en la rama de
/// trabajo y un parche autocontenido (limitado) para que el origen pueda revisarlo.
/// </summary>
public static class WorkspaceChanges
{
    private const int MaxDiffChars = 256 * 1024;

    public static async Task<WorkspaceChangeSet> CaptureAsync(
        string workspace,
        bool commitChanges,
        string taskId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        if (commitChanges)
        {
            var add = await GitAsync(workspace, ["add", "-A"], cancellationToken).ConfigureAwait(false);
            if (add.ExitCode != 0)
            {
                throw new WorkspaceException($"git add falló: {Tail(add.Error)}");
            }

            var names = await GitAsync(
                workspace,
                ["diff", "--cached", "--name-only", "-z"],
                cancellationToken).ConfigureAwait(false);

            if (names.ExitCode != 0)
            {
                throw new WorkspaceException($"git diff --name-only falló: {Tail(names.Error)}");
            }

            var paths = ParseNullSeparated(names.Output);
            string? commit = null;

            var diff = await GitAsync(
                workspace,
                ["diff", "--cached", "--binary", "--no-color"],
                cancellationToken).ConfigureAwait(false);

            if (diff.ExitCode != 0)
            {
                throw new WorkspaceException($"git diff falló: {Tail(diff.Error)}");
            }

            var (text, truncated) = Limit(diff.Output);

            if (paths.Count > 0)
            {
                var message = $"odw: {taskId}";
                var commitResult = await GitAsync(
                    workspace,
                    ["-c", "user.name=ODW", "-c", "user.email=odw@localhost",
                        "commit", "--no-gpg-sign", "-m", message],
                    cancellationToken).ConfigureAwait(false);

                if (commitResult.ExitCode != 0)
                {
                    throw new WorkspaceException($"git commit falló: {Tail(commitResult.Error)}");
                }

                var head = await GitAsync(workspace, ["rev-parse", "HEAD"], cancellationToken)
                    .ConfigureAwait(false);

                if (head.ExitCode == 0)
                {
                    commit = head.Output.Trim();
                }
            }

            return new WorkspaceChangeSet(paths, commit, text, truncated);
        }
        else
        {
            var status = await GitAsync(
                workspace,
                ["status", "--porcelain=v1", "-z", "--untracked-files=all"],
                cancellationToken).ConfigureAwait(false);

            if (status.ExitCode != 0)
            {
                throw new WorkspaceException($"git status falló: {Tail(status.Error)}");
            }

            var paths = ParseStatusPaths(status.Output);

            var diff = await GitAsync(
                workspace,
                ["diff", "--binary", "--no-color"],
                cancellationToken).ConfigureAwait(false);

            var (text, truncated) = diff.ExitCode == 0
                ? Limit(diff.Output)
                : ((string?)null, false);

            return new WorkspaceChangeSet(paths, null, text, truncated);
        }
    }

    private static (string? Text, bool Truncated) Limit(string text)
    {
        if (text.Length == 0)
        {
            return (null, false);
        }

        return text.Length <= MaxDiffChars
            ? (text, false)
            : (text[..MaxDiffChars] + "\n... (parche truncado)", true);
    }

    private static IReadOnlyList<string> ParseNullSeparated(string output)
        => output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(path => path.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ParseStatusPaths(string output)
    {
        var paths = new List<string>();

        foreach (var entry in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.Length < 4)
            {
                continue;
            }

            // "XY path" o "XY old -> new"; los renombrados traen dos entradas NUL.
            var path = entry[3..];
            var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0)
            {
                path = path[(arrow + 4)..];
            }

            path = Normalize(path);
            if (path.Length > 0)
            {
                paths.Add(path);
            }
        }

        return paths.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string Normalize(string path)
    {
        var value = path.Replace('\\', '/').Trim();

        while (value.StartsWith("./", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return value.TrimStart('/');
    }

    private static async Task<(int ExitCode, string Output, string Error)> GitAsync(
        string workspace,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspace,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new WorkspaceException("No se pudo iniciar git.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return (process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }

    private static string Tail(string text)
        => text.Length <= 500 ? text : text[^500..];
}
