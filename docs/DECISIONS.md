# ODW — Registro de decisiones (ADR)

> Cada decisión es vinculante para la implementación. Si una decisión debe cambiar,
> se agrega una ADR nueva que la reemplaza; no se edita el historial.

| ID | Decisión | Estado |
|---|---|---|
| ADR-001 | Plataforma objetivo: .NET 10 LTS | Aceptada |
| ADR-002 | Dos listeners, dos puertos, dos autenticaciones | Aceptada |
| ADR-003 | Cliente OpenCode tipado a mano contra `/openapi.json` fijado | Aceptada |
| ADR-004 | Una instancia de `opencode serve` por tarea | Aceptada |
| ADR-005 | ODW es el *broker* de permisos; nunca responde `always` | Aceptada |
| ADR-006 | Aislamiento por clon, no por `git worktree` (por defecto) | Aceptada |
| ADR-007 | Token por par + TLS de Tailscale; firma después | Aceptada |
| ADR-008 | SQLite con un único escritor (actor) y WAL | Aceptada |
| ADR-009 | Aprobación solo fuera de banda; invariante verificable | Aceptada |
| ADR-010 | Estado único con rol (`origin`/`target`) y estado relativo al rol | Aceptada |
| ADR-011 | `task_hash` = SHA-256 de proyección canónica RFC 8785 | Aceptada |
| ADR-012 | Repositorios por nombre lógico, nunca por ruta remota | Aceptada |

---

## ADR-001 — .NET 10 LTS

**Contexto.** `ARCHITECTURE.md` §11 fija .NET 8. El equipo tiene SDK 10.0.400 y 11.0.100;
.NET 8 salió de soporte en noviembre de 2026. Quedan 14 meses de soporte en .NET 10 LTS.

**Decisión.** `net10.0` con `global.json` que fija la banda `10.0.x` (roll-forward `latestPatch`).
Windows-first. `TargetFramework` único vía `Directory.Build.props`.

**Consecuencias.** Se pueden usar APIs nuevas (`System.Threading.Lock`, `TimeProvider`,
`JsonSchemaExporter`, `WindowsIdentity` moderno). No se depende de .NET 8.

**Alternativas.** .NET 8: sin soporte, descartado. .NET 11: no LTS, descartado para un servicio
de larga vida.

---

## ADR-002 — Dos listeners, dos puertos, dos autenticaciones

**Contexto.** La fachada MCP la consume el OpenCode local; la API de pares la consumen otras
PCs. Mezclarlas permite que un agente local alcance la API de pares, o que la red alcance MCP.

**Decisión.** Un solo proceso, dos endpoints Kestrel:

| Listener | Bind obligatorio | Puerto | Auth |
|---|---|---|---|
| Peer API | IP de la interfaz Tailscale (nunca `0.0.0.0`, nunca loopback) | 7421 | Bearer por par |
| MCP | `127.0.0.1` y `[::1]` | 7422 | Bearer de fachada (token de 32 bytes) |

Validación al arrancar: se enumeran las direcciones de las interfaces y se **rechaza** el
arranque si el bind de pares no es una IP Tailscale (`100.64.0.0/10` o interfaz `Tailscale`),
o si el bind MCP no es loopback. Además, autodiagnóstico: `GET /peer/v1/hello` desde
`127.0.0.1` debe dar `404/401` (no debe ser alcanzable por loopback) y `/mcp` desde la IP
Tailscale debe fallar.

**Consecuencias.** Dos `KestrelServerOptions`, dos `RequestDelegate` pipelines, sin rutas
compartidas. Un solo `IHost` para compartir DI, SQLite y el Task Manager.

**Alternativas.** mTLS mutuo en un solo puerto: más simple de exponer mal; descartado.

---

## ADR-003 — Cliente OpenCode escrito a mano, esquema fijado por hash

**Contexto.** El esquema (`docs/reference/opencode-2.3.2-openapi.json`) usa `contentMediaType`
para cuerpos SSE y `x-effect-stream`; los generadores (NSwag/Kiota) producen código incorrecto
o incompleto para esos casos. La superficie que ODW necesita son ~10 operaciones.

**Decisión.**
- `OpenCodeClient`: cliente `HttpClient` tipado, escrito a mano, ~10 métodos.
- DTOs inmutables con `System.Text.Json` (`JsonSerializerOptions` estricto: sin propiedades
  desconocidas en pruebas, `JsonNumberHandling` explícito).
- El archivo de especificación se versiona y su SHA-256 se compara al arrancar contra
  `GET /openapi.json` de la instancia lanzada. Si difiere: **advertencia**, y la prueba de
  contrato falla de forma visible (nunca silenciosa).
