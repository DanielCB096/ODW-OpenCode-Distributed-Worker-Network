# ODW — Fachada MCP local

> `http://127.0.0.1:7422/mcp` · Streamable HTTP · `Authorization: Bearer <fachada-token>`
> La consume el OpenCode de la misma PC. **Nunca** escucha en la interfaz Tailscale.
>
> **Estado (2026-11-24):** implementadas 5 herramientas: las 4 de solo lectura
> (`odw_list_peers`, `odw_get_peer_status`, `odw_get_task_status`, `odw_get_task_result`) y
> `odw_propose_task` (delegación real con política saliente). `odw_estimate_task`,
> `odw_send_task_message` y `odw_cancel_task` quedan para el siguiente paso.

---

## 1. Transporte y seguridad

- MCP Streamable HTTP, modo **sin estado** (sin `Mcp-Session-Id`): cada request es autónomo y
  el estado vive en SQLite.
- `POST /mcp` con `Content-Type: application/json`; respuestas JSON. No hay SSE ni mensajes
  iniciados por el servidor: `GET /mcp` devuelve `405`.
- Protocolo: acepta `2025-06-18` y `2024-11-05` (responde con el solicitado si es soportado;
  si no, con `2025-06-18`).
- Token de fachada: 32 bytes, generado en la instalación, guardado con ACL de solo lectura para
  el usuario de la PC; se escribe una sola vez en `opencode.json` como cabecera.
- Comparación del token en tiempo constante (SHA-256 + `FixedTimeEquals`); nunca se registra.
- Cuerpos ≤ 256 KiB (`413` si exceden); `401` sin token o con token inválido;
  `415` si el `Content-Type` no es JSON; `204` para notificaciones.

| Situación | HTTP |
|---|---|
| Sin `Authorization` / token inválido | `401` |
| `Content-Type` distinto de `application/json` | `415` |
| Cuerpo > 256 KiB | `413` |
| Notificación JSON-RPC (`notifications/initialized`) | `204` |
| Request JSON-RPC válido | `200` + respuesta |
| JSON inválido | `200` + error `-32700` |
| Lote (`[...]`) | `200` + error `-32600` |
| `GET /mcp` | `405` |

- El token de fachada **no** sirve en `/peer/v1` y viceversa. Tokens distintos, almacenes distintos.

### 1.1 Registro en OpenCode v2.3.2

```jsonc
// ~/.config/opencode/opencode.json  (o el opencode.json del proyecto)
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

`oauth:false` es obligatorio para un bearer estático; `codemode:false` expone las herramientas
directamente. Verificado en v2.3.2 (`docs/H0-FINDINGS.md` §1.2). El 2026-11-26 se repitió con
`tools/origin-node.ps1`: OpenCode cargó las cinco herramientas y `odw_propose_task` completó una
tarea real en `PC-CASA` tras aprobación por pipe.

### 1.2 Permisos recomendados en OpenCode

```jsonc
{
  "permission": {
    "odw_*": "ask"          // el humano ve y autoriza cada propuesta desde su cliente
  }
}
```

ODW **no depende** de este permiso para su seguridad: es una segunda barrera para que el agente
no delegue sin que el humano lo vea.

---

## 2. Catálogo de herramientas

| Herramienta | Efecto | Idempotente | Estado |
|---|---|---|---|
| `odw_list_peers` | Lee pares y estado | sí | ✅ implementada |
| `odw_get_peer_status` | Lee recursos de un par | sí | ✅ implementada |
| `odw_get_task_status` | Lee estado/progreso | sí | ✅ implementada |
| `odw_get_task_result` | Lee resultado/artefactos | sí | ✅ implementada |
| `odw_estimate_task` | Calcula opciones de ejecución | sí | ⬜ siguiente paso |
| `odw_propose_task` | Crea propuesta y la envía si la política es AUTO | por `Idempotency-Key` | ✅ implementada |
| `odw_send_task_message` | Envía mensaje a tarea en curso | por `message_id` | ⬜ siguiente paso |
| `odw_cancel_task` | Solicita cancelación | sí | ⬜ siguiente paso |

Las implementadas están anotadas con `annotations.readOnlyHint: true` en `tools/list`.
`odw_get_peer_status` **no contacta** al par: usa el último estado persistido y responde
`PEER_UNKNOWN` o `PEER_OFFLINE` según corresponda.

**No existe** (ni existirá) `odw_approve_task`. Ver ADR-009.

### 2.1 `odw_list_peers`

```json
// input
{ "include_offline": false }
// output (structuredContent)
{ "peers": [
    { "node_id": "PC-LAPTOP", "status": "online", "trust": "trusted",
      "last_seen": "2026-11-24T18:00:00.000Z", "load_score": 0.23,
      "accepts": ["analysis","test_run"], "active_tasks": 1 } ] }
