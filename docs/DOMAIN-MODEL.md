# ODW — Modelo de dominio

> Fuente de verdad de tipos, identificadores, contrato de tarea, hash y máquinas de estado.
> Cualquier discrepancia con el código es un defecto del código.

---

## 1. Identificadores

| Tipo | Formato | Regla |
|---|---|---|
| `node_id` | `^[A-Z0-9][A-Z0-9-]{1,31}$` | Configurado, estable, único en la red. Inmutable. |
| `task_id` | ULID (26 chars, Crockford base32, mayúsculas) | Generado por el origen. Monótono dentro del nodo (`+1 ms` si colisiona). Único globalmente por construcción. |
| `message_id` | ULID | Por mensaje. |
| `approval_id` | ULID | Por decisión humana. |
| `event_id` | ULID | Por evento de tarea. |
| `key_id` | 8 chars `[a-z2-7]` | Identifica el token de un par sin revelarlo. |

- Ningún identificador se reutiliza, ni siquiera tras cancelación.
- El `task_id` se valida con regex `^[0-9A-HJKMNP-TV-Z]{26}$` (Crockford, sin `I`, `L`, `O`, `U`).
- Los IDs remotos se tratan como **datos**, nunca como rutas ni como SQL.

### 1.1 Tiempo

- Todo instante es UTC, RFC 3339 con milisegundos: `2026-11-24T18:00:00.000Z`.
- Duraciones en el contrato: minutos enteros para `limits`, segundos para leases.
- El nodo usa `TimeProvider` inyectable: las pruebas no duermen.

---

## 2. Contrato de tarea (`TaskRequest`)

```jsonc
{
  "protocol_version": 1,
  "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "origin_node": "PC-A",
  "target_node": "PC-B",
  "parent_task_id": null,
  "lineage": { "hops": ["PC-A"], "max_depth": 1 },
  "kind": "analysis",
  "instructions": "Analizar la arquitectura y ejecutar las pruebas.",
  "workspace": {
    "repo": "SistemaMaestro",
    "base_ref": "main",
    "mode": "read_only",
    "strategy": "snapshot",          // snapshot | clone | worktree (opt-in)
    "allowed_paths": ["src/", "tests/"],
    "denied_paths": [".env", "secrets/"]
  },
  "limits": {
    "max_minutes": 45,
    "max_memory_gb": 12,
    "max_processes": 64,
    "network": "none",               // none | allow
    "max_cost_usd": 5.0
  },
  "executor": { "type": "opencode", "model_profile": "free", "model_hint": null },
  "created_at": "2026-11-24T18:00:00.000Z",
  "expires_at": "2026-11-24T19:00:00.000Z",
  "approval": {
    "approval_id": "01JAZ6...",
    "task_hash": "sha256:f39d...",
    "approved_by": "human:telegram:123456",
    "approved_at": "2026-11-24T18:02:11.000Z",
    "nonce": "b64:...",
    "signature": "hmac-sha256:..."     // firma del nodo origen
  }
}
```

### 2.1 Reglas de validación (todas obligatorias en origen y destino)

