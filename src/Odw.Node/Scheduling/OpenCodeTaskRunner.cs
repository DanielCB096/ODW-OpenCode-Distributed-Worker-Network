using System.Globalization;
using System.Text;
using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Executors.OpenCode;
using Odw.Node.Artifacts;
using Odw.Node.Persistence;
using Odw.Sandbox.Windows;
using Odw.Sandbox.Workspaces;

namespace Odw.Node.Scheduling;

public sealed record OpenCodeTaskRunnerOptions
{
    public required string Executable { get; init; }

    public string? PinnedVersion { get; init; } = "2.3.2";

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan ExecutionTimeout { get; init; } = TimeSpan.FromMinutes(45);

    public ToolPermissionHandler? PermissionHandler { get; init; }

    /// <summary>Perfiles de modelo permitidos por este nodo.</summary>
    public IReadOnlyDictionary<string, ModelProfile> ModelProfiles { get; init; } = ModelCatalog.DefaultProfiles;

    public string DefaultModelProfile { get; init; } = ModelCatalog.DefaultProfileName;

    /// <summary>Credenciales del agente restringido para lanzar `opencode serve`.</summary>
    public RunAsCredentials? RunAs { get; init; }

    /// <summary>URL del proxy de modelo (con credenciales) para el entorno del agente.</summary>
    public string? ModelProxyUrl { get; init; }

    /// <summary>Exige WFP activo y proxy para ejecutar tareas con red deshabilitada.</summary>
    public bool RequireNetworkIsolation { get; init; }

    /// <summary>Comprueba el aislamiento; por defecto consulta WFP. Inyectable en pruebas.</summary>
    public Func<bool>? NetworkIsolationProbe { get; init; }

    /// <summary>Periodo del watchdog de WFP durante la tarea.</summary>
    public TimeSpan NetworkWatchdogInterval { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Raíz de perfiles por tarea; si falta se usa el perfil real del agente.</summary>
    public string? AgentRuntimeRoot { get; init; }

    /// <summary>Plantilla de opencode.json copiada a cada perfil de tarea.</summary>
    public string? AgentConfigTemplate { get; init; }

    public string AgentAccount { get; init; } = "odw-agent";

    public string? ServiceAccount { get; init; }

    /// <summary>Raíz de artefactos descargables; null no guarda artefactos.</summary>
    public string? ArtifactsRoot { get; init; }

    /// <summary>Recibe stderr de OpenCode para diagnóstico.</summary>
    public Action<string>? ServerLog { get; init; }

    /// <summary>Permite construir un handler por tarea (política + contexto del contrato).</summary>
    public Func<ExecutionRequest, ToolPermissionHandler?>? PermissionHandlerFactory { get; init; }

    /// <summary>En Windows, asigna el servidor de OpenCode a un Job Object con los límites de la tarea.</summary>
    public bool UseJobObject { get; init; } = true;

    /// <summary>Espera para que el stream SSE quede suscrito antes de enviar el prompt.</summary>
    public TimeSpan SubscribeDelay { get; init; } = TimeSpan.FromMilliseconds(300);
}

/// <summary>
/// Runner real: workspace preparado → OpenCode bajo Job Object → sesión, stream y prompt →
/// resultado. Sin usuario restringido ni ACL todavía (H3); el límite de recursos sí se aplica.
/// </summary>
public sealed class OpenCodeTaskRunner : ITaskExecutionRunner
{
    private readonly OpenCodeTaskRunnerOptions _options;

    public OpenCodeTaskRunner(OpenCodeTaskRunnerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Executable);
    }

