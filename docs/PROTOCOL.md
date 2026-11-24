# ODW — Protocolo de pares v1

> Base: `https://<host>:7421/peer/v1` · `protocol_version = 1`
> Autenticación: Bearer por par · Transporte: Tailscale (WireGuard) + TLS cuando hay certificado.
>
> **Estado (2026-11-24):** implementado `GET /hello`, `GET /status`, `POST /tasks`,
> `GET /tasks/{id}`, `GET /tasks/{id}/result` y `GET /health` (solo loopback).
> Pendientes: SSE de eventos, mensajes y cancelación.
> La autenticación exige `Authorization: Bearer` **y** `X-Odw-Node`; ambos deben coincidir con un
> par configurado. El token es compartido por par.

---

## 1. Principios

1. **Idempotente por `task_id`.** Un reintento de transporte no crea ni ejecuta una segunda tarea.
2. **Sin aprobación por HTTP.** El receptor decide por sus propias reglas; la aprobación del
   origen se transporta solo como **evidencia auditable**, nunca como autorización.
3. **Explícito sobre recuperable.** Todo error dice si es reintentable y qué hacer.
4. **Tamaño acotado.** Cuerpos ≤ 1 MiB; instrucciones ≤ 64 KiB; artefactos por separado.
5. **Sin rutas del emisor.** Solo nombres lógicos (ADR-012).

---

## 2. Cabeceras

| Cabecera | Dirección | Uso |
|---|---|---|
| `Authorization: Bearer odw1.<key_id>.<secret>` | req | Autenticación de par |
| `X-Odw-Node` | req | `node_id` del emisor; debe coincidir con el dueño del token |
| `X-Odw-Protocol` | req/res | `1` |
| `Idempotency-Key` | req | `task_id` en `POST /tasks` |
| `X-Odw-Request-Id` | req/res | ULID para correlación en logs |
| `X-Odw-Timestamp` | req | RFC 3339; ventana ±5 min (anti-replay, v1.1) |
| `Retry-After` | res | Segundos, en `429`/`503` |

- `401` por token inválido, desconocido, revocado o `X-Odw-Node` inconsistente.
- El servidor responde siempre `Cache-Control: no-store`.

---

## 3. Errores (RFC 7807)

```json
{
  "type": "https://odw.local/errors/LINEAGE_LOOP",
  "title": "Delegation cycle detected",
  "status": 409,
  "code": "LINEAGE_LOOP",
  "detail": "node PC-B already appears in lineage.hops",
  "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "retryable": false,
  "trace_id": "01JAZ..."
}
```

| Código | HTTP | Retryable | Significado |
|---|---|---|---|
| `PROTOCOL_UNSUPPORTED` | 400 | no | Versión no soportada |
| `CONTRACT_INVALID` | 400 | no | Campo inválido o ausente |
| `PATH_INVALID` | 400 | no | Ruta no relativa / traversal / reparse |
| `LIMITS_INVALID` | 400 | no | Límites fuera de rango |
| `UNAUTHORIZED` | 401 | no | Token inválido |
| `PEER_UNKNOWN` | 403 | no | `X-Odw-Node` no es un par conocido |
| `POLICY_DENIED` | 403 | no | Política del destino deniega |
| `SELF_DELEGATION` | 400 | no | `origin_node == target_node` |
| `LINEAGE_LOOP` | 409 | no | Ciclo o profundidad excedida |
| `TASK_ID_CONFLICT` | 409 | no | Mismo `task_id`, distinto `task_hash` |
| `APPROVAL_INVALID` | 403 | no | Hash/nonce/firma no válidos o expirados |
| `REPO_UNKNOWN` | 422 | no | `workspace.repo` no registrado |
| `RESOURCES_INSUFFICIENT` | 422 | sí (tras espera) | No hay recursos ahora |
| `QUEUE_FULL` | 429 | sí | Cola del nodo llena (`Retry-After`) |
| `RATE_LIMITED` | 429 | sí | Límite por par |
| `TASK_NOT_FOUND` | 404 | no | ID desconocido para ese par |
| `TASK_STATE_CONFLICT` | 409 | no | Acción no válida en el estado actual |
| `TASK_EXPIRED` | 410 | no | `expires_at` vencido |
| `INTERNAL` | 500 | sí | Error no clasificado (sin detalles internos) |

