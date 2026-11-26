using System.Security.Cryptography;
using System.Text;
using Odw.Node.Artifacts;
using Xunit;

namespace Odw.Node.Tests;

public class ArtifactStoreTests
{
    private const string TaskId = "01JAZ5Z8Q4XK3V9C2M1N7P6R8T";

    [Fact]
    public async Task Saves_and_reads_an_artifact()
    {
        var root = CreateRoot();
        try
        {
            var store = new ArtifactStore(root);
            var bytes = Encoding.UTF8.GetBytes("diff --git a/x b/x\n");

            var saved = await store.SaveAsync(
                TaskId, "diff.patch", bytes, "text/x-diff", CancellationToken.None);

            Assert.Equal("diff.patch", saved.Name);
            Assert.Equal(bytes.Length, saved.Bytes);
            Assert.Equal("text/x-diff", saved.ContentType);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), saved.Sha256);

            Assert.True(store.TryGetPath(TaskId, "diff.patch", out var path));
            Assert.True(File.Exists(path));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            Delete(root);
        }
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData("con.txt")]
    [InlineData("a:b")]
    public void Unsafe_names_are_rejected(string name)
    {
        Assert.False(ArtifactStore.IsSafeName(name));
        Assert.Throws<ArtifactException>(() => ArtifactStore.ValidateName(name));
    }

    [Fact]
    public void Valid_names_are_accepted()
    {
        Assert.True(ArtifactStore.IsSafeName("diff.patch"));
        Assert.True(ArtifactStore.IsSafeName("summary.md"));
        Assert.True(ArtifactStore.IsSafeName("bundle-1.zip"));
        Assert.True(ArtifactStore.IsSafeName("a_b.c-d"));
    }

    [Fact]
    public void Unsafe_task_ids_are_rejected()
    {
        Assert.Throws<ArtifactException>(() => ArtifactStore.ValidateTaskId("no-es-ulid"));
        Assert.Throws<ArtifactException>(() => ArtifactStore.ValidateTaskId("../escape"));
    }

    [Fact]
    public void Missing_artifact_returns_false()
    {
        var root = CreateRoot();
        try
        {
            var store = new ArtifactStore(root);
            Assert.False(store.TryGetPath(TaskId, "missing.md", out _));
        }
        finally
        {
            Delete(root);
        }
    }

    [Fact]
    public async Task Oversized_artifact_is_rejected()
    {
        var root = CreateRoot();
        try
        {
            var store = new ArtifactStore(root);
            var oversized = new byte[ArtifactStore.MaxArtifactBytes + 1];

            await Assert.ThrowsAsync<ArtifactException>(
                () => store.SaveAsync(TaskId, "big.bin", oversized, "application/octet-stream", CancellationToken.None));
        }
        finally
        {
            Delete(root);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "odw-artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Delete(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
