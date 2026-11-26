using Odw.Core.Primitives;
using Xunit;

namespace Odw.Core.Tests;

public class ContractFormatTests
{
    [Theory]
    [InlineData("PC-A", true)]
    [InlineData("A1", true)]
    [InlineData("A-1", true)]
    [InlineData("A", false)]
    [InlineData("pc-a", false)]
    [InlineData("A_", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Node_ids_are_validated(string? value, bool expected)
        => Assert.Equal(expected, ContractFormat.IsValidNodeId(value));

    [Theory]
    [InlineData("SistemaMaestro", true)]
    [InlineData("a.b_c-1", true)]
    [InlineData("a", true)]
    [InlineData("bad/repo", false)]
    [InlineData(".bad", false)]
    [InlineData("-bad", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Repo_names_are_validated(string? value, bool expected)
        => Assert.Equal(expected, ContractFormat.IsValidRepoName(value));

    [Fact]
    public void Task_hashes_are_validated()
    {
        var hash = "sha256:" + new string('a', 64);

        Assert.True(ContractFormat.IsValidTaskHash(hash));
        Assert.False(ContractFormat.IsValidTaskHash(hash.ToUpperInvariant()));
        Assert.False(ContractFormat.IsValidTaskHash("sha256:" + new string('a', 63)));
        Assert.False(ContractFormat.IsValidTaskHash("md5:" + new string('a', 64)));
        Assert.False(ContractFormat.IsValidTaskHash(null));
    }

    [Theory]
    [InlineData("main", true)]
    [InlineData("feature/x", true)]
    [InlineData("v1.2.3", true)]
    [InlineData("a_b-c", true)]
    [InlineData("", false)]
    [InlineData("-bad", false)]
    [InlineData("/bad", false)]
    [InlineData(".bad", false)]
    [InlineData("bad.", false)]
    [InlineData("bad/", false)]
    [InlineData("bad..x", false)]
    [InlineData("bad@{x}", false)]
    [InlineData("bad.lock", false)]
    [InlineData("bad space", false)]
    public void Git_refs_are_validated(string value, bool expected)
        => Assert.Equal(expected, ContractFormat.IsValidGitRef(value));

    [Fact]
    public void Git_refs_have_a_length_limit()
    {
        Assert.False(ContractFormat.IsValidGitRef(new string('a', 129)));
        Assert.True(ContractFormat.IsValidGitRef(new string('a', 128)));
        Assert.False(ContractFormat.IsValidGitRef(null));
    }

    [Fact]
    public void Relative_paths_are_normalized()
    {
        Assert.True(ContractFormat.TryNormalizeRelativePath("src\\sub\\file.cs", out var windows));
        Assert.Equal("src/sub/file.cs", windows);

        Assert.True(ContractFormat.TryNormalizeRelativePath("./src/tests/", out var extra));
        Assert.Equal("src/tests", extra);

        Assert.True(ContractFormat.TryNormalizeRelativePath("src/./tests", out var dotted));
        Assert.Equal("src/tests", dotted);

        Assert.True(ContractFormat.TryNormalizeRelativePath("src/tests/", out var trailing));
        Assert.Equal("src/tests", trailing);
    }

    [Fact]
    public void Relative_paths_are_rejected_when_unsafe()
    {
        Assert.False(ContractFormat.TryNormalizeRelativePath(null, out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/../x", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src//x", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("C:/x", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath(@"\\server\share", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("CON", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/CON.txt", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/foo.", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/foo /bar", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/a:b", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/\0x", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/<x>", out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath(new string('a', 513), out _));
        Assert.False(ContractFormat.TryNormalizeRelativePath("src/\uD800", out _));
    }

    [Fact]
    public void Instructions_are_normalized()
    {
        Assert.Equal("a\n b", ContractFormat.NormalizeInstructions("  a \r\n b\t\r\n"));
        Assert.Equal("a\nb", ContractFormat.NormalizeInstructions("a\rb"));
        Assert.Equal("", ContractFormat.NormalizeInstructions(" \r\n\t"));
    }

    [Fact]
    public void Lone_surrogates_are_detected()
    {
        Assert.False(ContractFormat.HasLoneSurrogate("ok"));
        Assert.False(ContractFormat.HasLoneSurrogate("a\uD83D\uDE00"));
        Assert.True(ContractFormat.HasLoneSurrogate("a" + new string('\uD800', 1)));
        Assert.True(ContractFormat.HasLoneSurrogate("a" + new string('\uDC00', 1)));
    }
}
