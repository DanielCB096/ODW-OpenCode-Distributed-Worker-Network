# H0 — Hallazgos de verificación (OpenCode real)

> Fecha: 2026-11-24 · Equipo: nodo de desarrollo
> Método: instancia local efímera `opencode serve --port 7431 --hostname 127.0.0.1`
> con `OPENCODE_SERVER_PASSWORD` aleatoria, autenticación HTTP Basic (`opencode:<pass>`),
> `GET /openapi.json` y sondas `GET` sobre rutas candidatas.
> Artefacto: `docs/reference/opencode-2.3.2-openapi.json`
> (SHA-256 `48f6cb36f2cdf9d6fbc9b0a9f1a879d398fb7d5d4fe20718f85faaaf22099973`).

## 0. Resumen ejecutivo

`ARCHITECTURE.md` §2 estaba desactualizado en dos puntos críticos y correcto en el resto:

| Supuesto del documento | Realidad verificada en v2.3.2 |
|---|---|
| `GET /doc` devuelve OpenAPI 3.1 | **Falso.** `/doc` devuelve la SPA web (HTML). La especificación está en **`GET /openapi.json`** (OpenAPI 3.1.0). |
| API v1 (`/session`, `/event`) es la superficie principal | **Falso para esta versión.** La superficie servida es **V2**: todo bajo **`/api/...`** (`/api/session`, `/api/event`, `/api/session/{id}/prompt`). |
| Autenticación Basic aplica a `serve` | **Confirmado.** Sin credenciales: `401`. |
| `POST /session/:id/message` envía prompt | **No existe.** En v2 es `POST /api/session/{id}/prompt` con body `{text,...}`. |
| No hay app oficial, pero sí clientes | Sin cambios relevantes para ODW. |

Superficie real: **143 rutas** bajo `/api`, más `/auth/connect/{code}`.
Todas las rutas usan IDs con prefijo: sesiones `ses_*`, y los eventos son un SSE con
envelope `{id, event, data}`.

Consecuencia de diseño: el adaptador de OpenCode se construye contra **`/api/*` de v2.3.2**,
la especificación se **fija por hash**, y el cliente se genera/valida contra ese archivo,
no contra `/doc`.

---

## 1. Resolución de los `[VERIFICAR]` de §2

### 1.1 Enviar un prompt a una sesión — RESUELTO

```
POST /api/session/{sessionID}/prompt
Body: { "text": string, "id"?: Session.Message.ID, "files"?: [...],
        "agents"?: [...], "skills"?: [...], "metadata"?: object,
        "delivery"?: "steer" | "queue", "resume"?: boolean }
```

- `delivery` es el equivalente funcional del "mensaje a tarea en curso":
  `steer` interrumpe/redirige, `queue` encola. ODW mapea `odw_send_task_message`
  a `delivery:"steer"` solo si la política lo permite; por defecto `queue`.
- Crear sesión: `POST /api/session` con `{ title?, agent?, model?, location?: {directory},
  permissions?: Permission.Ruleset, metadata? }`.

### 1.2 Configuración de servidores MCP — RESUELTO (forma real)

`GET /api/config` devuelve un array de documentos. La forma vigente es:

```json
{ "mcp": { "servers": {
    "browse": { "type": "local", "command": ["...browse.exe","mcp"],
                "cwd": "C:\\Users\\...", "environment": { "K": "V" }, "pool": true }
} } }
```

- Es `mcp.servers`, **no** `mcp.<nombre>`.
- El tipo `local` está confirmado por una configuración real del equipo.
- **Tipo `remote` verificado** (2026-11-24): config temporal cargada por una instancia aislada y
  devuelta por `GET /api/config`. Forma exacta:

```jsonc
{
  "mcp": {
    "servers": {
      "odw": {
        "type": "remote",
        "url": "http://127.0.0.1:7422/mcp",
        "oauth": false,
        "codemode": false,
        "headers": { "Authorization": "Bearer {env:ODW_MCP_TOKEN}" }
      }
    }
  }
}
```

- `oauth: false` es **obligatorio** para un bearer estático: si se omite, OpenCode trata el
  `401` como inicio de OAuth.
