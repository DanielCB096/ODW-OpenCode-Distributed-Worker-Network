using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Odw.Executors.OpenCode;

public sealed record RunAsCredentials(string Account, string Password);

public sealed record OpenCodeServerOptions
{
    public required string Executable { get; init; }

    public string WorkingDirectory { get; init; } = Directory.GetCurrentDirectory();

    public string? PinnedVersion { get; init; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Si se indica, el servidor se lanza con estas credenciales (agente restringido).</summary>
    public RunAsCredentials? RunAs { get; init; }

    /// <summary>Perfil de ejecución por tarea; si falta se usa el perfil real del agente.</summary>
    public string? RuntimeRoot { get; init; }

    /// <summary>Se invoca tras el arranque para asignar el proceso a un Job Object u otro control.</summary>
    public Func<Process, IDisposable?>? AttachProcess { get; init; }
}

/// <summary>
/// Lanza <c>opencode serve --port 0 --hostname 127.0.0.1</c>, descubre la URL por stdout y
/// confirma con <c>/api/info</c>. Sin Job Object ni usuario restringido todavía: el
/// endurecimiento del sandbox es H3 y se documenta como pendiente.
/// </summary>
public sealed class OpenCodeServer : IAsyncDisposable
{
    private readonly Process _process;
    private readonly IDisposable? _attachment;
    private readonly Task _stdoutDrain;
    private readonly Task _stderrDrain;
    private bool _stopped;

    private OpenCodeServer(
        Process process,
        OpenCodeConnection connection,
        OpenCodeServerInfo info,
        IDisposable? attachment)
    {
        _process = process;
        _attachment = attachment;
        Connection = connection;
        Info = info;
        _stdoutDrain = Task.Run(() => DrainAsync(process.StandardOutput));
        _stderrDrain = Task.Run(() => DrainAsync(process.StandardError));
    }

    public OpenCodeConnection Connection { get; }

    public OpenCodeServerInfo Info { get; }

    public int ProcessId => Info.Pid > 0 ? Info.Pid : _process.Id;

    public string BaseUrl => Connection.BaseAddress.ToString().TrimEnd('/');

    public static async Task<OpenCodeServer> StartAsync(
        OpenCodeServerOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Executable);

        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var process = new Process { StartInfo = BuildStartInfo(options, password) };

        if (!process.Start())
        {
            throw new InvalidOperationException("No se pudo iniciar opencode serve.");
        }

        IDisposable? attachment = null;

        try
        {
            attachment = options.AttachProcess?.Invoke(process);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.StartupTimeout);

            var url = await WaitForUrlAsync(process, timeout.Token).ConfigureAwait(false);
            var connection = new OpenCodeConnection(new Uri(url), "opencode", password);

            using var client = new OpenCodeClient(connection);
            var info = await WaitForInfoAsync(client, process, timeout.Token).ConfigureAwait(false);

            if (options.PinnedVersion is not null
                && !string.Equals(info.Version, options.PinnedVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"OpenCode {info.Version} != versión fijada {options.PinnedVersion}.");
            }

            return new OpenCodeServer(process, connection, info, attachment);
        }
        catch
        {
            attachment?.Dispose();
            Kill(process);
            process.Dispose();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_stopped)
        {
            return;
        }

        _stopped = true;
        Kill(_process);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await Task.WhenAll(_stdoutDrain, _stderrDrain).ConfigureAwait(false);
        _attachment?.Dispose();
        _process.Dispose();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private static ProcessStartInfo BuildStartInfo(OpenCodeServerOptions options, string password)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        const string serveArguments = "serve --hostname 127.0.0.1 --port 0";

        if (OperatingSystem.IsWindows()
            && (options.Executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || options.Executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/c \"\"{options.Executable}\" {serveArguments}\"";
        }
        else
        {
            startInfo.FileName = options.Executable;
            startInfo.Arguments = serveArguments;
        }

        startInfo.Environment["OPENCODE_SERVER_PASSWORD"] = password;
        startInfo.Environment["OPENCODE_SERVER_USERNAME"] = "opencode";

        if (options.RunAs is { } runAs)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Ejecutar como otro usuario requiere Windows.");
            }

