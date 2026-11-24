namespace Odw.Sandbox.Workspaces;

public sealed record WorkspaceRequest(
    string TaskId,
    string Repo,
    string BaseRef,
    bool ReadOnly);

public sealed class WorkspaceException(string message) : Exception(message);

/// <summary>Workspace listo para ejecutar; al liberarse se borra salvo que se pida conservarlo.</summary>
public sealed class WorkspaceLease : IAsyncDisposable
{
    public WorkspaceLease(string path, string branch, bool readOnly, bool keep)
    {
        Path = path;
        Branch = branch;
        ReadOnly = readOnly;
        Keep = keep;
    }

    public string Path { get; }

    public string Branch { get; }

    public bool ReadOnly { get; }

    public bool Keep { get; }

    public ValueTask DisposeAsync()
    {
        if (Keep)
        {
            return ValueTask.CompletedTask;
        }

        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return ValueTask.CompletedTask;
    }
}

public interface IWorkspacePreparer
{
    Task<WorkspaceLease> PrepareAsync(WorkspaceRequest request, CancellationToken cancellationToken);
}
