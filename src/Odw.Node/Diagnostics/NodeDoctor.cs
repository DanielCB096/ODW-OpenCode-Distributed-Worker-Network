using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Odw.Node.Persistence;

namespace Odw.Node.Diagnostics;

/// <summary>
/// Comprobaciones locales de un nodo: base de datos, binds, exposición de puertos, token de
/// fachada, versión de OpenCode y hash del spec. No modifica nada (BD abierta en solo lectura).
/// </summary>
public static class NodeDoctor
{
    public static async Task<DoctorReport> RunAsync(DoctorOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var checks = new List<DoctorCheck>();

        var database = CheckDatabase(options);
        checks.AddRange(database);

        checks.Add(CheckPeerBind(options));

        checks.Add(CheckBind("bind.mcp", options.McpBind, required: BindPolicy.IsLoopbackBind,
            "La fachada MCP debe escuchar solo en loopback."));

        checks.Add(CheckListener(
            "listener.peer", options.PeerPort, tailnet: true, allowLoopbackPeer: options.AllowLoopbackPeerBind));
        checks.Add(CheckListener("listener.mcp", options.McpPort, tailnet: false));
        checks.Add(CheckToken(options));
        checks.Add(await CheckOpenCodeVersionAsync(options, cancellationToken).ConfigureAwait(false));
        checks.Add(CheckSpec(options));

        return new DoctorReport(checks);
    }

    private static IReadOnlyList<DoctorCheck> CheckDatabase(DoctorOptions options)
    {
        if (!File.Exists(options.DatabasePath))
        {
            return
            [
                new DoctorCheck(
                    "database.schema",
                    DoctorStatus.Fail,
                    $"No existe la base de datos: {options.DatabasePath}."),
            ];
        }

        SqliteConnection connection;
        try
        {
            connection = OpenReadOnly(options.DatabasePath);
        }
        catch (SqliteException exception)
        {
            return [new DoctorCheck("database.schema", DoctorStatus.Fail, exception.Message)];
        }

        var checks = new List<DoctorCheck>(3);

        using (connection)
        {
            try
            {
                var version = DatabaseMigrator.GetUserVersion(connection);
                var tables = ScalarLong(connection,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('tasks','task_events','audit_log','pending_actions');");

                checks.Add(version == DatabaseMigrator.CurrentVersion && tables == 4
                    ? new DoctorCheck("database.schema", DoctorStatus.Pass,
                        $"Esquema v{version}, tablas clave presentes.")
                    : new DoctorCheck(
                        "database.schema",
                        DoctorStatus.Fail,
                        $"user_version={version.ToString(CultureInfo.InvariantCulture)} (esperado {DatabaseMigrator.CurrentVersion}), tablas clave={tables}/4."));
            }
            catch (SqliteException exception)
            {
                checks.Add(new DoctorCheck("database.schema", DoctorStatus.Fail, exception.Message));
            }

            try
            {
                var journal = Convert.ToString(Scalar(connection, "PRAGMA journal_mode;"), CultureInfo.InvariantCulture);
                var result = Convert.ToString(Scalar(connection, "PRAGMA integrity_check;"), CultureInfo.InvariantCulture);
                var tasks = ScalarLong(connection, "SELECT COUNT(*) FROM tasks;");

                checks.Add(string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)
                           && string.Equals(journal, "wal", StringComparison.OrdinalIgnoreCase)
                    ? new DoctorCheck("database.integrity", DoctorStatus.Pass,
                        $"integrity_check=ok, journal_mode=wal, {tasks} tarea(s).")
                    : new DoctorCheck("database.integrity", DoctorStatus.Fail,
                        $"integrity_check={result}, journal_mode={journal}."));
            }
            catch (SqliteException exception)
            {
                checks.Add(new DoctorCheck("database.integrity", DoctorStatus.Fail, exception.Message));
            }

            try
            {
                var verification = AuditLog.Verify(connection);
                checks.Add(verification.IsValid
                    ? new DoctorCheck("database.audit", DoctorStatus.Pass, "Cadena de auditoría válida.")
                    : new DoctorCheck("database.audit", DoctorStatus.Fail,
                        $"Cadena rota en seq {verification.FirstBadSeq}: {verification.Detail}"));
            }
            catch (SqliteException exception)
            {
                checks.Add(new DoctorCheck("database.audit", DoctorStatus.Fail, exception.Message));
            }
        }

        return checks;
    }

