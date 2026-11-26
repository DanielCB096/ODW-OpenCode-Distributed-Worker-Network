namespace Odw.Mcp.Abstractions;

public sealed record ArtifactContent(string Name, string ContentType, byte[] Bytes);

/// <summary>Descarga un artefacto de la tarea desde el nodo que la ejecutó.</summary>
public interface IArtifactFetcher
{
    Task<ArtifactContent?> FetchAsync(string taskId, string name, CancellationToken cancellationToken);
}
