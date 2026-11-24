using Odw.Core.Delegation;
using Odw.Core.Model;
using Xunit;

namespace Odw.Core.Tests;

public class LineageGuardTests
{
    [Fact]
    public void Direct_delegation_is_allowed()
    {
        var lineage = new Lineage { Hops = ["PC-A"], MaxDepth = 1 };

        Assert.Equal(LineageCheck.Ok, LineageGuard.Evaluate("PC-A", "PC-B", lineage));
    }

    [Fact]
    public void Self_delegation_is_rejected()
    {
        Assert.Equal(
            LineageCheck.SelfDelegation,
            LineageGuard.Evaluate("PC-A", "PC-A", new Lineage { Hops = [], MaxDepth = 1 }));
    }

    [Fact]
    public void Target_in_hops_is_a_loop()
    {
        var lineage = new Lineage { Hops = ["PC-A", "PC-B"], MaxDepth = 2 };

        Assert.Equal(LineageCheck.Loop, LineageGuard.Evaluate("PC-A", "PC-B", lineage));
    }

    [Fact]
    public void Depth_is_enforced()
    {
        var lineage = new Lineage { Hops = ["PC-A", "PC-B"], MaxDepth = 1 };

        Assert.Equal(LineageCheck.DepthExceeded, LineageGuard.Evaluate("PC-A", "PC-C", lineage));
    }

    [Fact]
    public void Empty_hops_allows_first_send()
    {
        Assert.Equal(
            LineageCheck.Ok,
            LineageGuard.Evaluate("PC-A", "PC-B", new Lineage { Hops = [], MaxDepth = 1 }));
    }

    [Theory]
    [InlineData("pc-a")]
    [InlineData("")]
    [InlineData("PC A")]
    public void Invalid_node_ids_are_rejected(string origin)
    {
        Assert.Equal(
            LineageCheck.Invalid,
            LineageGuard.Evaluate(origin, "PC-B", new Lineage { Hops = [], MaxDepth = 1 }));
    }

    [Fact]
    public void Duplicate_hops_are_invalid()
    {
        var lineage = new Lineage { Hops = ["PC-A", "PC-A"], MaxDepth = 2 };

        Assert.Equal(LineageCheck.Invalid, LineageGuard.Evaluate("PC-A", "PC-C", lineage));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Max_depth_is_bounded(int depth)
    {
        Assert.Equal(
            LineageCheck.Invalid,
            LineageGuard.Evaluate("PC-A", "PC-B", new Lineage { Hops = [], MaxDepth = depth }));
    }
}
