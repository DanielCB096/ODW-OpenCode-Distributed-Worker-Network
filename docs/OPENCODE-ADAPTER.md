# ODW — Adaptador de OpenCode

> Superficie verificada: OpenCode **v2.3.2**, rutas `/api/*`, esquema fijado por SHA-256.
> Ver `docs/H0-FINDINGS.md`.
>
> **Estado (2026-11-24):** implementado y probado. `OpenCodeClient` (HTTP + SSE), `OpenCodeServer`
> (arranque loopback con puerto 0) y `OpenCodeExecutor` (sesión, prompt, eventos, permisos).
> 21 pruebas, incluida una **integración real** contra v2.3.2 (opt-in).
> Pendiente: Job Object/usuario restringido (H3) y `StopAsync` que mate el servidor (vive en el host).

---

## 1. Capas

```
Odw.Node (TaskManager)
   └── IExecutor                         (contrato de ODW, sin HTTP)
         └── OpenCodeExecutor            (sesión, prompt, eventos, broker de permisos)
               └── IOpenCodeClient       (puerto)
                     └── OpenCodeClient  (HttpClient tipado + lector SSE)
                           └── opencode serve (127.0.0.1, puerto 0, contraseña aleatoria)

OpenCodeServer  (proceso: arranque, descubrimiento de URL, parada)
```

- `Odw.Node` **no** conoce OpenCode: solo `IExecutor`.
- `OpenCodeExecutor` no decide políticas: recibe un `ExecutionPolicy` ya resuelto.
- `OpenCodeHttpClient` no conoce ODW: solo DTOs y transporte.

---

## 2. Contrato `IExecutor`

```csharp
public interface IExecutor
{
    Task<ExecutorSession> StartAsync(ExecutorStartRequest request, CancellationToken ct);
    Task SendAsync(string sessionId, string text, MessageDelivery delivery, CancellationToken ct);
    IAsyncEnumerable<ExecutorEvent> StreamAsync(string sessionId, CancellationToken ct);
    Task<ExecutorSnapshot> GetSnapshotAsync(string sessionId, CancellationToken ct);
    Task<ExecutorOutcome> WaitAsync(string sessionId, TimeSpan timeout, CancellationToken ct);
    Task AbortAsync(string sessionId, TimeSpan grace, CancellationToken ct);
    Task StopAsync(string sessionId, CancellationToken ct);
}
```

```csharp
public sealed record ExecutorStartRequest(
    string TaskId,
    string WorkspacePath,          // ruta local ya preparada y con ACL
    string Instructions,
    string? ModelHint,
    ExecutorLimits Limits,          // minutos, memoria, procesos, red, costo
    ExecutionPolicy Policy,         // qué herramientas se permiten (broker)
    IReadOnlyDictionary<string,string> Environment);

public sealed record ExecutorSession(
    string SessionId,      // "ses_..."
    int ProcessId,
    string BaseUrl,        // http://127.0.0.1:<puerto>
    DateTimeOffset StartedAt);
```

`ExecutorEvent` es un union: `Started`, `ToolCalled`, `PermissionAsked`, `TextDelta`,
`UsageUpdated`, `Completed`, `Failed`, `Interrupted`, `Heartbeat`.

---

## 3. `IOpenCodeClient` (puerto)

```csharp
public interface IOpenCodeClient : IAsyncDisposable
{
    Task<ServerInfo> GetInfoAsync(CancellationToken ct);
    Task<Session> CreateSessionAsync(string directory, SessionPermissions permissions, CancellationToken ct);
    Task SendPromptAsync(string sessionId, string text, MessageDelivery delivery, CancellationToken ct);
    IAsyncEnumerable<OpenCodeEvent> SubscribeAsync(IReadOnlySet<string>? types, CancellationToken ct);
    Task<SessionView> GetSessionAsync(string sessionId, CancellationToken ct);
    Task<IReadOnlyList<Message>> GetMessagesAsync(string sessionId, CancellationToken ct);
    Task ReplyPermissionAsync(string sessionId, string requestId, PermissionReply reply, string? message, CancellationToken ct);
    Task InterruptAsync(string sessionId, bool resume, CancellationToken ct);
    Task<SessionDiff> GetDiffAsync(string sessionId, CancellationToken ct);
    Task<bool> WaitAsync(string sessionId, TimeSpan timeout, CancellationToken ct);
}
```