- Pruebas de contrato: validación de payloads reales contra el JSON Schema del spec
  (`JsonSchema.Net`) + prueba de integración contra instancia real (`Category=Integration`).

**Consecuencias.** Menos superficie generada, control total del streaming SSE, y detección
temprana de cambios. Si OpenCode rompe compatibilidad, falla una prueba, no producción.

---

## ADR-004 — Una instancia de `opencode serve` por tarea

**Contexto.** `[VERIFICAR]` 6. Las sesiones se anclan a un directorio y el servidor mantiene
estado (config, MCP, permisos) por instancia.

**Decisión.** Por cada tarea ejecutada localmente:
1. Elegir puerto libre en `127.0.0.1` (bind temporal y liberar; carrera aceptada y verificada
   con `/api/info`).
2. Contraseña aleatoria de 32 bytes (solo en memoria del proceso hijo y del padre).
3. `opencode serve --hostname 127.0.0.1 --port <p>` bajo el usuario `odw-agent`, en Job Object,
   con `cwd` = workspace de la tarea.
4. Crear sesión con `location.directory` = workspace y `permissions` restrictivos.
5. Al terminar: `POST /api/session/{id}/interrupt` (si aplica), matar el Job Object, borrar
   el secreto.

**Consecuencias.** Aislamiento de estado, kill determinista, sin fugas entre tareas. Costo:
arranque por tarea; se mitiga manteniendo un *pool* de instancias precalentadas **por nodo**
(sin sesiones), si la medición lo justifica en H5.

**Alternativas.** Una instancia global: riesgo de que una tarea vea sesiones/permisos de otra;
descartado.

---

## ADR-005 — ODW como *broker* de permisos; nunca `always`

**Contexto.** En modo headless, `permission.asked` requiere que alguien responda. Si nadie
responde, la herramienta queda bloqueada. ODW ya tiene un motor de políticas y un canal de
aprobación humana.

**Decisión.** El `OpenCodeExecutor` consume `permission.asked` y decide:
- Acción permitida por la política de la tarea y dentro de `allowed_paths` → `once`.
- Acción riesgosa pero cubierta por la aprobación de tarea vigente y por la política
  (`code_change` con `ASK` ya aprobado) → solicitud al Approval Gateway; al humano se le
  muestran `action`, `resources` y `message`. Si aprueba → `once`; si no → `reject`.
- Todo lo demás → `reject` con mensaje explicativo.
- **`always` no se envía nunca.** Se verifica con una prueba de arquitectura que busca la
  constante en el ensamblado.

**Consecuencias.** El humano sigue en el circuito para acciones riesgosas, sin depender de un
cliente de UI conectado. La aprobación de tarea (nivel tarea) y la de herramienta (nivel
acción) son controles distintos y no se sustituyen.

---

## ADR-006 — Aislamiento por clon, no por `git worktree` (por defecto)

**Contexto.** Un worktree de Git contiene un archivo `.git` que apunta al repositorio
principal (`gitdir: <ruta fuera del sandbox>`). Si el agente puede leerlo y el usuario tiene
permisos, el "aislamiento" por ACL sobre `.odw/worktrees/` es ficticio: el agente llega al
repo real por esa ruta.

**Decisión.**
- `read_only`: exportar un snapshot (`git archive` o `git clone --no-hardlinks --depth`… se
  elige `git archive` + `git init` si se necesita diff) **sin** `.git` enlazado. Sin escritura.
- `read_write_worktree`: clon local del repo en `.odw/workspaces/<task_id>/` (copia completa,
  sin `--shared`/`--reference`), rama `odw/<task_id>`. El agente nunca ve el repo original.
- Al terminar, el servicio (no el agente) hace `git fetch` de la rama desde el clon hacia el
  repo principal, o entrega el bundle como artefacto. **Nunca** fusiona.
- `git worktree` queda como opción explícita (`workspace.strategy: "worktree"`), solo para
  repos grandes y con la advertencia registrada en auditoría.

**Consecuencias.** Más disco y tiempo de copia; aislamiento real. Se puede deduplicar con
`--local` clone si el repo es grande, siempre que el `.git` resultante quede dentro del
sandbox (no `--shared`).

---

## ADR-007 — Autenticación de pares: token por par sobre TLS

**Contexto.** MVP con N pequeño (2–5 nodos) y Tailscale como red de confianza.

