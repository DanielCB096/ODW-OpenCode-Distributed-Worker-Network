?# ODW — Estado de implementación

> Última actualización: 2026-11-24. Refleja lo que **existe y compila**, no lo planeado.

---

## 1. Solución

`Odw.slnx` (.NET 10, formato slnx). `global.json` fija la banda 10.0.x.
`Directory.Build.props`: `net10.0`, nullable, `TreatWarningsAsErrors`, builds deterministas.
`Directory.Packages.props`: versiones centrales (xunit 2.9.3, Test SDK 17.14.1).

```
src/
  Odw.Core/                  ← contrato, hash y estados (96 pruebas)
  Odw.Node/                  ← persistencia, host, origen, scheduler, aprobaciones y config (99 pruebas)
  Odw.Mcp/                   ← fachada MCP de solo lectura (23 pruebas)
  Odw.Executors.OpenCode/    ← cliente V2, SSE y ejecutor (23 pruebas)
  Odw.Policy/                ← motor de reglas (28 pruebas)
  Odw.Sandbox/               ← workspace Git y Job Objects (7 pruebas)
  Odw.Cli/                   ← ejecutable: init, doctor, version
  Odw.Trust/                 ← esqueleto
  Odw.Approvals/             ← esqueleto
  Odw.PeerApi/               ← esqueleto
tests/
  Odw.Core.Tests/            ← 96 pruebas
  Odw.Node.Tests/            ← 99 pruebas
  Odw.Mcp.Tests/             ← 26 pruebas
  Odw.Executors.OpenCode.Tests/ ← 23 pruebas
  Odw.Policy.Tests/          ← 28 pruebas
  Odw.Sandbox.Tests/         ← 7 pruebas
```

Dependencias: `Core` no referencia nada; `Policy`/`Trust`/`Sandbox`/`Executors.OpenCode` → `Core`;
`Approvals` → `Core`+`Trust`; `PeerApi`/`Mcp` → `Core`+`Policy`+`Trust`;
`Node` → todos; `Cli` → `Node`; `Executors.OpenCode` → `Core`; `Policy` → `Core`; `Sandbox` → `Core`.

---

## 2. Odw.Core

| Archivo | Contenido |
|---|---|
| `Primitives/Ulid.cs` | ULID Crockford base32, 128 bits, monótono por `TimeProvider` (estado por reloj), parse/format estricto |
| `Primitives/ContractFormat.cs` | Validación de `node_id`, repo, git ref, rutas relativas, normalización de instrucciones, Unicode |
| `Model/TaskRequest.cs` | Contrato inmutable: `TaskRequest`, `Lineage`, `WorkspaceSpec`, `TaskLimits`, `ExecutorSpec`, `ApprovalEvidence` |
| `Model/TaskResult.cs` | `TaskResult`, tests, artefactos, uso, error |
| `Model/Enums.cs` + `EnumExtensions.cs` | Estados, triggers, kinds; serialización a contrato (`WAITING_APPROVAL`, `read_only`, …) |
| `Hashing/CanonicalJson.cs` | Serializador canónico (subconjunto RFC 8785) con escapes de `JSON.stringify` |
| `Hashing/TaskHash.cs` | Proyección exacta + SHA-256; vectores V1/V2/V4 implementados |
| `Validation/TaskValidator.cs` | Reglas locales del contrato con códigos del protocolo |
| `Delegation/LineageGuard.cs` | Anti-ciclos, `max_depth`, self-delegation |
| `StateMachines/TaskStateMachine.cs` | Tabla role×from×trigger con guardas; primera guarda satisfecha gana |
| `StateMachines/TaskAggregate.cs` | Agregado por rol, `state_seq` monótono, cambios registrados |
| `StateMachines/TaskStateChange.cs` | Evento de cambio + `InvalidStateTransitionException` |
| `Contracts/IExecutor.cs` | Contrato del ejecutor y DTOs (`ExecutorLimits`, `ToolPolicy`, `ExecutorOutcome`, …) |
| `Contracts/IPolicyEngine.cs` | `PolicyEffect`, `PolicyRequest`, `PolicyDecision` |
| `Contracts/IApprovalChannel.cs` | Canal fuera de banda (Telegram/pipe), `ApprovalScope` |