- `codemode: false` expone las herramientas directamente en vez de a través de Code Mode.
- Los secretos se referencian con `{env:NAME}` (no `$NAME`).

### 1.3 Registrar MCP por API — RESUELTO

```
PUT    /api/experimental/mcp/{server}
DELETE /api/experimental/mcp/{server}
```

Marcado *experimental*: se usa para autoconfiguración **opcional**; el camino soportado
es editar `opencode.json` en `~/.config/opencode/` o en el proyecto.

### 1.4 `opencode pair` — RESUELTO (parcialmente)

Existe `POST /api/pair` (y `GET /api/integration/...`). No hay evidencia de un comando
`opencode pair` de CLI. ODW **no depende** de pairing: usa bearer token por par.

### 1.5 Permisos de herramientas — RESUELTO

- Evento SSE **`permission.asked`** con `data: { id, sessionID, action, resources[], save[], metadata, source: {type:"tool", messageID, id}, message }`.
- Evento **`permission.replied`** con `data: { sessionID, requestID, reply }`.
- Listar pendientes: `GET /api/session/{id}/permission` → `{ data: [ ... ] }`.
- Responder: `POST /api/session/{id}/permission/{requestID}/reply`
  con `{ "decision": "once" | "always" | "reject", "message"?: string }` → `204`.
- Crear solicitud (para pruebas): `POST /api/session/{id}/permission`.

**Forma real observada** (tarea que ejecuta `echo ODW_PERMISSION_TEST`):

```json
{ "type": "permission.asked",
  "data": { "id": "per_20ec99a34001QdLjZQQS0ZbePf",
            "sessionID": "ses_df1377bd6ffewN1ZL8NU7GHXkb",
            "action": "shell",
            "resources": ["echo ODW_PERMISSION_TEST"],
            "save": ["echo *"],
            "source": { "type": "tool", "messageID": "msg_...", "id": "call_..." } } }
```

- La acción es **`shell`** (nombre de herramienta), no `bash`.
- `resources` es el comando concreto; `save` es el patrón que se persistiría con `always`.
  Esto refuerza ADR-005: `always` convertiría un comando en una concesión permanente.
- `Permission.Ruleset` (H0-4, verificado al crear la sesión):
  `permissions: [{ "action": "*", "resource": "*", "effect": "ask" }]` hizo que la herramienta
  pidiera permiso. Efectos: `allow | deny | ask`.
- **Nunca** se responde `always`. Solo `once` o `reject`.

### 1.6 Varios directorios / proyectos — RESUELTO (parcialmente)

- La sesión se ancla a un directorio: `location: { directory }` (ruta absoluta).
- `GET /api/config` devuelve documentos por `type: "document" | "directory"`.
- `GET /api/mcp?directory=...`, `GET /api/session?directory=...`, `GET /api/project/{id}`.
- `POST /api/session/{id}/move` permite mover una sesión de directorio.

Diseño: **una instancia de `opencode serve` por tarea**, con `location.directory` fijado
al workspace de esa tarea y `--port` propio en `127.0.0.1`. Evita fugas de estado entre
tareas y hace trivial el kill por Job Object. Costo: arranque por tarea (aceptable, segundos).

### 1.7 Descubrimiento del servidor y puerto — RESUELTO (H0-3)

Evidencia (dos arranques, v2.3.2):

```
opencode serve --port 0 --hostname 127.0.0.1
stdout: server listening on http://127.0.0.1:50997
GET /api/info -> {"version":"2.3.2","pid":9452,"urls":["http://127.0.0.1:57222"],...}
```

- `--port 0` funciona: el SO asigna un puerto efímero.
- **stdout publica la URL real** en una línea parseable (`server listening on http://...`).
- `/api/info` devuelve `pid` y `urls`: permite **eliminar la carrera** de elegir puerto y
  además verificar que el proceso escuchado es el que lanzamos.
- `urls` contiene la URL de loopback, no la IP del host.
- Recomendación implementada en el diseño: lanzar con `--port 0`, leer `urls` de `/api/info`
  (con timeout 30 s y backoff 250 ms) y verificar `pid` contra el proceso del Job Object.

### 1.8 Prompt bloqueado por permiso (H0-1) — RESUELTO

Experimento: sesión con `permissions: */* = ask`, prompt que ejecuta un comando de shell.