| Campo | Regla | Código de error |
|---|---|---|
| `protocol_version` | `== 1` | `PROTOCOL_UNSUPPORTED` |
| `task_id`, `origin_node`, `target_node` | formato de §1 | `CONTRACT_INVALID` |
| `origin_node == target_node` | rechazado | `SELF_DELEGATION` |
| `lineage.hops` | sin duplicados; no contiene `target_node`; longitud `<= lineage.max_depth` | `LINEAGE_LOOP` |
| `lineage.max_depth` | `0..3` | `CONTRACT_INVALID` |
| `kind` | `analysis｜test_run｜code_change` | `CONTRACT_INVALID` |
| `workspace.mode` | `read_only｜read_write_worktree` | `CONTRACT_INVALID` |
| compatibilidad `kind`×`mode` | ver §2.2 | `CONTRACT_INVALID` |
| `workspace.repo` | nombre lógico registrado en el destino | `REPO_UNKNOWN` |
| `repo`, `base_ref`, rutas | relativas, sin `..`, sin unidad, sin UNC, sin `\` inicial, sin reparse point | `PATH_INVALID` |
| `allowed_paths` | 1..64 entradas, normalizadas, sin solapamiento redundante | `PATH_INVALID` |
| `denied_paths` | 0..64; gana sobre `allowed_paths` | `PATH_INVALID` |
| `instructions` | 1..65 536 caracteres UTF-8, sin NUL | `CONTRACT_INVALID` |
| `limits` | `max_minutes 1..240`, `max_memory_gb 1..1024`, `max_processes 1..512`, `max_cost_usd 0..100` | `LIMITS_INVALID` |
| `limits.network == "allow"` | requiere regla de política `AUTO` explícita | `POLICY_DENIED` |
| `executor.model_profile` | 1..64, `[A-Za-z0-9._-]`; si existe, debe estar en `models.allowed` | `MODEL_NOT_ALLOWED` |
| `executor.model_hint` | legado; debe coincidir con el `provider/model` de un perfil permitido | `MODEL_NOT_ALLOWED` |
| `expires_at` | futuro, `<= created_at + 24 h` | `CONTRACT_INVALID` |
| `approval` | presente solo si la política lo exige; si es obligatorio y falta, o `task_hash` recalcula y no coincide | `APPROVAL_INVALID` |

### 2.2 Compatibilidad `kind` × `mode`

| kind | `read_only` | `read_write_worktree` |
|---|---|---|
| `analysis` | sí | sí |
| `test_run` | sí | sí |
| `code_change` | **no** | sí |

`code_change` en `read_only` se rechaza: no se puede modificar nada; evita tareas incoherentes.

### 2.3 Campos que **no** viajan

- La conversación original, el historial del agente, rutas absolutas locales, variables de
  entorno, credenciales, nombres de usuario. El destino recibe instrucciones, no contexto.
- `executor.model_profile` es el nombre de un perfil declarado en el destino (`models.profiles`);
  el destino solo acepta los incluidos en `models.allowed`. Si falta, usa `models.default`.
- `executor.model_hint` es **legado**: solo se acepta si coincide exactamente con el
  `provider/model` de un perfil permitido. El resultado registra el perfil y el modelo usados.

---

## 3. Resultado (`TaskResult`)

```jsonc
{
  "protocol_version": 1,
  "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "state": "COMPLETED",
  "outcome": "succeeded",              // succeeded | failed | review_required | cancelled
  "summary_md": "Se analizaron 142 archivos. 38 pruebas, todas verdes.",
  "changed_paths": ["src/Foo.cs"],
  "branch": "odw/01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "commit": "9f1c2ab",
  "diff_sha256": "sha256:...",          // del diff unificado adjunto
  "tests": [{ "name": "dotnet test", "status": "passed", "duration_ms": 41230 }],
  "artifacts": ["diff.patch", "summary.md"],
  "cost_usd": 0.0,
  "model_profile": "free",
  "model_provider": "opencode",
  "model_id": "deepseek-v4.1-flash-free",
  "error": null,
  "started_at": "2026-11-24T18:02:40.000Z",
  "finished_at": "2026-11-24T18:19:02.000Z"
}
```

- `changed_paths` se calcula con `git status --porcelain` dentro del workspace y se **valida**
  contra `allowed_paths`/`denied_paths`. Si hay violación ⇒ `state=REVIEW_REQUIRED`,
  `outcome=review_required`, y el resultado **no** se marca como entregable.
- Artefactos: 2 MiB por archivo; `name` cumple `^[A-Za-z0-9._-]{1,64}$`, nunca una ruta.
  Contenido por `GET /tasks/{id}/artifacts/{name}`; la retención/purga queda pendiente.
- El diff se adjunta inline hasta 256 KiB y se marca `diff_truncated`; el artefacto
  `diff.patch` contiene el parche completo (hasta 2 MiB) y `summary.md` el informe.

---

## 4. `task_hash` (ADR-011)

### 4.1 Proyección

Se construye un objeto JSON con **exactamente** estas claves. Ninguna más, ninguna menos.

```
executor.type
executor.model_profile   (solo si existe)
instructions
kind
limits.{max_cost_usd, max_memory_gb, max_minutes, max_processes, network}
lineage.max_depth
origin_node
parent_task_id
target_node
task_id
workspace.{allowed_paths, base_ref, denied_paths, mode, repo}
```

- **Excluidos a propósito:** `approval` (se firma después), `created_at`, `expires_at`,
  `lineage.hops`, `executor.model_hint`, `workspace.strategy`, `protocol_version`.
  `executor.model_profile` **sí** se incluye cuando existe: cambiar de perfil cambia el hash y la
  aprobación deja de valer.
  Justificación: `hops` cambia en cada salto y una redelegación no debe invalidar la
  aprobación; `expires_at` es control operativo, no de contenido.
- **Normalización de `instructions`:** `\r\n`→`\n`, `\r`→`\n`, recorte de espacios finales
  por línea, `Trim` del inicio y fin. **No se agrega salto final.** Se conservan líneas vacías internas.
- **Normalización de rutas:** separador `/`, sin `/` inicial, se eliminan segmentos `.`,
  se rechazan `..`, `//`, unidad, UNC y ADS; se elimina el `/` final
  (`src/` y `src` producen la misma ruta canónica).