```

### 2.2 `odw_get_peer_status`

```json
// input
{ "node_id": "PC-LAPTOP" }
// output
{ "node_id": "PC-LAPTOP", "status": "online", "hardware": { "...": "..." },
  "resources": { "ram_available_gb": 21.0, "cpu_usage_percent": 18, "active_tasks": 1 },
  "capabilities": ["dotnet","git","gpu"], "repos": ["SistemaMaestro"],
  "measured_at": "..." }
```

Error si el par no existe (`PEER_UNKNOWN`) o está offline (`PEER_OFFLINE`, sin llamada de red).

### 2.3 `odw_estimate_task`

```json
// input
{ "kind": "analysis", "mode": "read_only",
  "instructions": "Analizar el módulo de pagos y ejecutar pruebas",
  "repo": "SistemaMaestro",
  "needs": { "gpu": false, "min_ram_gb": 8, "tools": ["dotnet"] } }
// output
{ "recommendation": "PC-LAPTOP",
  "options": [
    { "node_id": "PC-LOCAL", "score": 0.61, "eta_minutes": 38, "reasons": ["sin transferencia"] },
    { "node_id": "PC-LAPTOP", "score": 0.78, "eta_minutes": 24, "reasons": ["gpu libre","carga 0.2"] }
  ],
  "divisible": false,
  "policy_hint": "ASK" }
```

- Determinista y explicable. No inventa subtareas: `divisible` solo si la tarea declaró unidades
  independientes (`split_units`) y la política lo permite.
- `recommendation` puede ser `"LOCAL"` (ejecutar aquí) o `null` (no conviene delegar).

### 2.4 `odw_propose_task`

```json
// input
{
  "idempotency_key": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "target_node": "PC-LAPTOP",
  "kind": "analysis",
  "mode": "read_only",
  "repo": "SistemaMaestro",
  "base_ref": "main",
  "instructions": "Analizar el módulo de pagos y ejecutar las pruebas.",
  "model_profile": "free",
  "allowed_paths": ["src/", "tests/"],
  "denied_paths": [".env"],
  "limits": { "max_minutes": 45, "max_memory_gb": 12, "max_processes": 64,
              "network": "none", "max_cost_usd": 5 }
}
// output
{ "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
  "state": "WaitingApproval",
  "approval_required": true,
  "approval_channel": "telegram",
  "expires_at": "2026-11-24T18:15:00.000Z",
  "task_hash": "sha256:f39d...",
  "next_action": "wait_for_human" }
```

- `idempotency_key` **es** el `task_id` futuro: si el agente lo omite, ODW genera el ULID; si lo
  envía, debe ser un ULID válido. Repetir con la misma clave devuelve la misma tarea; con distinto
  contenido, `TASK_ID_CONFLICT`.
- `task_hash` se devuelve para que el humano pueda verificarlo en Telegram (8 primeros hex).
- `repo` debe existir en la configuración local; el agente no elige rutas.
- `model_profile` es un **nombre de perfil** del destino, no un provider/model ni una
  credencial. Si se omite, se usa `models.default`; `model_hint` solo se acepta si
  coincide con un perfil permitido.
- Errores: `POLICY_DENIED` (regla explícita), `CONTRACT_INVALID`, `PEER_OFFLINE`, `REPO_UNKNOWN`. También `MODEL_NOT_ALLOWED` si el perfil no está en `models.allowed`.

### 2.5 `odw_get_task_status`

```json
// input
{ "task_id": "01JAZ..." }
// output
{ "task_id": "01JAZ...", "role": "origin", "state": "Running",
  "progress": { "percent": 42, "phase": "tests" },
  "lease_expires_at": "...", "next_action": null,
  "last_event_at": "...", "counterpart_node": "PC-LAPTOP" }
