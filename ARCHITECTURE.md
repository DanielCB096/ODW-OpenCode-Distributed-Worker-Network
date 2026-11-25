# ODW — OpenCode Distributed Worker Network

> Documento de arquitectura y plan de implementación.
> Destinatario: agente de desarrollo (OpenCode u otro). Léelo completo antes de escribir código.
> Estado: **diseño validado parcialmente**. Las secciones marcadas `[VERIFICAR]` son supuestos que DEBEN confirmarse contra la versión real de OpenCode instalada antes de codificar sobre ellos.
>
> **Actualización 2026-11-24 (H0 parcial, OpenCode v2.3.2):** la verificación real está en
> [`docs/H0-FINDINGS.md`](docs/H0-FINDINGS.md). Cambios principales: `/doc` sirve la web y la
> especificación está en **`GET /openapi.json`**; la superficie es **V2** (`/api/...`); el prompt
> es `POST /api/session/{id}/prompt`. Las decisiones de diseño vinculantes están en
> [`docs/DECISIONS.md`](docs/DECISIONS.md) y el diseño detallado en [`docs/README.md`](docs/README.md).
>
> **Estado actual (2026-11-25):** H3 cerrado (odw-agent/odw-svc, ACLs, WFP + proxy, perfiles por
> tarea), delegación A→B verificada en loopback, resultado con `changed_paths`/commit/diff y
> **328/328 pruebas**. Para retomar el proyecto usa [`docs/HANDOFF.md`](docs/HANDOFF.md); este
> documento conserva la visión y el diseño originales. Pendiente: dos PCs/Tailscale, artefactos
> descargables, Telegram real y H4.

---

## 1. Propósito

Permitir que varias computadoras con OpenCode formen una red de ejecución cooperativa:

- Desde el celular (o desde cualquier sesión de OpenCode) se da una instrucción a una PC.
- Si esa PC no puede ejecutar la tarea (poca RAM, sin GPU, herramienta ausente), **delega en otra PC**.
- Si conviene ir más rápido, **divide la tarea y delega una parte** mientras continúa con la suya.
- La comunicación es **bidireccional**: cualquier nodo puede delegar y ejecutar; no hay nodo maestro.
- La delegación es **híbrida**: automática solo donde una regla explícita lo permite; el resto requiere aprobación humana.

### No-objetivos

- No reemplazar ni reimplementar OpenCode. ODW es una capa de coordinación **sobre** OpenCode.
- No construir (en el MVP) una app móvil propia. El celular usa clientes existentes y un bot de Telegram.
- No exponer `opencode serve` a la red bajo ninguna circunstancia.
- No soportar ejecución arbitraria remota de comandos. Solo tareas entregadas a un agente OpenCode dentro de un workspace aislado.

---

## 2. Hechos verificados sobre OpenCode

Fuente: documentación oficial (opencode.ai/docs/server) y fichas de clientes de la comunidad, consultadas en noviembre de 2026.

| Hecho | Detalle |
|---|---|
| Servidor headless | `opencode serve [--port] [--hostname] [--cors]`. Puerto por defecto 4096. |
| Autenticación | HTTP Basic. Contraseña en `OPENCODE_SERVER_PASSWORD`; usuario por defecto `opencode` (override: `OPENCODE_SERVER_USERNAME`). Aplica a `serve` y `web`. |
| Especificación | OpenAPI 3.1 en **`GET /openapi.json`**. (`GET /doc` sirve la SPA web en v2.3.2.) **Fuente de verdad: cliente tipado contrastado con este spec, fijado por hash.** |
| Sesiones (API v1) | `GET /session`, `POST /session` (body `{parentID?, title?}`), `GET /session/status`, `GET /session/:id`, `DELETE /session/:id`, `GET /session/:id/message`. **En v2.3.2 la superficie real es `/api/session...` (ver H0).** |
| Eventos | `GET /event`: stream SSE. Primer evento `server.connected`, luego eventos del bus. **En v2.3.2: `GET /api/event`, envelope `{id,event,data}`.** |
| Control del TUI | Endpoints `/tui/*` (usados por plugins de IDE). No se usarán. |
| Versiones de docs | Existen docs **V1** y **V2**. V2 muestra una superficie distinta (`/api/session/{id}/prompt`, rutas de shell, PTY, MCP) marcada **experimental**. |
| Clientes móviles | **No hay app oficial**. Existen clientes comunitarios no oficiales (apps Android, CrossCode, bot de Telegram, PWAs web). Todos son clientes de un `opencode serve`; **ninguno conecta PCs entre sí**. |

