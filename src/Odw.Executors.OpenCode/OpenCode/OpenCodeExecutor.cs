using System.Text.Json;
using Odw.Core.Contracts;
using Odw.Core.Model;

namespace Odw.Executors.OpenCode;

public enum ToolPermissionDecision
{
    AllowOnce,
    Reject,
}

/// <summary>
/// Decide un permiso de herramienta. La implementación con política/aprobación humana vive
/// fuera del ejecutor; si no hay handler, se rechaza.
/// </summary>
public delegate Task<ToolPermissionDecision> ToolPermissionHandler(
    OpenCodePermissionRequest request,
    CancellationToken cancellationToken);

/// <summary>
/// Ejecutor sobre una conexión OpenCode ya iniciada. Orquesta sesión, prompt, eventos y
/// permisos; el ciclo de vida del proceso servidor pertenece al host (OpenCodeServer).
/// Límites de CPU/RAM y usuario restringido son H3 (Job Objects), no están aquí.
/// </summary>
public sealed class OpenCodeExecutor : IExecutor
{
    private static readonly string[] WatchedTypes =
    [
        "permission.asked",
        "session.execution.started",
        "session.execution.succeeded",
        "session.execution.failed",
        "session.execution.interrupted",
        "session.tool.called",
        "session.tool.failed",
        "session.text.delta",
        "session.usage.updated",
    ];

    private readonly IOpenCodeClient _client;
    private readonly ToolPermissionHandler? _permissionHandler;
    private readonly TimeProvider _timeProvider;

    public OpenCodeExecutor(
        IOpenCodeClient client,
        ToolPermissionHandler? permissionHandler = null,
        TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _permissionHandler = permissionHandler;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ExecutorSession> StartAsync(
        ExecutorStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var info = await _client.GetInfoAsync(cancellationToken).ConfigureAwait(false);
        var session = await _client
            .CreateSessionAsync(BuildSessionRequest(request), cancellationToken)
            .ConfigureAwait(false);

        return new ExecutorSession(
            session.Id,
            info.Pid,
            _client.BaseAddress.ToString().TrimEnd('/'),
            _timeProvider.GetUtcNow());
    }

    public Task SendAsync(
        string sessionId,
        string text,
        MessageDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return _client.SendPromptAsync(
            sessionId,
            text,
            delivery == MessageDelivery.Steer ? "steer" : "queue",
            cancellationToken);
    }

    public async IAsyncEnumerable<ExecutorEvent> StreamAsync(
        string sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var terminal = false;

        await foreach (var item in _client.SubscribeAsync(WatchedTypes, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            if (item.SessionId is not null && !string.Equals(item.SessionId, sessionId, StringComparison.Ordinal))
            {
                continue;
            }

            var at = item.CreatedAt ?? _timeProvider.GetUtcNow();

            switch (item.Type)
            {
                case "permission.asked":
                {
                    var permission = ParsePermission(item);
                    if (permission is null)
                    {
                        break;
                    }

                    var decision = await BrokerAsync(permission, cancellationToken).ConfigureAwait(false);

                    yield return new ExecutorEvent(
                        ExecutorEventKind.PermissionAsked,
                        permission.Action,
                        decision == ToolPermissionDecision.AllowOnce ? "allowed_once" : "rejected",
                        at);
                    break;
                }

                case "session.execution.started":
                    yield return new ExecutorEvent(ExecutorEventKind.Started, At: at);
                    break;

                case "session.tool.called":
                    yield return new ExecutorEvent(
                        ExecutorEventKind.ToolCalled,
                        ToolName(item.Data),
                        OpenCodeJson.String(item.Data, "id"),
                        at);
                    break;

                case "session.tool.failed":
                    yield return new ExecutorEvent(
                        ExecutorEventKind.ToolFailed,
                        ToolName(item.Data),
                        ToolError(item.Data) ?? OpenCodeJson.String(item.Data, "id"),
                        at);
                    break;

                case "session.text.delta":
                    yield return new ExecutorEvent(
                        ExecutorEventKind.TextDelta,
                        Detail: OpenCodeJson.String(item.Data, "delta"),
                        At: at);
                    break;

                case "session.usage.updated":
                    yield return new ExecutorEvent(
                        ExecutorEventKind.UsageUpdated,
                        Detail: Usage(item.Data),
                        At: at);
                    break;

                case "session.execution.succeeded":
                    terminal = true;
                    yield return new ExecutorEvent(ExecutorEventKind.Completed, At: at);
                    break;

                case "session.execution.failed":
                    terminal = true;
                    yield return new ExecutorEvent(
                        ExecutorEventKind.Failed,
                        Detail: ToolError(item.Data) ?? "execution_failed",
                        At: at);
                    break;

                case "session.execution.interrupted":
                    terminal = true;
                    yield return new ExecutorEvent(ExecutorEventKind.Interrupted, At: at);
                    break;
            }

            if (terminal)
            {
                yield break;
            }
        }
    }

    public async Task<ExecutorSnapshot> GetSnapshotAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _client.GetSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return new ExecutorSnapshot("missing", null, null, null, true);
        }

        return new ExecutorSnapshot(
            session.IsBusy ? "running" : "idle",
            null,
            null,
            null,
            !session.IsBusy);
    }

