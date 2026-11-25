# ODW — Roadmap

> Cada hito cierra con criterios **ejecutables**. No se avanza con criterios abiertos.
> Estados: ✅ hecho · 🟡 parcial · ⬜ pendiente · ⛔ bloqueado.

| Hito | Estado | Nota |
|---|---|---|
| D0 — Diseño detallado | ✅ | Este paquete de documentos (2026-11-24) |
| H0 — Verificación de OpenCode | 🟡 | Solo queda H0-6 (aislamiento por directorio) |
| H1 — Nodo mínimo y delegación | 🟡 | Funcional con `odw serve`, TLS opt-in y servicio Windows (288 pruebas); falta cuenta dedicada y prueba en dos PCs reales |
| H2 — Aprobación desde el celular | 🟡 | Pipe + CLI + HMAC + Telegram implementados; falta probar con un bot real |
| H3 — Aislamiento y políticas | 🟡 | ACLs de workspace y `odw sandbox check`; falta usuario/launch real y red |
| H4 — Bidireccionalidad completa | ⬜ | Depende de H2/H3 |
| H5 — Colaboración y aceleración | ⬜ | Depende de H4 |
| H6 — Escalabilidad y usabilidad | ⬜ | Depende de H5 |

---

## D0 — Diseño detallado ✅

**Entregables**

- `docs/H0-FINDINGS.md` — hechos verificados y correcciones.
- `docs/DECISIONS.md` — 12 ADRs vinculantes.
- `docs/DOMAIN-MODEL.md` — contrato, hash con vectores, máquinas de estado.
- `docs/PROTOCOL.md` — API de pares v1.
- `docs/MCP-FACADE.md` — herramientas y registro en OpenCode.
- `docs/PERSISTENCE.md` — esquema SQLite, recuperación, auditoría.
- `docs/SECURITY.md` — amenazas, identidades, ACL, Job Objects, aprobaciones.
- `docs/OPENCODE-ADAPTER.md` — `IExecutor` y cliente V2.
- `docs/PLANNER-POLICY.md` — puntuación y reglas.
- `docs/TESTING.md` — estrategia y puertas de CI.
- `config/*.example.json`, `docs/schemas/*.schema.json`.

**Criterio de cierre** — una persona puede implementar H0/H1 sin tomar decisiones de diseño
nuevas. ✅ (las decisiones abiertas están listadas abajo, con dueño y fecha).

### Decisiones abiertas (requieren al propietario)

| # | Pregunta | Impacto | Propuesta |
|---|---|---|---|
| D0-1 | Cliente móvil para ver/guiar sesiones | UX, aprobaciones | Empezar con Telegram + PWA oficial; no bloquear H1 |
| D0-2 | ¿Reglas `AUTO` desde H1 o todo `ASK` hasta H3? | Velocidad vs. riesgo | Todo `ASK` hasta H3, salvo `analysis` local |
| D0-3 | ¿Linux/macOS? | Diseño de sandbox | Interfaces ya preparadas; implementación post-H6 |
| D0-4 | ¿Aprobación por herramienta también por Telegram en H2? | Carga humana | Sí, solo para acciones riesgosas; resto por política local |

---

## H0 — Verificación de OpenCode 🟡

**Hecho (2026-11-24)**

- Superficie V2 (143 rutas), auth Basic, SSE y permisos verificados contra v2.3.2.
- Esquema fijado: `docs/reference/opencode-2.3.2-openapi.json`
  (SHA-256 `48f6cb36…99973`).
- Resueltos `[VERIFICAR]` 1, 2 (parcial), 3, 4, 5, 6 (parcial).
- **H0-1**: un prompt sin respuesta de permiso queda bloqueado indefinidamente (≥ 120 s); con
  `once` continúa y con `reject` el turno termina igualmente `succeeded` con `tool.failed`.
