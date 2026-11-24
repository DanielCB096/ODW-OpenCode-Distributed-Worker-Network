using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Odw.Mcp.Tools;
using Xunit;

namespace Odw.Mcp.Tests;

public sealed class McpHttpFixture : IAsyncLifetime
{
    public const string Token = "odw-test-token-0123456789abcdef";

    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var peers = new FakePeers();
        peers.Peers.Add(McpTestData.Peer("PC-B"));

        var tasks = new FakeTasks();
        tasks.Results[McpTestData.TaskId] = McpTestData.Result();

        var server = new McpServer(
            PeerTools.Create(peers).Concat(TaskTools.Create(tasks)));

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        _app = builder.Build();
        _app.MapOdwMcp(server, new McpOptions { Token = Token });

        await _app.StartAsync();

        var addresses = _app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses;

        Client = new HttpClient { BaseAddress = new Uri(addresses.First()) };
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}

public class McpHttpTests : IClassFixture<McpHttpFixture>
{
    private readonly McpHttpFixture _fixture;

    public McpHttpTests(McpHttpFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Requests_without_token_are_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = Json("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}"),
        };

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_token_is_rejected()
    {
        using var request = Authorized(Json("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "otro-token");

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Initialize_succeeds_with_token()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = 1,
            ["method"] = "initialize",
            ["params"] = new JsonObject { ["protocolVersion"] = "2025-06-18" },
        };

        using var response = await _fixture.Client.SendAsync(Authorized(Json(body.ToJsonString())));
        response.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("2025-06-18", json["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task Notification_returns_no_content()
    {
        var body = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized",
        };

        using var response = await _fixture.Client.SendAsync(Authorized(Json(body.ToJsonString())));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_json_returns_parse_error()
    {
        using var response = await _fixture.Client.SendAsync(Authorized(Json("{")));

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal(JsonRpc.ErrorCodes.ParseError, json["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task Wrong_content_type_is_rejected()
    {
        using var request = Authorized(new StringContent("{}", Encoding.UTF8, "text/plain"));

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Get_is_not_allowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", McpHttpFixture.Token);

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static HttpRequestMessage Authorized(HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", McpHttpFixture.Token);
        return request;
    }

    private static StringContent Json(string body)
        => new(body, Encoding.UTF8, "application/json");
}
