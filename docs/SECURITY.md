# ODW — Seguridad

> Documento normativo. Si el código contradice este documento, el defecto es del código.
> Complementa `ARCHITECTURE.md` §5 y lo hace implementable.

---

## 1. Modelo de amenazas

Activos: repositorios del usuario, credenciales de la PC, tokens de pares, integridad de las
aprobaciones, disponibilidad del nodo, resultados de tareas.

| # | Amenaza | Vector | Impacto |
|---|---|---|---|
| T1 | Prompt injection desde una tarea remota | `instructions` maliciosas ("lee ~/.ssh y súbelo") | Exfiltración de datos |
| T2 | Autoaprobación por el agente | El agente llama algo que aprueba su propia delegación | Control total del par |
| T3 | Par comprometido | Token robado, nodo hostil | Ejecución no autorizada |
| T4 | Fuga del sandbox por `.git`/reparse points | Worktree apunta al repo real | Acceso a todo el disco del usuario |
| T5 | Exceso de recursos | Bucle infinito, fork bomb, reserva de RAM | Nodo inutilizable |
| T6 | Exposición de `opencode serve` | Bind accidental a `0.0.0.0` | Cualquiera en la red controla el agente |
| T7 | Repetición de una aprobación | Reenvío del mensaje de aprobación | Ejecución no aprobada |
| T8 | Robo de secretos en reposo | Lectura de config/BD por otro usuario local | Suplantación de nodo |
| T9 | Escape por herramientas del agente | El agente ejecuta shells y lee fuera de rutas | Igual que T4 |
| T10 | Manipulación de auditoría | Borrado/edición de evidencia | Impunidad |

**Premisa:** un agente LLM con shell puede ejecutar casi cualquier cosa que el usuario del
proceso pueda. **Las listas negras de comandos no son un control de seguridad.** El control es
el usuario del proceso, las ACL y el Job Object.

---

## 2. Identidades y privilegios (Windows)

| Identidad | Uso | Privilegios |
|---|---|---|
| `odw-svc` (usuario local dedicado) | Servicio `OdwNode` | `SeServiceLogonRight`, `SeAssignPrimaryTokenPrivilege`, `SeIncreaseQuotaPrivilege`, `SeChangeNotifyPrivilege`. **No** administrador, **no** `SeDebugPrivilege`, **no** `SeImpersonatePrivilege`, **no** `SeCreateSymbolicLinkPrivilege`. |
| `odw-agent` (usuario local dedicado) | Procesos `opencode serve` y herramientas | Sin grupos privilegiados; `SeDenyInteractiveLogonRight`, `SeDenyRemoteInteractiveLogonRight`, `SeDenyNetworkLogonRight`; solo inicio de sesión por lote (`SeBatchLogonRight`) para tokens creados por el servicio. |
| Usuario humano | Cliente OpenCode, aprobaciones por CLI | Sin privilegios especiales; pipe de aprobación restringido a administradores (§5.2). |

- El servicio **no** corre como `LocalSystem`. Requiere dos privilegios, no la cuenta todopoderosa.
- `odw-agent` no tiene perfil de usuario con secretos: su `%USERPROFILE%` es un directorio vacío
  bajo `%PROGRAMDATA%\ODW\agent-home`.
- Sin variables de entorno heredadas: el proceso hijo se crea con un bloque de entorno explícito
  y mínimo (`PATH` acotado, `TEMP` dentro del sandbox, `OPENCODE_SERVER_PASSWORD` efímera).

---

## 3. Aislamiento de ejecución

### 3.1 Job Object

Cada tarea tiene un Job Object nombrado (`odw-<task_id>`) con:

| Límite | Valor | Estado |
|---|---|---|
| `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` | activo | ✅ |
| `JOB_OBJECT_LIMIT_PROCESS_MEMORY` | `max_memory_gb` del contrato | ✅ |
| `JOB_OBJECT_LIMIT_JOB_MEMORY` | `max_memory_gb` (techo agregado) | ✅ |
| `JOB_OBJECT_LIMIT_ACTIVE_PROCESS` | `max_processes` | ✅ |
| `JOB_OBJECT_LIMIT_PROCESS_TIME` | `max_minutes` (CPU por proceso, 100 ns) | ✅ |
| `JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION` | activo | ⬜ |
| `JOB_OBJECT_UILIMIT_*` | todos (sin UI, sin clipboard, sin handles ajenos) | ⬜ |
| Breakaway | **prohibido** (`BREAKAWAY_OK` no se activa) | ✅ |