- **Normalización de listas:** `allowed_paths` y `denied_paths` se deduplican y **ordenan**
  lexicográficamente por bytes UTF-8 antes de serializar.
- **Serialización:** RFC 8785 (JCS): claves ordenadas por unidades UTF-16, sin espacios,
  números en forma canónica de ECMAScript, `null` explícito.
- **Digest:** `sha256:` + hex minúsculas de SHA-256 sobre los bytes UTF-8.

### 4.2 Vectores de prueba (obligatorios en CI)

**V1 — canónico.**

```json
{"executor":{"type":"opencode"},"instructions":"Analizar la arquitectura y ejecutar las pruebas.\nNo modificar archivos.","kind":"analysis","limits":{"max_cost_usd":5,"max_memory_gb":12,"max_minutes":45,"max_processes":64,"network":"none"},"lineage":{"max_depth":1},"origin_node":"PC-A","parent_task_id":null,"target_node":"PC-B","task_id":"01JAZ5Z8Q4XK3V9C2M1N7P6R8T","workspace":{"allowed_paths":["src","tests"],"base_ref":"main","denied_paths":[],"mode":"read_only","repo":"SistemaMaestro"}}
```

`task_hash = sha256:1811ced43c7c77ec46a771902cdd039b5c8985bfdd70997f92696f3f6d4721d0`

**V2 — una instrucción cambia.**

`instructions` = `"Analizar la arquitectura y ejecutar las pruebas.\nNo modificar archivos ni ejecutar scripts."`

`task_hash = sha256:f6424f1c4e6e5d0767d6da32d0750f9a4ecd2a0d3cc550ae79657c74a2823b7c`

**V3 — orden de entrada alterado, mismo hash.**

`allowed_paths` de entrada `["tests/","src/"]` (y un duplicado `"src/"`) debe producir el
mismo texto canónico que V1 y por tanto el mismo hash. Si no, la normalización está mal.

**V4 — cambio de destino.**

`target_node: "PC-C"` ⇒ `sha256:663a3edee18349352d72d32e95a811d8feba941f1168f3bcb3cd24771d5e404b`
(distinto de V1).

### 4.3 Invariantes

1. Recalcular el hash en el destino y compararlo con `approval.task_hash`; diferencia ⇒
   `APPROVAL_INVALID`, sin ejecutar nada.
2. El hash se persiste junto a la aprobación y con cada transición relevante.
3. El hash **nunca** se calcula sobre el JSON recibido tal cual (orden y espacios no son
   confiables): siempre sobre la proyección.

---

## 5. Lineage y anti-ciclos

- `lineage.hops` = nodos que ya **ejecutaron o aceptaron** la tarea, en orden.
- Al enviar, el origen agrega su propio `node_id` a `hops` si no está.
- El destino rechaza si: `target_node ∈ hops`, o `hops.Count > max_depth`, o
  `target_node == origin_node`. `hops` incluye al emisor: `max_depth = 1` permite A→B y
  prohíbe B→C.