```

`state` es la vista del rol local (ver `DOMAIN-MODEL.md` §6.3). Se incluye `next_action`
(`wait_for_human`, `retry_later`, `read_result`, `none`) para que el agente sepa qué hacer.

### 2.6 `odw_get_task_result`

```json
// input
{ "task_id": "01JAZ...", "include_diff": true }
// output
{ "task_id": "01JAZ...", "outcome": "succeeded",
  "summary_md": "...", "changed_paths": ["src/Pagos.cs"],
  "branch": "odw/01JAZ...", "commit": "9f1c2ab",
  "tests": [{ "name": "dotnet test", "status": "passed" }],
  "artifacts": ["diff.patch", "summary.md"],
  "diff": "diff --git ...",           // truncado a 256 KiB; el resto queda pendiente
  "model_profile": "free", "model_provider": "opencode",
  "model_id": "deepseek-v4.1-flash-free", "cost_usd": 0.0,
  "requires_review": false }
```

- Si la tarea no terminó: `TASK_STATE_CONFLICT` con el estado actual (no bloquea).
- El agente **no** puede fusionar: el resultado es una rama y un informe.

### 2.7 `odw_send_task_message`

```json
// input
{ "task_id": "01JAZ...", "message_id": "01JAZ7...", "text": "Prioriza pagos.",
  "delivery": "queue" }
// output
{ "message_id": "01JAZ7...", "accepted_at": "...", "delivered": false }
```

`delivery:"steer"` requiere que la tarea esté `Running` y que la política lo permita.

### 2.8 `odw_cancel_task`

```json
// input
{ "task_id": "01JAZ...", "reason": "el usuario cambió de opinión" }
// output
{ "task_id": "01JAZ...", "state": "Cancelling", "accepted_at": "..." }
```

### 2.9 `odw_get_task_artifact`

```json
// input
{ "task_id": "01JAZ...", "name": "diff.patch" }
// output
{ "task_id": "01JAZ...", "name": "diff.patch", "content_type": "text/x-diff",
  "bytes": 8123, "sha256": "...", "encoding": "utf8", "content": "diff --git ..." }
```

- Límite de 512 KiB por respuesta MCP; para artefactos mayores usa
  `GET /peer/v1/tasks/{id}/artifacts/{name}` con el token de par.
- `encoding` es `utf8` para texto y `base64` para binario.

---

## 3. Formato de respuesta MCP

Cada herramienta devuelve:

```json
{
  "content": [{ "type": "text", "text": "Tarea 01JAZ... en estado WaitingApproval. Esperando aprobación humana por Telegram." }],
  "structuredContent": { "...": "..." },
  "isError": false
}
```

- `content[0].text` es un resumen de ≤ 400 caracteres, sin secretos ni rutas absolutas.
- Errores de negocio: `isError: true` con `{ "code": "...", "retryable": bool, "detail": "..." }`
  en `structuredContent`. Nunca se lanza una excepción con stack trace al modelo.
- Texto de salida saneado: se eliminan secuencias de control y se truncan logs.

---

## 4. Invariantes de la fachada

1. Ninguna herramienta aprueba, instala, publica ni fusiona.
2. Ninguna herramienta acepta rutas absolutas, unidades, UNC o `..`.
3. Ninguna herramienta expone tokens, hashes de tokens ni rutas del sistema.
4. `odw_propose_task` nunca ejecuta: solo crea propuesta y aplica política.
5. Un agente no puede cancelar tareas que no originó en este nodo.
6. Toda llamada queda auditada; el audit log incluye el `request_hash` (no el cuerpo completo).
7. El error devuelto al modelo no revela si un `task_id` existe en otro nodo.