Reglas: nunca se filtran rutas locales, stack traces ni nombres de usuario. Los errores de
validación incluyen `errors: [{field, rule}]`.

---

## 4. Endpoints

### 4.1 `GET /peer/v1/hello`

Handshake sin estado, para `odw doctor` y negociación.

```json
{
  "node_id": "PC-B",
  "protocol_version": 1,
  "min_protocol": 1,
  "max_protocol": 1,
  "odw_version": "0.1.0",
  "opencode_version": "2.3.2",
  "server_time": "2026-11-24T18:00:00.000Z",
  "capabilities": ["analysis", "test_run", "code_change"]
}
```

### 4.2 `GET /peer/v1/status`

```json
{
  "node_id": "PC-B",
  "protocol_version": 1,
  "status": "online",
  "hardware": { "cpu": "Ryzen 7 5800X", "cpu_cores": 8, "ram_total_gb": 32,
                "gpu": "RTX 4060", "gpu_vram_gb": 8 },
  "resources": { "ram_available_gb": 21.0, "cpu_usage_percent": 18,
                 "gpu_available": true, "disk_free_gb": 240,
                 "active_tasks": 1, "queued_tasks": 0, "queue_capacity": 8 },
  "capabilities": ["dotnet", "python", "git", "opencode", "gpu"],
  "accepts": ["analysis", "test_run"],
  "repos": ["SistemaMaestro", "Scraper"],
  "load_score": 0.23,
  "measured_at": "2026-11-24T18:00:00.000Z"
}
```

- `status ∈ {online, draining, maintenance}`. `draining` acepta tareas nuevas con `ASK`.
- `load_score` es un escalar 0..1 documentado en `docs/PLANNER-POLICY.md` §3; el planner
  **no** usa los números crudos de otro nodo como verdad: los pondera con su propio historial.

### 4.3 `POST /peer/v1/tasks`

```http
POST /peer/v1/tasks
Idempotency-Key: 01JAZ5Z8Q4XK3V9C2M1N7P6R8T
Content-Type: application/json
```

Cuerpo: `TaskRequest` completo (ver `DOMAIN-MODEL.md` §2).

| Resultado | HTTP | Cuerpo |
|---|---|---|
| Aceptada | `202` | `TaskView` (`state=Received｜Queued｜AwaitingLocalApproval`) + `Location: /peer/v1/tasks/{id}` |
| Repetida idéntica | `200` | `TaskView` actual, cabecera `Idempotent-Replay: true` |
| Rechazada | `4xx` | Problem Details |

```json
{
  "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "state": "AwaitingLocalApproval",
  "state_seq": 1,
  "accepted_at": "2026-11-24T18:00:01.120Z",
  "lease_expires_at": "2026-11-24T18:01:31.120Z",
  "approval_required": true,
  "next_action": "human_approval",
  "state_changed_at": "2026-11-24T18:00:01.120Z"
}
```

**Atomicidad.** La inserción de la tarea, su evento inicial y el encolado ocurren en una sola
transacción. Si el proceso cae después del commit y antes de la respuesta, el reintento
devuelve `200` idempotente: no hay doble ejecución.

**Concurrencia.** Dos `POST` simultáneos con el mismo `task_id`: uno gana; el otro ve el
registro y responde `200` o `409` según el hash, nunca `500`.

**Implementado (H1).** El alta real:

1. Exige `Bearer` + `X-Odw-Node` de un par configurado (`401` si no).
2. Lee el contrato (`TaskContractReader`) y lo valida; el error incluye el campo exacto.
3. Exige `target_node == node_id` local (`400`).
4. Idempotencia: mismo `task_hash` ⇒ `200` + `Idempotent-Replay: true`; distinto ⇒ `409`.
5. Evalúa **su** política: `DENY` ⇒ `403 POLICY_DENIED` (no se persiste); `ASK` ⇒ estado
   `AWAITING_LOCAL_APPROVAL`; `AUTO` ⇒ `QUEUED`.
