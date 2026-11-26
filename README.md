# ODW — OpenCode Distributed Worker Network

Red cooperativa de PCs con OpenCode: cualquier nodo puede delegar una tarea a otro, ejecutarla
en un workspace aislado y devolver el resultado. Sin nodo maestro, sin ejecución remota arbitraria
y con aprobación humana fuera de banda.

Repositorio: <https://github.com/DanielCB096/ODW-OpenCode-Distributed-Worker-Network>

> Continuar desde otra PC: [`CONTINUE.md`](CONTINUE.md) · [`docs/HANDOFF.md`](docs/HANDOFF.md)
> · Grafo: [`graphify-out/GRAPH_REPORT.md`](graphify-out/GRAPH_REPORT.md)

## Estado

- Diseño cerrado y verificado contra **OpenCode 2.3.2**.
- Delegación **A→B→resultado** funcionando: propuesta, política, envío, scheduler, workspace Git,
  Job Object, ejecutor OpenCode y poller de resultado. **E2E A→B verificado en un PC** con dos nodos
  loopback y **salto real `PC-OFICINA` → `PC-CASA` por Tailscale verificado (2026-11-26)**, en
  `read_only` y escritura (`src/remoto.txt`, commit y diff).
- Resultado con `changed_paths`, `commit` y `diff` inline para tareas `read_write_worktree`; el
  workspace temporal se borra, así que el diff es la evidencia persistente.
- Aprobaciones humanas por **named pipe + CLI + Telegram**, con firma HMAC y un solo uso.
- Nodo arrancable con `odw serve --config node.json`.
- **328 pruebas**; build sin advertencias. Integración real de OpenCode con modelo gratuito.
- TLS opt-in en la API de pares y servicio Windows instalable con `odw service`.
- ACLs de workspace, `odw-agent` con DPAPI, `opencode serve` como agente, modelo gratuito propio,
  red aislada con WFP + proxy, `odw-svc` sin admin y perfiles por tarea.

Detalle: [`docs/IMPLEMENTATION.md`](docs/IMPLEMENTATION.md) · Hoja de ruta:
[`docs/ROADMAP.md`](docs/ROADMAP.md).

## Arquitectura en una vista

```
OpenCode (PC) ──MCP localhost──► ODW Node ──HTTPS/tailnet──► ODW Node ──loopback──► opencode serve
                                   │
                                   ├── SQLite (tareas, eventos, auditoría, outbox, aprobaciones)
                                   ├── Policy Engine (AUTO/ASK/DENY)
                                   ├── Scheduler + Job Objects + workspace Git aislado
                                   └── Approval Gateway (pipe / CLI; Telegram en H2)
```

- La **fachada MCP** (`127.0.0.1:7422`) expone `odw_list_peers`, `odw_get_peer_status`,
  `odw_propose_task`, `odw_get_task_status` y `odw_get_task_result`.
- La **API de pares** (`:7421`, solo tailnet) autentica por Bearer + `X-Odw-Node`.
- **No existe** aprobación por HTTP ni herramienta MCP de aprobación.
- `opencode serve` corre siempre en `127.0.0.1`, con contraseña efímera.

## Compilar y probar

```powershell
dotnet build Odw.slnx -warnaserror
dotnet test Odw.slnx
```

Integración real con OpenCode (opt-in, modelo gratuito por defecto):

```powershell
$env:ODW_OPENCODE_EXE = "C:\ruta\a\opencode.cmd"
$env:ODW_TEST_MODEL = "opencode-go/hungerg-free"
dotnet test --filter "FullyQualifiedName~OpenCodeIntegrationTests"
dotnet test --filter "FullyQualifiedName~OpenCodeTaskRunnerIntegrationTests"
```

Si no hubiera modelo gratuito disponible, se puede fijar el fallback acordado en `ODW_TEST_MODEL`.

## Arrancar un nodo

1. Copia `config/node.example.json` y `config/peers.example.json`.
2. Define los secretos por referencia (nunca en el archivo):

```powershell
$env:ODW_MCP_TOKEN = [Convert]::ToBase64String([byte[]]::new(32))
$env:ODW_PEER_TOKEN_LAPTOP = '<token-compartido-del-par>'
```

3. Ajusta `node_id`, `peer_api` (IP tailnet), repos y la ruta de `opencode`.
4. Arranca:

```powershell
dotnet run --project src/Odw.Cli -- serve --config config\node.example.json
```

`odw serve` valida binds, arranca SQLite + recuperación, los dos listeners y, si está habilitado,
el scheduler y el poller. Como servicio Windows:

```powershell
dotnet publish src/Odw.Cli -c Release -o .\publish
.\publish\odw.exe service install --config C:\ODW\node.json --exe .\publish\odw.exe
.\publish\odw.exe service status
```

## Sandbox (H3)

Con una consola **elevada** (token sin filtrar), provisiona el agente:

```powershell
powershell -ExecutionPolicy Bypass -File tools/provision-sandbox.ps1
odw sandbox check
```

El script crea `odw-agent` y `odw-svc`, guarda sus secretos con DPAPI de máquina, aplica ACLs al
workspace, copia `opencode.exe` a `%PROGRAMDATA%\ODW\opencode`, escribe la configuración del agente
con el modelo gratuito `opencode/deepseek-v4.1-flash-free` (sin credenciales del usuario), prepara
un perfil desechable por tarea y endurece `%PROGRAMDATA%\ODW` para que el agente no pueda leer
configuración, bases ni claves. Con `sandbox.run_as_agent` activo, `opencode serve` se lanza como
`odw-agent`; `odw sandbox check` debe mostrar `admin: True`, `agent: True`, `network: True` y
`enforced: True`.

El nodo corre como `odw-svc` (sin administrador):