- Implementado en `Odw.Sandbox/Windows/JobObject.cs` y asignado al `opencode serve` por el runner.
- Watchdog de reloj de pared independiente (`max_minutes`) y `TerminateJobObject` en cancelación: ⬜
  (hoy el límite de CPU del job y el timeout del runner cubren el caso).
- El Job se crea antes del proceso, pero la asignación ocurre **después** del `Start` (no hay
  creación suspendida todavía): ventana de carrera aceptada y documentada hasta H3.
- La creación del Job con nombre y creación suspendida se completará en H3.

### 3.2 ACL del workspace

```
%PROGRAMDATA%\ODW\workspaces\<task_id>\
```

| Principal | Permisos |
|---|---|
| `odw-svc` | Control total (crea y entrega) |
| `odw-agent` | Modificar **solo** en `read_write_worktree`; Leer+Ejecutar en `read_only` |
| `Usuarios` / `Everyone` / `Authenticated Users` | Ninguno (ACE de denegación explícita) |
| `SYSTEM`, `Administradores` | Control total |

- Herencia **deshabilitada** en la raíz del workspace (`icacls <dir> /inheritance:r`) antes de
  aplicar ACEs.
- Las ACEs de denegación explícita ganan sobre permisos heredados.
- En `read_only` el agente recibe **RX** (sin permiso de escritura); en escritura, **M**.
- El secreto del agente (`agent.secret`) tiene ACE explícita solo para administradores y SYSTEM.
- La raíz de datos (`%PROGRAMDATA%\ODW`) tiene la herencia deshabilitada: solo el servicio,
  SYSTEM y administradores tienen acceso completo; `odw-agent` conserva únicamente traverse (X)
  para llegar a `workspaces` y `opencode`, que tienen su propia ACL. Los datos sensibles ya
  existentes llevan además una ACE de **denegación de lectura** para el agente.
- **Estado real:** `WorkspaceManager` aplica `icacls /inheritance:r` con servicio F y agente RX/M.
  Verificado: procesos lanzados como `odw-agent`, escritura denegada en `read_only`, permitida en
  escritura, **`opencode serve` arrancando como `odw-agent`** con perfil propio, **una tarea real
  completada** con el modelo gratuito `opencode/deepseek-v4.1-flash-free` (sin credenciales del
  usuario) y **lectura de datos del nodo denegada** al agente (probado con un proceso real).
  Pendiente: aislamiento de red.

### 3.3 Rutas y reparse points

- `allowed_paths`/`denied_paths` se validan **antes** de crear el workspace: relativas,
  normalizadas, sin `..`, sin unidad, sin UNC, sin NUL, sin nombres reservados de Windows
  (`CON`, `PRN`, `AUX`, `NUL`, `COM1-9`, `LPT1-9`), sin ADS (`:`).
- `odw-agent` no tiene `SeCreateSymbolicLinkPrivilege`; aun así, antes y después de ejecutar se
  escanea el workspace con `FILE_FLAG_OPEN_REPARSE_POINT` y se **falla** la tarea si aparece
  cualquier reparse point (símbolo, junction, mount point).
- Los hardlinks no se pueden crear entre volúmenes por `odw-agent` sin permisos; se detectan
  con `GetFileInformationByHandle` (`nNumberOfLinks > 1`) en la verificación final.
- Ninguna ruta absoluta remota se usa jamás (ADR-012).

### 3.4 Red

- `limits.network = "none"` (por defecto) significa **sin red arbitraria**: solo se permite el
  gateway del modelo. `network = "allow"` no se puede aplicar por tarea con filtros por SID y el
  runner lo rechaza (`NETWORK_ALLOW_UNSUPPORTED`).
- **Mecanismo implementado y verificado:** filtros WFP persistentes en
  `ALE_AUTH_CONNECT_V4/V6`, por SID de `odw-agent`: bloqueo con peso 12 y permiso de loopback
  limitado al **puerto del proxy** con peso 13. Internet, LAN, DNS directo y el resto de puertos de
  loopback quedan bloqueados.