### Correcciones a borradores previos

- La variable de contraseña es `OPENCODE_SERVER_PASSWORD` (no `OPENCODE_PASSWORD`).
- Las llamadas a la API no son estables entre V1 y V2: todo acceso pasa por un adaptador versionado (sección 8).

### Supuestos pendientes `[VERIFICAR]`

1. Endpoint exacto para **enviar un prompt a una sesión** en la versión instalada. **Resuelto en H0:** v2.3.2 usa `POST /api/session/{id}/prompt` con body `{text}` (no `/message`).
2. Formato exacto de configuración de servidores MCP en `opencode.json` (local y remoto) y cómo declarar permisos "preguntar antes de ejecutar" para herramientas MCP.
3. Si V2 permite registrar servidores MCP vía API (`PUT /api/mcp/{server}`); útil para autoconfiguración.
4. Si `opencode pair` es un comando oficial. Solo aparece en la ficha de una app no oficial. **No depender de él.**
5. Cómo expone OpenCode las solicitudes de permiso de herramientas (evento SSE y endpoint de respuesta), para poder aprobarlas remotamente.
6. Comportamiento de `opencode serve` con varios directorios/proyectos (¿un servidor por workspace o uno global?).

---

## 3. Arquitectura

### 3.1 Principio: un solo servicio, ODW Node, por PC

No hay roles fijos de Coordinator/Worker. Cada PC ejecuta el mismo servicio. Un nodo puede delegar una tarea y ejecutar otra al mismo tiempo.

```
Celular ──(cliente OpenCode / bot Telegram)──► OpenCode A ──MCP localhost──► ODW Node A
                                                                                 ║
                                                                       Tailscale (API de pares)
                                                                                 ║
Celular ◄──────────── aprobaciones (Telegram) ───────────────────────────── ODW Node B ──HTTP localhost──► opencode serve (B)
```

### 3.2 Dos interfaces separadas

| Interfaz | Transporte | Visible para | Propósito |
|---|---|---|---|
| **Fachada local (MCP)** | Streamable HTTP sobre `127.0.0.1` | El OpenCode de la misma PC | Herramientas `odw_*` |
| **API de pares** | HTTPS sobre la red Tailscale | Otros ODW Node | Tareas, mensajes, resultados |

El modelo **nunca** habla con otro nodo directamente. Solo habla con su nodo local, que aplica políticas. MCP no viaja por la red.

### 3.3 Módulos de un nodo

| Módulo | Responsabilidad |
|---|---|
| **Identity & Trust** | Identidad del nodo, lista de pares y credenciales por par |
| **Resource Monitor** | CPU, RAM, GPU/VRAM, herramientas instaladas, carga y tareas activas |
| **Planner** | Puntúa si conviene delegar, a quién, y si la tarea es divisible |
| **Policy Engine** | Decide para tareas entrantes y salientes: `AUTO`, `ASK`, `DENY` |
| **Task Manager** | Máquina de estados, persistencia, recuperación, leases |
| **Executor (adaptador)** | `IExecutor`; primera implementación: OpenCode |
| **Sandbox** | Usuario de bajo privilegio, Job Objects, worktrees |
| **Approval Gateway** | Aprobación fuera de banda (Telegram + CLI) |
| **Audit Log** | Registro append-only de aprobaciones, tareas y comandos |