**Decisión.**
- Token de 32 bytes, formato `odw1.<key_id>.<secret_base32>`; se guarda solo
  `SHA-256(secret)` y `key_id` para búsqueda O(1).
- Transporte: HTTPS con certificado de Tailscale (`tailscale cert`) cuando exista;
  HTTP aceptado **solo** si `peer_api.allow_plaintext=true` y el bind es la IP Tailscale
  (WireGuard ya cifra). Se registra en auditoría.
- Idempotencia por `task_id` + hash; ventana anti-replay por `sent_at` (±5 min) en mensajes
  firmados (v1.1).
- Rotación: dos tokens activos por par como máximo (`peers.tokens`), con `not_after`.
- Ed25519 (firma de peticiones) se implementa en H6, no antes.

**Consecuencias.** Simple, auditable, sin PKI propia. El token viaja en cada request; TLS/
WireGuard lo protege.

---

## ADR-008 — SQLite con un único escritor

**Contexto.** SQLite en WAL permite lecturas concurrentes, pero las escrituras serializan y
`SQLITE_BUSY` produce fallos intermitentes difíciles de reproducir.

**Decisión.** Un actor `SqliteWriter` con `Channel<WriteCommand>` propio. Toda escritura pasa
por él (una transacción por comando). Lecturas con conexiones de solo lectura y
`busy_timeout=5000`. `journal_mode=WAL`, `synchronous=NORMAL`, `wal_autocheckpoint=1000`,
`foreign_keys=ON`, `temp_store=MEMORY`. Checkpoint `TRUNCATE` en apagado ordenado.

**Consecuencias.** Serialización determinista, sin `BUSY` bajo carga normal. Un escritor lento
es el límite de throughput; aceptable (decenas de tareas, no miles).

---

## ADR-009 — Aprobación solo fuera de banda

**Decisión.** Canales válidos: bot de Telegram (long polling, allowlist de `user_id`) y named
pipe `\\.\pipe\odw-approvals` con SDDL `D:(A;;GA;;;BA)(A;;GA;;;SY)`. **No existe** ruta HTTP
ni herramienta MCP que apruebe. La aprobación se vincula a `(task_id, task_hash, nonce)`,
caduca a los 15 minutos y es de un solo uso.

**Verificación (prueba de arquitectura, obligatoria en CI).**
1. Recorrer los endpoints de ambos pipelines: ninguno contiene `approve`/`approval`.
2. Recorrer las herramientas MCP registradas: ninguna coincide con `odw_approve*`.
3. Prueba de integración: `POST /peer/v1/tasks/{id}/approve` → `404`.

---

## ADR-010 — Un estado por rol, en una sola tabla

**Contexto.** El origen y el destino ven estados distintos del mismo `task_id`. Duplicar
máquinas complica la reconciliación.

**Decisión.** Tabla `tasks` con PK `(task_id, role)` y columna `state`. Dos máquinas de estado
separadas (`OriginStateMachine`, `TargetStateMachine`) sobre el **mismo** enum `TaskState`,
con tablas de transición explícitas. Los eventos de red se traducen a transiciones con guardas.

**Consecuencias.** Un nodo puede ser origen y destino de tareas distintas sin ambigüedad.
`role` es inmutable. `UNREACHABLE` es un estado del origen, no del destino.

---

## ADR-011 — Hash de tarea canónico

**Decisión.** `task_hash = "sha256:" + hex(SHA-256(UTF8(JCS(proyección))))`, con JCS = RFC 8785.
Proyección e invariantes en `docs/DOMAIN-MODEL.md` §4. Vectores de prueba incluidos.
Cualquier cambio en `instructions`, `workspace`, `limits`, `kind`, `origin_node`, `target_node`,
`parent_task_id`, `executor.type` o `lineage.max_depth` invalida la aprobación.

**Consecuencias.** Implementación reproducible entre lenguajes y verificable con vectores.
Nunca se serializa "a ojo": se usa el serializador canónico y se comparan los vectores en CI.

---

## ADR-012 — Repositorios por nombre lógico

**Decisión.** El contrato remoto lleva `workspace.repo` como **nombre lógico** (`"SistemaMaestro"`),
resuelto por una tabla local `repos(name → path, allowed, default_base_ref)`. El destino
rechaza la tarea si el nombre no está registrado (`REPO_UNKNOWN`). Ninguna ruta absoluta
remota se acepta en `repo`, `allowed_paths` ni `denied_paths`; las rutas se validan como
relativas, normalizadas, sin `..`, sin unidad, sin UNC, sin reparse points.

**Consecuencias.** Se elimina una clase entera de path traversal y de filtración de la
topología del disco.