Mapeo exacto (verificado):

| Método | HTTP |
|---|---|
| `GetInfoAsync` | `GET /api/info` |
| `CreateSessionAsync` | `POST /api/session` cuerpo `{ location:{directory}, permissions, title }` |
| `SendPromptAsync` | `POST /api/session/{id}/prompt` cuerpo `{ text, delivery }` |
| `SubscribeAsync` | `GET /api/event?types=...` (SSE) |
| `GetSessionAsync` | `GET /api/session/{id}` |
| `GetMessagesAsync` | `GET /api/session/{id}/message` |
| `ReplyPermissionAsync` | `POST /api/session/{id}/permission/{requestId}/reply` `{ decision, message }` |
| `InterruptAsync` | `POST /api/session/{id}/interrupt?resume=false` |
| `GetDiffAsync` | `GET /api/session/{id}/diff` |
| `WaitAsync` | `POST /api/experimental/session/{id}/wait` (con fallback por eventos) |

### 3.1 Formas de respuesta verificadas (importante para DTOs)

| Operación | Respuesta real |
|---|---|
| `POST /api/session` | `200` con **`{ "data": { ...session } }`** (envuelto) |
| `GET /api/session/{id}` | `{ "data": { ...session } }`; `time.busy`/`time.idle` indican actividad |
| `POST /api/session/{id}/prompt` | `200` **inmediato**; la ejecución ocurre en segundo plano |
| `POST .../permission/{requestId}/reply` | `204` sin cuerpo |
| `GET /api/session/{id}/permission` | `{ "data": [ { id, sessionID, action, resources, save, source } ] }` |
| `GET /api/session/{id}/message` | `{ "data": [ { id, type:"assistant"｜"idle", content:[...], tokens, cost } ] }` — usa `content`, no `parts` |
| `GET /api/event` | SSE con líneas `data: {json}` y `: heartbeat`; **sin** `event:` ni `id:` |

**Consecuencia:** la reanudación con `Last-Event-ID` **no existe** (H0-5). Tras un corte hay que
reconciliar con `GET /api/session/{id}`, `.../message` y `.../permission` antes de continuar.

---

## 4. Ciclo de vida de una ejecución (destino)

```
1. TaskManager: state Queued → Running (transacción + evento).
2. WorkspaceManager: prepara snapshot/clon, aplica ACL, valida rutas y reparse points.
3. Generar contraseña 256-bit y lanzar `opencode serve --hostname 127.0.0.1 --port 0`.
4. Crear Job Object (límites del contrato) + proceso suspendido:
      token: CreateRestrictedToken(odw-agent) ; AssignProcessToJobObject ; ResumeThread
5. Leer stdout hasta `server listening on http://127.0.0.1:<p>` (máx. 30 s) y confirmar con
   `/api/info` (version fijada + pid esperado). No se elige puerto antes: no hay carrera.
6. Verificar bind loopback con Get-NetTCPConnection -OwningProcess <pid>.
7. POST /api/session (location.directory = workspace, permissions = política de sesión).
8. Abrir SSE /api/event (filtrado por tipos) ANTES de enviar el prompt.
9. POST /api/session/{id}/prompt { text: instructions, delivery: "queue" }.
10. Bucle de eventos:
      - session.execution.started → heartbeat
      - session.tool.called      → task.progress (nombre + input resumido)
      - session.tool.failed      → marca de degradación (ver §6)
      - permission.asked         → broker (§5)
      - session.text.delta       → buffer (solo para el informe, sin log excesivo)
      - session.usage.updated    → task.progress (tokens/costo)
      - session.execution.succeeded|failed|interrupted → fin lógico