---

## 4. Modelo de dominio

### 4.1 Identificadores

- `node_id`: nombre estable configurado (ej. `PC-MAIN`).
- `task_id`: ULID, único globalmente.

### 4.2 Contrato de tarea

```json
{
  "task_id": "01J9ZQ...",
  "origin_node": "PC-A",
  "target_node": "PC-B",
  "parent_task_id": null,
  "lineage": { "hops": ["PC-A"], "max_depth": 1 },
  "kind": "analysis",
  "instructions": "Analizar arquitectura y ejecutar pruebas",
  "workspace": {
    "repo": "SistemaMaestro",
    "base_ref": "main",
    "mode": "read_only",
    "allowed_paths": ["src/", "tests/"]
  },
  "limits": {
    "max_minutes": 45,
    "max_memory_gb": 12,
    "max_processes": 64,
    "network": "none"
  },
  "executor": { "type": "opencode", "model_hint": null },
  "approval": {
    "task_hash": "sha256:...",
    "approved_by": "human:telegram:123456",
    "approved_at": "2026-11-24T18:00:00Z"
  }
}
```

- `kind`: `analysis` | `test_run` | `code_change`.
- `workspace.mode`: `read_only` | `read_write_worktree`.
- `task_hash`: SHA-256 de `{instructions, origin_node, target_node, workspace, limits, kind}`. Cualquier cambio invalida la aprobación.
- El contrato es independiente del modelo y del ejecutor.

### 4.3 Estados (vista de cada nodo)

**Nodo origen:** `PROPOSED` → `WAITING_APPROVAL` → `APPROVED` → `SENT` → `ACCEPTED` → `RUNNING` → `DONE` | `FAILED` | `CANCELLED` | `UNREACHABLE` | `REJECTED`.

**Nodo destino:** `RECEIVED` → `AWAITING_LOCAL_APPROVAL` → `QUEUED` → `RUNNING` → `COMPLETED` | `FAILED` | `CANCELLED` | `REVIEW_REQUIRED`.

- `UNREACHABLE` se asigna cuando vence el lease/heartbeat. **No implica fallo**: el origen no debe reejecutar automáticamente.
- Las transiciones inválidas deben lanzar excepción y estar cubiertas por pruebas.

### 4.4 Lineage y anti-ciclos

- Un nodo rechaza una tarea si su `node_id` ya está en `lineage.hops` o si `hops.Count > max_depth`
  (`hops` incluye al emisor; `max_depth = 1` permite exactamente un salto A→B).
- Por defecto `max_depth = 1`: la redelegación es opt-in por política.

---

## 5. Seguridad

### 5.1 Modelo de amenazas

1. Un agente LLM con acceso a shell puede ejecutar casi cualquier cosa. **Las listas negras de comandos no son un control de seguridad.**
2. El agente no debe poder aprobar sus propias delegaciones.
3. Un par comprometido no debe obtener control total de otro nodo.
4. `opencode serve` jamás es alcanzable desde la red.

### 5.2 Controles

