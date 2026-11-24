using Odw.Core.Model;
using Odw.Core.Primitives;

namespace Odw.Core.Delegation;

public enum LineageCheck
{
    Ok,
    Invalid,
    SelfDelegation,
    Loop,
    DepthExceeded,
}

/// <summary>
/// Anti-ciclos y profundidad de delegación (docs/DOMAIN-MODEL.md §5).
/// <c>hops</c> son los nodos que ya intervinieron; el emisor se agrega al enviar.
/// Se permite enviar mientras <c>hops.Count &lt;= max_depth</c>.
/// </summary>
public static class LineageGuard
{
    public const int MaxAllowedDepth = 3;

    public static LineageCheck Evaluate(string originNode, string targetNode, Lineage lineage)
    {
        if (!ContractFormat.IsValidNodeId(originNode) || !ContractFormat.IsValidNodeId(targetNode))
        {
            return LineageCheck.Invalid;
        }

        if (string.Equals(originNode, targetNode, StringComparison.Ordinal))
        {
            return LineageCheck.SelfDelegation;
        }

        if (lineage.MaxDepth is < 0 or > MaxAllowedDepth)
        {
            return LineageCheck.Invalid;
        }

        if (lineage.Hops.Count > lineage.MaxDepth)
        {
            return LineageCheck.DepthExceeded;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hop in lineage.Hops)
        {
            if (!ContractFormat.IsValidNodeId(hop) || !seen.Add(hop))
            {
                return LineageCheck.Invalid;
            }
        }

        if (seen.Contains(targetNode))
        {
            return LineageCheck.Loop;
        }

        return LineageCheck.Ok;
    }
}
