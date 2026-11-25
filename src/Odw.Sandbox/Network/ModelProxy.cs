using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Odw.Sandbox.Network;

public sealed record ModelProxyOptions
{
    /// <summary>Hosts permitidos (coincidencia exacta o subdominio).</summary>
    public IReadOnlyCollection<string> AllowedHosts { get; init; } = ["opencode.ai"];

    /// <summary>Puertos de destino permitidos; por defecto solo HTTPS.</summary>
    public IReadOnlyCollection<int> AllowedPorts { get; init; } = [443];

    /// <summary>Puerto de escucha; 0 elige uno libre.</summary>
    public int Port { get; init; }

    /// <summary>Token de <c>Proxy-Authorization</c>; si falta se genera uno.</summary>
    public string? AuthToken { get; init; }

    /// <summary>Filtro de direcciones resueltas. Por defecto rechaza rangos privados.</summary>
    public Func<IPAddress, bool> AddressFilter { get; init; } = ModelProxy.IsPublicAddress;

    public Func<string, CancellationToken, Task<IPAddress[]>>? Resolver { get; init; }

    public Action<string>? Log { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public int MaxRequestBytes { get; init; } = 16 * 1024;
}

/// <summary>
/// Proxy HTTP CONNECT en loopback con allowlist de hosts. Permite que el agente alcance
/// únicamente el endpoint del modelo sin darle salida arbitraria a Internet o a la LAN.
/// No inspecciona TLS: solo decide el destino y transporta bytes.
/// </summary>
public sealed class ModelProxy : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly ModelProxyOptions _options;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<long, TcpClient> _clients = new();
    private readonly SemaphoreSlim _slots = new(64, 64);
    private readonly Task _acceptLoop;
    private long _nextConnection;
    private bool _disposed;

    private ModelProxy(TcpListener listener, ModelProxyOptions options, string token)
    {
        _listener = listener;
        _options = options;
        Token = token;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }

    public string Token { get; }

    /// <summary>URL de proxy con credenciales, lista para <c>HTTPS_PROXY</c>.</summary>
    public string Url => $"http://odw:{Token}@127.0.0.1:{Port}";

    public static ModelProxy Start(ModelProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.AllowedHosts.Count == 0)
        {
            throw new ArgumentException("El proxy necesita al menos un host permitido.", nameof(options));
        }

        var token = options.AuthToken ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var listener = new TcpListener(IPAddress.Loopback, options.Port);
        listener.Start();

        return new ModelProxy(listener, options, token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping.Cancel();
        _listener.Stop();

        foreach (var client in _clients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch (Exception)
            {
            }
        }

        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _stopping.Dispose();
        _slots.Dispose();
    }

    public static bool IsHostAllowed(string host, IEnumerable<string> allowedHosts)
    {
        var value = host.Trim().TrimEnd('.').ToLowerInvariant();

        if (value.Length == 0 || IPAddress.TryParse(value, out _))
        {
            return false;
        }

        foreach (var allowed in allowedHosts)
        {
            var candidate = allowed.Trim().TrimEnd('.').ToLowerInvariant();
            if (candidate.Length == 0)
            {
                continue;
            }

            if (string.Equals(value, candidate, StringComparison.Ordinal)
                || value.EndsWith("." + candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            return bytes[0] switch
            {
                10 or 127 or 0 => false,
                100 when bytes[1] >= 64 && bytes[1] <= 127 => false,
                169 when bytes[1] == 254 => false,
                172 when bytes[1] >= 16 && bytes[1] <= 31 => false,
                192 when bytes[1] == 168 => false,
                192 when bytes[1] == 0 && bytes[2] == 2 => false,
                198 when bytes[1] == 18 || bytes[1] == 19 => false,
                198 when bytes[1] == 51 && bytes[2] == 100 => false,
                203 when bytes[1] == 0 && bytes[2] == 113 => false,
                >= 224 => false,
                _ => true,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || address.Equals(IPAddress.IPv6Any))
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            return (bytes[0] & 0xFE) != 0xFC;
        }

        return false;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException) when (_stopping.IsCancellationRequested)
            {
                return;
            }

            var id = Interlocked.Increment(ref _nextConnection);
            if (!_slots.Wait(0))
            {
                _options.Log?.Invoke("proxy: demasiadas conexiones; se rechaza una");
                client.Dispose();
                continue;
            }

            _clients[id] = client;
            _ = HandleAsync(id, client);
        }
    }

