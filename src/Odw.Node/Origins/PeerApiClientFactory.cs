using Odw.Node.Peers;
using Odw.PeerApi;

namespace Odw.Node.Origins;

public sealed class OriginException(string message) : Exception(message);

/// <summary>Crea clientes salientes a partir de la configuración de pares (solo si hay address).</summary>
public sealed class PeerApiClientFactory
{
    private readonly string _nodeId;
    private readonly HttpClient? _http;

    public PeerApiClientFactory(string nodeId, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _nodeId = nodeId;
        _http = httpClient;
    }

    public PeerApiClient Create(PeerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (string.IsNullOrWhiteSpace(registration.Address))
        {
            throw new OriginException($"El par {registration.NodeId} no tiene address configurada.");
        }

        var address = registration.Address.Contains("://", StringComparison.Ordinal)
            ? registration.Address
            : $"http://{registration.Address}";

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new OriginException($"Address inválida para {registration.NodeId}: {registration.Address}.");
        }

        return new PeerApiClient(new PeerConnection(uri, _nodeId, registration.Token), _http);
    }
}
