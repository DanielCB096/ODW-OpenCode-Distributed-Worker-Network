using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.Json.Nodes;
using Odw.Approvals;
using Odw.Node.Configuration;
using Odw.Node.Diagnostics;
using Odw.Node.Hosting;
using Odw.Node.Persistence;
using Odw.Sandbox.Windows;

namespace Odw.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return 0;
        }

        return args[0] switch
        {
            "version" or "--version" => PrintVersion(),
            "init" => await InitAsync(args[1..]).ConfigureAwait(false),
            "serve" => await ServeAsync(args[1..]).ConfigureAwait(false),
            "service" => await ServiceAsync(args[1..]).ConfigureAwait(false),
            "sandbox" => SandboxAsync(args[1..]),
            "doctor" => await DoctorAsync(args[1..]).ConfigureAwait(false),
            "approvals" => await ApprovalsAsync(args[1..]).ConfigureAwait(false),
            _ => UsageError($"Comando desconocido: {args[0]}."),
        };
    }

    private static async Task<int> ServeAsync(string[] args)
    {
        string? configPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                case "-c":
                    if (!TryNext(args, ref i, out configPath)) return MissingValue("--config");
                    break;
                case "--help":
                case "-h":
                    PrintServeUsage();
                    return 0;
                default:
                    return UsageError($"Opción desconocida: {args[i]}.");
            }
        }

        if (string.IsNullOrWhiteSpace(configPath))
        {
            return UsageError("Falta --config <node.json>.");
        }

        try
        {
            await ServeHost.RunAsync(configPath).ConfigureAwait(false);
            return 0;
        }
        catch (NodeConfigurationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"No se pudo iniciar el nodo: {exception.Message}");
            return 1;
        }
    }

    private static void PrintServeUsage()
    {
        Console.WriteLine("Uso: odw serve --config <node.json>");
        Console.WriteLine("  --config, -c   Ruta al node.json. Requerido.");
    }

    private static int SandboxAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine("Uso: odw sandbox check [--agent <cuenta>]");
            Console.WriteLine("     odw sandbox net <install|uninstall|status> [--agent <cuenta>] [--port 7423] [--state <ruta>]");
            return 0;
        }

        if (args[0] == "net")
        {
            return SandboxNetwork(args[1..]);
        }

        if (args[0] != "check")
        {
            return UsageError($"Subcomando desconocido: {args[0]}.");
        }

        var agent = "odw-agent";
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--agent" && i + 1 < args.Length)
            {
                agent = args[++i];
            }
            else
            {
                return UsageError($"Opción desconocida: {args[i]}.");
            }
        }

        var report = SandboxInspector.Inspect(agent);
        Console.WriteLine($"windows:      {report.IsWindows}");
        Console.WriteLine($"admin:        {report.IsAdministrator}");
        Console.WriteLine($"agent:        {agent} (existe: {report.AgentAccountExists})");
        Console.WriteLine($"acls:         {report.AclsSupported}");
        Console.WriteLine($"network:      {report.NetworkEnforced}");
        Console.WriteLine($"enforced:     {report.FullyEnforced}");
        Console.WriteLine(report.Detail);
        return report.FullyEnforced ? 0 : 1;
    }

    private static int SandboxNetwork(string[] args)
    {
        var action = args.Length > 0 ? args[0] : "status";
        var agent = "odw-agent";
        var port = 7423;
        var statePath = WfpNetworkPolicy.DefaultStatePath;

        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--agent" && i + 1 < args.Length)
            {
                agent = args[++i];
            }
            else if (args[i] == "--port" && i + 1 < args.Length)
            {
                if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
                {
                    return UsageError("--port no es un número válido.");
                }
            }
            else if (args[i] == "--state" && i + 1 < args.Length)
            {
                statePath = args[++i];
            }
            else
            {
                return UsageError($"Opción desconocida: {args[i]}.");
            }
        }

        try
        {
            switch (action)
            {
                case "install":
                    WfpNetworkPolicy.Install(agent, port, statePath);
                    Console.WriteLine(
                        $"WFP instalado para {agent}: salida bloqueada; solo loopback:{port}.");
                    return 0;

                case "uninstall":
                    WfpNetworkPolicy.Uninstall(statePath);
                    Console.WriteLine("WFP de ODW eliminado.");
                    return 0;

                case "status":
                    var installed = WfpNetworkPolicy.IsInstalled(statePath);
                    Console.WriteLine($"network:      {installed}");
                    return installed ? 0 : 1;

                default:
                    return UsageError($"Subcomando desconocido: net {action}.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception
                                              or PlatformNotSupportedException
                                              or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> ServiceAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintServiceUsage();
            return 0;
        }

        if (!OperatingSystem.IsWindows())
        {
            return UsageError("El servicio Windows solo está disponible en Windows.");
        }

        var command = args[0];
        string? configPath = null;
        string? executable = null;
        string? account = null;
        string? secretPath = null;
        var name = "OdwNode";

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--config":
                case "-c":
                    if (!TryNext(args, ref i, out configPath)) return MissingValue("--config");
                    break;
                case "--exe":
                    if (!TryNext(args, ref i, out executable)) return MissingValue("--exe");
                    break;
                case "--account":
                    if (!TryNext(args, ref i, out account)) return MissingValue("--account");
                    break;
                case "--secret":
                    if (!TryNext(args, ref i, out secretPath)) return MissingValue("--secret");
                    break;
                case "--name":
                    if (!TryNext(args, ref i, out name)) return MissingValue("--name");
                    break;
                default:
                    return UsageError($"Opción desconocida: {args[i]}.");
            }
        }

        if (command == "install")
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                return UsageError("Falta --config <node.json>.");
            }

            executable ??= Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable)
                || Path.GetFileName(executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return UsageError(
                    "Publica odw como ejecutable (dotnet publish) o indica --exe <ruta a odw.exe>.");
            }

            var config = Path.GetFullPath(configPath);
            var binPath = $"\"{executable}\" serve --config \"{config}\"";

            var created = await RunScAsync(
                ["create", name, "binPath=", binPath, "start=", "auto", "DisplayName=", "ODW Node"])
                .ConfigureAwait(false);

            if (created != 0)
            {
                return created;
            }

            if (!string.IsNullOrWhiteSpace(account))
            {
                var builtIn = account.StartsWith("NT AUTHORITY\\", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(account, "LocalSystem", StringComparison.OrdinalIgnoreCase);
                var password = builtIn
                    ? null
                    : DpapiSecret.Load(secretPath ?? DefaultServiceSecretPath());

                if (!ChangeServiceAccount(name, account, password, out var error))
                {
                    Console.Error.WriteLine($"No se pudo configurar la cuenta del servicio: {error}");
                    _ = await RunScAsync(["delete", name]).ConfigureAwait(false);
                    return 1;
                }
            }

            return await RunScAsync(
                ["description", name, "OpenCode Distributed Worker Network node"],
                ["start", name]).ConfigureAwait(false);
        }

        return command switch
        {
            "uninstall" => await UninstallServiceAsync(name).ConfigureAwait(false),
            "status" => await RunScAsync(["query", name]).ConfigureAwait(false),
            _ => UsageError($"Subcomando desconocido: {command}."),
        };
    }

    private static async Task<int> UninstallServiceAsync(string name)
    {
        // Si el servicio ya está parado, 'sc stop' devuelve error; hay que borrarlo igualmente.
        _ = await RunScAsync(["stop", name]).ConfigureAwait(false);
        return await RunScAsync(["delete", name]).ConfigureAwait(false);
    }

    private static string DefaultServiceSecretPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ODW",
            "service.secret");

    private static bool ChangeServiceAccount(
        string name,
        string account,
        string? password,
        out string error)
    {
        const uint ScManagerConnect = 0x0001;
        const uint ServiceChangeConfig = 0x0002;
        const uint ServiceNoChange = 0xFFFFFFFF;

        error = string.Empty;
        var manager = OpenSCManager(null, null, ScManagerConnect);

        if (manager == IntPtr.Zero)
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        try
        {
            var service = OpenService(manager, name, ServiceChangeConfig);
            if (service == IntPtr.Zero)
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return false;
            }

            try
            {
                if (!ChangeServiceConfig(
                        service,
                        ServiceNoChange,
                        ServiceNoChange,
                        ServiceNoChange,
                        null,
                        null,
                        IntPtr.Zero,
                        null,
                        account,
                        password,
                        null))
                {
                    error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return false;
                }

                return true;
            }
            finally
            {
                _ = CloseServiceHandle(service);
            }
        }
        finally
        {
            _ = CloseServiceHandle(manager);
        }
    }

