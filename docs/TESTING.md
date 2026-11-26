# ODW — Estrategia de pruebas

> Regla: ninguna funcionalidad se considera terminada sin su prueba. Las pruebas son
> ejecutables y deterministas; nada de esperas fijas.

---

## 1. Pirámide y proyectos

| Proyecto | Qué prueba | Velocidad |
|---|---|---|
| `Odw.Core.Tests` | Contrato, hash canónico, lineage, máquinas de estado, ULID | ms |
| `Odw.Policy.Tests` | Matriz de reglas, prioridades, recarga, validación | ms |
| `Odw.Planner.Tests` | Puntuación, empates, viabilidad, divisibilidad | ms |
| `Odw.Node.Tests` | Persistencia, outbox, recuperación, scheduler (SQLite temporal) | 10–100 ms |
| `Odw.Trust.Tests` | Tokens, hash de tokens, nonce, firma de aprobaciones | ms |
| `Odw.Sandbox.Tests` | ACLs, Job Objects, WFP, proxy de modelo, reparse points, kill de árbol (Windows) | s |
| `Odw.Integration.Tests` | Dos nodos en loopback, OpenCode real, SSE, cancelación | 10–120 s |
| `Odw.Architecture.Tests` | Invariantes estructurales (sin aprobar por HTTP, sin `Always`) | ms |

Herramientas: xUnit, `Microsoft.Extensions.TimeProvider.Testing` (reloj falso), `JsonSchema.Net`
(validación contra el spec), `Testcontainers` **no** (no hace falta). Sin mocks de SQLite: se usa
un archivo temporal real, porque los bugs de SQLite no aparecen en memoria.

---

## 2. Reglas de oro

1. **Reloj inyectado.** Cero `DateTime.UtcNow` y cero `Task.Delay` en producción; `TimeProvider`
   en todo el código, `FakeTimeProvider` en pruebas.
2. **Aislamiento de estado.** Cada prueba usa su propio directorio (`Path.GetTempPath()/odw-tests/<guid>`)
   y su propia BD. Se limpia en `Dispose`.
3. **Sin red externa.** Los pares se simulan con `HttpMessageHandler` o con dos `Kestrel` en
   puertos efímeros de loopback.
4. **Sin orden entre pruebas.** xUnit con paralelismo por colección; las de Windows sandbox van
   en una colección serial (`[Collection("WindowsSandbox")]`).
5. **Determinismo.** Nada de `Random` sin semilla; ULIDs con `TimeProvider` y contador fijo.
6. **Fallar visible.** Una prueba de contrato que detecta deriva de esquema debe fallar con un
   mensaje que incluya el endpoint y la diferencia, no un `NullReferenceException`.

---

## 3. Pruebas de caracterización antes de refactorizar

Antes de tocar un módulo existente:
1. Escribir pruebas que capturen el comportamiento actual (incluidos los defectos conocidos).
2. Verificar que pasan.
3. Refactorizar con la red puesta.
4. Cambiar comportamiento solo con una prueba nueva que lo especifique.

Aplica especialmente a: máquina de estados, hash, política y persistencia.

---

## 4. Pruebas de la máquina de estados

- **Matriz completa**: para cada `(rol, estado, evento)` se declara el resultado esperado
  (`nuevo estado` o `InvalidStateTransition`). 100 % de las combinaciones, generadas por
  reflexión sobre los enums, con una tabla de casos explícita.
- Propiedades:
  - desde un estado terminal no sale ninguna transición;
  - `state_seq` solo crece;
  - toda transición produce exactamente un evento;
  - el estado destino del origen es coherente con la vista del destino (§6.3 de `DOMAIN-MODEL.md`).

---

## 5. Pruebas de contrato con OpenCode

- **Fixtures**: se guardan payloads reales por evento (`session.execution.started`,
  `permission.asked`, `session.text.delta`, `session.execution.failed`, …) en
  `tests/fixtures/opencode-2.3.2/` y se validan contra el JSON Schema fijado.
- **Prueba en vivo** (`Category=Integration`, opt-in con `ODW_OPENCODE_E2E=1`):
  1. arranca `opencode serve` en loopback con contraseña aleatoria,
  2. crea sesión en un directorio temporal,
  3. envía un prompt de solo lectura ("responde OK"),
  4. espera `execution.succeeded` o `failed`,
  5. verifica `/api/info` y el hash del spec.
- Si no hay binario de OpenCode, la prueba se **salta** con un mensaje claro (no se simula
  silenciosamente un éxito).

---

## 6. Pruebas de seguridad (hostiles)

Se ejecutan contra un workspace real bajo `odw-agent`; requieren elevación para crear el usuario
y las ACL, y se marcan `[Trait("Category","WindowsSecurity")]`.