### 2.1 Invariantes ya cubiertos por pruebas

- Vectores de hash V1/V2/V4; orden y duplicados de rutas; normalización de CRLF y espacios.
- Campos excluidos: `approval`, fechas, `hops`, `model_hint`, `strategy`, `protocol_version`.
- Campos incluidos: nodos, padre, `kind`, límites, rutas, repo, `max_depth`.
- Matriz completa role×estado×trigger sin excepciones; estados terminales sin salida.
- `state_seq` monótono; transición inválida lanza y no muta el agregado.
- Validación: traversal, unidad, UNC, `//`, nombres reservados, límites, ventana de 24 h,
  `code_change`+`read_only`, lineage, aprobación manipulada, aprobador no humano.
- ULID: formato, round-trip, monotonía en el mismo ms, rechazo de alfabeto inválido.

---

## 3. Odw.Node.Persistence

| Archivo | Contenido |
|---|---|
| `Persistence/Migrations/0001_init.sql` | Esquema completo + triggers append-only (auditoría y eventos) |
| `Persistence/DatabaseMigrator.cs` | Migraciones embebidas, `user_version`, backup previo, rollback total |
| `Persistence/SqliteDatabase.cs` | PRAGMAs (WAL, FK, busy_timeout…), conexiones de lectura, ciclo de vida |
| `Persistence/SqliteWriter.cs` | Canal acotado, una transacción `BEGIN IMMEDIATE` por comando, métricas |
| `Persistence/ChainVerification.cs` | Hash encadenado con longitud prefijada + resultado de verificación |
| `Persistence/AuditLog.cs` | Append y verificación de la cadena de auditoría |
| `Persistence/TaskEventLog.cs` | Append y verificación por `(task_id, role)` |
| `Persistence/Tasks/TaskStore.cs` | Create atómico (tarea+evento+auditoría+outbox), transición CAS, lecturas |
| `Persistence/Outbox/OutboxStore.cs` | Reclamo atómico, backoff 1–16 s, dead-letter, reset tras caída |
| `Persistence/StartupRecovery.cs` | `RUNNING`→`INTERRUPTED`, lease vencido→`UNREACHABLE`+`poll_task`, outbox, `service.start` |

Invariantes cubiertos: PRAGMAs y tablas; migración idempotente y rollback ante fallo; triggers
append-only; 100 escrituras concurrentes serializadas; rollback de escritura fallida; CAS con
`state_seq`; eventos y auditoría verificables; detección de manipulación; outbox exclusivo,
backoff y dead-letter; recuperación sin reejecución.

---

## 4. Odw.Mcp + Odw.Cli

| Archivo | Contenido |
|---|---|
| `Mcp/JsonRpc.cs` | Respuestas JSON-RPC 2.0 y códigos de error |
| `Mcp/McpServer.cs` | `initialize`, `ping`, `tools/list`, `tools/call`; notificaciones sin respuesta |
| `Mcp/McpEndpoint.cs` | `POST /mcp` sin estado: bearer en tiempo constante, `401/413/415/204/405` |
| `Mcp/Tools/PeerTools.cs` | `odw_list_peers`, `odw_get_peer_status` (solo lectura) |
| `Mcp/Tools/TaskTools.cs` | `odw_get_task_status`, `odw_get_task_result` (solo lectura) |
| `Node/Persistence/Peers/SqlitePeerDirectory.cs` | Pares + último estado persistido; online por `last_seen_at` |
| `Node/Persistence/Queries/SqliteTaskQuery.cs` | Vistas de estado/resultado y `next_action` |
| `Node/Diagnostics/NodeDoctor.cs` | 10 comprobaciones: BD/integridad/auditoría, binds, listeners, token, OpenCode, spec |
| `Cli/CliApplication.cs` | `odw init`, `odw doctor [--json]`, `odw version` |