#pragma warning disable SYSLIB1054
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(
        string? machineName,
        string? databaseName,
        uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(
        IntPtr serviceControlManager,
        string serviceName,
        uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);
#pragma warning restore SYSLIB1054

    private static async Task<int> RunScAsync(params string[][] commands)
    {        foreach (var arguments in commands)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sc.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                Console.Error.WriteLine("No se pudo ejecutar sc.exe.");
                return 1;
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine(output.Trim());
            }

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine(string.IsNullOrWhiteSpace(error) ? "sc.exe falló." : error.Trim());
                return 1;
            }
        }

        return 0;
    }

    private static void PrintServiceUsage()
    {
        Console.WriteLine("Uso:");
        Console.WriteLine("  odw service install   --config <node.json> [--exe <odw.exe>] [--account .\\odw-svc] [--secret <ruta>] [--name OdwNode]");
        Console.WriteLine("  odw service uninstall [--name OdwNode]");
        Console.WriteLine("  odw service status    [--name OdwNode]");
        Console.WriteLine("Sin --account el servicio corre como LocalSystem. Con --account usa el secreto DPAPI");
        Console.WriteLine("(por defecto %PROGRAMDATA%\\ODW\\service.secret) o vacío para cuentas NT AUTHORITY.");
        Console.WriteLine("Requiere una consola elevada.");
    }

    private static async Task<int> ApprovalsAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintApprovalsUsage();
            return 0;
        }

        var command = args[0];
        var pipeName = ApprovalPipeClient.DefaultPipeName;
        var by = "human:cli";
        string? reason = null;
        string? id = null;
        var json = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pipe":
                    if (!TryNext(args, ref i, out pipeName)) return MissingValue("--pipe");
                    break;
                case "--by":
                    if (!TryNext(args, ref i, out by)) return MissingValue("--by");
                    break;
                case "--reason":
                    if (!TryNext(args, ref i, out reason)) return MissingValue("--reason");
                    break;
                case "--json":
                    json = true;
                    break;
                default:
                    if (args[i].StartsWith('-'))
                    {
                        return UsageError($"Opción desconocida: {args[i]}.");
                    }

                    if (id is not null)
                    {
                        return UsageError("Sobra un argumento.");
                    }

                    id = args[i];
                    break;
            }
        }

        if (command is "approve" or "deny" && string.IsNullOrWhiteSpace(id))
        {
            return UsageError("Falta el approval_id.");
        }

        var request = new System.Text.Json.Nodes.JsonObject
        {
            ["command"] = command,
        };

        if (id is not null)
        {
            request["id"] = id;
        }

        if (command is "approve" or "deny")
        {
            request["by"] = $"{by}:{Environment.UserName}";
        }

        if (reason is not null)
        {
            request["reason"] = reason;
        }

        try
        {
            var response = await ApprovalPipeClient.SendAsync(
                pipeName!,
                request,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            if (response is null)
            {
                Console.Error.WriteLine("El servicio no respondió por el pipe.");
                return 1;
            }

            if (json)
            {
                Console.WriteLine(response.ToJsonString(new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }));
            }
            else
            {
                PrintApprovalsResponse(response);
            }

            return response["ok"]?.GetValue<bool>() == true ? 0 : 1;
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine($"No se pudo conectar al pipe '{pipeName}'. ¿Está el servicio iniciado?");
            return 1;
        }
        catch (System.IO.IOException exception)
        {
            Console.Error.WriteLine($"Error de pipe: {exception.Message}");
            return 1;
        }
    }

    private static void PrintApprovalsResponse(System.Text.Json.Nodes.JsonNode response)
    {
        if (response["ok"]?.GetValue<bool>() != true)
        {
            Console.Error.WriteLine($"{response["code"]}: {response["detail"]}");
            return;
        }

        var data = response["data"];
        if (data is not System.Text.Json.Nodes.JsonArray array)
        {
            Console.WriteLine(data?.ToJsonString() ?? "OK");
            return;
        }

        if (array.Count == 0)
        {
            Console.WriteLine("No hay aprobaciones pendientes.");
            return;
        }

        foreach (var item in array)
        {
            Console.WriteLine(
                $"{item!["approval_id"]}  {item["task_id"]}  {item["status"]}  "
                + $"expira {item["expires_at"]}  {item["summary"]}");
        }
    }

    private static void PrintApprovalsUsage()
    {
        Console.WriteLine("Uso:");
        Console.WriteLine("  odw approvals list   [--pipe <nombre>] [--json]");
        Console.WriteLine("  odw approvals approve <approval_id> [--by <usuario>] [--pipe <nombre>]");
        Console.WriteLine("  odw approvals deny <approval_id> [--reason <texto>] [--pipe <nombre>]");
        Console.WriteLine("  odw approvals expire [--pipe <nombre>]");
    }

    private static async Task<int> InitAsync(string[] args)
    {
        string? database = Environment.GetEnvironmentVariable("ODW_DB");

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--db":
                    if (!TryNext(args, ref i, out database)) return MissingValue("--db");
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
                default:
                    return UsageError($"Opción desconocida: {args[i]}.");
            }
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            return UsageError("Falta --db (o la variable ODW_DB).");
        }

        await using var connection = SqliteDatabase.Open(database, new SqliteDatabaseOptions { Pooling = false });
        Console.WriteLine($"Base inicializada: {database} (esquema v{connection.SchemaVersion}).");
        return 0;
    }

    private static async Task<int> DoctorAsync(string[] args)
    {
        string? database = Environment.GetEnvironmentVariable("ODW_DB");
        var peerBind = "tailscale";
        var peerPort = 7421;
        var mcpBind = "127.0.0.1";
        var mcpPort = 7422;
        string? mcpToken = Environment.GetEnvironmentVariable("ODW_MCP_TOKEN");
        string? openCode = null;
        string? openCodeVersion = null;
        string? spec = null;
        string? specSha = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var argument = args[i];
            switch (argument)
            {
                case "--db":
                    if (!TryNext(args, ref i, out database)) return MissingValue(argument);
                    break;
                case "--peer-bind":
                    if (!TryNext(args, ref i, out peerBind)) return MissingValue(argument);
                    break;
                case "--peer-port":
                    if (!TryNextInt(args, ref i, out peerPort)) return InvalidValue(argument);
                    break;
                case "--mcp-bind":
                    if (!TryNext(args, ref i, out mcpBind)) return MissingValue(argument);
                    break;
                case "--mcp-port":
                    if (!TryNextInt(args, ref i, out mcpPort)) return InvalidValue(argument);
                    break;
                case "--mcp-token":
                    if (!TryNext(args, ref i, out mcpToken)) return MissingValue(argument);
                    break;
                case "--opencode":
                    if (!TryNext(args, ref i, out openCode)) return MissingValue(argument);
                    break;
                case "--opencode-version":
                    if (!TryNext(args, ref i, out openCodeVersion)) return MissingValue(argument);
                    break;
                case "--spec":
                    if (!TryNext(args, ref i, out spec)) return MissingValue(argument);
                    break;
                case "--spec-sha256":
                    if (!TryNext(args, ref i, out specSha)) return MissingValue(argument);
                    break;
                case "--json":
                    json = true;
                    break;
                case "--help":
                case "-h":
                    PrintDoctorUsage();
                    return 0;
                default:
                    return UsageError($"Opción desconocida: {argument}.");
            }
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            return UsageError("Falta --db (o la variable ODW_DB).");
        }

        var options = new DoctorOptions
        {
            DatabasePath = database,
            PeerBind = peerBind,
            PeerPort = peerPort,
            McpBind = mcpBind,
            McpPort = mcpPort,
            McpToken = mcpToken,
            OpenCodeExecutable = openCode,
            PinnedOpenCodeVersion = openCodeVersion,
            SpecPath = spec,
            ExpectedSpecSha256 = specSha,
        };

        var report = await NodeDoctor.RunAsync(options).ConfigureAwait(false);

        if (json)
        {
            Console.WriteLine(ToJson(report).ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        }
        else
        {
            foreach (var check in report.Checks)
            {
                Console.WriteLine($"{Label(check.Status),-5} {check.Name,-22} {check.Detail}");
            }

            Console.WriteLine();
            Console.WriteLine(report.Ok
                ? $"Resultado: OK ({report.Warnings} advertencia(s))."
                : $"Resultado: FALLÓ ({report.Failures} fallo(s), {report.Warnings} advertencia(s)).");
        }

        return report.Ok ? 0 : 1;
    }

    private static JsonObject ToJson(DoctorReport report)
    {
        var checks = new JsonArray();
        foreach (var check in report.Checks)
        {
            checks.Add(new JsonObject
            {
                ["name"] = check.Name,
                ["status"] = check.Status.ToString().ToUpperInvariant(),
                ["detail"] = check.Detail,
            });
        }

        return new JsonObject
        {
            ["ok"] = report.Ok,
            ["failures"] = report.Failures,
            ["warnings"] = report.Warnings,
            ["checks"] = checks,
        };
    }

    private static string Label(DoctorStatus status) => status switch
    {
        DoctorStatus.Pass => "PASS",
        DoctorStatus.Warn => "WARN",
        _ => "FAIL",
    };

    private static int PrintVersion()
    {
        var version = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? "0.0.0";

        Console.WriteLine($"odw {version}");
        return 0;
    }

    private static bool TryNext(
        string[] args,
        ref int index,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryNextInt(string[] args, ref int index, out int value)
    {
        value = 0;
        return TryNext(args, ref index, out var text)
               && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
               && value is > 0 and < 65536;
    }

    private static int MissingValue(string argument)
        => UsageError($"Falta el valor de {argument}.");

    private static int InvalidValue(string argument)
        => UsageError($"Valor inválido para {argument}.");

    private static int UsageError(string message)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine("Usa 'odw --help' para ver la ayuda.");
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("odw — OpenCode Distributed Worker Network");
        Console.WriteLine();
        Console.WriteLine("Comandos:");
        Console.WriteLine("  odw init --db <ruta>    Crea la base del nodo y aplica migraciones.");
        Console.WriteLine("  odw serve --config <n>  Arranca el nodo con node.json.");
        Console.WriteLine("  odw service install     Instala el nodo como servicio Windows.");
        Console.WriteLine("  odw sandbox check       Comprueba usuario restringido, ACLs y red (H3).");
        Console.WriteLine("  odw sandbox net install Instala los filtros WFP de bloqueo (admin).");
        Console.WriteLine("  odw doctor [opciones]   Comprueba base de datos, binds, token y OpenCode.");
        Console.WriteLine("  odw approvals <cmd>     list | approve | deny | expire (named pipe).");
        Console.WriteLine("  odw version             Muestra la versión.");
        Console.WriteLine();
        PrintDoctorUsage();
    }

    private static void PrintDoctorUsage()
    {
        Console.WriteLine("Opciones de doctor:");
        Console.WriteLine("  --db <ruta>              Base SQLite del nodo (o ODW_DB). Obligatorio.");
        Console.WriteLine("  --peer-bind <valor>      Default: tailscale.");
        Console.WriteLine("  --peer-port <puerto>     Default: 7421.");
        Console.WriteLine("  --mcp-bind <valor>       Default: 127.0.0.1.");
        Console.WriteLine("  --mcp-port <puerto>      Default: 7422.");
        Console.WriteLine("  --mcp-token <token>      Token base64 de la fachada (o ODW_MCP_TOKEN).");
        Console.WriteLine("  --opencode <ruta>        Ejecutable para comprobar la versión.");
        Console.WriteLine("  --opencode-version <v>   Versión fijada (ej. 2.3.2).");
        Console.WriteLine("  --spec <ruta>            Spec OpenAPI local a verificar.");
        Console.WriteLine("  --spec-sha256 <hash>     Hash esperado del spec.");
        Console.WriteLine("  --json                   Salida JSON.");
        Console.WriteLine();
        Console.WriteLine("Códigos de salida: 0 OK/advertencias, 1 fallo, 2 uso incorrecto.");
    }
}