| Caso | Entrada | Esperado |
|---|---|---|
| Lectura fuera del workspace | instrucción que intenta leer `%USERPROFILE%\.ssh\id_rsa` | ACL deniega; `ReviewRequired`/`Failed` |
| Path traversal | `allowed_paths: ["../../"]` | `PATH_INVALID` antes de crear nada |
| Reparse point | crear junction a `C:\Windows` | `REPARSE_DETECTED`, tarea fallida |
| Memoria | reservar 2× límite | Job Object mata; nodo estable |
| Procesos | 200 procesos | límite respetado |
| Tiempo | bucle infinito | timeout + `TerminateJobObject` |
| Red | `curl` externo con `network:none` | bloqueado por WFP; solo loopback:<proxy> permitido |
| Servicio real | nodo como `odw-svc` + `POST /peer/v1/tasks` | tarea `E2E-SVC` completada con proxy/WFP |
| Cancelación | cancelar en `Running` | árbol muerto ≤ 5 s, `Cancelled` |
| Autoaprobación | `POST /peer/v1/tasks/{id}/approve` | `404` |
| Herramienta de aprobación | buscar `odw_approve` | no existe |

## E2E loopback (un PC)

`tools/e2e-loopback.ps1` levanta `PC-A` (origen) y `PC-B` (destino) como `odw-svc`, propone por
MCP, aprueba por el pipe y comprueba el resultado. Verificado: tarea `DONE`, resumen
`E2E-LOOPBACK`; con `-WriteTask`, `E2E-WRITE`, `changed_paths=["src/e2e-changed.txt"]`, commit y
diff. No valida Tailscale/firewall; eso queda para la prueba en dos PCs.

---

## 7. Pruebas de extremo a extremo (dos nodos)

Arnés `TwoNodeHarness`:
- Dos instancias de `Odw.Node` en el mismo equipo, con directorios, BDs y puertos distintos,
  escuchando en `127.0.0.1` (modo prueba; en producción solo Tailscale).
- Un `IExecutor` falso que simula OpenCode con retrasos controlados por `FakeTimeProvider`.
- Aprobaciones conectadas a un `IApprovalChannel` de prueba (no Telegram).

Escenarios:

| Escenario | Esperado |
|---|---|
| A delega `analysis` a B y recibe resultado | `Done`, resultado íntegro |
| B delega a A en paralelo | ambas completan; sin bloqueo mutuo |
| Ciclo A→B→A | `LINEAGE_LOOP` |
| Reinicio de B en `Running` | A ve `Unreachable`/`Interrupted`; B no reejecuta |
| Cancelación durante `Running` | `Cancelled` en ambos |
| Reintento de transporte tras caída | una sola ejecución (idempotencia) |
| Cambio de instrucción tras aprobar | `APPROVAL_INVALID` |
| Tarea `code_change` sin `read_write_worktree` | `CONTRACT_INVALID` |
| Dos tareas sobre el mismo repo | workspaces distintos; sin colisión de archivos |
| Par caído | `offline`; no se elige en el planner |

---

## 8. Invariantes de arquitectura (CI)

`Odw.Architecture.Tests` inspecciona ensamblados y metadatos:

1. Ningún endpoint de `Odw.PeerApi` ni `Odw.Mcp` contiene `approve`/`approval`.
2. Ninguna herramienta MCP coincide con `odw_approve*`.
3. La constante `PermissionReply.Always` no aparece en `Odw.Executors.OpenCode`.
4. `Odw.Core` no referencia `Odw.Executors.*`, `Odw.PeerApi` ni `Odw.Mcp`.
5. `Odw.Policy` no referencia `System.Net.Http`.
6. Ningún `DateTime.Now/UtcNow` fuera de `TimeProvider` (búsqueda IL/roslyn).
7. `Odw.Sandbox` no se referencia desde `Odw.Core`.

---

## 9. Cobertura mínima por módulo

| Módulo | Líneas | Ramas |
|---|---|---|
| `Odw.Core` (estados, hash, lineage) | 90 % | 85 % |
| `Odw.Policy` | 95 % | 90 % |
| `Odw.Trust` | 95 % | 90 % |
| `Odw.Node` (persistencia/scheduler) | 80 % | 70 % |
| `Odw.Sandbox` | 70 % (medido en integration) | — |
| Resto | 60 % | — |

La cobertura es un piso, no un objetivo: una prueba sin aserción (o con aserción trivial) se
rechaza en revisión.

Medición local (2026-11-26): `dotnet test tests/Odw.Core.Tests --collect:"XPlat Code Coverage" --settings coverlet.runsettings`
-> `Odw.Core` 97,8 % líneas / 95 % ramas (1013/1036). El `coverlet.runsettings` excluye código
generado (`CompilerGenerated`/`GeneratedCode` y `obj/**`); sin exclusiones el dato incluye regex
y records generados y no es comparable.

---

## 10. Puertas de CI

```powershell
dotnet build -warnaserror
dotnet test --filter "Category!=Integration&Category!=WindowsSecurity"
dotnet test --filter "Category=Integration"      # nightly o manual
dotnet test --filter "Category=WindowsSecurity"  # nightly con elevación
dotnet test Odw.Architecture.Tests
```

- `warnaserror` obligatorio (nullable habilitado).
- Nada se fusiona con la suite roja.
- Cada hito actualiza `ROADMAP.md` con el resultado real de sus criterios de aceptación.
