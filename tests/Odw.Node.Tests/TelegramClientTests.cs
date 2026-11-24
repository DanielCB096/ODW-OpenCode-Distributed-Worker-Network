using System.Net;
using System.Text;
using System.Text.Json;
using Odw.Approvals;
using Xunit;

namespace Odw.Node.Tests;

public class TelegramClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<(string Method, string Path, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Calls.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            return _responder(request);
        }
    }

    private static HttpResponseMessage Json(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    [Fact]
    public async Task Send_approval_uses_inline_keyboard()
    {
        var handler = new StubHandler(_ => Json("""{"ok":true,"result":{}}"""));
        using var client = new TelegramClient("token", new HttpClient(handler));

        await client.SendApprovalAsync(42, "texto", "approve:id", "deny:id", CancellationToken.None);

        var call = Assert.Single(handler.Calls);
        Assert.EndsWith("/sendMessage", call.Path);
        using var document = JsonDocument.Parse(call.Body);
        Assert.Equal(42, document.RootElement.GetProperty("chat_id").GetInt64());
        Assert.Equal("texto", document.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Get_updates_parses_callbacks()
    {
        var handler = new StubHandler(_ => Json(
            """
            {"ok":true,"result":[
              {"update_id":7,
               "callback_query":{"id":"cb1","from":{"id":123},"message":{"chat":{"id":456}},
                                 "data":"approve:01JAZ5Z8Q4XK3V9C2M1N7P6R8T"}}]}
            """));

        using var client = new TelegramClient("token", new HttpClient(handler));

        var callbacks = await client.GetUpdatesAsync(0, 20, CancellationToken.None);

        var callback = Assert.Single(callbacks);
        Assert.Equal(7, callback.UpdateId);
        Assert.Equal(123, callback.UserId);
        Assert.Equal(456, callback.ChatId);
        Assert.Equal("approve:01JAZ5Z8Q4XK3V9C2M1N7P6R8T", callback.Data);
    }

    [Fact]
    public async Task Api_errors_are_reported()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad", Encoding.UTF8, "text/plain"),
        });

        using var client = new TelegramClient("token", new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendApprovalAsync(1, "x", "a", "d", CancellationToken.None));
    }
}