El doctor abre SQLite en **solo lectura** (no migra ni escribe), valida que el bind de pares sea
tailnet y el de MCP loopback, detecta puertos expuestos, exige token de 32 bytes, ejecuta
`opencode --version` (también con shims `.cmd`) y verifica el hash del spec.

Salida real comprobada en este equipo (OpenCode 2.3.2 + spec fijado): 8 `PASS` y 2 `WARN`
(listeners no iniciados), `exit 0`. Con versión fijada distinta: `opencode.version FAIL`,
`exit 1`.

---

## 5. Odw.Executors.OpenCode

| Archivo | Contenido |
|---|---|
| `OpenCode/OpenCodeDtos.cs` | Conexión, reglas de permiso, sesión, mensajes, permisos, errores y parser |
| `OpenCode/IOpenCodeClient.cs` | Puerto del cliente (10 operaciones) |
| `OpenCode/OpenCodeClient.cs` | HTTP tipado: rutas `/api/*`, Basic auth, cuerpos exactos, SSE `data:` |
| `OpenCode/OpenCodeServer.cs` | `serve --port 0 --hostname 127.0.0.1`, URL por stdout, `/api/info`, parada del árbol |
| `OpenCode/OpenCodeExecutor.cs` | `IExecutor`: sesión, prompt, mapeo de eventos, broker de permisos |

- El cliente **rechaza bases que no sean HTTP loopback**.
- `types` del SSE se envía repetido (`?types=a&types=b`); con comas OpenCode responde `400`.
- El broker solo responde `once` o `reject`; nunca `always`. Sin handler, rechaza.
- `OpenCodeServer` no aplica Job Object ni usuario restringido (H3); se documenta como límite.
- Integración real verificada: arranque, sesión, prompt y `OK` contra OpenCode 2.3.2 (21/21).

---

## 6. Odw.Policy

| Archivo | Contenido |
|---|---|
| `PolicyModels.cs` | `PolicyDocument`, `PolicyRule`, `PolicyWhen`, rangos, ventana horaria y errores |
| `PolicyDocumentLoader.cs` | Carga JSON estricta (miembros desconocidos fallan), validación fail-closed |
| `PolicyEngine.cs` | Orden por prioridad y declaración, primera coincidencia, evaluación pura |

- Semántica: claves distintas = AND; valores de un array = OR; `direction` deriva del rol
  (`out` origen, `in` destino).
- `any_action` usa el nuevo `PolicyRequest.Actions` (lo llena el broker de permisos).
- Ventana horaria: `days` 1=lunes…7=domingo; `from > to` se interpreta como ventana nocturna
  que termina al día siguiente. `tz` se resuelve con el resolver inyectado.
- Validación: versión, ID único con regex, prioridad 0..1000, efecto, razón, enums, rangos
  (`gte <= lte`), días, `HH:mm`, zona horaria y límite de 256 reglas. Cualquier problema lanza
  `PolicyValidationException` con la lista de issues: el host no debe arrancar.
- 28 pruebas, incluida la carga de `config/policy.example.json` y la evaluación real de sus
  reglas.

---

## 7. Host del nodo y API de pares

| Archivo | Contenido |
|---|---|
| `Hosting/NodeHostOptions.cs` | Opciones y validación fail-closed de binds, node_id y token |
| `Hosting/NodeHost.cs` | Dos `WebApplication` (pares y MCP), SQLite, recuperación y parada ordenada |
| `PeerApi/PeerEndpoints.cs` | `hello`, `status`, `health`, `POST /tasks`, `GET /tasks/{id}` |
| `Peers/PeerRegistry.cs` | Pares configurados y autenticación Bearer con hash y comparación constante |
| `Contracts/TaskContractReader.cs` | JSON del contrato → `TaskRequest`, con campo exacto en el error |
| `Tasks/TaskCoordinator.cs` | `TaskAggregate` + `TaskStore`: CAS, evento, auditoría y reintento |

Comportamiento del alta de tarea:

1. Auth: `Authorization: Bearer` + `X-Odw-Node`; el nodo debe estar configurado.
2. Contrato → validación → `target_node` debe ser este nodo.
3. Idempotencia por `task_id`: mismo hash ⇒ `200` + `Idempotent-Replay`;
   hash distinto ⇒ `409 TASK_ID_CONFLICT`.
4. Política del destino: `DENY` ⇒ `403`; `ASK` ⇒ `AWAITING_LOCAL_APPROVAL`;
   `AUTO` ⇒ `QUEUED`.
5. Persistencia atómica: tarea + evento `task.received` + auditoría.
6. `GET /tasks/{id}` solo para el par que la envió, con `ETag` y `304`.

Limitaciones explícitas de este corte:

- No hay scheduler todavía: las tareas quedan en `QUEUED` (o esperando aprobación) y nadie las
  ejecuta todavía.
- No están implementados `SSE`, `messages`, `cancel`, `result` ni artefactos.
- Sin Approval Gateway, `ASK` deja la tarea esperando; no se autoaprueba nunca.
- `PeerEndpoints` vive en `Odw.Node` (necesita el coordinador); el proyecto `Odw.PeerApi`
  sigue como marcador hasta extraer una abstracción.

---

## 8. Sandbox y scheduler de ejecución

| Archivo | Contenido |
|---|---|
| `Sandbox/Workspaces/WorkspaceManager.cs` | Clon local completo, sin remoto; rama `odw/<task>` o base detached |
| `Sandbox/Windows/JobObject.cs` | Job Object: memoria, procesos, CPU y muerte en cascada |
| `Node/Scheduling/ExecutionScheduler.cs` | `QUEUED` → workspace → `RUNNING` → runner → estado final (CAS) |
| `Node/Scheduling/OpenCodeTaskRunner.cs` | Servidor OpenCode bajo Job Object, stream, prompt y resultado |

Flujo del scheduler:

1. Toma hasta `MaxConcurrentTasks` tareas `QUEUED`.
2. Parsea el contrato guardado; prepara el workspace (fallo ⇒ `FAILED`).
3. `QUEUED` → `RUNNING` con lease de `max_minutes` (CAS).
4. Ejecuta el runner; `Succeeded` ⇒ `COMPLETED` (o `REVIEW_REQUIRED` si hubo
   `tool.failed`/revisión); `Failed` ⇒ `FAILED`; cancelado ⇒ `CANCELLING` → `CANCELLED`.
5. Borra el workspace al terminar (salvo `KeepWorkspaces`).

El runner real arranca `opencode serve` en el workspace, asigna el proceso al Job Object, abre
sesión, suscribe el SSE, envía el prompt y construye el resultado con el último mensaje del
asistente. Integración real ejecutada contra OpenCode 2.3.2 (job + workspace + `OK`).

Límites que siguen abiertos y se declaran:

- `read_only` significa base detached, **no** ACL de solo lectura; el proceso aún puede escribir.
  La protección real por ACL/usuario restringido es H3.
- No hay renovación de lease durante la ejecución: se usa `max_minutes`.
- El resultado no calcula todavía `changed_paths`/`commit` (quedan vacíos) y no empaqueta artefactos.
- El runner usa `ToolPolicy.AskAll`; sin un `ToolPermissionHandler`, toda herramienta se rechaza.

---

## 9. Lado origen y poller

| Archivo | Contenido |
|---|---|
| `PeerApi/PeerApiClient.cs` | Cliente de pares: alta, estado, resultado y ping (Bearer + `X-Odw-Node`) |
| `Node/Origins/OriginProposalService.cs` | Valida, aplica política saliente, persiste y envía |
| `Node/Origins/OriginPoller.cs` | Refresca estado/resultado del par y avanza la máquina de estados |
| `Node/Contracts/TaskContractWriter.cs` | Serializa el contrato al JSON wire |
| `Node/Persistence/Peers/PeerStore.cs` | Sincroniza pares configurados y `last_seen_at` |

Flujo:

1. `odw_propose_task` (o el servicio) valida, calcula hash y consulta política **saliente**.
2. `AUTO` ⇒ persiste `APPROVED`, marca `SENT`, hace `POST /tasks`; `202` ⇒ `ACCEPTED`.
3. `ASK` ⇒ queda en `WAITING_APPROVAL` y **no se envía nada** (falta Approval Gateway).
4. `DENY` ⇒ error `POLICY_DENIED`.
5. El poller consulta el par, mapea estados remotos (`QUEUED`, `RUNNING`, `COMPLETED`…) y
   recupera el resultado; `Accepted→Running→Done` con `result_json` local.
6. Idempotencia: repetir la misma propuesta devuelve la tarea existente sin reenviar.

Prueba real de dos nodos en el mismo proceso: A propone, B acepta, el scheduler de B ejecuta y A
recupera `succeeded`; también se cubren ASK sin envío, idempotencia y la herramienta MCP.

Límites actuales: sin aprobación humana accionable (`ASK` espera), sin cancelación remota, sin SSE
y con un token **compartido por par** (bidireccional) en vez de tokens direccionales.

---

## 10. Aprobaciones humanas

| Archivo | Contenido |
|---|---|
| `Persistence/Migrations/0002_approval_requests.sql` | Solicitudes con hash, nonce, estado, expiración y uso |
| `Node/Approvals/ApprovalService.cs` | Crear/listar/decidir/expirar/consumir + auditoría |
| `Node/Approvals/ApprovalCoordinator.cs` | Aplica la decisión, transiciona y envía si se aprobó |
| `Node/Approvals/ApprovalPipeServer.cs` | Named pipe con comandos `list/approve/deny/expire` |
| `Approvals/ApprovalPipeClient.cs` | Cliente del pipe para el CLI |
| `Cli` | `odw approvals list|approve|deny|expire` |

Garantías:

- La aprobación está vinculada a `(task_id, task_hash, nonce)`; un hash distinto devuelve
  `HashMismatch` y no decide nada.
- Expira (15 min por defecto) y es de **un solo uso** (`used_at`).
- Aprobar transiciona `WAITING_APPROVAL → APPROVED` y envía; denegar → `REJECTED`.
- Cada solicitud y decisión queda en la auditoría encadenada.
- La DACL del pipe por defecto es administradores + SYSTEM; en pruebas se usa la ACL del proceso.

Pendiente de H2: bot de Telegram, firma HMAC de la aprobación y notificación proactiva.

---

## 11. Configuración y `odw serve`

| Archivo | Contenido |
|---|---|
| `Configuration/NodeConfig.cs` | Modelo de `node.json` (listeners, peers, políticas, scheduler, poller, aprobaciones) |
| `Configuration/NodeConfigLoader.cs` | Carga, expande rutas, resuelve secretos y valida |
| `Hosting/NodeBootstrap.cs` | Compone `NodeHostOptions` desde la configuración |
| `Odw.Cli serve` | Arranca el nodo en primer plano hasta Ctrl+C |

Reglas de configuración:

- `peer_api.bind: "tailscale"` resuelve la IP de la interfaz Tailscale; si no existe, hay que
  indicar `peer_api.address` o una IP concreta.
- Secretos **solo** por referencia `env:NAME` o `file:ruta`; un secreto literal falla.
  `dpapi:` está declarado pero aún no implementado.
- `policy` es una ruta a `policy.json`; si se omite, se aplica `ASK` por defecto.
- `scheduler.enabled` exige `opencode.executable` y al menos un repo registrado.
- `approvals.pipe_name` debe coincidir con el `--pipe` del CLI.

Ejemplo completo en `config/node.example.json` (`config/peers.example.json` como `peers_file`).

```powershell
$env:ODW_MCP_TOKEN = [Convert]::ToBase64String([byte[]]::new(32))
$env:ODW_PEER_TOKEN_LAPTOP = '<token-compartido>'
odw serve --config config\node.example.json
```

Límites: `odw serve` es un proceso en primer plano; instalar como **servicio Windows** y TLS en
la API de pares quedan pendientes. El modo desarrollo puede usar loopback con
`peer_api.allow_loopback: true`.

---

## 12. Comandos

