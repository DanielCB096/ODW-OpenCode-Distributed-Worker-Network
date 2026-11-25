using Odw.Sandbox.Windows;
using Xunit;

namespace Odw.Sandbox.Tests;

public class AgentRuntimeTests
{
    [Fact]
    public void Rejects_task_ids_that_are_not_safe_path_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), "odw-runtime", Guid.NewGuid().ToString("N"));
        var template = Path.Combine(root, "opencode.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(template, "{}");

        try
        {
            var options = new AgentRuntimeOptions
            {
                Root = root,
                ConfigTemplate = template,
                AgentAccount = "odw-agent",
                ApplyAcls = false,
            };

            Assert.Throws<ArgumentException>(() => AgentRuntime.Create("..\\escape", options));
            Assert.Throws<ArgumentException>(() => AgentRuntime.Create("../escape", options));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Creates_and_removes_a_per_task_profile()
    {
        var root = Path.Combine(Path.GetTempPath(), "odw-runtime", Guid.NewGuid().ToString("N"));
        var template = Path.Combine(root, "opencode.json");
        Directory.CreateDirectory(root);
        File.WriteAllText(template, "{\"model\":\"opencode/deepseek-v4.1-flash-free\"}");

        try
        {
            var runtime = AgentRuntime.Create(Guid.NewGuid().ToString("N"), new AgentRuntimeOptions
            {
                Root = root,
                ConfigTemplate = template,
                AgentAccount = "odw-agent",
                ApplyAcls = false,
            });

            var path = runtime.Path;
            Assert.True(File.Exists(Path.Combine(path, ".config", "opencode", "opencode.json")));
            Assert.True(Directory.Exists(Path.Combine(path, ".local", "share")));
            Assert.Contains(
                "directory = *",
                File.ReadAllText(Path.Combine(path, ".gitconfig")),
                StringComparison.Ordinal);

            await runtime.DisposeAsync();
            Assert.False(Directory.Exists(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