| Control | Implementación |
|---|---|
| Red | Tailscale con ACLs de mínimo privilegio. ODW escucha solo en la interfaz Tailscale. |
| `opencode serve` | Bind a `127.0.0.1`, contraseña aleatoria por instancia, nunca expuesto. |
| Autenticación de pares | MVP: token por par (se guarda solo su hash). Evolución: firma de peticiones con Ed25519. |
| Aislamiento de ejecución | El agente remoto corre bajo el usuario de Windows `odw-agent` (sin privilegios), con ACLs de sistema de archivos que limitan el acceso a `.odw/worktrees/`. |
| Límites de recursos | Windows **Job Objects**: memoria, número de procesos, tiempo de CPU/reloj. Matar el job al cancelar o vencer. |
| Credenciales | `odw-agent` no tiene acceso a llaves SSH, tokens de Git ni variables de entorno personales. |
| Rutas permitidas | Aplicadas por ACL del sistema operativo, **no** por instrucciones en el prompt. |
| Aprobación | Vinculada a `task_hash` y firmada/registrada por el nodo. Invalidable. |
| Aprobación fuera de banda | Telegram (con lista blanca de `user_id`) y CLI por **named pipe con ACL** restringida a administradores. **No existe endpoint HTTP de aprobación.** |
| Auditoría | Registro append-only con hash encadenado de cada transición, aprobación y comando ejecutado. |
| Opción reforzada (hito posterior) | WSL2 o contenedor por tarea con red restringida. |

### 5.3 Limitación conocida (documentar en `SECURITY.md`)

En el nodo origen, el OpenCode del usuario podría intentar autoaprobarse si pudiera alcanzar el canal de aprobación. Por eso: (a) no hay API HTTP de aprobación, (b) la aprobación por Telegram solo la procesa el servicio con el `user_id` autorizado, (c) el pipe de CLI requiere permisos de administrador, y (d) se recomienda configurar OpenCode para denegar la lectura de los archivos de configuración de ODW.

### 5.4 Matriz de políticas inicial

| Operación | Política |
|---|---|
| Consultar recursos / estado de tareas | Permitido |
| Crear propuesta de tarea | Permitido |
| Enviar tarea (origen) | `ASK` salvo regla `AUTO` explícita |
| Aceptar `analysis` / `test_run` en `read_only` desde par de confianza | Configurable (`AUTO` o `ASK`); por defecto `ASK` |
| Aceptar `code_change` | `ASK` siempre en el MVP |
| Instalar paquetes | `ASK`; requiere `network != none` |
| Fusionar a rama principal | Denegado (solo manual) |
| Publicar / desplegar / acceder a credenciales | Denegado |

Las reglas se definen en `policy.json` y se evalúan en orden; gana la primera coincidencia; la regla por defecto es `ASK`.

---

## 6. Interfaces

### 6.1 Herramientas MCP locales (OpenCode → ODW Node)

| Herramienta | Descripción |
|---|---|
| `odw_list_peers` | Pares conocidos y su estado (online/offline) |
| `odw_get_peer_status` | Recursos y capacidades de un par |
| `odw_estimate_task` | Puntaje de dónde conviene ejecutar (local vs. pares) |
| `odw_propose_task` | Crea una propuesta; queda en `WAITING_APPROVAL` si la política lo exige |
| `odw_get_task_status` | Estado y progreso |
| `odw_get_task_result` | Resultado, informe, ramas o diffs |
| `odw_send_task_message` | Envía un mensaje a una tarea en curso (instrucción adicional o respuesta) |
| `odw_cancel_task` | Solicita cancelación |

**No existe** `odw_approve_task`. La aprobación nunca es una herramienta del modelo.

`[VERIFICAR]` Configurar estas herramientas en OpenCode con permiso "preguntar" para que las solicitudes se aprueben desde el cliente móvil.

### 6.2 API de pares (ODW Node ↔ ODW Node)

Base: `https://<nodo>.<tailnet>.ts.net:<puerto>/peer/v1`. Todas las rutas requieren autenticación de par.

| Método | Ruta | Descripción |
|---|---|---|
| `GET` | `/status` | Capacidades, recursos y carga |
| `POST` | `/tasks` | Enviar tarea (contrato 4.2) |
| `GET` | `/tasks/{id}` | Estado |
| `GET` | `/tasks/{id}/events` | SSE de progreso |
| `POST` | `/tasks/{id}/messages` | Mensaje al agente o al origen |
| `POST` | `/tasks/{id}/cancel` | Cancelar |
| `GET` | `/tasks/{id}/result` | Resultado final y artefactos |

