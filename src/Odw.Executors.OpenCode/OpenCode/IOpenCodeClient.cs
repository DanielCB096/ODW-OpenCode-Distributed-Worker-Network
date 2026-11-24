using System.Text.Json;

namespace Odw.Executors.OpenCode;

/// <summary>Evento del stream SSE de OpenCode (`data:` con `type` dentro del JSON).</summary>
public sealed record OpenCodeEvent(
    string Id,
    string Type,
    DateTimeOffset? CreatedAt,
    string? SessionId,
    JsonElement Data,
    JsonElement Raw);

public interface IOpenCodeClient
{
    Uri BaseAddress { get; }

    Task<OpenCodeServerInfo> GetInfoAsync(CancellationToken cancellationToken);

    Task<OpenCodeSession> CreateSessionAsync(OpenCodeSessionRequest request, CancellationToken cancellationToken);

    Task SendPromptAsync(string sessionId, string text, string delivery, CancellationToken cancellationToken);

    Task<OpenCodeSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OpenCodeMessage>> GetMessagesAsync(string sessionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OpenCodePermissionRequest>> GetPendingPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken);

    Task ReplyPermissionAsync(
        string sessionId,
        string permissionId,
        OpenCodePermissionReply reply,
        string? message,
        CancellationToken cancellationToken);

    Task InterruptAsync(string sessionId, CancellationToken cancellationToken);

    IAsyncEnumerable<OpenCodeEvent> SubscribeAsync(
        IReadOnlyCollection<string>? types,
        CancellationToken cancellationToken);
}