11. Verificación final (§6) y empaquetado del resultado.
12. Interrupt + TerminateJobObject + borrar contraseña + limpiar workspace según retención.
```

- El SSE se abre **antes** del prompt para no perder el primer `execution.started`.
- Si el SSE se corta: reconectar con backoff y **reconciliar** con `GET /api/session/{id}`,
  `GET /api/session/{id}/message` y `GET /api/session/{id}/permission` (no hay `Last-Event-ID`).
- Un `session.idle` sin `execution.succeeded` no se interpreta como éxito.
- `prompt` retorna `200` antes de terminar: el fin se detecta por eventos, no por la respuesta.

---

## 5. Broker de permisos (ADR-005)

```csharp
async ValueTask OnPermissionAsked(PermissionAsked p, ExecutionPolicy policy)
{
    var decision = policy.Classify(p.Action, p.Resources, p.Message);

    switch (decision)
    {
        case ToolDecision.AllowOnce:
            await client.ReplyPermissionAsync(p.SessionId, p.Id, PermissionReply.Once, null, ct);
            break;

        case ToolDecision.AskHuman:
            var answer = await approvals.AskAsync(
                ApprovalScope.ToolUse, taskId, summary: $"{p.Action} {p.Resources.FirstOrDefault()}");
            await client.ReplyPermissionAsync(
                p.SessionId, p.Id,
                answer.Approved ? PermissionReply.Once : PermissionReply.Reject,
                answer.Approved ? null : "Denegado por política ODW", ct);
            break;

        default:
            await client.ReplyPermissionAsync(
                p.SessionId, p.Id, PermissionReply.Reject, "Acción no permitida por la política de la tarea", ct);
            break;
    }
}
```

- `PermissionReply` solo puede valer `Once` o `Reject` en el código de ODW. La prueba de
  arquitectura falla si aparece `Always`.
- Valores reales verificados (H0-1): `action` es el nombre de herramienta (`shell`, `read`,
  `edit`, …), `resources` es el recurso concreto y `save` el patrón que se persistiría con
  `always`. El broker clasifica por `action` + `resources`, nunca por `save`.
- Si no hay humano disponible en 10 minutos ⇒ `Reject` (nunca dejar la tarea colgada).
- Un `reject` **no aborta el turno**: OpenCode emite `session.tool.failed` y el modelo puede
  seguir (incluso intentar otra vía) y terminar con `execution.succeeded`. El executor debe
  contar los rechazos y degradar el resultado (§6).
- Toda decisión va al audit log con `action`, `resources` y `decision`, **sin** el contenido
  completo de los recursos (puede contener secretos).

### 5.1 Permisos de sesión (defensa en profundidad)

Al crear la sesión se envía un `Permission.Ruleset` que:
- permite lectura dentro del workspace y ejecución de pruebas declaradas;
- deniega escritura fuera de `allowed_paths`;
- deniega red si `limits.network == "none"`;
- deniega herramientas de publicación/despliegue y acceso a credenciales.

Forma verificada (H0-4): `permissions: [{ "action": "*", "resource": "*", "effect": "ask" }]`
surtió efecto real al crear la sesión. Efectos: `allow | deny | ask`.
El ruleset es defensa en profundidad; el broker de eventos sigue siendo obligatorio porque
la sesión **no** falla si falta una respuesta: se cuelga.

---

## 6. Verificación final (en el destino)

| Comprobación | Fuente | Si falla |
|---|---|---|
| `changed_paths ⊆ allowed_paths` y `∩ denied_paths = ∅` | `GET /api/session/{id}/diff` + `git status --porcelain` | `ReviewRequired` |
| Sin reparse points ni hardlinks | recorrido del filesystem | `ReviewRequired` / `Failed` |
| Procesos del Job terminados | `QueryInformationJobObject` | `TerminateJobObject`, `Failed` |
| Presupuesto de costo respetado | `session.usage.updated` acumulado | `Failed` (`COST_EXCEEDED`) |
| Pruebas declaradas ejecutadas | eventos `session.tool.called` + `git` | `ReviewRequired` |
| Sin `session.tool.failed` que afecte a la tarea | eventos del turno | `ReviewRequired` |
| Commit en rama `odw/<task_id>` | `git rev-parse` | Adjuntar sin commit |
| Tamaño del diff ≤ límite | `stat` | Diff como artefacto |

El informe (`report.md`) se genera **desde el resultado**, no desde el texto del agente:
resumen (≤ 2 KiB), lista de cambios, pruebas, uso y errores.

**Regla derivada de H0-1:** `session.execution.succeeded` solo significa que el turno terminó.
Un permiso rechazado produce `session.tool.failed` y el turno igualmente "sucede". Por tanto:

```csharp
outcome = toolFailures.Count == 0 && requiredEvidenceSatisfied
        ? Outcome.Succeeded
        : Outcome.ReviewRequired;