- **H0-3**: `--port 0` + `server listening on http://127.0.0.1:<p>` + `pid`/`urls` en `/api/info`.
- **H0-4**: `Permission.Ruleset` = `[{action, resource, effect}]`, aplicado al crear la sesión.
- **H0-5**: SSE sin `event:`/`id:`; `Last-Event-ID` no aplica; reconciliación por `GET`.
- **H0-2**: `mcp.servers.<n>` con `type:"remote"`, `url`, `oauth:false`, `headers`,
  `codemode:false`; `GET /api/config` lo carga. Registro de la fachada desbloqueado.

**Pendiente (no bloquea H1)**

| ID | Tarea | Criterio de cierre |
|---|---|---|
| H0-6 | Aislamiento de sesiones por directorio | Dos directorios, sin visibilidad cruzada |

**Entregable de cierre:** `tools/h0-probe.ps1` (arnés reproducible; 15/15 comprobaciones en verde
el 2026-11-24) y este documento actualizado.

---

## H1 — Nodo mínimo y delegación básica 🟡

**Estado:** solución, `Odw.Core`, persistencia, fachada MCP de lectura, doctor y adaptador de
OpenCode completados (2026-11-24). Resto pendiente.

- ✅ `Odw.slnx` con todos los proyectos y dependencias cableadas.
- ✅ `Odw.Core`: contrato, hash con vectores, máquina de estados, validación, lineage,
  ULID, interfaces `IExecutor`/`IPolicyEngine`/`IApprovalChannel`.
- ✅ `Odw.Node.Persistence`: migración 0001, `SqliteWriter`, `TaskStore` con CAS y auditoría
  encadenada, outbox con backoff, recuperación al arranque.
- ✅ `Odw.Mcp`: fachada de solo lectura (`odw_list_peers`, `odw_get_peer_status`,
  `odw_get_task_status`, `odw_get_task_result`) sin estado y con token bearer.
- ✅ `odw doctor`: BD/integridad/auditoría, binds, exposición de puertos, token, versión de
  OpenCode y hash del spec. `odw init` crea la base.
- ✅ `Odw.Executors.OpenCode`: cliente V2 + SSE, arranque de `serve` y ejecutor con broker de
  permisos; integración real contra 2.3.2 en verde.
- ✅ `Odw.Policy`: carga/validación fail-closed, prioridad y primera coincidencia, ventanas
  horarias, límites, capacidades y `any_action`; `config/policy.example.json` verificado.
- ✅ Host del nodo: dos listeners con binds validados, autenticación de pares, `hello`/`status`,
  `POST /tasks` idempotente con política del destino, `GET /tasks/{id}` con ETag, y la unión
  `TaskAggregate` + `TaskStore` vía `TaskCoordinator`.
- ✅ Scheduler de ejecución: workspace Git aislado, Job Objects, `QUEUED`→`RUNNING`→estado
  final, runner real de OpenCode con resultado; integración real contra 2.3.2.
- ✅ Lado origen: cliente de pares, `odw_propose_task`, política saliente, idempotencia,
  poller de estado/resultado y prueba real A→B→resultado.
- ✅ Aprobaciones por named pipe y CLI (`list/approve/deny/expire`) vinculadas a `task_hash`,
  con expiración y un solo uso.
- ✅ `node.json` + `peers_file` + `policy.json`, secretos por `env:`/`file:` y `odw serve` en
  primer plano.
- ✅ TLS opt-in en la API de pares y `odw service install` (sc.exe) con host de servicio .NET.
- ⬜ Cuenta dedicada y prueba en dos PCs reales.
- ✅ 284 pruebas verdes, build sin advertencias.
- ⬜ Listeners del host, monitor, herramientas MCP de escritura, scheduler y sandbox.

**Entregables**

- Solución `Odw.slnx` con `Odw.Core`, `Odw.Node`, `Odw.Policy`, `Odw.Trust`, `Odw.PeerApi`,
  `Odw.Mcp`, `Odw.Executors.OpenCode`, `Odw.Cli`, `Odw.Sandbox` (esqueleto).
- Servicio Windows con dos listeners (ADR-002) y validación de binds.
- SQLite con migración 0001 y `SqliteWriter`.
- Resource Monitor (CPU, RAM, GPU opcional, herramientas).
- Fachada MCP con 5 herramientas: `odw_list_peers`, `odw_get_peer_status`,
  `odw_propose_task`, `odw_get_task_status`, `odw_get_task_result`.