| Observación | Resultado |
|---|---|
| `POST /api/session/{id}/prompt` | `200` inmediato; la ejecución es asíncrona (no bloquea la petición) |
| Evento | `permission.asked` con `action:"shell"` y el comando en `resources` |
| Sin responder 120 s | **Sin timeout, sin `session.idle`, sin evento terminal**; `time.busy` sigue fijado |
| `GET /api/session/{id}/permission` a los 120 s | La solicitud sigue pendiente |
| Responder `once` → `204` | `permission.replied`, la herramienta se ejecuta, `session.execution.succeeded`, `time.idle` |
| Responder `reject` → `204` | `permission.replied` (`reject`), `session.tool.failed`, el modelo continúa y **`session.execution.succeeded`** |

**Consecuencias de diseño (obligatorias):**

1. ODW **debe** ser el *broker*: sin respuesta, la tarea queda colgada indefinidamente.
   El `OpenCodeExecutor` no puede depender de un cliente de UI conectado.
2. Timeout humano (10 min) ⇒ `reject`, nunca dejar la sesión abierta.
3. **`session.execution.succeeded` no implica éxito de la tarea.** Un `reject` produce
   `tool.failed` y aun así la ejecución "sucede". La verificación final debe exigir que las
   herramientas/pruebas requeridas se hayan ejecutado; si hubo `tool.failed` o falta la
   evidencia esperada ⇒ `ReviewRequired`, no `Completed`.
4. Un rechazo no aborta el turno: el agente puede intentar otra vía. La política debe impedir
   la re-formulación trivial del comando (misma acción, mismo recurso efectivo).

---

## 2. Superficie relevante para ODW (verificada)

| Necesidad | Ruta v2.3.2 |
|---|---|
| Info/versión del servidor | `GET /api/info` → `{version,pid,hostname,urls,paths,capabilities}` |
| Crear sesión | `POST /api/session` |
| Enviar prompt | `POST /api/session/{id}/prompt` |
| Estado de sesión | `GET /api/session/{id}`; `GET /api/session/active` |
| Mensajes | `GET /api/session/{id}/message`, `GET /api/session/{id}/message/{mid}` |
| Interrumpir | `POST /api/session/{id}/interrupt?resume=false` |
| Esperar fin de turno | `POST /api/experimental/session/{id}/wait` (experimental) |
| Eventos | `GET /api/event?types=...` (SSE) |
| Permisos | `GET/POST /api/session/{id}/permission`, `.../{requestID}/reply` |
| Diff de la sesión | `GET /api/session/{id}/diff` |
| Git | `/api/vcs/{status,branch,diff,base,init}`, `/api/worktree` |
| Uso/costo | evento `session.usage.updated` (`cost`, `tokens`) |
| Config | `GET /api/config`, `/api/config/shell`, `/api/location` |

### 2.1 Eventos SSE útiles (100 tipos en el esquema)

`session.created`, `session.execution.started|succeeded|failed|interrupted`,
`session.idle`, `session.step.started|ended|failed|first-token|streamed`,
`session.text.started|delta|ended`, `session.reasoning.*`,
`session.tool.called|success|failed|input.*|progress`,
`session.usage.updated`, `session.retry.scheduled`,
`session.inbox.enqueued|delivered|cancelled|delivery.changed`,
`permission.asked|replied`, `session.diff` implícito vía endpoint.

SSE real (verificado con `curl -N`): **no se emiten líneas `event:`**; cada mensaje es una sola
línea `data:` cuyo JSON trae `type`, más comentarios `: heartbeat` periódicos.

```
data: {"id":"evt_...","created":1795544352719,"type":"session.created",
       "location":{"directory":"C:\\..."},"data":{"sessionID":"ses_..."},
       "durable":{"aggregateID":"ses_...","seq":0,"version":1}}

: heartbeat
```

- Envelope real: `{ id, created, type, location?, data, durable? }`. La carga está en
  **`data`** (no hay `data.data`).
- Filtro opcional: `GET /api/event?types=...`.
  **Se repite el parámetro** (`?types=a&types=b`). Un valor con comas devuelve
  `400 InvalidRequestError` (verificado); no usar `types=a,b`.
