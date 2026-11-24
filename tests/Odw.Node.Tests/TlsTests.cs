using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Odw.Node.Hosting;
using Odw.Node.Peers;
using Xunit;

namespace Odw.Node.Tests;

public class TlsTests
{
    private static X509Certificate2 SelfSigned()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=127.0.0.1",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());

        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        var pfx = generated.Export(X509ContentType.Pfx);
        return X509CertificateLoader.LoadPkcs12(pfx, null, X509KeyStorageFlags.Exportable);
    }

    [Fact]
    public async Task Peer_api_uses_https_when_a_certificate_is_configured()
    {
        var folder = Path.Combine(Path.GetTempPath(), "odw-tls-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        using var certificate = SelfSigned();
        const string token = "pair-token";

        var options = new NodeHostOptions
        {
            NodeId = "PC-A",
            DatabasePath = Path.Combine(folder, "odw.db"),
            McpToken = Convert.ToBase64String(new byte[32]),
            PeerBind = "127.0.0.1",
            PeerPort = 0,
            McpBind = "127.0.0.1",
            McpPort = 0,
            AllowLoopbackPeerBind = true,
            Peers = [new PeerRegistration("PC-B", token)],
            PeerCertificate = certificate,
            EnableApprovals = false,
            EnableOriginPoller = false,
            Pooling = false,
        };

        try
        {
            await using var host = await NodeHost.StartAsync(options);

            Assert.Equal("https", host.PeerBaseAddress.Scheme);

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            };
            using var client = new HttpClient(handler) { BaseAddress = host.PeerBaseAddress };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Odw-Node", "PC-B");

            var response = await client.GetAsync("/peer/v1/hello");
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("\"node_id\":\"PC-A\"", body);
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