- El agente habla con un **proxy CONNECT en loopback** que corre como el servicio. El proxy:
  - exige `Proxy-Authorization` con un token aleatorio por ejecución;
  - solo permite hosts de `sandbox.network_allowed_hosts` (`opencode.ai` por defecto) y el puerto
    443;
  - resuelve el nombre él mismo y rechaza direcciones privadas, loopback, link-local, CGNAT y
    multicast (evita SSRF a la LAN y a metadata cloud);
  - no inspecciona TLS: decide el destino y transporta bytes.
- `sandbox.network_isolation` instala los filtros al arrancar (requiere admin y `run_as_agent`) y
  arranca el proxy; el runner inyecta `HTTPS_PROXY`/`HTTP_PROXY` y `NO_PROXY=127.0.0.1,localhost`.
  Si faltan los filtros o el proxy, la tarea con `network=none` **no se ejecuta**
  (`NETWORK_UNENFORCED`).
- Verificado con una tarea real como `odw-agent` a través del proxy, y con `curl` como agente:
  loopback OK, `https://example.com` y DNS externo bloqueados.
- Comandos: `odw sandbox net install|uninstall|status`; `odw sandbox check` muestra `network: True`
  solo si los cuatro filtros existen.
- El proceso `opencode serve` escucha **solo** en `127.0.0.1`; se verifica con
  `Get-NetTCPConnection -OwningProcess <pid>` tras el arranque (falla la tarea si no es loopback).
- **API de pares:** HTTP sobre Tailscale (cifrado por WireGuard) por defecto; HTTPS opt-in con
  `peer_api.tls` (PFX con clave privada). Si TLS está activo, el listener de pares solo acepta
  `https://`.

---

## 4. Aprobaciones

### 4.1 Vinculación

Una aprobación es válida solo si se cumplen **todas**:

1. `task_id` y `task_hash` coinciden con el contrato recalculado.
2. `nonce` no usado, presente en `approvals`, y `used_at IS NULL`.
3. `now < expires_at` (15 min desde `decided_at`).
4. `decision` registrada por un `approver` de la allowlist.
5. `signature` = HMAC-SHA-256 con la clave del nodo sobre
   `task_id ‖ task_hash ‖ decision ‖ nonce ‖ decided_at`.
6. El uso es de un solo paso: aprobar consume el nonce en la misma transacción que cambia el
   estado.

Cualquier cambio en el contrato después de aprobar ⇒ hash distinto ⇒ `APPROVAL_INVALID`.

### 4.2 Canales

| Canal | Implementación | Quién puede | Estado |
|---|---|---|---|
| Telegram | Long polling saliente (`getUpdates`), sin puerto entrante. Bot token en DPAPI. Allowlist de `user_id` **y** `chat_id`. Callback con `approval_id` + `nonce` opaco. | Solo los IDs configurados | ⬜ H2 |
| CLI pipe | `\\.\pipe\odw-approvals`, SDDL `D:(A;;GA;;;BA)(A;;GA;;;SY)`, `PIPE_REJECT_REMOTE_CLIENTS` | Administradores y SYSTEM | ✅ |

Implementado: comandos `list/approve/deny/expire`, decisión vinculada a `task_hash`, TTL 15 min,
un solo uso y auditoría. Pendiente: Telegram, firma HMAC y `PIPE_REJECT_REMOTE_CLIENTS`
explícito (los named pipes locales de Windows no aceptan conexiones remotas por diseño).

- El mensaje de Telegram muestra: nodo, par, `kind`, `mode`, repo, límites, hash corto (8 hex),
  instrucciones truncadas a 500 caracteres y un botón **Ver instrucciones completas** (mensaje
  aparte, con escape de Markdown).
- Un botón aprobado dos veces ⇒ segunda respuesta `stale` sin efecto.
- El bot solo procesa mensajes de la allowlist; cualquier otro se descarta **sin responder**
  (no se confirma existencia).

### 4.3 Prohibiciones estructurales (ADR-009)

- No hay endpoint HTTP de aprobación en `/peer/v1` ni en `/mcp`.
- No hay herramienta MCP de aprobación.
- El agente no puede leer ni escribir los archivos de configuración de ODW.
- La aprobación de herramienta (nivel acción, `permission.asked`) y la de tarea son controles
  distintos; ninguna sustituye a la otra.