Reglas: idempotencia por `task_id`; respuestas con `version` de protocolo; rechazo con códigos claros (`LINEAGE_LOOP`, `POLICY_DENIED`, `APPROVAL_INVALID`, `RESOURCES_INSUFFICIENT`).

### 6.3 Ejemplo de respuesta de `/status`

```json
{
  "node_id": "PC-MAIN",
  "protocol_version": 1,
  "status": "online",
  "hardware": { "cpu": "Ryzen 7", "ram_total_gb": 32, "gpu": "RTX 4060", "gpu_vram_gb": 8 },
  "resources": { "ram_available_gb": 21.0, "cpu_usage_percent": 18, "gpu_available": true, "active_tasks": 0 },
  "capabilities": ["dotnet", "python", "git", "opencode", "gpu"],
  "accepts": ["analysis", "test_run"]
}
```
Los valores son ilustrativos; el monitor debe medirlos en tiempo real.

---

## 7. Flujo de ejecución

1. El usuario da una orden (desde el celular o la PC) a su OpenCode.
2. El agente decide que no puede o que conviene delegar y llama `odw_estimate_task`.
3. `odw_propose_task` crea la tarea; el Policy Engine de origen evalúa `AUTO | ASK | DENY`.
4. Si `ASK`: Approval Gateway notifica por Telegram; el humano aprueba/rechaza. Se registra la aprobación con `task_hash`.
5. Origen envía `POST /tasks` al destino.
6. El Policy Engine de destino evalúa por **sus propias reglas** (`AUTO | ASK | DENY`). La aprobación del origen **no sustituye** la del destino.
7. El destino crea un worktree, inicia/usa `opencode serve` bajo `odw-agent` y crea una sesión.
8. Se envían las instrucciones y archivos necesarios (no se copia la conversación original).
9. El destino sigue el progreso por el stream de eventos y emite heartbeats al origen.
10. Al finalizar: validación (rutas modificadas dentro de lo permitido, pruebas ejecutadas), empaquetado del resultado y notificación al origen.
11. El origen entrega el resultado al agente solicitante.

**Modo aceleración:** el agente divide el trabajo en subtareas independientes, delega una o más y continúa con otra; luego integra los resultados. La integración de código siempre pasa por ramas y revisión, nunca por escritura directa.

---

## 8. Adaptador de OpenCode

- Interfaz `IExecutor` con la implementación `OpenCodeExecutor`.
- Todo el acceso HTTP a OpenCode pasa por `OpenCodeClient`, **contrastado contra el OpenAPI de `/openapi.json`** de la versión fijada y fijado por SHA-256.
- Fijar la versión de OpenCode en configuración; al arrancar, comparar con la versión detectada y advertir si difiere.
- Pruebas de contrato contra una instancia real de OpenCode (`[Trait("Category","Integration")]`).
- Funciones mínimas: `StartServer`, `CreateSession`, `SendPrompt`, `GetStatus`, `StreamEvents`, `GetMessages`, `AbortSession`, `Shutdown`.
- `opencode serve`: puerto efímero local, contraseña aleatoria por instancia, un proceso por workspace/tarea (`[VERIFICAR]` si basta con uno global).

---

## 9. Workspace aislado

- Un **Git worktree por tarea** bajo `.odw/worktrees/<task_id>/`, en rama `odw/<task_id>`.
- Dos agentes nunca comparten directorio de trabajo.
- Modo `read_only`: worktree sin permisos de escritura para `odw-agent` (o copia de solo lectura).
- El agente puede leer, modificar rutas permitidas, ejecutar pruebas y crear commits **en su rama**.
- Fusionar a la rama principal queda fuera del alcance de ODW.
- Entrega: informe en Markdown + diff/rama + resultados de pruebas.

---

## 10. Persistencia

SQLite (un archivo por nodo). Tablas mínimas: `peers`, `tasks`, `task_events`, `approvals`, `messages`, `audit_log`, `schema_version`. Migraciones versionadas.

