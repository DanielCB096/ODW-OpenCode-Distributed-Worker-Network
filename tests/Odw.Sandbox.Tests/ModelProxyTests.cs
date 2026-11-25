using System.Net;
using System.Net.Sockets;
using System.Text;
using Odw.Sandbox.Network;
using Xunit;

namespace Odw.Sandbox.Tests;

public class ModelProxyTests
{
    [Fact]
    public async Task Rejects_request_without_proxy_authentication()
    {
        await using var proxy = ModelProxy.Start(new ModelProxyOptions
        {
            AllowedHosts = ["test.local"],
            AllowedPorts = [443],
            AuthToken = "secret",
        });

        var response = await SendAsync(proxy, "test.local:443", auth: null);

        Assert.StartsWith("HTTP/1.1 407", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_host_not_in_allowlist()
    {
        await using var proxy = ModelProxy.Start(new ModelProxyOptions
        {
            AllowedHosts = ["test.local"],
            AllowedPorts = [443],
            AuthToken = "secret",
        });

        var response = await SendAsync(proxy, "evil.local:443");

        Assert.StartsWith("HTTP/1.1 403", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_private_address_even_if_host_is_allowed()
    {
        await using var proxy = ModelProxy.Start(new ModelProxyOptions
        {
            AllowedHosts = ["internal.local"],
            AllowedPorts = [443],
            AuthToken = "secret",
            Resolver = (_, _) => Task.FromResult(new[] { IPAddress.Parse("10.1.2.3") }),
        });

        var response = await SendAsync(proxy, "internal.local:443");

        Assert.StartsWith("HTTP/1.1 403", response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tunnels_bytes_to_the_allowed_upstream()
    {
        var (upstreamPort, upstream) = StartEchoUpstream();

        await using var proxy = ModelProxy.Start(new ModelProxyOptions
        {
            AllowedHosts = ["test.local"],
            AllowedPorts = [upstreamPort],
            AuthToken = "secret",
            AddressFilter = _ => true,
            Resolver = (_, _) => Task.FromResult(new[] { IPAddress.Loopback }),
        });

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port);
        using var stream = client.GetStream();

        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            $"CONNECT test.local:{upstreamPort} HTTP/1.1\r\n"
            + $"Host: test.local:{upstreamPort}\r\n"
            + $"Proxy-Authorization: Basic {Basic("secret")}\r\n\r\n"));

        var header = await ReadHeaderAsync(stream);
        Assert.StartsWith("HTTP/1.1 200", header, StringComparison.Ordinal);

        await stream.WriteAsync(Encoding.ASCII.GetBytes("PING"));
        var reply = new byte[4];
        await stream.ReadExactlyAsync(reply);

        Assert.Equal("PONG", Encoding.ASCII.GetString(reply));
        await upstream;
    }

    [Theory]
    [InlineData("10.0.0.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.1.1", false)]
    [InlineData("172.16.4.2", false)]
    [InlineData("192.168.1.10", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    public void Public_address_filter_blocks_private_ranges(string address, bool expected)
        => Assert.Equal(expected, ModelProxy.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("fc00::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("::1", false)]
    [InlineData("2001:4860:4860::8888", true)]
    public void Public_address_filter_handles_ipv6(string address, bool expected)
        => Assert.Equal(expected, ModelProxy.IsPublicAddress(IPAddress.Parse(address)));

    private static string Basic(string password)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"odw:{password}"));

    private static async Task<string> SendAsync(ModelProxy proxy, string target, string? auth = "secret")
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port);
        using var stream = client.GetStream();

        var request = $"CONNECT {target} HTTP/1.1\r\nHost: {target}\r\n";
        if (auth is not null)
        {
            request += $"Proxy-Authorization: Basic {Basic(auth)}\r\n";
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes(request + "\r\n"));
        return await ReadHeaderAsync(stream);
    }

    private static async Task<string> ReadHeaderAsync(NetworkStream stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new List<byte>(256);
        var one = new byte[1];

        while (buffer.Count < 16 * 1024)
        {
            await stream.ReadExactlyAsync(one, timeout.Token);
            buffer.Add(one[0]);

            if (buffer.Count >= 4
                && buffer[^4] == '\r'
                && buffer[^3] == '\n'
                && buffer[^2] == '\r'
                && buffer[^1] == '\n')
            {
                return Encoding.ASCII.GetString(buffer.ToArray());
            }
        }

        throw new InvalidOperationException("Cabecera de proxy demasiado larga.");
    }

    private static (int Port, Task Server) StartEchoUpstream()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var request = new byte[4];
            await stream.ReadExactlyAsync(request);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("PONG"));
        });

        return (port, server);
    }
}
