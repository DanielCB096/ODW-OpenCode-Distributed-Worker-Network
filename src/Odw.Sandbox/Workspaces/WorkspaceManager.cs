using System.Diagnostics;
using System.Globalization;
using Odw.Core.Primitives;

namespace Odw.Sandbox.Workspaces;

public sealed record WorkspaceOptions
{
    public required string WorkspaceRoot { get; init; }

    public required IReadOnlyDictionary<string, string> Repos { get; init; }

    public string GitExecutable { get; init; } = "git";

    public bool KeepWorkspaces { get; init; }

    public TimeSpan GitTimeout { get; init; } = TimeSpan.FromMinutes(5);

    public bool ApplyAcls { get; init; }

    public string? AgentAccount { get; init; }

    public string? ServiceAccount { get; init; }
}

/// <summary>
/// Prepara un clon local del repo dentro de <c>WorkspaceRoot</c>. El clon es completo
/// (sin <c>--shared</c>), se le quita el remoto y, en modo escritura, se crea la rama
/// <c>odw/&lt;task_id&gt;</c>. Así el agente no puede alcanzar el repo original.
/// </summary>
public sealed class WorkspaceManager : IWorkspacePreparer
{
    private readonly WorkspaceOptions _options;

    public WorkspaceManager(WorkspaceOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.WorkspaceRoot))
        {
            throw new ArgumentException("Falta WorkspaceRoot.", nameof(options));
        }
    }

    public async Task<WorkspaceLease> PrepareAsync(
        WorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Ulid.TryParse(request.TaskId, out _))
        {
            throw new WorkspaceException($"task_id inválido para el workspace: {request.TaskId}.");
        }

        if (!_options.Repos.TryGetValue(request.Repo, out var repoPath))
        {
            throw new WorkspaceException($"Repo no registrado: {request.Repo}.");
        }

        if (!Directory.Exists(repoPath))
        {
            throw new WorkspaceException($"La ruta del repo no existe: {repoPath}.");
        }

        await EnsureRootAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(_options.WorkspaceRoot);
        var workspace = Path.Combine(_options.WorkspaceRoot, request.TaskId);

        if (Directory.Exists(workspace))
        {
            throw new WorkspaceException($"El workspace ya existe: {workspace}.");
        }

        Directory.CreateDirectory(workspace);
        if (_options.ApplyAcls)
        {
            // Antes del clone: los archivos (incluido .git) heredan la ACL. Aplicarla después
            // con /T deja sin acceso los archivos ocultos de .git.
            await ApplyAclsAsync(workspace, request.ReadOnly, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var clone = await GitAsync(
                null,
                ["-c", "safe.directory=*", "clone", "--no-hardlinks", "--quiet", repoPath, workspace],
                cancellationToken).ConfigureAwait(false);

            if (clone.ExitCode != 0)
            {
                throw new WorkspaceException($"git clone falló: {Tail(clone.Error)}");
            }

            var branch = request.ReadOnly
                ? await DetachAsync(workspace, request.BaseRef, cancellationToken).ConfigureAwait(false)
                : await BranchAsync(workspace, request.BaseRef, request.TaskId, cancellationToken).ConfigureAwait(false);

            var removeRemote = await GitAsync(workspace, ["remote", "remove", "origin"], cancellationToken)
                .ConfigureAwait(false);

            if (removeRemote.ExitCode != 0)
            {
                throw new WorkspaceException($"No se pudo quitar origin: {Tail(removeRemote.Error)}");
            }

            return new WorkspaceLease(workspace, branch, request.ReadOnly, _options.KeepWorkspaces);
        }
        catch
        {
            TryDelete(workspace);
            throw;
        }
    }

    private async Task<string> DetachAsync(string workspace, string baseRef, CancellationToken cancellationToken)
    {
        var result = await GitAsync(workspace, ["checkout", "--quiet", "--detach", baseRef], cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new WorkspaceException($"git checkout {baseRef} falló: {Tail(result.Error)}");
        }

        return $"detached:{baseRef}";
    }

    private async Task<string> BranchAsync(
        string workspace,
        string baseRef,
        string taskId,
        CancellationToken cancellationToken)
    {
        var branch = $"odw/{taskId}";
        var result = await GitAsync(workspace, ["checkout", "--quiet", "-b", branch, baseRef], cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new WorkspaceException($"git checkout -b {branch} falló: {Tail(result.Error)}");
        }

        return branch;
    }

    private async Task<(int ExitCode, string Output, string Error)> GitAsync(
        string? workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.GitExecutable,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.GitTimeout);

        using var process = Process.Start(startInfo)
            ?? throw new WorkspaceException("No se pudo iniciar git.");

        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new WorkspaceException(
                $"git {string.Join(" ", arguments)} excedió {_options.GitTimeout.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)}s.");
        }

        return (process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }

    private async Task EnsureRootAsync(CancellationToken cancellationToken)
    {
        var root = _options.WorkspaceRoot;
        Directory.CreateDirectory(root);

        if (!_options.ApplyAcls || !OperatingSystem.IsWindows())
        {
            return;
        }

        var agent = _options.AgentAccount;
        if (string.IsNullOrWhiteSpace(agent))
        {
            throw new WorkspaceException("ApplyAcls requiere AgentAccount.");
        }

        var service = string.IsNullOrWhiteSpace(_options.ServiceAccount)
            ? System.Security.Principal.WindowsIdentity.GetCurrent().Name
            : _options.ServiceAccount;

        // Sin /T: no degradar los workspaces existentes (que ya tienen su ACL explícita).
        var result = await RunToolAsync(
            "icacls",
            [root, "/inheritance:r",
                "/grant:r", $"{service}:(OI)(CI)F",
                "/grant:r", "*S-1-5-18:(OI)(CI)F",
                "/grant:r", "*S-1-5-32-544:(OI)(CI)F",
                "/grant:r", $"{agent}:(OI)(CI)RX"],
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new WorkspaceException($"icacls de la raíz falló: {Tail(result.Error)}");
        }

        var parent = Path.GetDirectoryName(root);
        if (!string.IsNullOrWhiteSpace(parent)
            && !string.Equals(Path.GetPathRoot(parent), parent, StringComparison.OrdinalIgnoreCase))
        {
            // Git hace stat del padre del workspace; sin lectura de atributos falla con EPERM.
            var parentGrant = await RunToolAsync(
                "icacls",
                [parent, "/grant:r", $"{agent}:(RX)"],
                cancellationToken).ConfigureAwait(false);

            if (parentGrant.ExitCode != 0)
            {
                throw new WorkspaceException($"icacls del padre del workspace falló: {Tail(parentGrant.Error)}");
            }
        }
    }

    private async Task ApplyAclsAsync(string workspace, bool readOnly, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new WorkspaceException("Las ACLs de workspace requieren Windows.");
        }

        var agent = _options.AgentAccount;
        if (string.IsNullOrWhiteSpace(agent))
        {
            throw new WorkspaceException("ApplyAcls requiere AgentAccount.");
        }

        var service = string.IsNullOrWhiteSpace(_options.ServiceAccount)
            ? System.Security.Principal.WindowsIdentity.GetCurrent().Name
            : _options.ServiceAccount;

        var agentRights = readOnly ? "(OI)(CI)RX" : "(OI)(CI)M";

        var inheritance = await RunToolAsync(
            "icacls",
            [workspace, "/inheritance:r"],
            cancellationToken).ConfigureAwait(false);

        if (inheritance.ExitCode != 0)
        {
            throw new WorkspaceException($"icacls /inheritance falló: {Tail(inheritance.Error)}");
        }

        var grant = await RunToolAsync(
            "icacls",
            [workspace, "/grant:r", $"{service}:(OI)(CI)F",
                "/grant:r", "*S-1-5-18:(OI)(CI)F",
                "/grant:r", "*S-1-5-32-544:(OI)(CI)F",
                "/grant:r", $"{agent}:{agentRights}"],
            cancellationToken).ConfigureAwait(false);

        if (grant.ExitCode != 0)
        {
            throw new WorkspaceException($"icacls /grant falló: {Tail(grant.Error)}");
        }
    }

    private async Task<(int ExitCode, string Output, string Error)> RunToolAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.GitTimeout);

        using var process = Process.Start(startInfo)
            ?? throw new WorkspaceException($"No se pudo iniciar {executable}.");

        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);

        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        return (process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }

    private static string Tail(string text)
        => text.Length <= 400 ? text.Trim() : text[^400..].Trim();

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