    public async Task<ExecutionResult> RunAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ModelProfileResolver.TryResolve(
                request.Contract.Executor.ModelProfile,
                request.Contract.Executor.ModelHint,
                _options.DefaultModelProfile,
                _options.ModelProfiles,
                out var selectedModel,
                out var modelCode,
                out var modelDetail))
        {
            return new ExecutionResult(
                ExecutorOutcomeKind.Failed,
                ResultJson(
                    "failed",
                    $"{modelCode ?? "MODEL_NOT_ALLOWED"}: {modelDetail ?? "modelo no permitido."}",
                    request.WorkspaceBranch,
                    review: false));
        }

        var model = selectedModel
                    ?? throw new InvalidOperationException("El resolvedor de modelo no devolvió un perfil.");

        if (_options.RequireNetworkIsolation)
        {
            if (request.Contract.Limits.Network != NetworkAccess.None)
            {
                return new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson(
                        "failed",
                        "NETWORK_ALLOW_UNSUPPORTED: con aislamiento WFP por usuario no se puede "
                        + "abrir la red para una sola tarea; la tarea no se ejecuta.",
                        request.WorkspaceBranch,
                        review: false));
            }

            if (_options.ModelProxyUrl is null
                || !(_options.NetworkIsolationProbe?.Invoke()
                     ?? (OperatingSystem.IsWindows() && WfpNetworkPolicy.IsInstalled())))
            {
                return new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson(
                        "failed",
                        "NETWORK_UNENFORCED: falta el proxy del modelo o los filtros WFP; "
                        + "la tarea con network=none no se ejecuta.",
                        request.WorkspaceBranch,
                        review: false));
            }
        }

        var serverEnvironment = _options.ModelProxyUrl is null
            ? null
            : new Dictionary<string, string>
            {
                ["HTTPS_PROXY"] = _options.ModelProxyUrl,
                ["HTTP_PROXY"] = _options.ModelProxyUrl,
                ["NO_PROXY"] = "127.0.0.1,localhost",
            };

        AgentRuntime? runtime = null;
        JobObject? job = null;

        try
        {
            try
            {
                WorkspaceGuard.EnsureNoReparsePoints(request.WorkspacePath);
            }
            catch (WorkspaceGuardException exception)
            {
                return new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson("failed", exception.Message, request.WorkspaceBranch, review: false));
            }

            if (_options.AgentRuntimeRoot is not null)
            {
                if (string.IsNullOrWhiteSpace(_options.AgentConfigTemplate))
                {
                    throw new InvalidOperationException(
                        "sandbox.runtime_root requiere sandbox.agent_config_path.");
                }

                runtime = AgentRuntime.Create(request.Snapshot.TaskId, new AgentRuntimeOptions
                {
                    Root = _options.AgentRuntimeRoot,
                    ConfigTemplate = _options.AgentConfigTemplate,
                    AgentAccount = _options.AgentAccount,
                    ServiceAccount = _options.ServiceAccount,
                });
            }

            if (_options.UseJobObject && OperatingSystem.IsWindows())
            {
                job = JobObject.Create(
                    JobLimits.FromTask(
                        request.Contract.Limits.MaxMemoryGb,
                        request.Contract.Limits.MaxProcesses,
                        request.Contract.Limits.MaxMinutes),
                    $"odw-{request.Snapshot.TaskId}");
            }

            await using var server = await OpenCodeServer.StartAsync(new OpenCodeServerOptions
            {
                Executable = _options.Executable,
                WorkingDirectory = request.WorkspacePath,
                PinnedVersion = _options.PinnedVersion,
                StartupTimeout = _options.StartupTimeout,
                RunAs = _options.RunAs,
                RuntimeRoot = runtime?.Path,
                Log = _options.ServerLog,
                Environment = serverEnvironment,
                AttachProcess = job is null
                    ? null
                    : process =>
                    {
                        job!.Assign(process);
                        return job;
                    },
            }, cancellationToken).ConfigureAwait(false);

            using var client = new OpenCodeClient(server.Connection);
            var handler = _options.PermissionHandlerFactory?.Invoke(request) ?? _options.PermissionHandler;
            var executor = new OpenCodeExecutor(client, handler, _options.TimeProvider);

            var session = await executor.StartAsync(new ExecutorStartRequest(
                request.Snapshot.TaskId,
                request.WorkspacePath,
                request.Contract.Instructions,
                model.QualifiedName,
                new ExecutorLimits(
                    request.Contract.Limits.MaxMinutes,
                    request.Contract.Limits.MaxMemoryGb,
                    request.Contract.Limits.MaxProcesses,
                    request.Contract.Limits.Network == NetworkAccess.Allow,
                    request.Contract.Limits.MaxCostUsd),
                ToolPolicy.AskAll,
                new Dictionary<string, string>()), cancellationToken).ConfigureAwait(false);

            using var execution = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            execution.CancelAfter(_options.ExecutionTimeout);

            await using var watchdog = _options.RequireNetworkIsolation
                ? new NetworkIsolationWatchdog(
                    () => _options.NetworkIsolationProbe?.Invoke()
                          ?? (OperatingSystem.IsWindows() && WfpNetworkPolicy.IsInstalled()),
                    _options.NetworkWatchdogInterval,
                    execution.Cancel)
                : null;

            var events = new List<ExecutorEvent>();
            var pump = Task.Run(async () =>
            {
                await foreach (var item in executor.StreamAsync(session.SessionId, execution.Token)
                                   .ConfigureAwait(false))
                {
                    events.Add(item);
                }
            }, execution.Token);

            if (_options.SubscribeDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.SubscribeDelay, execution.Token).ConfigureAwait(false);
            }

            await executor.SendAsync(
                session.SessionId,
                request.Contract.Instructions,
                MessageDelivery.Queue,
                execution.Token).ConfigureAwait(false);

            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (watchdog?.Lost == true)
                {
                    return new ExecutionResult(
                        ExecutorOutcomeKind.Failed,
                        ResultJson(
                            "failed",
                            "NETWORK_UNENFORCED: los filtros WFP dejaron de estar activos durante "
                            + "la tarea; se canceló la ejecución.",
                            request.WorkspaceBranch,
                            review: false));
                }

                return new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson("failed", "TIMEOUT: la ejecución excedió el tiempo.", request.WorkspaceBranch, review: false));
            }

            var terminal = events.LastOrDefault(item => item.Kind is
                ExecutorEventKind.Completed or ExecutorEventKind.Failed or ExecutorEventKind.Interrupted);

            if (terminal?.Kind == ExecutorEventKind.Completed)
            {
                try
                {
                    WorkspaceGuard.EnsureNoReparsePoints(request.WorkspacePath);
                }
                catch (WorkspaceGuardException exception)
                {
                    return new ExecutionResult(
                        ExecutorOutcomeKind.Succeeded,
                        ResultJson(
                            "review_required",
                            exception.Message,
                            request.WorkspaceBranch,
                            review: true),
                        RequiresReview: true);
                }
            }

            var failedTools = events.Any(item => item.Kind == ExecutorEventKind.ToolFailed);
            var summary = await SummaryAsync(client, session.SessionId, cancellationToken).ConfigureAwait(false);

            var changedPaths = Array.Empty<string>();
            string? commit = null;
            string? diff = null;
            string? fullDiff = null;
            var diffTruncated = false;
            string? reviewReason = null;

            if (terminal?.Kind == ExecutorEventKind.Completed)
            {
                try
                {
                    var changes = await WorkspaceChanges.CaptureAsync(
                        request.WorkspacePath,
                        request.Contract.Workspace.Mode == WorkspaceMode.ReadWriteWorktree,
                        request.Snapshot.TaskId,
                        cancellationToken).ConfigureAwait(false);

                    changedPaths = changes.Paths.ToArray();
                    commit = changes.Commit;
                    diff = changes.Diff;
                    fullDiff = changes.FullDiff;
                    diffTruncated = changes.DiffTruncated;

                    var violations = FindPathViolations(request.Contract, changedPaths);
                    if (violations.Count > 0)
                    {
                        reviewReason = "Cambios fuera de allowed_paths/denied_paths: "
                                       + string.Join(", ", violations);
                        summary = string.IsNullOrWhiteSpace(summary)
                            ? reviewReason
                            : summary + "\n\n" + reviewReason;
                    }
                }
                catch (Exception exception) when (exception is WorkspaceException or IOException)
                {
                    reviewReason = $"CAPTURE_FAILED: {exception.Message}";
                    summary = string.IsNullOrWhiteSpace(summary)
                        ? reviewReason
                        : summary + "\n\n" + reviewReason;
                }
            }

            var artifacts = new List<string>();
            if (terminal?.Kind == ExecutorEventKind.Completed && _options.ArtifactsRoot is not null)
            {
                try
                {
                    var store = new ArtifactStore(_options.ArtifactsRoot);

                    if (!string.IsNullOrWhiteSpace(summary))
                    {
                        var saved = await store.SaveAsync(
                            request.Snapshot.TaskId,
                            "summary.md",
                            Encoding.UTF8.GetBytes(summary),
                            "text/markdown",
                            CancellationToken.None).ConfigureAwait(false);
                        artifacts.Add(saved.Name);
                    }

                    var patch = fullDiff ?? diff;
                    if (!string.IsNullOrWhiteSpace(patch))
                    {
                        var saved = await store.SaveAsync(
                            request.Snapshot.TaskId,
                            "diff.patch",
                            Encoding.UTF8.GetBytes(patch),
                            "text/x-diff",
                            CancellationToken.None).ConfigureAwait(false);
                        artifacts.Add(saved.Name);
                    }
                }
                catch (Exception exception) when (exception is ArtifactException or IOException or UnauthorizedAccessException)
                {
                    reviewReason = $"ARTIFACT_FAILED: {exception.Message}";
                    summary = string.IsNullOrWhiteSpace(summary)
                        ? reviewReason
                        : summary + "\n\n" + reviewReason;
                }
            }

            var review = failedTools || reviewReason is not null;
            var costUsd = LastCost(events);

            return terminal?.Kind switch
            {
                ExecutorEventKind.Completed => new ExecutionResult(
                    ExecutorOutcomeKind.Succeeded,
                    ResultJson(
                        review ? "review_required" : "succeeded",
                        summary,
                        request.WorkspaceBranch,
                        review,
                        changedPaths,
                        commit,
                        diff,
                        diffTruncated,
                        reviewReason,
                        model,
                        costUsd,
                        artifacts),
                    review),
                ExecutorEventKind.Interrupted => new ExecutionResult(
                    ExecutorOutcomeKind.Interrupted,
                    ResultJson(
                        "cancelled", summary, request.WorkspaceBranch, review: false,
                        model: model, costUsd: costUsd)),
                _ => new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson(
                        "failed", terminal?.Detail ?? summary, request.WorkspaceBranch, review: false,
                        model: model, costUsd: costUsd)),
            };
        }
        finally
        {
            job?.Dispose();

            if (runtime is not null)
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static IReadOnlyList<string> FindPathViolations(
        TaskRequest contract,
        IReadOnlyList<string> changedPaths)
    {
        if (changedPaths.Count == 0)
        {
            return [];
        }

        var allowed = contract.Workspace.AllowedPaths
            .Select(NormalizePath)
            .Where(path => path.Length > 0)
            .ToArray();
        var denied = contract.Workspace.DeniedPaths
            .Select(NormalizePath)
            .Where(path => path.Length > 0)
            .ToArray();

        var violations = new List<string>();

        foreach (var path in changedPaths)
        {
            var value = NormalizePath(path);
            var isDenied = denied.Any(prefix => IsUnder(value, prefix));
            var isAllowed = allowed.Length == 0 || allowed.Any(prefix => IsUnder(value, prefix));

            if (isDenied || !isAllowed)
            {
                violations.Add(value);
            }
        }

        return violations;
    }

    private static string NormalizePath(string path)
    {
        var value = path.Replace('\\', '/').Trim();

        while (value.StartsWith("./", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return value.TrimStart('/');
    }

    private static bool IsUnder(string path, string prefix)
    {
        var root = prefix.TrimEnd('/');
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
               || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> SummaryAsync(
        OpenCodeClient client,
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = await client.GetMessagesAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var text = messages
                .Where(message => message.Type == "assistant")
                .SelectMany(message => message.TextParts)
                .LastOrDefault();

            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Length <= 8192 ? text : text[..8192];
        }
        catch (OpenCodeApiException)
        {
            return string.Empty;
        }
    }

    private static decimal? LastCost(IReadOnlyList<ExecutorEvent> events)
    {
        var detail = events.LastOrDefault(item => item.Kind == ExecutorEventKind.UsageUpdated)?.Detail;
        const string marker = "cost_usd=";
        if (detail is null)
        {
            return null;
        }

        var index = detail.IndexOf(marker, StringComparison.Ordinal);
        return index >= 0
               && decimal.TryParse(
                   detail[(index + marker.Length)..],
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out var value)
            ? value
            : null;
    }

    private static string ResultJson(
        string outcome,
        string summary,
        string branch,
        bool review,
        IReadOnlyList<string>? changedPaths = null,
        string? commit = null,
        string? diff = null,
        bool diffTruncated = false,
        string? reviewReason = null,
        ModelProfile? model = null,
        decimal? costUsd = null,
        IReadOnlyList<string>? artifacts = null)
        => PersistenceJson.Serialize(new
        {
            outcome,
            summary_md = summary,
            changed_paths = changedPaths ?? Array.Empty<string>(),
            branch = string.IsNullOrWhiteSpace(branch) ? null : branch,
            commit,
            diff,
            diff_truncated = diffTruncated,
            review_reason = reviewReason,
            artifacts = artifacts ?? Array.Empty<string>(),
            requires_review = review,
            model_profile = model?.Name,
            model_provider = model?.Provider,
            model_id = model?.Model,
            cost_usd = costUsd,
        });
}