- Los eventos no llevan `event:` ni `id:` de SSE: la reconexión con `Last-Event-ID` es
  **no soportada por el formato**; el cliente debe reconciliar con `GET` (H0-5).

Mapa a ODW:

| Evento OpenCode | Evento ODW |
|---|---|
| `session.execution.started` | `task.progress` (fase `running`) |
| `session.tool.called` | `task.progress` (herramienta + input resumido) |
| `session.tool.failed` | `task.progress` con `tool_denied/failed`; **degrada el resultado** |
| `session.text.delta` | `task.log` (opcional, truncado) |
| `session.usage.updated` | `task.progress` (tokens/costo) |
| `session.execution.succeeded` | `task.completed` **solo si la verificación final pasa** |
| `session.execution.failed` | `task.failed` |
| `session.execution.interrupted` | `task.cancelled` |
| `permission.asked` | `task.permission` (flujo de aprobación de herramienta) |

---

## 3. Correcciones al documento de arquitectura

1. §2: sustituir `GET /doc` por **`GET /openapi.json`**; anotar que `/doc` sirve la web.
2. §2: la API es **V2**; no asumir `/session`, `/event` sin prefijo `/api`.
3. §2: el prompt es `POST /api/session/{id}/prompt` (`text`), no `/message`.
4. §2: los permisos se responden con `decision ∈ {once, always, reject}`;
   ODW usa `once|reject`.
5. §8: "generar cliente desde `/doc`" pasa a "generar desde `/openapi.json` fijado por hash".
   El esquema usa `contentMediaType` y `x-effect-stream`, que los generadores no soportan
   de forma uniforme ⇒ **cliente tipado escrito a mano + validación de esquema**, no generación ciega.
6. §11: plataforma objetivo pasa a **.NET 10 LTS** (ver ADR-001).

---

## 4. Tareas H0

### Cerradas

| ID | Pregunta | Resultado |
|---|---|---|
| H0-1 | ¿Un `prompt` queda bloqueado sin respuesta de permiso? | **Sí, indefinidamente** (≥ 120 s sin eventos). Ver §1.8. |
| H0-3 | ¿`--port 0` y URL publicada? | **Sí**: stdout `server listening on http://127.0.0.1:<p>` + `pid`/`urls` en `/api/info`. Ver §1.7. |
| H0-4 | Forma de `Permission.Ruleset` | **Verificado**: `[{action, resource, effect}]` aplicado al crear la sesión surte efecto real. |
| H0-5 | Reanudación SSE (`Last-Event-ID`) | **No soportada**: el stream no emite `id:` ni `event:`; solo `data:` + `: heartbeat`. Se reconcilia con `GET` y `GET .../permission`. |
| H0-2 | Forma de `mcp.servers.<n>.type = "remote"` | **Verificado**: `type`, `url`, `oauth:false`, `headers`, `codemode:false`; `GET /api/config` lo devuelve. Ver §1.2. |

### Pendientes

| ID | Pregunta | Cómo se cierra | Impacto si falla |
|---|---|---|---|
| H0-6 | Aislamiento por directorio: ¿una instancia ve sesiones de otro directorio? | Crear sesiones en dos dirs y listar. | Refuerza la decisión "una instancia por tarea". |

---

## 5. Reproducción

Arnés único (arranca, verifica H0-1/3/4/5 y limpia todo):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/h0-probe.ps1 -KeepArtifacts
```

Resultado esperado: 15 comprobaciones en verde y `exit 0`. `-KeepArtifacts` conserva el
directorio temporal con `serve.out`, `events.txt` y `messages.json`.

Sonda manual:

```powershell
$env:OPENCODE_SERVER_PASSWORD = '<aleatoria>'
opencode serve --port 0 --hostname 127.0.0.1
# stdout: server listening on http://127.0.0.1:<p>
# en otra consola
$auth = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("opencode:$env:OPENCODE_SERVER_PASSWORD"))
Invoke-WebRequest http://127.0.0.1:<p>/openapi.json -Headers @{Authorization=$auth} -OutFile spec.json
```

Nota de seguridad: las instancias se enlazaron solo a `127.0.0.1`, con contraseña efímera, y se
detuvieron al terminar. No se expusieron en la interfaz Tailscale.