    public async Task<ExecutorOutcome> WaitAsync(
        string sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await foreach (var item in StreamAsync(sessionId, timeoutSource.Token)
                               .WithCancellation(timeoutSource.Token)
                               .ConfigureAwait(false))
            {
                switch (item.Kind)
                {
                    case ExecutorEventKind.Completed:
                        return new ExecutorOutcome(ExecutorOutcomeKind.Succeeded);
                    case ExecutorEventKind.Failed:
                        return new ExecutorOutcome(ExecutorOutcomeKind.Failed, "EXECUTION_FAILED", item.Detail);
                    case ExecutorEventKind.Interrupted:
                        return new ExecutorOutcome(ExecutorOutcomeKind.Interrupted);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ExecutorOutcome(ExecutorOutcomeKind.Failed, "TIMEOUT", "La ejecución excedió el tiempo.");
        }

        return new ExecutorOutcome(ExecutorOutcomeKind.Failed, "STREAM_ENDED", "El stream terminó sin evento terminal.");
    }

    public Task AbortAsync(string sessionId, TimeSpan grace, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return _client.InterruptAsync(sessionId, cancellationToken);
    }

    public Task StopAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return _client.InterruptAsync(sessionId, cancellationToken);
    }

    private async Task<ToolPermissionDecision> BrokerAsync(
        OpenCodePermissionRequest permission,
        CancellationToken cancellationToken)
    {
        if (_permissionHandler is null)
        {
            await RejectAsync(permission, cancellationToken).ConfigureAwait(false);
            return ToolPermissionDecision.Reject;
        }

        ToolPermissionDecision decision;
        try
        {
            decision = await _permissionHandler(permission, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            decision = ToolPermissionDecision.Reject;
        }

        if (decision == ToolPermissionDecision.AllowOnce)
        {
            await _client.ReplyPermissionAsync(
                permission.SessionId,
                permission.Id,
                OpenCodePermissionReply.Once,
                null,
                cancellationToken).ConfigureAwait(false);

            return decision;
        }

        await RejectAsync(permission, cancellationToken).ConfigureAwait(false);
        return ToolPermissionDecision.Reject;
    }

    private Task RejectAsync(OpenCodePermissionRequest permission, CancellationToken cancellationToken)
        => _client.ReplyPermissionAsync(
            permission.SessionId,
            permission.Id,
            OpenCodePermissionReply.Reject,
            "Denegado por la política de la tarea.",
            cancellationToken);

    private static OpenCodeSessionRequest BuildSessionRequest(ExecutorStartRequest request)
    {
        var rules = request.Policy.Rules
            .Select(rule => new OpenCodePermissionRule(
                rule.Action,
                rule.Resource,
                rule.Effect switch
                {
                    ToolEffect.Allow => OpenCodePermissionEffect.Allow,
                    ToolEffect.Deny => OpenCodePermissionEffect.Deny,
                    _ => OpenCodePermissionEffect.Ask,
                }))
            .ToArray();

        var (provider, model) = SplitModelHint(request.ModelHint);

        return new OpenCodeSessionRequest(
            request.WorkspacePath,
            $"ODW {request.TaskId}",
            rules.Length == 0 ? null : rules,
            Agent: null,
            ModelProvider: provider,
            ModelId: model);
    }

    private static (string? Provider, string? Model) SplitModelHint(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
        {
            return (null, null);
        }

        var separator = hint.IndexOf('/');
        return separator <= 0 || separator == hint.Length - 1
            ? (null, null)
            : (hint[..separator], hint[(separator + 1)..]);
    }

    private OpenCodePermissionRequest? ParsePermission(OpenCodeEvent item)
    {
        var data = item.Data;
        var id = OpenCodeJson.String(data, "id");
        var action = OpenCodeJson.String(data, "action");

        if (id is null || action is null)
        {
            return null;
        }

        var sessionId = item.SessionId ?? OpenCodeJson.String(data, "sessionID");
        if (sessionId is null)
        {
            return null;
        }

        return new OpenCodePermissionRequest(
            id,
            sessionId,
            action,
            OpenCodeJson.Strings(data, "resources"),
            OpenCodeJson.Strings(data, "save"),
            OpenCodeJson.String(data, "message"));
    }

    private static string? ToolName(JsonElement data)
        => OpenCodeJson.String(data, "name")
           ?? OpenCodeJson.NestedString(data, "native", "name")
           ?? OpenCodeJson.String(data, "tool");

    private static string? ToolError(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("error", out var error))
        {
            return null;
        }

        if (error.ValueKind == JsonValueKind.String)
        {
            return error.GetString();
        }

        return OpenCodeJson.String(error, "message")
               ?? OpenCodeJson.String(error, "name");
    }

    private static string Usage(JsonElement data)
    {
        var cost = OpenCodeJson.Double(data, "cost");
        return cost is null ? "usage" : $"cost_usd={cost.Value:0.####}";
    }
}
