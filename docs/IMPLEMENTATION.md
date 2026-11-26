# ODW — Estado de implementación

> Última actualización: 2026-11-24. Refleja lo que **existe y compila**, no lo planeado.

---

## 1. Solución

`Odw.slnx` (.NET 10, formato slnx). `global.json` fija la banda 10.0.x.
`Directory.Build.props`: `net10.0`, nullable, `TreatWarningsAsErrors`, builds deterministas.
`Directory.Packages.props`: versiones centrales (xunit 2.9.3, Test SDK 17.14.1).

```
src/
  Odw.Core/                  ← contrato, hash y estados (96 pruebas)
  Odw.Node/                  ← persistencia, host, origen, scheduler, aprobaciones y config (117 pruebas)
  Odw.Mcp/                   ← fachada MCP de solo lectura (23 pruebas)
  Odw.Executors.OpenCode/    ← cliente V2, SSE y ejecutor (23 pruebas)
  Odw.Policy/                ← motor de reglas (28 pruebas)
  Odw.Sandbox/               ← workspace Git, ACLs, WFP, proxy y perfiles por tarea (38 pruebas)
  Odw.Cli/                   ← ejecutable: init, doctor, sandbox, service
  Odw.Trust/                 ← esqueleto
  Odw.Approvals/             ← esqueleto
  Odw.PeerApi/               ← esqueleto
tests/
  Odw.Core.Tests/            ← 96 pruebas
  Odw.Node.Tests/            ← 117 pruebas
  Odw.Mcp.Tests/             ← 26 pruebas
  Odw.Executors.OpenCode.Tests/ ← 23 pruebas
  Odw.Policy.Tests/          ← 28 pruebas
  Odw.Sandbox.Tests/         ← 38 pruebas
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
- Campos excluidos: `approval`, fechas, `hops`, `model_hint`, `strategy`, `protocol_version`; `model_profile` se incluye si existe.
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
| `Sandbox/Workspaces/WorkspaceManager.cs` | Clon local completo, sin remoto; rama `odw/<task>` o base detached; ACLs opcionales |
| `Sandbox/Windows/JobObject.cs` | Job Object: memoria, procesos, CPU y muerte en cascada |
| `Sandbox/Windows/SandboxInspector.cs` | Comprueba Windows, admin, cuenta `odw-agent` y si el aislamiento está activo |
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

- `read_only` combina clon detached y ACL de solo lectura para el agente (RX). La protección
  depende de que `sandbox.run_as_agent` esté activo; si OpenCode corre como el servicio, la ACL
  no le afecta.
- Verificado en esta máquina: `odw-agent` creado, secreto DPAPI, ACLs sin herencia, ejecución de
  procesos como el agente, **`opencode serve` arrancando como `odw-agent`** con su perfil y
  variables de entorno propias, y **una tarea real completada** con el modelo gratuito
  `opencode/deepseek-v4.1-flash-free`, sin credenciales del usuario. Escritura denegada en
  `read_only` y permitida en escritura. Lectura de los datos del nodo denegada al agente (probado
  con un proceso real).
- Red verificada con WFP + proxy: como `odw-agent`, Internet, LAN, DNS directo y otros puertos de
  loopback quedan bloqueados; solo `127.0.0.1:<proxy>` está permitido y el modelo se alcanza por un
  proxy CONNECT con allowlist (`opencode.ai`). Una tarea real completó a través del proxy.
  `models.dev` no es necesario en ejecución (el modelo se fija en el contrato/config); si se
  necesita refrescar el catálogo, puede añadirse a `network_allowed_hosts`.
- Perfiles de modelo (`models`): `default`, `allowed` y `profiles`. Solo los perfiles
  de `allowed` se exponen al runner; `paid-deepseek` puede estar definido pero
  deshabilitado. `odw_propose_task` acepta `model_profile`; el destino valida contra
  la allowlist y el resultado incluye `model_profile`, `model_provider`, `model_id` y
  `cost_usd`. `model_hint` queda como compatibilidad y solo se acepta si coincide con
  un perfil permitido. Las credenciales de un modelo de pago no se copian al agente;
  requieren gateway o credencial del servicio.
  Verificado en dos PCs el 2026-11-26: tarea `01M8CGJSEFCR29SPN1SSJ4M7C7` con
  `model_profile=free` → `DONE`, `MODELPROFILE-FREE`, `model_provider=opencode`,
  `model_id=deepseek-v4.1-flash-free`, `cost_usd=0`; `PC-CASA` en `87855c7`.
- `tools/doctor-node.ps1` ejecuta `odw doctor` desde `node.json` (resuelve el token sin
  imprimirlo). En PC-OFICINA: 10/10 PASS con `-AllowLoopback`; falta ejecutarlo en PC-CASA.
- Perfil desechable por tarea (`AgentRuntime`): configuración, datos, caché y temporales propios;
  el perfil real del agente queda sin escritura. `sandbox.run_as_agent` exige
  `scheduler.max_concurrent=1` y `workspaces.keep=false` (`SandboxGuard`).
- `WorkspaceGuard` escanea reparse points y hard links antes y después de la tarea; un junction
  previo impide ejecutar y uno creado durante la tarea deja `ReviewRequired`.
- `NetworkIsolationWatchdog` comprueba el aislamiento durante la ejecución y cancela si
  desaparece.
- El nodo corre como `odw-svc` (sin administrador): el aprovisionamiento instala WFP y le concede
  `SeServiceLogonRight`/`SeIncreaseQuotaPrivilege`; `odw service install --account` usa DPAPI y
  `ChangeServiceConfig`. **Tarea real A→B completada como servicio** (`E2E-SVC`) con workspace,
  proxy, WFP y perfil por tarea.
- El workspace aplica la ACL **antes** del `git clone` para que los archivos ocultos de `.git`
  hereden el acceso del agente; el padre de la raíz de workspaces recibe `RX` para que git pueda
  hacer `stat` al descubrir el repo. El runtime del agente incluye `.gitconfig` con
  `safe.directory=*` (el repo lo crea `odw-svc` y git corre como `odw-agent`). El repo de origen
  debe ser legible por `odw-svc`.
- `snapshots=false` en la configuración del agente: evita que OpenCode intente operar git en un
  workspace de solo lectura. El resultado sigue siendo el resumen del modelo.
- `odw-agent` conserva el logon interactivo local que exige `CreateProcessWithLogonW`; tiene
  denegados el logon de red y el interactivo remoto.
- E2E loopback verificado: dos nodos `odw-svc` (`PC-A` origen, `PC-B` destino), propuesta por MCP,
  aprobación por pipe, ejecución con sandbox. Casos `E2E-LOOPBACK` (read_only) y `E2E-WRITE`
  (read_write con `changed_paths`, `commit` y `diff`). **Prueba real en dos PCs verificada
  (2026-11-26):** `PC-OFICINA` → `PC-CASA` por Tailscale, con los casos `E2E-REMOTE` y
  `E2E-REMOTE-WRITE`.
- Nodo destino real preparado (`tools/prepare-destination-node.ps1`): servicio `OdwNode` como
  `.\odw-svc`, dependiente de `Tailscale` y con reinicio automático; listener en la IPv4 tailnet;
  firewall solo en la interfaz Tailscale; energía sin suspensión y Update sin reinicio automático.
  `peer_api.address` explícito evita que el bind resuelva la IPv6 de Tailscale.
- Ensayo A→B contra la IP tailnet con el origen en el mismo equipo
  (`tools/h1-remote-test.ps1`): `E2E-REMOTE` (read_only) y `E2E-REMOTE-WRITE`
  (`changed_paths=["src/remoto.txt"]`, commit y diff), con aprobación por pipe sin elevación
  (`approvals.security_descriptor` incluye el SID del usuario). El **salto real entre dos PCs** se
  verificó el 2026-11-26 desde `PC-OFICINA` a `PC-CASA` con el token del kit: `read_only`
  (`01M8C901HCSFCE93QJH1P3YRYD`) y `read_write_worktree` (`01M8C90GX6903D3VZBMEGXKAD9`,
  `changed_paths=["src/remoto.txt"]`, commit `48d36b9…`, diff `+HOLA`). El token del kit sigue
  vivo y se mantiene para la campaña de pruebas; al terminar, rotarlo y borrar el repo puente y
  el kit.
- `tools/origin-node.ps1` deja un origen persistente con MCP en `127.0.0.1:7422`; verificado
  desde OpenCode el 2026-11-26: `odw_propose_task`, aprobación por pipe y resultado
  `HOLA DESDE PC-CASA` (tarea `01M8CA57XKBFBGGTWPMQQVASVD`).
- `network: "none"` pasa a significar **sin red arbitraria**: se permite únicamente el gateway del
  modelo. `network: "allow"` se rechaza mientras el aislamiento sea por SID (no hay granularidad
  por tarea); el runner falla con `NETWORK_ALLOW_UNSUPPORTED`.
- Pendiente: doctor en PC-CASA y reinicio a mitad; cobertura 97,8 %. El
  resultado ya calcula `changed_paths`, `commit` y un `diff` inline (256 KiB) para tareas de
  escritura; el empaquetado de artefactos queda pendiente.
- No hay renovación de lease durante la ejecución: se usa `max_minutes`.
- El runner captura los cambios antes de borrar el workspace: en `read_write_worktree` hace
  `git add`, guarda el diff, crea el commit y valida las rutas contra `allowed_paths`/`denied_paths`.
  El workspace temporal se borra después: el `diff` inline es la evidencia persistente; el SHA es
  informativo y un commit/artefacto descargable queda pendiente.
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
| `Node/Approvals/ApprovalSigner.cs` | HMAC-SHA-256 sobre la decisión (hash, nonce, approver, fecha) |
| `Node/Approvals/TelegramApprovalService.cs` | Notificaciones y callbacks con allowlist |
| `Approvals/TelegramClient.cs` | `getUpdates`, `sendMessage` con teclado inline, `answerCallbackQuery` |
| `Approvals/ApprovalPipeClient.cs` | Cliente del pipe para el CLI |
| `Cli` | `odw approvals list|approve|deny|expire` |

Garantías:

- La aprobación está vinculada a `(task_id, task_hash, nonce)`; un hash distinto devuelve
  `HashMismatch` y no decide nada.
- Expira (15 min por defecto) y es de **un solo uso** (`used_at`).
- Aprobar transiciona `WAITING_APPROVAL → APPROVED` y envía; denegar → `REJECTED`.
- Cada solicitud y decisión queda en la auditoría encadenada.
- La decisión se firma con **HMAC-SHA-256**; `ApprovalService.Verify` detecta manipulación o clave
  distinta. La clave se toma de `approvals.hmac_key_ref` (`env:`/`file:`) o se genera en
  `<data_dir>/approvals.key` la primera vez.
- La DACL del pipe por defecto es administradores + SYSTEM; en pruebas se usa la ACL del proceso.
- **Telegram** (opt-in): long polling saliente, allowlist de `user_id` y `chat_id`, botones
  Aprobar/Denegar y respuesta al callback. No abre puertos.

Pendiente: probar con un bot real (no hay token en el repositorio).

---

## 11. Configuración y `odw serve`

| Archivo | Contenido |
|---|---|
| `Configuration/NodeConfig.cs` | Modelo de `node.json` (listeners, peers, políticas, scheduler, poller, aprobaciones) |
| `Configuration/NodeConfigLoader.cs` | Carga, expande rutas, resuelve secretos y valida |
| `Hosting/NodeBootstrap.cs` | Compone `NodeHostOptions` desde la configuración |
| `Odw.Cli/ServeHost.cs` | `IHostedService` del nodo (consola y servicio Windows) |
| `Odw.Cli serve` | Arranca el nodo hasta Ctrl+C o parada del servicio |

Reglas de configuración:

- `peer_api.bind: "tailscale"` resuelve la IP de la interfaz Tailscale; si no existe, hay que
  indicar `peer_api.address` o una IP concreta.
- Secretos **solo** por referencia `env:NAME` o `file:ruta`; un secreto literal falla.
  `dpapi:` está declarado pero aún no implementado.
- `policy` es una ruta a `policy.json`; si se omite, se aplica `ASK` por defecto.
- `scheduler.enabled` exige `opencode.executable` y al menos un repo registrado.
- `approvals.pipe_name` debe coincidir con el `--pipe` del CLI.
- `approvals.telegram.enabled` exige `bot_token_ref`, `allowed_user_ids` y `allowed_chat_ids`.
- `approvals.hmac_key_ref` es opcional; si falta, se genera `<data_dir>/approvals.key`.
- `sandbox.run_as_agent` arranca `opencode serve` con las credenciales DPAPI de `agent_secret_path`;
  el ejecutable debe ser accesible al agente (el aprovisionamiento copia `opencode.exe` a
  `<data_dir>/opencode` con RX).
- El aprovisionamiento escribe `C:\Users\<agente>\.config\opencode\opencode.json` con el
  modelo gratuito `opencode/deepseek-v4.1-flash-free` (parámetro `-AgentModel`). No se copian
  credenciales ni la base de datos del usuario; los modelos `opencode-go/*` requieren su cuenta y
  no están disponibles para el agente.
- El aprovisionamiento endurece `%PROGRAMDATA%\ODW`: raíz solo para servicio/SYSTEM/administradores,
  agente con traverse y `DENY (R)` sobre configuración, bases y claves existentes. Ejecútalo con el
  nodo detenido.
- `sandbox.network_isolation` (requiere `run_as_agent`) exige filtros WFP persistentes instalados por
  el aprovisionamiento para el SID del agente: bloqueo total salvo
  `127.0.0.1:<network_proxy_port>` (por defecto 7423). El nodo verifica los filtros (admin) o la
  marca `network_state_path` (servicio sin privilegios), arranca un proxy CONNECT en ese puerto y
  pasa `HTTPS_PROXY`/`HTTP_PROXY` al agente. La allowlist (`network_allowed_hosts`) limita los
  destinos del proxy y rechaza IPs privadas/LAN. Comandos:
  `odw sandbox net install|uninstall|status`.
- `sandbox.runtime_root` y `sandbox.agent_config_path` definen el perfil desechable por tarea y la
  plantilla de `opencode.json`; el aprovisionamiento los crea. El perfil real del agente queda sin
  escritura.
- `peer_api.tls.enabled` exige `certificate_path` (PFX con clave privada) y permite
  `password_ref`; con TLS el listener de pares usa **HTTPS**.
- `odw serve` usa el *host* de .NET: Ctrl+C en consola y parada del servicio Windows.
- `odw service install --account .\odw-svc` crea el servicio con `sc.exe`, lee el secreto DPAPI
  (`service.secret`) y cambia la cuenta con `ChangeServiceConfig` (sin exponer la contraseña en la
  línea de comandos). Requiere consola elevada y un `odw.exe` publicado con `dotnet publish`;
  `odw-svc` necesita RX sobre ese binario y su directorio.

Ejemplo completo en `config/node.example.json` (`config/peers.example.json` como `peers_file`).

```powershell
$env:ODW_MCP_TOKEN = [Convert]::ToBase64String([byte[]]::new(32))
$env:ODW_PEER_TOKEN_LAPTOP = '<token-compartido>'
odw serve --config config\node.example.json
```

El servicio se instala con `sc.exe` y corre como `.\odw-svc` con derechos de logon dedicados; el
salto real entre PC-OFICINA y PC-CASA está verificado (2026-11-26). El modo desarrollo puede usar
loopback con `peer_api.allow_loopback: true`.

---

## 12. Comandos

```powershell
dotnet build Odw.slnx -warnaserror
dotnet test Odw.slnx
dotnet run --project src/Odw.Cli -- init --db .\odw.db
dotnet run --project src/Odw.Cli -- doctor --db .\odw.db --mcp-token <base64-32B> --json
dotnet run --project src/Odw.Cli -- serve --config config\node.example.json
# instalación como servicio (consola elevada, odw.exe publicado)
odw service install --config C:\ODW\node.json --exe C:\ODW\odw.exe `
  --account .\odw-svc --secret %PROGRAMDATA%\ODW\service.secret

# sandbox H3 (consola elevada)
powershell -ExecutionPolicy Bypass -File tools\provision-sandbox.ps1
odw sandbox check --agent odw-agent
odw sandbox net install --agent odw-agent --port 7423

# E2E origen→destino en un PC (dos nodos loopback como odw-svc)
powershell -ExecutionPolicy Bypass -File tools\e2e-loopback.ps1

# nodo destino permanente + kit y prueba desde otra PC (ver docs/RUNBOOK-DOS-PCS.md)
powershell -ExecutionPolicy Bypass -File tools\prepare-destination-node.ps1 -OriginAddress http://<IP-TAILNET-OFICINA>:7421
powershell -ExecutionPolicy Bypass -File tools\prepare-origin-kit.ps1
powershell -ExecutionPolicy Bypass -File tools\h1-remote-test.ps1

# integración real con OpenCode (opt-in). Modelo gratuito por defecto;
# si no está disponible, fija ODW_TEST_MODEL (fallback acordado).
$env:ODW_OPENCODE_EXE = "C:\ruta\a\opencode.cmd"
$env:ODW_TEST_MODEL = "opencode/deepseek-v4.1-flash-free"
dotnet test --filter "FullyQualifiedName~OpenCodeIntegrationTests"

# ejecución real como odw-agent (requiere aprovisionamiento y consola elevada)
$env:ODW_AGENT_E2E = "1"
dotnet test --filter "FullyQualifiedName~SandboxAgentTests"

# aprobaciones por pipe (con el servicio iniciado)
odw approvals list --pipe odw-approvals
odw approvals approve <approval_id> --by cli:tu-usuario
```

Estado actual: build 0 warnings, **413/413 pruebas** (176 Core + 122 Node + 26 MCP + 23 OpenCode + 28 Policy + 38 Sandbox).
Cobertura `Odw.Core`: 97,8 % líneas / 95 % ramas (1013/1036) con `coverlet.runsettings`,
que excluye código generado. Comando: `dotnet test tests/Odw.Core.Tests/Odw.Core.Tests.csproj --collect:"XPlat Code Coverage" --settings coverlet.runsettings`.
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
- `read_only` combina base detached con ACL RX para el agente; si `run_as_agent` está apagado, la
  ACL no protege porque OpenCode corre como el servicio.
- Token de par **compartido** (bidireccional) en este MVP; la rotación/direccionalidad llega con H6.
- `ASK` en origen no envía: la tarea queda esperando aprobación humana.
- El poller es el único que muta el origen tras el envío; las consultas MCP siguen siendo locales.
- La aprobación se vincula al hash, expira y es de un solo uso; el pipe usa DACL admin+SYSTEM.
- Las pruebas de OpenCode usan modelo gratuito por defecto (`ODW_TEST_MODEL` permite el fallback).

---

## 14. Siguiente

1. Prueba real en dos PCs (H1): delegación A→B con el servicio `odw-svc`. El nodo casa está
   preparado y el kit de origen está en `C:\ODW\kit-origen.zip`; pasos en `docs/RUNBOOK-DOS-PCS.md`.
2. Probar Telegram con un bot real (H2); la firma HMAC ya está implementada.
3. Empaquetar artefactos descargables (el diff ya viaja inline, truncado a 256 KiB).
4. H4: leases, mensajes, cancelación remota y reconciliación tras reinicio.
5. Cerrar H0-6 (aislamiento por directorio) con instancias ya separadas.