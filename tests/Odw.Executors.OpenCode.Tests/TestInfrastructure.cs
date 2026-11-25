using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Odw.Executors.OpenCode.Tests;

internal sealed record RecordedCall(HttpMethod Method, string Path, string? Body, string? Authorization);

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _responder;

    public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<RecordedCall> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Calls.Add(new RecordedCall(
            request.Method,
            request.RequestUri!.PathAndQuery,
            body,
            request.Headers.Authorization?.ToString()));

        return _responder(request, cancellationToken);
    }
}

internal static class StubResponses
{
    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage EventStream(string body)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
        };
}

internal sealed class FakeOpenCodeClient : IOpenCodeClient
{
    public Uri BaseAddress { get; } = new("http://127.0.0.1:9999/");

    public OpenCodeServerInfo Info { get; set; } = new("2.3.2", 4242, ["http://127.0.0.1:9999"]);

    public OpenCodeSession Session { get; set; } = new(
        "ses_test",
        "ODW",
        "C:\\ws",
        DateTimeOffset.UnixEpoch,
        null,
        null);

    public OpenCodeSessionRequest? LastSessionRequest { get; private set; }

    public List<(string SessionId, string Text, string Delivery)> Prompts { get; } = [];

    public List<(string SessionId, string PermissionId, OpenCodePermissionReply Reply)> Replies { get; } = [];

    public List<OpenCodeEvent> Events { get; } = [];

    public List<string> Interrupts { get; } = [];

    public bool BlockAfterEvents { get; set; }

    public bool SessionMissing { get; set; }

    public Task<OpenCodeServerInfo> GetInfoAsync(CancellationToken cancellationToken)
        => Task.FromResult(Info);

    public Task<OpenCodeSession> CreateSessionAsync(
        OpenCodeSessionRequest request,
        CancellationToken cancellationToken)
    {
        LastSessionRequest = request;
        return Task.FromResult(Session);
    }

    public Task SendPromptAsync(
        string sessionId,
        string text,
        string delivery,
        CancellationToken cancellationToken)
    {
        Prompts.Add((sessionId, text, delivery));
        return Task.CompletedTask;
    }

    public Task<OpenCodeSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
        => Task.FromResult<OpenCodeSession?>(SessionMissing ? null : Session);

    public Task<IReadOnlyList<OpenCodeMessage>> GetMessagesAsync(
        string sessionId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OpenCodeMessage>>([]);

    public Task<IReadOnlyList<OpenCodePermissionRequest>> GetPendingPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<OpenCodePermissionRequest>>([]);

    public Task ReplyPermissionAsync(
        string sessionId,
        string permissionId,
        OpenCodePermissionReply reply,
        string? message,
        CancellationToken cancellationToken)
    {
        Replies.Add((sessionId, permissionId, reply));
        return Task.CompletedTask;
    }

    public Task InterruptAsync(string sessionId, CancellationToken cancellationToken)
    {
        Interrupts.Add(sessionId);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<OpenCodeEvent> SubscribeAsync(
        IReadOnlyCollection<string>? types,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var item in Events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }

        if (BlockAfterEvents)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}

internal static class EventFactory
{
    public static OpenCodeEvent Create(string type, string? sessionId, string dataJson)
    {
        using var data = JsonDocument.Parse(dataJson);
        using var raw = JsonDocument.Parse($$"""
            {"id":"evt_1","created":1700000000000,"type":"{{type}}","data":{{dataJson}}}
            """);

        return new OpenCodeEvent(
            "evt_1",
            type,
            DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
            sessionId,
            data.RootElement.Clone(),
            raw.RootElement.Clone());
    }
}

internal static class TestModels
{
    public static string FreeOrFallback =>
        Environment.GetEnvironmentVariable("ODW_TEST_MODEL") ?? "opencode/deepseek-v4.1-flash-free";
}

internal static class TestRequests
{
    public static Odw.Core.Contracts.ExecutorStartRequest Start(
        Odw.Core.Contracts.ToolPolicy? policy = null,
        string? modelHint = null)
        => new(
            "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
            "C:\\ws",
            "Analiza y ejecuta las pruebas.",
            modelHint,
            new Odw.Core.Contracts.ExecutorLimits(45, 12, 64, false, 5m),
            policy ?? Odw.Core.Contracts.ToolPolicy.AskAll,
            new Dictionary<string, string>());
}
