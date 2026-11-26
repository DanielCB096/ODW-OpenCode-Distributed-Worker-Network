using System.Security.Cryptography;
using System.Text;
using Odw.Core.Model;
using Odw.Core.Primitives;

namespace Odw.Core.Hashing;

/// <summary>
/// Hash de tarea: SHA-256 sobre la proyección canónica (ADR-011).
/// Cualquier cambio en instrucciones, workspace, límites, kind, nodos, padre,
/// executor.type o lineage.max_depth lo invalida.
/// </summary>
public static class TaskHash
{
    public const string Prefix = "sha256:";

    public static string Compute(TaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ComputeFromCanonical(BuildCanonicalJson(request));
    }

    public static string ComputeFromCanonical(string canonicalJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalJson);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Prefix + Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Proyección exacta (docs/DOMAIN-MODEL.md §4.1). Expuesta a las pruebas de vectores.
    /// </summary>
    internal static string BuildCanonicalJson(TaskRequest request)
    {
        var limits = new CanonicalObject();
        limits.Add("max_cost_usd", new CanonicalNumber(request.Limits.MaxCostUsd));
        limits.Add("max_memory_gb", new CanonicalNumber(request.Limits.MaxMemoryGb));
        limits.Add("max_minutes", new CanonicalNumber(request.Limits.MaxMinutes));
        limits.Add("max_processes", new CanonicalNumber(request.Limits.MaxProcesses));
        limits.Add("network", new CanonicalString(request.Limits.Network.ToWireValue()));

        var lineage = new CanonicalObject();
        lineage.Add("max_depth", new CanonicalNumber(request.Lineage.MaxDepth));

        var workspace = new CanonicalObject();
        workspace.Add("allowed_paths", NormalizePaths(request.Workspace.AllowedPaths));
        workspace.Add("base_ref", new CanonicalString(request.Workspace.BaseRef));
        workspace.Add("denied_paths", NormalizePaths(request.Workspace.DeniedPaths));
        workspace.Add("mode", new CanonicalString(request.Workspace.Mode.ToWireValue()));
        workspace.Add("repo", new CanonicalString(request.Workspace.Repo));

        var executor = new CanonicalObject();
        executor.Add("type", new CanonicalString(request.Executor.Type));
        if (request.Executor.ModelProfile is not null)
        {
            // El perfil forma parte de la aprobacion: cambiarlo cambia el hash.
            executor.Add("model_profile", new CanonicalString(request.Executor.ModelProfile));
        }

        var projection = new CanonicalObject();
        projection.Add("executor", executor);
        projection.Add("instructions", new CanonicalString(
            ContractFormat.NormalizeInstructions(request.Instructions)));
        projection.Add("kind", new CanonicalString(request.Kind.ToWireValue()));
        projection.Add("limits", limits);
        projection.Add("lineage", lineage);
        projection.Add("origin_node", new CanonicalString(request.OriginNode));
        projection.Add("parent_task_id", request.ParentTaskId is null
            ? CanonicalNull.Instance
            : new CanonicalString(request.ParentTaskId));
        projection.Add("target_node", new CanonicalString(request.TargetNode));
        projection.Add("task_id", new CanonicalString(request.TaskId));
        projection.Add("workspace", workspace);

        return CanonicalJson.Serialize(projection);
    }

    private static CanonicalArray NormalizePaths(IReadOnlyList<string> paths)
    {
        var normalized = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            // Una petición inválida sigue siendo hasheable: el validador la rechaza aparte.
            normalized.Add(ContractFormat.TryNormalizeRelativePath(path, out var value)
                ? value
                : path.Trim());
        }

        var array = new CanonicalArray();
        foreach (var path in normalized)
        {
            array.Add(new CanonicalString(path));
        }

        return array;
    }
}