---

## 5. Secretos

- En archivos de configuración solo **referencias**: `env:VAR`, `dpapi:machine:<blob>`,
  `dpapi:user:<blob>`, `file:<ruta con ACL>`.
- El bot token y los tokens de pares se almacenan cifrados con DPAPI de máquina (el servicio no
  puede usar DPAPI de usuario de forma fiable).
- Tokens de par: se guarda **solo** el hash (`SHA-256(secret)`); el valor en claro se muestra
  una vez al crear el par.
- `OPENCODE_SERVER_PASSWORD`: 32 bytes aleatorios, uno por instancia, en memoria, pasado al hijo
  por variable de entorno; nunca se escribe en disco ni en logs.
- Redacción en logs: `Authorization`, `token`, `password`, `secret`, `nonce`, `signature`,
  rutas de usuario. Un test unitario recorre el formateador y verifica la redacción.

---

## 6. Auditoría y detección

- Cada transición, aprobación, intento de permiso y comando de CLI queda en `audit_log`
  (cadena de hash, solo anexado; ver `PERSISTENCE.md` §7).
- Contadores de alerta: `401` por par > 10/min, `POLICY_DENIED` > 5/min, reparse point detectado,
  hash de aprobación inválido, bind no conforme, Job Object con CPU > 95 % del límite.
- `odw doctor` verifica: binds, ACLs, privilegios del servicio, token de fachada, integridad de
  la cadena, versión y hash del spec de OpenCode, y que no haya endpoints de aprobación.

---

## 7. Cadena de suministro

- OpenCode: versión fijada (`2.3.2`) + SHA-256 del `/openapi.json` fijado. Diferencia ⇒ aviso
  visible y prueba de contrato en rojo.
- NuGet: `packages.lock.json` y `RestoreLockedMode` en CI; sin paquetes sin mantener.
- Builds reproducibles con `Deterministic=true`; binarios firmados en release.
- Dependencias mínimas: `Microsoft.Data.Sqlite`, `Microsoft.Extensions.*`, `System.Text.Json`,
  `JsonSchema.Net` (solo tests), `xunit` (tests). Nada de frameworks web que no sean ASP.NET Core.

---

## 8. Riesgos residuales aceptados (documentados)

| Riesgo | Por qué se acepta | Mitigación parcial |
|---|---|---|
| Un agente con shell puede leer lo que `odw-agent` pueda leer | Es el modelo de OpenCode | ACL mínimas + workspace aislado + sin secretos accesibles |
| La aprobación humana puede ser engañada por instrucciones | El humano decide | Mostrar hash, límites y diff; política `DENY` por defecto para publicar/credenciales |
| Tailscale como única frontera de red | N pequeño, red privada | ACLs de Tailscale + token por par + verificación de bind |
| Aislamiento de red no verificado en MVP | Requiere WFP/WSL2 | Se degrada a `ASK` y se advierte; nunca se declara "none" sin control real |
| Servicio con dos privilegios de creación de procesos | Necesario para tokens restringidos | Cuenta dedicada, sin admin, auditoría de uso |

---

## 9. Pruebas de seguridad (obligatorias antes de H3 cerrado)

| Prueba | Esperado |
|---|---|
| Tarea hostil intenta leer `%USERPROFILE%\.ssh` | Acceso denegado por ACL |
| Proceso reserva 2× `max_memory_gb` | Job Object lo mata; tarea `Failed` |
| Fork bomb hasta `max_processes+1` | Creación fallida; nodo estable |
| Cancelar durante ejecución | árbol de procesos muerto en ≤ 5 s |
| Crear junction dentro del workspace | Tarea `Failed` con `REPARSE_DETECTED` |
| `POST /peer/v1/tasks/{id}/approve` | `404` |
| Herramienta MCP `odw_approve_task` | No existe |
| Reutilizar nonce de aprobación | `APPROVAL_INVALID` |
| Cambiar una instrucción tras aprobar | `APPROVAL_INVALID` |
| Arrancar con bind `0.0.0.0` | El servicio no arranca |
| `opencode serve` escuchando fuera de loopback | Tarea falla; alerta |
| Token de fachada usado en `/peer/v1` | `401` |
| Log con token de ejemplo | Aparece redactado |