Al reiniciar el servicio: las tareas `RUNNING` pasan a `REVIEW_REQUIRED` (o `INTERRUPTED`) y se reportan al origen. Nunca se reejecutan automáticamente.

---

## 11. Tecnología y estructura del repositorio

**Plataforma:** .NET 10 LTS (ADR-001; el documento original decía .NET 8, fuera de soporte), Windows primero (servicio de Windows, Job Objects, ACLs). Diseño con interfaces para portar a Linux/macOS más adelante.

```
odw/
├── src/
│   ├── Odw.Core/                 # modelos, contratos, estados, lineage, hash de tarea
│   ├── Odw.Node/                 # host: servicio, scheduler, task manager, persistencia
│   ├── Odw.Policy/               # motor de reglas
│   ├── Odw.Trust/                # pares, tokens/claves, firmas
│   ├── Odw.Sandbox/              # usuario restringido, Job Objects, ACLs, worktrees
│   ├── Odw.Executors.OpenCode/   # OpenCodeClient + OpenCodeExecutor
│   ├── Odw.PeerApi/              # API node-a-node
│   ├── Odw.Mcp/                  # fachada MCP local
│   ├── Odw.Approvals/            # Telegram + pipe de CLI
│   └── Odw.Cli/                  # status, peers, approve, cancel, doctor
├── tests/
│   ├── Odw.Core.Tests/
│   ├── Odw.Node.Tests/
│   ├── Odw.Policy.Tests/
│   └── Odw.Integration.Tests/
├── config/
│   ├── node.example.json
│   ├── peers.example.json
│   └── policy.example.json
└── docs/
    ├── ARCHITECTURE.md
    ├── SECURITY.md
    ├── PROTOCOL.md
    └── ROADMAP.md
```

Configuración de un nodo (`node.example.json`, orientativo):

```json
{
  "node_id": "PC-MAIN",
  "listen": { "tailscale_interface": true, "port": 7421 },
  "opencode": { "pinned_version": "<fijar>", "executable": "opencode" },
  "peers": [
    { "node_id": "PC-LAPTOP", "address": "pc-laptop.tailnet.ts.net:7421", "token_ref": "env:ODW_PEER_TOKEN_LAPTOP" }
  ],
  "approvals": { "telegram": { "bot_token_ref": "env:ODW_TG_TOKEN", "allowed_user_ids": [0] } }
}
```

---

## 12. Plan de implementación e hitos

Cada hito termina con criterios de aceptación **ejecutables**. No avanzar al siguiente sin cumplirlos.

