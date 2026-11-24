# ODW — Diseño detallado

Este directorio contiene el diseño implementable del **OpenCode Distributed Worker Network**.
`../ARCHITECTURE.md` es el documento original (propósito, hitos, visión). Aquí se baja a
contratos verificables contra la versión real de OpenCode.

**Estado:** diseño cerrado (D0), OpenCode verificado casi por completo (H0 🟡, solo H0-6).
H1 funcional de punta a punta y arrancable con `odw serve` + `node.json`: delegación A→B,
scheduler con Job Objects, resultado y aprobaciones por named pipe/CLI (279 pruebas).
Falta instalarlo como servicio Windows y Telegram/HMAC.
Ver `IMPLEMENTATION.md`.

---

## Cómo leer

| Si vas a… | Lee |
|---|---|
| Entender qué se verificó y qué cambió respecto del original | `H0-FINDINGS.md` |
| Conocer las decisiones vinculantes | `DECISIONS.md` |
| Implementar contratos, estados o el hash | `DOMAIN-MODEL.md` |
| Implementar la API entre nodos | `PROTOCOL.md` |
| Implementar las herramientas del agente | `MCP-FACADE.md` |
| Implementar persistencia y recuperación | `PERSISTENCE.md` |
| Implementar sandbox, ACL, Job Objects, aprobaciones | `SECURITY.md` |
| Implementar el adaptador de OpenCode | `OPENCODE-ADAPTER.md` |
| Implementar planner y políticas | `PLANNER-POLICY.md` |
| Escribir pruebas | `TESTING.md` |
| Saber qué sigue | `ROADMAP.md` |
| Ver qué está implementado y cómo compilar | `IMPLEMENTATION.md` |

Referencia cruda: `reference/opencode-2.3.2-openapi.json` (OpenAPI 3.1, 143 rutas,
SHA-256 `48f6cb36f2cdf9d6fbc9b0a9f1a879d398fb7d5d4fe20718f85faaaf22099973`).

---

## Reglas que no se negocian

1. `opencode serve` **nunca** se expone fuera de `127.0.0.1` (salvo el experimento H0, ya cerrado).
2. **No existe** aprobación por HTTP ni por herramienta MCP. Canales: Telegram y pipe de CLI.
3. El agente no puede aprobar sus propias delegaciones.
4. La aprobación está vinculada a `task_hash`; cualquier cambio la invalida.
5. Ninguna ruta absoluta remota entra en el contrato: solo nombres lógicos de repo.
6. Toda transición de estado y toda aprobación van al audit log encadenado.
7. Un estado `Running` nunca se reejecuta automáticamente tras un reinicio.
8. `Unreachable` no significa fallo.

---

## Mapa de componentes

```
Odw.Node (servicio Windows, .NET 10)
├── PeerApi        :7421  TLS/tailnet   Bearer por par
├── Mcp            :7422  127.0.0.1     Bearer de fachada
├── TaskManager    máquina de estados por rol, leases, outbox
├── ResourceMonitor CPU/RAM/GPU/herramientas
├── PolicyEngine   reglas ordenadas, primera coincidencia
├── Planner        puntuación determinista
├── Trust          tokens por par, nonces, firmas de aprobación
├── Approvals      Telegram long-poll + named pipe
├── Sandbox        Job Objects, ACLs, usuario odw-agent
└── Persistence    SQLite (un escritor, WAL, auditoría encadenada)

Odw.Executors.OpenCode → opencode serve (una instancia por tarea, 127.0.0.1)
```

---

## Configuración

- `../config/node.example.json` — nodo, listeners, OpenCode, rutas.
- `../config/peers.example.json` — pares y tokens (solo hashes en la BD).
- `../config/policy.example.json` — reglas de ejemplo (deny-first).
- `schemas/` — JSON Schemas de `TaskRequest`, `TaskResult` y `policy.json`.