```powershell
dotnet build Odw.slnx -warnaserror
dotnet test Odw.slnx
dotnet run --project src/Odw.Cli -- init --db .\odw.db
dotnet run --project src/Odw.Cli -- doctor --db .\odw.db --mcp-token <base64-32B> --json
dotnet run --project src/Odw.Cli -- serve --config config\node.example.json

# integración real con OpenCode (opt-in). Modelo gratuito por defecto;
# si no está disponible, fija ODW_TEST_MODEL (fallback acordado).
$env:ODW_OPENCODE_EXE = "C:\ruta\a\opencode.cmd"
$env:ODW_TEST_MODEL = "opencode-go/hungerg-free"
dotnet test --filter "FullyQualifiedName~OpenCodeIntegrationTests"

# aprobaciones por pipe (con el servicio iniciado)
odw approvals list --pipe odw-approvals
odw approvals approve <approval_id> --by cli:tu-usuario
```

Estado actual: build 0 warnings, **279/279 pruebas** (96 Core + 99 Node + 26 MCP + 23 OpenCode + 28 Policy + 7 Sandbox).
La integración con OpenCode se ejecutó contra v2.3.2 con modelo gratuito y no corre sin `ODW_OPENCODE_EXE`.

---

## 13. Decisiones realizadas en código

- `max_depth` se compara con `>`: `hops` incluye al emisor, `max_depth = 1` = un salto (A→B).
- Rutas canónicas sin `/` final; `src/` y `src` son la misma ruta para el hash.
- Instrucciones sin salto final; se recortan espacios finales por línea.
- `ReviewRequired` se elige cuando `ValidationViolated`, no por `TaskFailed` del ejecutor.
- `UNREACHABLE` es recuperable: `TaskProgress` lo devuelve a `Running`.
- `state`/`role`/`kind`/`mode` se guardan en su forma de contrato (legible en SQL).
- La cadena de auditoría/eventos usa concatenación con longitud prefijada, no JCS.
- Microsoft.Data.Sqlite **10.0.15** (la 8.x arrastraba SQLitePCLRaw vulnerable).
- MCP sin estado: solo `POST`, sin SSE ni `Mcp-Session-Id`; `GET /mcp` = `405`.
- La fachada no contacta pares ni cambia estado: lee SQLite y devuelve `next_action`.
- El doctor es de solo lectura sobre la BD y falla (no advierte) ante binds comodín o token débil.
- El cliente de OpenCode solo acepta loopback y el SSE usa `types` repetido.
- `OpenCodeServer` acepta un `AttachProcess`; el runner lo usa para el Job Object.
- La política es fail-closed: documento inválido lanza y el host no debe arrancar.
- Las reglas se evalúan sin reloj: `Now` viaja en `PolicyRequest`.
- La API de pares exige `X-Odw-Node` y lo cruza con el token; un token válido de otro par no sirve.
- `ASK` en el destino persiste `AWAITING_LOCAL_APPROVAL`; no existe autoaprobación.
- El scheduler usa CAS: dos instancias no ejecutan la misma tarea dos veces.
- `read_only` hoy es base detached, no ACL: se declara como pendiente de H3.
- Token de par **compartido** (bidireccional) en este MVP; la rotación/direccionalidad llega con H6.
- `ASK` en origen no envía: la tarea queda esperando aprobación humana.
- El poller es el único que muta el origen tras el envío; las consultas MCP siguen siendo locales.
- La aprobación se vincula al hash, expira y es de un solo uso; el pipe usa DACL admin+SYSTEM.
- Las pruebas de OpenCode usan modelo gratuito por defecto (`ODW_TEST_MODEL` permite el fallback).

---

## 14. Siguiente

1. Instalar `odw serve` como servicio Windows (`sc.exe`/`UseWindowsService`) y TLS en pares.
2. Bot de Telegram + firma HMAC de aprobaciones (resto de H2).
3. `Odw.Sandbox` H3: usuario `odw-agent`, ACLs y red restringida.
4. Protocolo restante: SSE de eventos, mensajes, cancelación y artefactos.
5. Cerrar H0-6 (aislamiento por directorio) con instancias ya separadas.

