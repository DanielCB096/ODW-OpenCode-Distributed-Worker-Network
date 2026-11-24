using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;

namespace Odw.Approvals;

/// <summary>Protocolo del pipe de aprobaciones (una línea JSON por conexión).</summary>
public static class ApprovalPipeClient
{
    public const string DefaultPipeName = "odw-approvals";

    public static async Task<JsonNode?> SendAsync(
        string pipeName,
        JsonObject request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(request);

        using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        var milliseconds = (int)(timeout ?? TimeSpan.FromSeconds(5)).TotalMilliseconds;
        await pipe.ConnectAsync(milliseconds, cancellationToken).ConfigureAwait(false);

        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(request.ToJsonString()).ConfigureAwait(false);
        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        return line is null ? null : JsonNode.Parse(line);
    }
}