- `max_depth = 0`: el origen ejecuta y no delega. `1`: un salto. Máximo 3.
- Un nodo **no** acepta una tarea cuyo `task_id` ya conoce (mismo rol destino), salvo que sea
  idéntica (`task_hash` igual) ⇒ idempotencia.

### 5.1 Tareas hijas (división)

- `parent_task_id` enlaza una subtarea con su padre; la profundidad del árbol se limita con
  `lineage.max_depth` **del padre** y con un máximo global de 8 nodos de árbol.
- Una subtarea no puede tener `expires_at` posterior al del padre.
- El padre no puede darse por `COMPLETED` con hijas activas: queda en `REVIEW_REQUIRED`.

---

## 6. Máquinas de estado

Enum único `TaskState`. Cada rol usa un subconjunto. Toda transición no listada lanza
`InvalidStateTransitionException` y está cubierta por una prueba (matriz completa).

```csharp
public enum TaskState {
    Proposed, WaitingApproval, Approved, Sent, Accepted, Running, Unreachable,
    Cancelling, Done, Failed, Rejected, Cancelled, Expired, Interrupted,
    Received, AwaitingLocalApproval, Queued, Completed, ReviewRequired, Interrupted
}
```

### 6.1 Origen

| Desde | Evento/disparador | Guarda | Hacia |
|---|---|---|---|
| `Proposed` | `policy.evaluated(AUTO)` | regla AUTO | `Approved` |
| `Proposed` | `policy.evaluated(ASK)` | — | `WaitingApproval` |
| `Proposed` | `policy.evaluated(DENY)` | — | `Rejected` |
| `Proposed` | `user.cancel` | — | `Cancelled` |
| `WaitingApproval` | `approval.granted` | hash coincide, nonce válido, no expirado | `Approved` |
| `WaitingApproval` | `approval.denied` | — | `Rejected` |
| `WaitingApproval` | `approval.expired` | — | `Expired` |
| `WaitingApproval` | `user.cancel` | — | `Cancelled` |
| `Approved` | `transport.send` | — | `Sent` |
| `Approved` | `approval.expired` | `now > approved_at + 15 min` | `Expired` |
| `Sent` | `transport.2xx` | — | `Accepted` |
| `Sent` | `transport.4xx` | — | `Rejected` |
| `Sent` | `transport.timeout/5xx` | reintentos agotados | `Unreachable` |
| `Sent` | `task.interrupted` | - | `Interrupted` |
| `Accepted` | `task.progress`/`lease` | — | `Running` |
| `Accepted` | `task.rejected` | — | `Rejected` |
| `Accepted` | `task.interrupted` | - | `Interrupted` |
| `Accepted` | `lease.expired` | `now > lease_expires_at` | `Unreachable` |
| `Running` | `task.completed` | `outcome ∈ {succeeded, review_required}` | `Done` |
| `Running` | `task.failed` | — | `Failed` |
| `Running` | `user.cancel` | — | `Cancelling` |
| `Running` | `lease.expired` | — | `Unreachable` |
| `Running` | `task.interrupted` | - | `Interrupted` |
| `Unreachable` | `task.progress`/`lease` | — | `Running` |
| `Unreachable` | `task.completed` | — | `Done` |
| `Unreachable` | `task.failed` | — | `Failed` |
| `Unreachable` | `user.cancel` | — | `Cancelling` |
| `Unreachable` | `task.ttl.expired` | `now > expires_at + 24 h` | `Expired` |
| `Unreachable` | `task.interrupted` | - | `Interrupted` |
| `Interrupted` | `task.completed` | - | `Done` |
| `Interrupted` | `task.failed` | - | `Failed` |
| `Interrupted` | `task.rejected` | - | `Rejected` |
| `Interrupted` | `task.cancelled` | - | `Cancelled` |
| `Cancelling` | `task.cancelled` | — | `Cancelled` |
| `Cancelling` | `task.completed` | carrera: ya terminó | `Done` |
| `Cancelling` | `task.failed` | — | `Failed` |
| `Cancelling` | `cancel.timeout` | 5 min sin confirmación | `Cancelled` (con `reason=unconfirmed`) |
| `Done`,`Failed`,`Rejected`,`Cancelled`,`Expired` | - | terminal | - |

