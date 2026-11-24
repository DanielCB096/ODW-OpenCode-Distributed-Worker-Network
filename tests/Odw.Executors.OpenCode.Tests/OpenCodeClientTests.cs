using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Odw.Executors.OpenCode.Tests;

public class OpenCodeClientTests
{
    private static (OpenCodeClient Client, StubHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        var client = new OpenCodeClient(
            new OpenCodeConnection(new Uri("http://127.0.0.1:4321"), "opencode", "secret"),
            new HttpClient(handler));
        return (client, handler);
    }

    [Fact]
    public async Task GetInfo_sends_basic_auth_and_parses()
    {
        var (client, handler) = Create((_, _) => StubResponses.Json(
            """{"version":"2.3.2","pid":42,"urls":["http://127.0.0.1:4321"]}"""));

        using (client)
        {
            var info = await client.GetInfoAsync(CancellationToken.None);

            Assert.Equal("2.3.2", info.Version);
            Assert.Equal(42, info.Pid);
            Assert.Contains("http://127.0.0.1:4321", info.Urls);
        }

        var call = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Get, call.Method);
        Assert.Equal("/api/info", call.Path);
        Assert.Equal(
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("opencode:secret")),
            call.Authorization);
    }

    [Fact]
    public async Task CreateSession_sends_location_permissions_and_model()
    {
        var (client, handler) = Create((_, _) => StubResponses.Json(
            """
            {"data":{"id":"ses_1","title":"ODW","location":{"directory":"C:\\ws"},
             "time":{"created":1700000000000,"busy":null}}}
            """));

        using (client)
        {
            var session = await client.CreateSessionAsync(new OpenCodeSessionRequest(
                "C:\\ws",
                "ODW task",
                [new OpenCodePermissionRule("*", "*", OpenCodePermissionEffect.Ask)],
                ModelProvider: "anthropic",
                ModelId: "claude"), CancellationToken.None);

            Assert.Equal("ses_1", session.Id);
            Assert.Equal("C:\\ws", session.Directory);
        }

        var call = Assert.Single(handler.Calls);
        Assert.Equal("/api/session", call.Path);

        using var document = JsonDocument.Parse(call.Body!);
        var root = document.RootElement;
        Assert.Equal("C:\\ws", root.GetProperty("location").GetProperty("directory").GetString());
        Assert.Equal("ODW task", root.GetProperty("title").GetString());

        var rules = root.GetProperty("permissions");
        Assert.Equal(1, rules.GetArrayLength());
        Assert.Equal("ask", rules[0].GetProperty("effect").GetString());

        Assert.Equal("anthropic", root.GetProperty("model").GetProperty("providerID").GetString());
        Assert.Equal("claude", root.GetProperty("model").GetProperty("id").GetString());
    }

    [Fact]
    public async Task SendPrompt_posts_text_and_delivery()
    {
        var (client, handler) = Create((_, _) => StubResponses.Json("{}"));

        using (client)
        {
            await client.SendPromptAsync("ses_1", "hola", "steer", CancellationToken.None);
        }

        var call = Assert.Single(handler.Calls);
        Assert.Equal("/api/session/ses_1/prompt", call.Path);

        using var document = JsonDocument.Parse(call.Body!);
        Assert.Equal("hola", document.RootElement.GetProperty("text").GetString());
        Assert.Equal("steer", document.RootElement.GetProperty("delivery").GetString());
    }

    [Fact]
    public async Task ReplyPermission_maps_decisions()
    {
        var (client, handler) = Create((_, _) => StubResponses.Json("{}"));

        using (client)
        {
            await client.ReplyPermissionAsync(
                "ses_1", "per_1", OpenCodePermissionReply.Once, null, CancellationToken.None);
            await client.ReplyPermissionAsync(
                "ses_1", "per_2", OpenCodePermissionReply.Reject, "no", CancellationToken.None);
        }

        Assert.Equal("/api/session/ses_1/permission/per_1/reply", handler.Calls[0].Path);
        Assert.Contains("\"decision\":\"once\"", handler.Calls[0].Body);
        Assert.Contains("\"decision\":\"reject\"", handler.Calls[1].Body);
        Assert.Contains("\"message\":\"no\"", handler.Calls[1].Body);
    }

    [Fact]
    public async Task Pending_permissions_are_parsed()
    {
        var (client, _) = Create((_, _) => StubResponses.Json(
            """
            {"data":[{"id":"per_1","sessionID":"ses_1","action":"shell",
              "resources":["echo x"],"save":["echo *"],"message":"m"}]}
            """));

        using (client)
        {
            var permissions = await client.GetPendingPermissionsAsync("ses_1", CancellationToken.None);

            var permission = Assert.Single(permissions);
            Assert.Equal("per_1", permission.Id);
            Assert.Equal("shell", permission.Action);
            Assert.Equal(["echo x"], permission.Resources);
            Assert.Equal(["echo *"], permission.Save);
        }
    }

    [Fact]
    public async Task GetSession_returns_null_on_404()
    {
        var (client, _) = Create((_, _) => StubResponses.Json("{}", HttpStatusCode.NotFound));

        using (client)
        {
            Assert.Null(await client.GetSessionAsync("ses_1", CancellationToken.None));
        }
    }

    [Fact]
    public async Task Api_error_throws_with_status()
    {
        var (client, _) = Create((_, _) => StubResponses.Json("nope", HttpStatusCode.Unauthorized));

        using (client)
        {
            var exception = await Assert.ThrowsAsync<OpenCodeApiException>(
                () => client.GetInfoAsync(CancellationToken.None));

            Assert.Equal(401, exception.StatusCode);
            Assert.Contains("/api/info", exception.Message);
        }
    }

    [Fact]
    public void Non_loopback_base_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new OpenCodeClient(
            new OpenCodeConnection(new Uri("http://10.0.0.5:4096"), "opencode", "secret")));
    }

    [Fact]
    public async Task Sse_filter_uses_repeated_types_parameter()
    {
        var (client, handler) = Create((_, _) => StubResponses.EventStream(string.Empty));

        using (client)
        {
            await foreach (var _ in client.SubscribeAsync(
                               ["permission.asked", "session.execution.succeeded"],
                               CancellationToken.None))
            {
            }
        }

        var path = Assert.Single(handler.Calls).Path;
        Assert.Contains("types=permission.asked", path);
        Assert.Contains("types=session.execution.succeeded", path);
        Assert.DoesNotContain("%2C", path);
    }

    [Fact]
    public async Task Sse_stream_parses_data_lines_and_ignores_heartbeat()
    {
        var body =
            """
            data: {"id":"evt_1","created":1700000000000,"type":"session.execution.started","data":{"sessionID":"ses_1"}}

            : heartbeat

            data: {"id":"evt_2","type":"permission.asked","data":{"sessionID":"ses_1","id":"per_1","action":"shell","resources":["echo x"],"save":["echo *"]}}

            """;

        var (client, _) = Create((_, _) => StubResponses.EventStream(body));
        var events = new List<OpenCodeEvent>();

        using (client)
        {
            await foreach (var item in client.SubscribeAsync(null, CancellationToken.None))
            {
                events.Add(item);
            }
        }

        Assert.Equal(2, events.Count);
        Assert.Equal("session.execution.started", events[0].Type);
        Assert.Equal("ses_1", events[0].SessionId);
        Assert.Equal("permission.asked", events[1].Type);
        Assert.Equal("shell", events[1].Data.GetProperty("action").GetString());
    }
}