    private static DoctorCheck CheckPeerBind(DoctorOptions options)
    {
        var bind = options.PeerBind;
        if (BindPolicy.IsAnyAddress(bind))
        {
            return new DoctorCheck("bind.peer", DoctorStatus.Fail,
                $"Bind comodín prohibido: {bind}. El bind de pares debe ser la interfaz Tailscale.");
        }

        if (BindPolicy.IsTailnetBind(bind))
        {
            return new DoctorCheck("bind.peer", DoctorStatus.Pass, $"Bind admitido: {bind}.");
        }

        if (options.AllowLoopbackPeerBind && BindPolicy.IsLoopbackBind(bind))
        {
            return new DoctorCheck("bind.peer", DoctorStatus.Pass,
                $"Bind loopback admitido para pruebas: {bind}.");
        }

        return new DoctorCheck("bind.peer", DoctorStatus.Fail,
            $"Bind no admitido: {bind}. El bind de pares debe ser la interfaz Tailscale "
            + "(o loopback con --allow-loopback para pruebas).");
    }

    private static DoctorCheck CheckBind(
        string name,
        string bind,
        Func<string?, bool> required,
        string guidance)
    {
        if (BindPolicy.IsAnyAddress(bind))
        {
            return new DoctorCheck(name, DoctorStatus.Fail, $"Bind comodín prohibido: {bind}. {guidance}");
        }

        return required(bind)
            ? new DoctorCheck(name, DoctorStatus.Pass, $"Bind admitido: {bind}.")
            : new DoctorCheck(name, DoctorStatus.Fail, $"Bind no admitido: {bind}. {guidance}");
    }

    private static DoctorCheck CheckListener(
        string name,
        int port,
        bool tailnet,
        bool allowLoopbackPeer = false)
    {
        var endpoints = IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Where(endpoint => endpoint.Port == port)
            .Select(endpoint => endpoint.Address)
            .ToArray();

        if (endpoints.Length == 0)
        {
            return new DoctorCheck(name, DoctorStatus.Warn,
                $"No hay listener en el puerto {port} (servicio no iniciado o aún no enlazado).");
        }

        if (tailnet)
        {
            var exposed = endpoints.Where(address =>
                address.Equals(IPAddress.Any)
                || address.Equals(IPAddress.IPv6Any)
                || (!allowLoopbackPeer && BindPolicy.IsLoopbackBind(address.ToString()))).ToArray();

            if (exposed.Length > 0)
            {
                return new DoctorCheck(name, DoctorStatus.Fail,
                    $"El puerto {port} escucha en {Join(exposed)}; los pares solo deben escuchar en tailnet.");
            }

            var unknown = endpoints.Where(address =>
                !BindPolicy.IsTailnetBind(address.ToString())
                && !(allowLoopbackPeer && BindPolicy.IsLoopbackBind(address.ToString()))).ToArray();

            return unknown.Length == 0
                ? new DoctorCheck(
                    name,
                    DoctorStatus.Pass,
                    allowLoopbackPeer && endpoints.Any(address => BindPolicy.IsLoopbackBind(address.ToString()))
                        ? $"Puerto {port} en {Join(endpoints)} (loopback de pruebas)."
                        : $"Puerto {port} en {Join(endpoints)} (tailnet).")
                : new DoctorCheck(name, DoctorStatus.Warn,
                    $"Puerto {port} en direcciones no reconocidas como tailnet: {Join(unknown)}.");
        }

        var nonLoopback = endpoints.Where(address => !BindPolicy.IsLoopbackBind(address.ToString())).ToArray();
        return nonLoopback.Length == 0
            ? new DoctorCheck(name, DoctorStatus.Pass, $"Puerto {port} solo en loopback ({Join(endpoints)}).")
            : new DoctorCheck(name, DoctorStatus.Fail,
                $"El puerto {port} de MCP escucha fuera de loopback: {Join(nonLoopback)}.");
    }