`Interrupted` no es terminal: el origen espera inspección/resultado y nunca vuelve a `Running`
por sí solo. El poller sigue consultando el estado remoto hasta `Done`/`Failed`.

`Unreachable` **no** implica fallo y **no** autoriza reejecutar. Si el origen decide reintentar,
crea una **tarea nueva** (`task_id` nuevo), con aprobación nueva si aplica.

### 6.2 Destino

| Desde | Evento/disparador | Guarda | Hacia |
|---|---|---|---|
| `Received` | `contract.valid` + `lineage.ok` + `policy(AUTO)` | — | `Queued` |
| `Received` | `policy(ASK)` | — | `AwaitingLocalApproval` |
| `Received` | `policy(DENY)`/error de contrato | — | `Rejected` |
| `AwaitingLocalApproval` | `approval.granted` | hash coincide | `Queued` |
| `AwaitingLocalApproval` | `approval.denied` | — | `Rejected` |
| `AwaitingLocalApproval` | `approval.expired` | 15 min | `Rejected` (`reason=approval_timeout`) |
| `Queued` | `scheduler.slot` | recursos suficientes | `Running` |
| `Queued` | `setup.failed` | — | `Failed` |
| `Queued` | `user.cancel` | — | `Cancelled` |
| `Queued` | `scheduler.rejected` | recursos insuficientes tras reintentos | `Rejected` (`RESOURCES_INSUFFICIENT`) |
| `Running` | `executor.succeeded` + `validation.ok` | rutas conformes, pruebas ejecutadas y **sin `tool.failed` relevante** | `Completed` |
| `Running` | `executor.succeeded` + `validation.violation` | — | `ReviewRequired` |
| `Running` | `executor.failed` | — | `Failed` |
| `Running` | `user.cancel` | — | `Cancelling` |
| `Running` | `service.restart` | — | `Interrupted` |
| `Cancelling` | `job.killed` | — | `Cancelled` |
| `Cancelling` | `executor.succeeded` | carrera | `Completed` |
| `Interrupted` | `operator.inspect` | — | `ReviewRequired` |
| `Interrupted` | `operator.discard` | — | `Failed` (`reason=interrupted`) |

- `ReviewRequired` se reporta al origen como `Done` con `outcome=review_required`.
- `Interrupted` **nunca** pasa a `Running` automáticamente.

### 6.3 Reconciliación de vistas

| Destino | Origen |
|---|---|
| `Received`, `AwaitingLocalApproval` | `Accepted` |
| `Queued` | `Accepted` |
| `Running` | `Running` |
| `Completed` | `Done` |
| `ReviewRequired` | `Done` (`outcome=review_required`) |
| `Failed` | `Failed` |
| `Rejected` | `Rejected` |
| `Cancelled` | `Cancelled` |
| `Interrupted` | `Interrupted` (aviso; se sondea hasta ReviewRequired/Failed) |

---

## 7. Invariantes del dominio

1. Ningún estado terminal transiciona.
2. `task_hash` se recalcula en cada frontera de confianza.
3. Un `task_id` con `task_hash` distinto en el mismo nodo ⇒ `TASK_ID_CONFLICT` (409), sin efectos.
4. `role` es inmutable.
5. `state_seq` es monótono por `(task_id, role)`; nunca hay huecos ni retrocesos.
6. Cada transición escribe un registro en `task_events` y en `audit_log`.
7. Una tarea con `approval` presente exige `approved_by`, `nonce` y `signature` verificables.
8. Los estados terminales del destino son `Completed`, `Failed`, `Rejected`, `Cancelled`.
9. `Cancelling` no es terminal y tiene límite de 5 minutos.
10. Ninguna tarea se ejecuta dos veces en el mismo nodo por reintento de transporte.