```powershell
odw service install --config C:\ODW\node.json --exe C:\ODW\odw.exe `
  --account .\odw-svc --secret %PROGRAMDATA%\ODW\service.secret
```

La red se aísla con **WFP por SID** más un **proxy CONNECT en loopback**:

```powershell
odw sandbox net install --agent odw-agent --port 7423 `
  --state %PROGRAMDATA%\ODW\network.state
odw sandbox net status
odw sandbox net uninstall
```

Con `sandbox.network_isolation` activo, el nodo verifica los filtros (o la marca del instalador),
levanta el proxy en `network_proxy_port` (7423 por defecto) y limita al agente a `opencode.ai`
(configurable con `network_allowed_hosts`). Internet, LAN, metadata, DNS directo y el resto de
puertos de loopback quedan bloqueados; una tarea con `network: "allow"` se rechaza porque el filtro
es por usuario, no por tarea.

Además, cada tarea recibe un perfil/config/datos propios (`runtime_root`), se escanean reparse
points y hard links antes/después, y un watchdog cancela la tarea si WFP deja de estar activo. Con
`run_as_agent`, el nodo exige `scheduler.max_concurrent=1` y `workspaces.keep=false` para no
mezclar workspaces del mismo SID.

El repo configurado en `repos` debe ser **legible por `odw-svc`** (es quien clona). El agente lleva
`snapshots=false` y `safe.directory=*` en su `.gitconfig` para trabajar con un workspace creado por
otra cuenta sin intentar escribir en `.git` en modo `read_only`.

## E2E local (un solo PC)

Prueba el ciclo completo origen→destino sin una segunda máquina:

```powershell
powershell -ExecutionPolicy Bypass -File tools/e2e-loopback.ps1
```

Arranca dos nodos separados como `odw-svc` (`PC-A` origen y `PC-B` destino) en loopback, propone
por MCP, aprueba por el pipe y espera el resultado del destino con sandbox real. Requiere el
aprovisionamiento previo y consola elevada. Usa `-Keep` para conservar logs y configuración y
`-WriteTask` para validar `changed_paths`, commit y diff.

No sustituye a la prueba en dos PCs: no valida Tailscale, el firewall ni la red física.

## CLI

| Comando | Uso |
|---|---|
| `odw serve --config node.json` | Arranca el nodo |
| `odw doctor --db ... --mcp-token ...` | Diagnóstico: BD, binds, listeners, token, OpenCode y spec |
| `odw approvals list\|approve\|deny\|expire` | Aprobaciones por named pipe |
| `odw init --db ...` | Crea la base y aplica migraciones |
| `odw service install\|uninstall\|status` | Servicio Windows (`--account .\odw-svc` para mínimo privilegio) |
| `odw sandbox check` | Usuario, ACLs, red y watchdog |
| `odw sandbox net install\|uninstall\|status` | Filtros WFP del agente |
| `tools/e2e-loopback.ps1` | E2E origen→destino en un PC |
| `odw version` | Versión |

## Documentación

| Tema | Documento |
|---|---|
| Diseño y verificación de OpenCode | [`docs/H0-FINDINGS.md`](docs/H0-FINDINGS.md) |
| Decisiones (ADR) | [`docs/DECISIONS.md`](docs/DECISIONS.md) |
| Contrato, hash y estados | [`docs/DOMAIN-MODEL.md`](docs/DOMAIN-MODEL.md) |
| API de pares | [`docs/PROTOCOL.md`](docs/PROTOCOL.md) |
| Fachada MCP | [`docs/MCP-FACADE.md`](docs/MCP-FACADE.md) |
| SQLite y auditoría | [`docs/PERSISTENCE.md`](docs/PERSISTENCE.md) |
| Seguridad y sandbox | [`docs/SECURITY.md`](docs/SECURITY.md) |
| Adaptador de OpenCode | [`docs/OPENCODE-ADAPTER.md`](docs/OPENCODE-ADAPTER.md) |
| Planner y políticas | [`docs/PLANNER-POLICY.md`](docs/PLANNER-POLICY.md) |
| Pruebas | [`docs/TESTING.md`](docs/TESTING.md) |
| **Continuar desde otra PC** | [`docs/HANDOFF.md`](docs/HANDOFF.md) |
| Grafo del proyecto | [`graphify-out/GRAPH_REPORT.md`](graphify-out/GRAPH_REPORT.md) |
| Arquitectura original | [`ARCHITECTURE.md`](ARCHITECTURE.md) |

## Seguridad (resumen)

- La API de pares nunca escucha en `0.0.0.0`; MCP nunca sale de loopback.
- Tokens de par hasheados y comparación en tiempo constante; el kit se transporta por USB,
  Taildrop o gestor de contraseñas, nunca por git.
- La aprobación se vincula a `(task_id, task_hash, nonce)`, expira y es de un solo uso.
- Job Objects limitan memoria, procesos y CPU; el workspace es un clon sin remoto.
- H3 verificado: usuario `odw-agent`, ACLs de sistema de archivos, red WFP + proxy y perfiles por tarea.

## Estructura

```
src/Odw.Core                  contrato, hash canónico, máquina de estados, validación
src/Odw.Node                  servicio, SQLite, host, API de pares, scheduler, origen, aprobaciones
src/Odw.Mcp                   fachada MCP local
src/Odw.Policy                motor de reglas determinista
src/Odw.Executors.OpenCode    cliente V2 + SSE + ejecutor
src/Odw.Sandbox               workspace Git + Job Objects
src/Odw.PeerApi               cliente de pares
src/Odw.Approvals             cliente del pipe de aprobaciones
src/Odw.Cli                   ejecutable odw
tests/*                       328 pruebas
```