    private async Task HandleAsync(long id, TcpClient client)
    {
        try
        {
            client.NoDelay = true;
            using var stream = client.GetStream();
            using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            headerTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            var header = await ReadHeaderAsync(stream, headerTimeout.Token).ConfigureAwait(false);

            if (header is null)
            {
                return;
            }

            var lines = header.Split("\r\n", StringSplitOptions.None);
            var request = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (request.Length < 2)
            {
                await DenyAsync(stream, 400, "Bad Request").ConfigureAwait(false);
                return;
            }

            if (!IsAuthorized(lines))
            {
                _options.Log?.Invoke("proxy: autenticación de proxy rechazada");
                await WriteAsync(
                    stream,
                    "HTTP/1.1 407 Proxy Authentication Required\r\n"
                    + "Proxy-Authenticate: Basic realm=\"odw\"\r\n"
                    + "Content-Length: 0\r\n\r\n").ConfigureAwait(false);
                return;
            }

            if (!string.Equals(request[0], "CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                await DenyAsync(stream, 405, "Method Not Allowed").ConfigureAwait(false);
                return;
            }

            if (!TryParseAuthority(request[1], out var host, out var port))
            {
                await DenyAsync(stream, 400, "Bad Request").ConfigureAwait(false);
                return;
            }

            if (!IsHostAllowed(host, _options.AllowedHosts) || !_options.AllowedPorts.Contains(port))
            {
                _options.Log?.Invoke($"proxy: host denegado {host}:{port}");
                await DenyAsync(stream, 403, "Forbidden").ConfigureAwait(false);
                return;
            }

            var addresses = await ResolveAsync(host).ConfigureAwait(false);
            var target = addresses.FirstOrDefault(_options.AddressFilter);

            if (target is null)
            {
                _options.Log?.Invoke($"proxy: dirección no permitida para {host}");
                await DenyAsync(stream, 403, "Forbidden").ConfigureAwait(false);
                return;
            }

            using var upstream = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            timeout.CancelAfter(_options.ConnectTimeout);

            try
            {
                await upstream.ConnectAsync(target, port, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                _options.Log?.Invoke($"proxy: no se pudo conectar con {host}:{port} ({exception.GetType().Name})");
                await DenyAsync(stream, 502, "Bad Gateway").ConfigureAwait(false);
                return;
            }

            await WriteAsync(stream, "HTTP/1.1 200 Connection Established\r\n\r\n").ConfigureAwait(false);
            _options.Log?.Invoke($"proxy: túnel {host}:{port}");

            using var upstreamStream = upstream.GetStream();
            await PumpAsync(stream, upstreamStream, _stopping.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            _options.Log?.Invoke($"proxy: error inesperado ({exception.GetType().Name})");
        }
        finally
        {
            _clients.TryRemove(id, out _);
            _slots.Release();
            client.Dispose();
        }
    }

    private async Task<IPAddress[]> ResolveAsync(string host)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        try
        {
            if (_options.Resolver is not null)
            {
                return await _options.Resolver(host, timeout.Token).ConfigureAwait(false);
            }

            return await Dns.GetHostAddressesAsync(host, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            _options.Log?.Invoke($"proxy: DNS falló para {host}");
            return [];
        }
    }

    private bool IsAuthorized(IReadOnlyList<string> lines)
    {
        if (string.IsNullOrEmpty(_options.AuthToken))
        {
            return true;
        }

        foreach (var line in lines)
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0
                || !line.AsSpan(0, separator).Trim().Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(separator + 1)..].Trim();
            if (!value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value[6..]));
                var colon = decoded.IndexOf(':', StringComparison.Ordinal);
                var password = colon >= 0 ? decoded[(colon + 1)..] : decoded;

                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password),
                    Encoding.UTF8.GetBytes(_options.AuthToken));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryParseAuthority(string authority, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (authority.StartsWith("[", StringComparison.Ordinal))
        {
            return false;
        }

        var separator = authority.LastIndexOf(':');
        if (separator <= 0 || separator == authority.Length - 1)
        {
            return false;
        }

        host = authority[..separator].Trim();
        return host.Length > 0
               && int.TryParse(authority[(separator + 1)..], out port)
               && port is > 0 and <= 65535;
    }

    private async Task<string?> ReadHeaderAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.MaxRequestBytes];
        var length = 0;

        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            length += read;

            if (length >= 4
                && buffer[length - 4] == '\r'
                && buffer[length - 3] == '\n'
                && buffer[length - 2] == '\r'
                && buffer[length - 1] == '\n')
            {
                return Encoding.ASCII.GetString(buffer, 0, length);
            }
        }

        return null;
    }

    private static async Task PumpAsync(NetworkStream client, NetworkStream upstream, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var clientToUpstream = client.CopyToAsync(upstream, linked.Token);
        var upstreamToClient = upstream.CopyToAsync(client, linked.Token);

        await Task.WhenAny(clientToUpstream, upstreamToClient).ConfigureAwait(false);
        linked.Cancel();

        try
        {
            await Task.WhenAll(clientToUpstream, upstreamToClient).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
        {
        }
    }

    private static Task DenyAsync(NetworkStream stream, int status, string reason)
        => WriteAsync(stream, $"HTTP/1.1 {status} {reason}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");

    private static async Task WriteAsync(NetworkStream stream, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        await stream.WriteAsync(bytes).ConfigureAwait(false);
    }
}