    private static DoctorCheck CheckToken(DoctorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.McpToken))
        {
            return new DoctorCheck("token.mcp", DoctorStatus.Fail,
                "Falta el token de la fachada MCP (--mcp-token o ODW_MCP_TOKEN).");
        }

        try
        {
            var bytes = Convert.FromBase64String(options.McpToken);
            return bytes.Length >= 32
                ? new DoctorCheck("token.mcp", DoctorStatus.Pass, $"Token de fachada presente ({bytes.Length} bytes).")
                : new DoctorCheck("token.mcp", DoctorStatus.Fail,
                    $"Token demasiado corto ({bytes.Length} bytes; mínimo 32).");
        }
        catch (FormatException)
        {
            return new DoctorCheck("token.mcp", DoctorStatus.Fail, "El token no es base64 válido.");
        }
    }

    private static async Task<DoctorCheck> CheckOpenCodeVersionAsync(
        DoctorOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.OpenCodeExecutable))
        {
            return new DoctorCheck("opencode.version", DoctorStatus.Warn,
                "No se comprobó: indica --opencode <ruta> para verificar la versión.");
        }

        if (string.IsNullOrWhiteSpace(options.PinnedOpenCodeVersion))
        {
            return new DoctorCheck("opencode.version", DoctorStatus.Warn,
                "No se comprobó: no hay versión fijada.");
        }

        try
        {
            var output = await RunVersionAsync(options.OpenCodeExecutable, cancellationToken).ConfigureAwait(false);
            if (!OpenCodeVersion.TryParse(output, out var version))
            {
                return new DoctorCheck("opencode.version", DoctorStatus.Warn,
                    $"No se pudo interpretar la salida de --version: {Trim(output)}");
            }

            return string.Equals(version, options.PinnedOpenCodeVersion, StringComparison.Ordinal)
                ? new DoctorCheck("opencode.version", DoctorStatus.Pass,
                    $"OpenCode {version} coincide con la versión fijada.")
                : new DoctorCheck("opencode.version", DoctorStatus.Fail,
                    $"OpenCode {version} != versión fijada {options.PinnedOpenCodeVersion}.");
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
                                         or InvalidOperationException
                                         or OperationCanceledException)
        {
            return new DoctorCheck("opencode.version", DoctorStatus.Warn,
                $"No se pudo ejecutar {options.OpenCodeExecutable} --version: {exception.Message}");
        }
    }

    private static DoctorCheck CheckSpec(DoctorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SpecPath) || string.IsNullOrWhiteSpace(options.ExpectedSpecSha256))
        {
            return new DoctorCheck("opencode.spec", DoctorStatus.Warn,
                "No se comprobó: indica --spec <ruta> y --spec-sha256 <hash>.");
        }

        if (!File.Exists(options.SpecPath))
        {
            return new DoctorCheck("opencode.spec", DoctorStatus.Fail,
                $"No existe el spec indicado: {options.SpecPath}.");
        }

        try
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(options.SpecPath)));
            return string.Equals(hash, options.ExpectedSpecSha256.Trim(), StringComparison.OrdinalIgnoreCase)
                ? new DoctorCheck("opencode.spec", DoctorStatus.Pass, "Hash del spec coincide.")
                : new DoctorCheck("opencode.spec", DoctorStatus.Fail,
                    $"Hash del spec no coincide: {hash} != {options.ExpectedSpecSha256}.");
        }
        catch (IOException exception)
        {
            return new DoctorCheck("opencode.spec", DoctorStatus.Fail, exception.Message);
        }
    }

    private static async Task<string> RunVersionAsync(string executable, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "--version",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // npm instala un shim .cmd; CreateProcess no lo ejecuta directamente.
        if (OperatingSystem.IsWindows()
            && (executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                || executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)))
        {
            startInfo.Arguments = $"/c \"\"{executable}\" --version\"";
            startInfo.FileName = "cmd.exe";
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("No se pudo iniciar el proceso.");

        var output = await process.StandardOutput.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(timeout.Token).ConfigureAwait(false);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(output) ? error : output;
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

        connection.Open();
        return connection;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
        => Convert.ToInt64(Scalar(connection, sql), CultureInfo.InvariantCulture);

    private static string Join(IEnumerable<IPAddress> addresses)
        => string.Join(", ", addresses.Select(address => address.ToString()));

    private static string Trim(string value)
        => value.Length <= 120 ? value.Trim() : value[..117].Trim() + "...";
}