6. Persiste tarea, evento `task.received` y auditoría en una transacción.

La evidencia de aprobación del origen se valida (hash) pero **no** autoriza nada en el destino.

### 4.4 `GET /peer/v1/tasks/{task_id}`

```json
{
  "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "state": "Running",
  "state_seq": 7,
  "progress": { "percent": 42, "phase": "tests", "detail": "dotnet test (2/3)" },
  "lease_expires_at": "2026-11-24T18:20:30.000Z",
  "approval_required": false,
  "next_action": null,
  "state_changed_at": "2026-11-24T18:12:00.000Z",
  "result_available": false,
  "cancel_requested": false
}
```

- `ETag: "seq-7"`; `If-None-Match` ⇒ `304`.
- Solo el par que envió la tarea puede leerla (`TASK_NOT_FOUND` para cualquier otro, para no
  filtrar existencia).

### 4.5 `GET /peer/v1/tasks/{task_id}/events` (SSE)

```http
GET /peer/v1/tasks/{task_id}/events
Accept: text/event-stream
Last-Event-ID: 7
```

```
id: 8
event: task.state
data: {"task_id":"01JAZ...","seq":8,"state":"Running","at":"2026-11-24T18:12:00.000Z"}

id: 9
event: task.progress
data: {"task_id":"01JAZ...","seq":9,"percent":42,"phase":"tests","detail":"dotnet test (2/3)"}

: ping
```

- Eventos: `task.state`, `task.progress`, `task.log` (truncado, 4 KiB), `task.message`,
  `task.permission`, `task.rejected`, `task.completed`, `task.failed`, `task.cancelled`.
- `seq` es el `state_seq` persistido: reanudable con `Last-Event-ID` (si el servidor no soporta
  reanudación, responde `200` con `X-Odw-Resume: unsupported` y el cliente reconcilia con `GET`).
- `: ping` cada 15 s. Corte del servidor a los 30 min; el cliente reconecta con `Last-Event-ID`.
- Máximo 4 streams simultáneos por par; el quinto recibe `429`.

### 4.6 `POST /peer/v1/tasks/{task_id}/messages`

```json
{ "message_id": "01JAZ7...", "text": "Prioriza el módulo de pagos.", "delivery": "queue" }
```

- `delivery ∈ {queue, steer}`. `steer` solo si la tarea está `Running` y la política lo permite;
  si no, `TASK_STATE_CONFLICT`.
- Idempotente por `message_id`.
- Respuesta `202` con `{message_id, accepted_at, delivered}`.

### 4.7 `POST /peer/v1/tasks/{task_id}/cancel`

```json
{ "reason": "user_cancelled", "requested_by": "PC-A" }
```

- `202` siempre que la tarea exista y no sea terminal (idempotente si ya está `Cancelling`).
- El destino mata el Job Object en ≤ 5 s y emite `task.cancelled`.
- `409 TASK_STATE_CONFLICT` si ya terminó; el cliente lee el resultado/estado final.

### 4.8 `GET /peer/v1/tasks/{task_id}/result`

- `200` con `TaskResult` cuando el estado es terminal.
- `409 TASK_STATE_CONFLICT` con `{state}` si no terminó.
- `304` con `ETag` si no cambió.

**Implementado:** devuelve `{task_id, state, result_available, result}`; `result` es el
`result_json` guardado por el destino. `409` si el estado no es
`COMPLETED`/`REVIEW_REQUIRED`/`FAILED`. Solo el par que envió la tarea puede leerlo.

### 4.9 `GET /peer/v1/tasks/{task_id}/artifacts/{name}`

- `200` binario con `Content-Type` declarado, `Content-Length`, `ETag` (sha256).
- `404` si no existe; `410` si el artefacto fue purgado (retención 7 días).
- Se sirve solo si el solicitante es el origen de la tarea.

### 4.10 `GET /peer/v1/health`

Solo loopback (no expuesto en la interfaz Tailscale). `{"status":"ok","db":"ok","writer_queue":0}`.

---

## 5. Idempotencia y reintentos

