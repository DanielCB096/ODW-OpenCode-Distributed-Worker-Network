using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Executors.OpenCode;
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
                request.Contract.Executor.ModelHint,
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

            return terminal?.Kind switch
            {
                ExecutorEventKind.Completed => new ExecutionResult(
                    ExecutorOutcomeKind.Succeeded,
                    ResultJson(
                        failedTools ? "review_required" : "succeeded",
                        summary,
                        request.WorkspaceBranch,
                        failedTools),
                    failedTools),
                ExecutorEventKind.Interrupted => new ExecutionResult(
                    ExecutorOutcomeKind.Interrupted,
                    ResultJson("cancelled", summary, request.WorkspaceBranch, review: false)),
                _ => new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson("failed", terminal?.Detail ?? summary, request.WorkspaceBranch, review: false)),
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

    private static string ResultJson(string outcome, string summary, string branch, bool review)
        => PersistenceJson.Serialize(new
        {
            outcome,
            summary_md = summary,
            changed_paths = Array.Empty<string>(),
            branch = string.IsNullOrWhiteSpace(branch) ? null : branch,
            commit = (string?)null,
            requires_review = review,
        });
}