            startInfo.UserName = runAs.Account;
            startInfo.PasswordInClearText = runAs.Password;
            startInfo.Domain = ".";
            startInfo.LoadUserProfile = true;

            ApplyAgentEnvironment(startInfo, runAs.Account, options.RuntimeRoot);
        }

        if (options.Environment is not null)
        {
            foreach (var (key, value) in options.Environment)
            {
                startInfo.Environment[key] = value;
            }
        }

        return startInfo;
    }

    private static async Task<string> WaitForUrlAsync(Process process, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                var error = string.Empty;
                try
                {
                    error = process.StandardError.ReadToEnd().Trim();
                }
                catch (Exception)
                {
                }

                throw new InvalidOperationException(
                    $"opencode serve terminó antes de publicar la URL (código {SafeExitCode(process)}). {Tail(error)}");
            }

            var match = Regex.Match(line, @"http://127\.0\.0\.1:\d+", RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Value;
            }
        }

        throw new OperationCanceledException("Tiempo de arranque de opencode agotado.", cancellationToken);
    }

    private static async Task<OpenCodeServerInfo> WaitForInfoAsync(
        OpenCodeClient client,
        Process process,
        CancellationToken cancellationToken)
    {
        Exception? last = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"opencode serve terminó antes de responder /api/info (código {process.ExitCode}).");
            }

            try
            {
                return await client.GetInfoAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or OpenCodeApiException)
            {
                last = exception;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        throw new TimeoutException("No se pudo confirmar /api/info a tiempo.", last);
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is not null)
            {
            }
        }
        catch (Exception)
        {
            // El proceso cerró el pipe; no hay nada que drenar.
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException)
        {
            return "desconocido";
        }
    }

    private static string Tail(string text)
        => text.Length <= 500 ? text : text[^500..];

    private static void ApplyAgentEnvironment(
        ProcessStartInfo startInfo,
        string account,
        string? runtimeRoot)
    {
        string profile;

        if (!string.IsNullOrWhiteSpace(runtimeRoot))
        {
            profile = Path.GetFullPath(runtimeRoot);
        }
        else
        {
            var usersRoot = Directory.GetParent(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))?.FullName;

            if (usersRoot is null)
            {
                return;
            }

            profile = Path.Combine(usersRoot, account);
        }

        var appData = Path.Combine(profile, "AppData");
        var local = Path.Combine(appData, "Local");
        var roaming = Path.Combine(appData, "Roaming");
        var temp = Path.Combine(local, "Temp");

        try
        {
            Directory.CreateDirectory(temp);
            Directory.CreateDirectory(roaming);
            Directory.CreateDirectory(Path.Combine(profile, ".config"));
            Directory.CreateDirectory(Path.Combine(profile, ".local", "share"));
        }
        catch (IOException)
        {
        }

        startInfo.Environment["USERPROFILE"] = profile;
        startInfo.Environment["HOME"] = profile;
        startInfo.Environment["APPDATA"] = roaming;
        startInfo.Environment["LOCALAPPDATA"] = local;
        startInfo.Environment["TEMP"] = temp;
        startInfo.Environment["TMP"] = temp;
        startInfo.Environment["USERNAME"] = account;
        startInfo.Environment["USERDOMAIN"] = Environment.MachineName;
        startInfo.Environment["XDG_CONFIG_HOME"] = Path.Combine(profile, ".config");
        startInfo.Environment["XDG_DATA_HOME"] = Path.Combine(profile, ".local", "share");
        startInfo.Environment["XDG_CACHE_HOME"] = Path.Combine(profile, ".cache");

        var root = Path.GetPathRoot(profile);
        if (root is not null)
        {
            startInfo.Environment["HOMEDRIVE"] = root.TrimEnd('\\');
            startInfo.Environment["HOMEPATH"] = profile[root.Length..];
        }
    }
}