### H0 — Prototipo de validación (1–2 días)
Script (PowerShell o C#) que, desde la PC A, por Tailscale: consulta `/openapi.json` de un `opencode serve` en B, crea una sesión, envía un prompt, lee el resultado y escucha `/api/event`.
**Entregables:** `docs/H0-findings.md` con los endpoints reales, formato de eventos, formato de permisos y resolución de cada `[VERIFICAR]` de la sección 2.
**Nota:** para H0 el serve de B puede exponerse temporalmente en la interfaz Tailscale; **desde H1 solo en localhost**.

### H1 — Nodo mínimo y delegación básica
- Solución .NET 10, `Odw.Core`, nodo como servicio, Resource Monitor, SQLite.
- Fachada MCP local con `odw_list_peers`, `odw_get_peer_status`, `odw_propose_task`, `odw_get_task_status`, `odw_get_task_result`.
- Pares fijos por archivo; token por par.
- **Aceptación:** una sesión de OpenCode en A delega una tarea `analysis` de solo lectura a B y recupera el resultado.

### H2 — Aprobación desde el celular
- Approval Gateway: bot de Telegram con lista blanca; pipe de CLI.
- Vinculación de la aprobación a `task_hash`.
- **Aceptación:** tarea sin aprobar no se ejecuta; tarea aprobada se ejecuta; cambiar una instrucción tras aprobar invalida la aprobación.

### H3 — Aislamiento y políticas
- Usuario `odw-agent`, ACLs, Job Objects, worktrees, `policy.json`, auditoría.
- Políticas del nodo destino (`AUTO | ASK | DENY`).
- **Aceptación:** el agente no puede leer fuera del worktree ni exceder límites de memoria/tiempo (probado con tareas hostiles de prueba); el destino puede rechazar una tarea que el origen aprobó.

### H4 — Bidireccionalidad completa
- Lineage y anti-ciclos, mensajes por tarea, cancelación, heartbeats/leases, recuperación tras reinicio.
- **Aceptación:** A delega a B y B delega a A en tareas distintas simultáneamente; un ciclo A→B→A es rechazado; reiniciar B a mitad de tarea deja la tarea en estado recuperable y el origen lo ve.

### H5 — Colaboración y aceleración
- `code_change` con worktree y rama, informes, verificación de pruebas, `odw_estimate_task` con puntuación, división de tareas.
- **Aceptación:** dos tareas simultáneas sobre un mismo repositorio usan worktrees distintos y producen ramas sin conflicto de archivos de trabajo.

### H6 — Escalabilidad y usabilidad
- Emparejamiento por QR, firma de peticiones (Ed25519), N nodos, panel de estado, políticas configurables.
- **Aceptación:** incorporar un tercer nodo sin cambios de código.

---

## 13. Pruebas de aceptación transversales

| Prueba | Resultado esperado |
|---|---|
| Par desconectado | Se detecta y se marca `offline` |
| Tarea sin aprobación | No se ejecuta |
| Aprobación con hash alterado | Rechazada |
| Token de par inválido | `401`, sin efectos |
| Tarea aprobada | Se crea sesión remota y se ejecuta |
| Cancelación | El job se termina y el estado queda `CANCELLED` |
| Reinicio del nodo | Estado persistido; tareas en curso quedan identificadas |
| Ciclo de delegación | Rechazo con `LINEAGE_LOOP` |
| Dos tareas simultáneas | Worktrees separados |
| Intento de acceso fuera de `allowed_paths` | Denegado por el sistema operativo |
| Cambio de versión de OpenCode | Advertencia y pruebas de contrato fallan de forma visible |

---

## 14. Instrucciones para el agente de desarrollo

1. **Empieza por H0.** No escribas la solución completa hasta resolver los `[VERIFICAR]`.
2. Cuando la documentación y el comportamiento real difieran, **gana el comportamiento real**; documenta la diferencia en `docs/H0-findings.md`.
3. No agregues funciones fuera del hito en curso.
4. Nunca expongas `opencode serve` fuera de `127.0.0.1` (salvo el experimento acotado de H0).
5. Nunca implementes una herramienta MCP ni un endpoint HTTP que permita aprobar tareas.
6. Escribe pruebas junto con cada módulo; el Policy Engine y la máquina de estados requieren cobertura alta.
7. Registra en el audit log toda transición de estado y toda aprobación.
8. Secretos solo por referencia (`env:`/almacén de credenciales); nunca en archivos versionados.
9. Al terminar cada hito, actualiza `ROADMAP.md` y reporta qué criterios de aceptación pasaron y cuáles no.
10. Ante ambigüedad de seguridad, elige la opción más restrictiva y documenta la decisión.

---

## 15. Decisiones abiertas (resolver con el propietario)

- Cliente móvil preferido para monitorear/guiar sesiones (¿cuál de los clientes comunitarios o PWA?) y su soporte de aprobaciones.
- Un `opencode serve` por tarea vs. uno por nodo (depende de H0).
- Si habrá reglas `AUTO` desde el inicio o solo `ASK` hasta completar H3.
- Soporte de Linux/macOS: ¿cuándo?