- Pares por archivo, token por par, `GET /hello` y `GET /status`.
- Ejecución `analysis` + `read_only` en el destino con `OpenCodeExecutor` mínimo.

**Criterios de aceptación**

1. `odw doctor` en verde en dos PCs.
2. Una sesión de OpenCode en A delega `analysis` a B y recupera el informe.
3. Repetir el `POST` no duplica la tarea (`Idempotent-Replay: true`).
4. Reiniciar B a mitad de tarea deja la tarea en `Interrupted` y A lo ve.
5. Bind no conforme (`0.0.0.0` o loopback en pares) impide el arranque.
6. `/mcp` no es alcanzable desde la IP Tailscale; `/peer/v1` no desde loopback.
7. Suite unitaria verde; cobertura de `Odw.Core` ≥ 90 %.

**Riesgos:** H0-1 (permisos bloqueantes), H0-3 (descubrimiento de puerto), GPU sin `nvidia-smi`.

---

## H2 — Aprobación desde el celular 🟡

**Estado:** pipe + CLI completos (2026-11-24). Telegram y HMAC pendientes.

- ✅ Named pipe `odw-approvals` con DACL admin+SYSTEM y comandos `list/approve/deny/expire`.
- ✅ CLI `odw approvals ...`.
- ✅ Vinculación `(task_id, task_hash, nonce)`, TTL 15 min y un solo uso (probado).
- ✅ Firma HMAC-SHA-256 de la decisión y verificación.
- ✅ Bot de Telegram: long polling, allowlist, botones y callbacks.
- ✅ Auditoría de solicitud y decisión.
- ⬜ Prueba con un bot real (falta token).

**Entregables**

- `Odw.Approvals` con `IApprovalChannel` + Telegram (long polling, allowlist) + pipe de CLI.
- Vinculación `(task_id, task_hash, nonce)`, TTL 15 min, un solo uso, HMAC.
- `odw approve|deny|pending` por CLI.
- Notificaciones de estado terminal.

**Criterios de aceptación**

1. Tarea `ASK` no se ejecuta sin decisión humana.
2. Aprobar ejecuta; denegar no.
3. Cambiar una instrucción tras aprobar ⇒ `APPROVAL_INVALID` (probado en los dos nodos).
4. Reutilizar un nonce ⇒ rechazado.
5. Un `user_id` fuera de la allowlist no recibe respuesta ni efecto.
6. `POST /peer/v1/tasks/{id}/approve` ⇒ `404`; no existe herramienta de aprobación.
7. Auditoría: cada decisión con `approver`, canal, hash y timestamp.

---

## H3 — Aislamiento y políticas 🟡

**Estado:** ACLs de workspace, usuario `odw-agent`, lanzamiento de `opencode serve` como agente y
modelo gratuito propio implementados y verificados. La cuenta de servicio y el aislamiento de red
siguen pendientes y **no se declaran aislamiento** hasta verificarlos.

- ✅ Job Objects (memoria, procesos, CPU, kill on close).
- ✅ Workspace por clon y ACLs opcionales (servicio F, agente RX/M).
- ✅ `odw sandbox check` reporta si el aislamiento está activo.
- ✅ Policy Engine y auditoría encadenada.
- ✅ Crear `odw-agent`, secreto DPAPI, ACLs y ejecución de procesos como el agente (verificado).
- ✅ Lanzar `opencode serve` como `odw-agent` (verificado con perfil propio).
- ✅ Configuración y modelo propios del agente: `opencode/deepseek-v4.1-flash-free`, sin
  credenciales del usuario (tarea real completada como `odw-agent`).
- ⬜ Aislamiento de red (firewall por usuario no soportado en esta versión de Windows).
- ⬜ ACE de denegación para `node.json`, `odw.db` y claves frente a `odw-agent`.
- ⬜ Escaneo de reparse points y pruebas hostiles automatizadas.

