using System.Diagnostics;
using System.Globalization;

namespace Odw.Sandbox.Windows;

public sealed record AgentRuntimeOptions
{
    /// <summary>Directorio raíz de ejecuciones; cada tarea recibe un subdirectorio desechable.</summary>
    public required string Root { get; init; }

    /// <summary>Plantilla de opencode.json que se copia a la configuración de la tarea.</summary>
    public required string ConfigTemplate { get; init; }

    public required string AgentAccount { get; init; }

    public string? ServiceAccount { get; init; }

    public bool ApplyAcls { get; init; } = true;
}

/// <summary>
/// Perfil de ejecución aislado por tarea: configuración, datos, caché y temporales propios.
/// Evita que una tarea deje plugins o configuración que afecten a las siguientes, y que pueda
/// leer los datos de sesión de otra tarea a través del perfil compartido del agente.
/// </summary>
public sealed class AgentRuntime : IAsyncDisposable
{
    private AgentRuntime(string path) => Path = path;

    public string Path { get; }

    /// <summary>Elimina perfiles de tareas anteriores al arrancar el nodo.</summary>
    public static void Cleanup(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            TryDelete(directory);
        }
    }

    public static AgentRuntime Create(string taskId, AgentRuntimeOptions options)
    {        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(options);

        if (!IsSafeSegment(taskId))
        {
            throw new ArgumentException("task_id no es un segmento de ruta válido.", nameof(taskId));
        }

        if (!File.Exists(options.ConfigTemplate))
        {
            throw new FileNotFoundException(
                "Falta la plantilla de configuración del agente.", options.ConfigTemplate);
        }

        var root = System.IO.Path.GetFullPath(options.Root);
        Directory.CreateDirectory(root);

        var path = System.IO.Path.Combine(root, taskId);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
        Directory.CreateDirectory(System.IO.Path.Combine(path, ".config", "opencode"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, ".local", "share"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, ".local", "state"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, ".cache"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "AppData", "Local"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "AppData", "Roaming"));
        Directory.CreateDirectory(System.IO.Path.Combine(path, "Temp"));

        File.Copy(
            options.ConfigTemplate,
            System.IO.Path.Combine(path, ".config", "opencode", "opencode.json"),
            overwrite: true);

        // El workspace lo crea odw-svc y git lo ejecuta odw-agent: sin safe.directory, git
        // rechaza el repo por propietario distinto. El ACL del workspace sigue siendo la frontera.
        File.WriteAllText(
            System.IO.Path.Combine(path, ".gitconfig"),
            "[safe]" + Environment.NewLine + "\tdirectory = *" + Environment.NewLine);

        if (options.ApplyAcls && OperatingSystem.IsWindows())
        {
            ApplyAcls(path, options);
        }

        return new AgentRuntime(path);
    }

    public ValueTask DisposeAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            TryDelete(Path);
            return ValueTask.CompletedTask;
        }

        TryDelete(Path);
        return ValueTask.CompletedTask;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void ApplyAcls(string path, AgentRuntimeOptions options)
    {
        var service = options.ServiceAccount;
        if (string.IsNullOrWhiteSpace(service))
        {
            service = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        }

        RunIcacls(path, "/inheritance:r");
        RunIcacls(path, "/grant:r", $"{service}:(OI)(CI)F",
            "/grant:r", "*S-1-5-18:(OI)(CI)F",
            "/grant:r", "*S-1-5-32-544:(OI)(CI)F",
            "/grant:r", $"{options.AgentAccount}:(OI)(CI)M", "/T");
    }

    private static void RunIcacls(string path, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "icacls",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(path);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("No se pudo ejecutar icacls.");

        _ = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"icacls falló en {path}: {error.Trim()}"));
        }
    }

    private static bool IsSafeSegment(string value)
        => value.Length is > 0 and <= 128
           && value.All(character => char.IsAsciiLetterOrDigit(character)
                                     || character is '-' or '_');

    private static void TryDelete(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