```

- `requiredEvidenceSatisfied` depende del `kind`: para `test_run`, que el comando de pruebas
  declarado se haya ejecutado y su salida no indique fallo; para `code_change`, que el diff
  exista y esté dentro de `allowed_paths`; para `analysis`, que no haya `tool.failed` relevante.
- Se guarda la lista de `tool.failed` (acción + recurso saneado) en el resultado, no solo en logs.

---

## 7. Versión y compatibilidad

- `opencode.pinned_version = "2.3.2"` y `opencode.spec_sha256 = "48f6cb…"`.
- Al arrancar cada instancia: `GET /api/info` → `version`. Si difiere:
  - se registra `OPENCODE_VERSION_DRIFT`,
  - se marca la tarea como apta solo si `opencode.allow_drift=true`,
  - `odw doctor` lo muestra en rojo.
- Al arrancar el servicio: descargar `GET /openapi.json` de una instancia de referencia y
  comparar SHA-256 con el fijado. Diferencia ⇒ advertencia, no bloqueo (salvo `strict_mode`).
- Las pruebas de contrato validan payloads reales contra el esquema fijado; un cambio de
  esquema rompe CI, no producción.

---

## 8. Errores y mapeo

| Situación | `ExecutorOutcome` | Estado destino |
|---|---|---|
| `execution.succeeded` + verificación OK | `Succeeded` | `Completed` |
| `execution.succeeded` + violación | `ReviewRequired` | `ReviewRequired` |
| `execution.failed` | `Failed(code, message)` | `Failed` |
| `execution.interrupted` | `Interrupted` | `Cancelling→Cancelled` |
| Proceso murió sin evento | `Failed(EXECUTOR_CRASH)` | `Failed` |
| Timeout de reloj | `Failed(TIMEOUT)` | `Failed` |
| Costo excedido | `Failed(COST_EXCEEDED)` | `Failed` |
| OpenCode no responde en 30 s al arrancar | `Failed(STARTUP_TIMEOUT)` | `Failed` |
| Bind no loopback | `Failed(SANDBOX_NETWORK_BIND)` | `Failed` |

Nunca se expone al origen el mensaje crudo de una excepción de OpenCode: se clasifica y se
adjunta un extracto saneado al informe.

---

## 9. Idempotencia y concurrencia

- `StartAsync` con un `task_id` ya en `Running` ⇒ error, no segunda sesión.
- Un solo stream SSE por sesión; el executor multiplexa a `IAsyncEnumerable`.
- `AbortAsync` es idempotente: si el proceso ya no existe, retorna.
- `StopAsync` siempre se llama en `finally`, incluso si el arranque falló a medias.

---

## 10. Pruebas

| Prueba | Tipo | Estado |
|---|---|---|
| Cliente: rutas, Basic auth, cuerpos y errores | Unit (stub HTTP) | ✅ |
| SSE: `data:`, `: heartbeat`, filtro `types` repetido | Unit (stub HTTP) | ✅ |
| Executor: mapeo de eventos, permiso `once`/`reject`, timeout, abort | Unit (fake client) | ✅ |
| Arranque real, sesión, prompt y `OK` | Integration (`ODW_OPENCODE_EXE`) | ✅ ejecutada contra 2.3.2 |
| Corte de SSE y reconciliación | Integration (proxy que corta) | ⬜ |
| Job Object mata el árbol | Integration Windows | ⬜ H3 |
| `Always` ausente del ensamblado | Architecture | ⬜ |

### 10.1 Limitaciones actuales (explícitas)

- `OpenCodeServer` **no** crea Job Object ni usuario restringido: es H3. Hoy el proceso hereda la
  identidad del servicio; no se declara aislamiento que no exista.
- `OpenCodeExecutor.StopAsync` interrumpe la sesión, pero no mata el servidor: el ciclo de vida
  del proceso pertenece al host del nodo (una instancia por tarea).
- El consumidor debe **abrir el stream antes de enviar el prompt** (documentado en §4). `WaitAsync`
  es una conveniencia posterior y puede perder eventos si se usa sin stream abierto.
- El filtro `types` se envía repetido (`?types=a&types=b`); con comas OpenCode responde `400`.