**Entregables**

- Usuario `odw-agent`, servicio `odw-svc`, ACLs, Job Objects, escaneo de reparse points.
- Workspace por clon (ADR-006) y modo `read_only`.
- `policy.json` + Policy Engine + `policy_hash` persistido.
- Auditoría con cadena de hash y `odw audit verify`.
- Mecanismo de red verificado (o degradación explícita a `ASK`).

**Criterios de aceptación**

1. Tarea hostil no lee fuera del workspace (ACL) — evidencia con intento real.
2. Exceder memoria/procesos/tiempo mata el Job; nodo estable.
3. Cancelar mata el árbol en ≤ 5 s.
4. Junction dentro del workspace ⇒ `REPARSE_DETECTED`.
5. `policy.json` inválido impide arrancar.
6. El destino rechaza una tarea que el origen aprobó (`POLICY_DENIED`).
7. `odw audit verify` detecta una fila alterada.
8. `network:"none"` verificado o degradado y documentado.

---

## H4 — Bidireccionalidad completa ⬜

**Entregables**

- Lineage y anti-ciclos, tareas hijas, mensajes, cancelación, leases, outbox.
- Recuperación tras reinicio y reconciliación de vistas.

**Criterios de aceptación**

1. A→B y B→A simultáneas en tareas distintas: ambas completan.
2. A→B→A rechazado con `LINEAGE_LOOP`.
3. Reinicio de B: A ve `Unreachable`; B en `Interrupted`; sin reejecución.
4. Corte de SSE: reconexión sin duplicados.
5. Cancelación remota durante `Running` confirmada por ambos.
6. 100 mensajes idempotentes no generan duplicados.

---

## H5 — Colaboración y aceleración ⬜

**Entregables**

- `code_change` con workspace escribible, rama, diff, informe y verificación de pruebas.
- Planner con puntuación y explicación; `odw_estimate_task` completo.
- División en subtareas declaradas; integración manual.

**Criterios de aceptación**

1. Dos `code_change` simultáneos sobre el mismo repo usan workspaces distintos y ramas distintas.
2. `changed_paths` fuera de `allowed_paths` ⇒ `ReviewRequired`.
3. El planner explica su recomendación con factores y ahorro estimado.
4. Una división con rutas solapadas se rechaza.
5. El padre no queda `Completed` con hijas activas.

---

## H6 — Escalabilidad y usabilidad ⬜

**Entregables**

- Emparejamiento por QR, firma Ed25519, N nodos, panel de estado, políticas configurables.
- Migración/portabilidad a Linux/macOS (si se decide).

**Criterios de aceptación**

1. Tercer nodo sin cambios de código ni reinicio del resto.
2. Rotación de token sin caída.
3. Firma de peticiones verificada y replay rechazado.
4. Panel muestra estado, colas y últimas aprobaciones.

---

## Orden de trabajo inmediato

1. ✅ Crear la solución y las interfaces de `Odw.Core`
   (`TaskRequest`, `TaskState`, `IExecutor`, `IPolicyEngine`, `IApprovalChannel`).
2. ✅ Implementar `task_hash` + máquina de estados con sus vectores y matriz de pruebas.
3. ✅ Implementar SQLite migración 0001 + `SqliteWriter` (pruebas sobre archivo real).
4. ✅ Fachada MCP con herramientas de solo lectura (sin salida a red) y `odw doctor`.
5. ✅ Adaptador de OpenCode: cliente V2, SSE y ejecutor con broker de permisos.
6. ✅ Motor de políticas con validación fail-closed y matriz de pruebas.
7. ✅ Host del nodo y `POST /tasks` (dos listeners, auth, idempotencia, política).
8. ✅ Scheduler de ejecución con workspace Git, Job Objects y runner de OpenCode.
9. ✅ Lado origen, `odw_propose_task`, poller y recuperación del resultado (A→B→resultado).
10. ✅ Aprobaciones por pipe + CLI y `odw serve` con `node.json`/`policy.json`.
11. Servicio Windows + Telegram/HMAC para cerrar H1/H2.