| Operación | Clave | Efecto de repetir |
|---|---|---|
| `POST /tasks` | `task_id` + `task_hash` | `200` con vista actual |
| `POST /messages` | `message_id` | `200` con `delivered` actual |
| `POST /cancel` | `task_id` | `202`/`409` sin efecto nuevo |
| `POST /permission` (interno) | `request_id` | una sola respuesta |

**Política de reintentos del origen:** 5 intentos con backoff exponencial (1, 2, 4, 8, 16 s)
solo en `429`, `500`, `502`, `503`, `504` y errores de red. Nunca en `4xx` de contrato/política.
Si se agotan ⇒ `Unreachable`. El envío es durable: un *outbox* reintenta tras reinicio.

**Orden:** los eventos de una tarea se aplican en orden de `seq`. Hueco detectado ⇒ recarga
completa con `GET /tasks/{id}` y reconciliación; si el hueco persiste, `Unreachable`.

---

## 6. Leases y heartbeats

- Toda `TaskView` incluye `lease_expires_at = now + 90 s`, renovado por: transición de estado,
  `task.progress`, o `GET /status` del propio nodo consultado por el origen.
- El origen hace `GET /tasks/{id}` cada 30 s si no tiene stream. Tres fallos consecutivos o
  lease vencido ⇒ `Unreachable`.
- El destino renueva el lease desde su scheduler cada 30 s mientras `Running`.
- `Unreachable` no es fallo: no hay reejecución automática (ver `DOMAIN-MODEL.md` §6.1).

---

## 7. Versionado

- Prefijo de ruta `/peer/v1`. Cambios incompatibles ⇒ `/peer/v2` con convivencia.
- Campos nuevos **opcionales** no rompen v1; el receptor ignora desconocidos al deserializar
  (excepto en pruebas, donde se exige estricto).
- `GET /hello` negocia: si `min_protocol > 1`, el origen no envía y reporta
  `PROTOCOL_UNSUPPORTED` al usuario.
- Toda respuesta incluye `X-Odw-Protocol`.

---

## 8. Límites operativos

| Límite | Valor |
|---|---|
| Cuerpo máximo | 1 MiB |
| Instrucciones | 64 KiB |
| Tareas activas por par | 4 |
| Tareas activas totales | 8 |
| Cola | 8 (`QUEUE_FULL` después) |
| Streams SSE por par | 4 |
| Ruta de artefacto | 2 MiB, 20 por tarea, 20 MiB total |
| Retención de resultados | 30 días (configurable) |
| Reloj | desfase tolerado ±5 min; mayor ⇒ `CONTRACT_INVALID` |

---

## 9. Secuencia (camino feliz)

```
PC-A                                  PC-B
 │  policy(ASK) → Telegram → humano    │
 │  POST /peer/v1/tasks  ────────────► │  valida contrato + lineage
 │  ◄──────────── 202 TaskView         │  policy(ASK) → pipe/Telegram
 │                                     │  humano local aprueba
 │  GET /tasks/{id}/events ◄────────── │  Queued → Running
 │  ◄──────── task.progress ...        │  opencode serve (odw-agent, Job Object)
 │  ◄──────── task.permission          │  ODW responde once|reject
 │  ◄──────── task.completed           │  valida rutas + pruebas
 │  GET /tasks/{id}/result ◄────────── │  TaskResult
 │  entrega al agente solicitante      │
```

---

## 10. Pruebas de contrato del protocolo

| Prueba | Esperado |
|---|---|
| `POST /tasks` dos veces, mismo hash | `202` y `200`; una sola ejecución |
| Mismo `task_id`, otro hash | `409 TASK_ID_CONFLICT`; estado intacto |
| Token de otro par | `401/403`; sin efectos |
| `lineage` con el destino | `409 LINEAGE_LOOP` |
| `repo` desconocido | `422 REPO_UNKNOWN` |
| Ruta `../secret` | `400 PATH_INVALID` |
| `expires_at` pasado | `400 CONTRACT_INVALID` |
| Cancelar en `Completed` | `409`; resultado intacto |
| SSE con `Last-Event-ID` | sin pérdida ni duplicados |
| Cuerpo de 2 MiB | `413` |
| 5.º stream concurrente | `429` |
