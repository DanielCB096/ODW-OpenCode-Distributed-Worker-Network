# ODW — Planner y Policy Engine

> Dos componentes deterministas. Ninguno usa un LLM. Ambos son explicables: toda decisión
> devuelve la regla o los factores que la produjeron.
>
> **Estado (2026-11-24):** Policy Engine implementado y probado (28 pruebas). Planner sigue en
> diseño: depende del host y del monitor de recursos.

---

## 1. Policy Engine

### 1.1 Semántica

- Reglas ordenadas por `priority` ascendente (menor = antes). **Gana la primera coincidencia.**
  En empate, orden de declaración.
- Si ninguna coincide: se aplica `default` (`ASK` en la práctica si no se especifica).
- Efectos: `AUTO` (permitido), `ASK` (requiere humano), `DENY` (rechazado).
- Evaluación **pura**: mismas entradas ⇒ misma salida, sin E/S, sin reloj salvo `now` inyectado.
- La evaluación se hace **dos veces**: en el origen (antes de enviar) y en el destino (antes de
  ejecutar). Una aprobación del origen **no** sustituye la del destino.
- Cada evaluación devuelve `{effect, matched_rule_id, reason}`; el llamador lo audita.
- Las acciones de herramienta viajan en `PolicyRequest.Actions` (lo llena el broker de
  permisos); así una regla con `any_action` funciona sin acoplar el motor al ejecutor.

### 1.2 Estructura

```jsonc
{
  "version": 1,
  "default": "ASK",
  "rules": [
    {
      "id": "deny-publish",
      "priority": 10,
      "effect": "DENY",
      "when": { "any_action": ["publish", "deploy", "merge_main", "read_credentials"] },
      "reason": "Publicar, desplegar, fusionar o leer credenciales está prohibido"
    },
    {
      "id": "ask-code-change",
      "priority": 20,
      "effect": "ASK",
      "when": { "direction": "in", "kind": "code_change" },
      "reason": "Cambios de código requieren aprobación local siempre (MVP)"
    },
    {
      "id": "auto-analysis-trusted",
      "priority": 30,
      "effect": "AUTO",
      "when": {
        "direction": "in",
        "kind": ["analysis", "test_run"],
        "mode": "read_only",
        "peer_trust": "trusted",
        "limits": { "max_minutes": { "lte": 60 }, "network": "none" },
        "capabilities_all": ["git"]
      },
      "reason": "Análisis de solo lectura desde par de confianza"
    },
    {
      "id": "ask-network",
      "priority": 25,
      "effect": "ASK",
      "when": { "limits": { "network": "allow" } },
      "reason": "Acceso a red requiere aprobación"
    },
    {
      "id": "deny-unknown-peer",
      "priority": 5,
      "effect": "DENY",
      "when": { "peer_trust": ["blocked", "unknown"] },
      "reason": "Par no autorizado"
    }
  ]
}
```

### 1.3 Condiciones soportadas

| Clave | Tipo | Semántica |
|---|---|---|
| `direction` | `in｜out` | Tarea entrante o saliente |
| `kind` | string/array | `analysis｜test_run｜code_change` |
| `mode` | string/array | `read_only｜read_write_worktree` |
| `peer` | string/array | `node_id` |
| `peer_trust` | string/array | `trusted｜limited｜blocked｜unknown` |
| `repo` | string/array | nombre lógico |
| `limits.max_minutes` | `{lte,gte}` | minutos |
| `limits.max_memory_gb` | `{lte,gte}` | GiB |
| `limits.network` | `none｜allow` | red |
| `limits.max_cost_usd` | `{lte,gte}` | USD |
| `capabilities_all` | array | el destino tiene todas |
| `capabilities_none` | array | el destino no tiene ninguna |
| `time_window` | `{days:[1-7], from:"HH:mm", to:"HH:mm", tz:"..."}` | ventana local |
| `parent` | `bool` | es subtarea |
| `any_action` | array | acciones de herramienta (`publish`, `deploy`, …) |

- Un array dentro de una condición significa **OR**; claves distintas significan **AND**.
- `null`/ausente en la tarea no coincide con una condición que exige valor.
- Reglas con `when` vacío coinciden siempre (usar con cuidado; el validador lo advierte).

### 1.4 Validación del archivo

- Esquema JSON estricto (`docs/schemas/policy.schema.json`).
- `id` único, `^[a-z0-9-]{3,64}$`; `priority` 0..1000; `effect` obligatorio; `reason` obligatorio.
- Al arrancar: si `policy.json` es inválido, el servicio **no arranca** (fail-closed) y el error
  se reporta por `odw doctor`.
- Reemplazo en caliente: al cambiar el archivo se recarga; las tareas ya aprobadas conservan la
  política con la que se aprobaron (se guarda `policy_hash` en la tarea).

### 1.5 Matriz inicial (resumen de `ARCHITECTURE.md` §5.4)

| Operación | Efecto |
|---|---|
| Consultar recursos/estado | AUTO |
| Crear propuesta | AUTO (no ejecuta) |
| Enviar tarea (origen) | ASK salvo regla AUTO explícita |
| Aceptar `analysis`/`test_run` `read_only` de par trusted | ASK (configurable a AUTO) |
| Aceptar `code_change` | ASK siempre en MVP |
| `network: allow` | ASK |
| Instalar paquetes | ASK + `network: allow` |
| Acciones de herramienta `publish`/`deploy`/`merge_main`/`read_credentials` | DENY |
| Par `blocked`/desconocido | DENY |

### 1.6 Pruebas

Implementadas en `tests/Odw.Policy.Tests` (28 casos):

- prioridad gana sobre orden de declaración; empate resuelto por declaración;
- `DENY` de prioridad menor no se anula;
- `default` cuando no coincide nada;
- `direction`, `kind`/`mode` (string o array), peer, trust, repo, `parent`, `any_action`;
- límites (`lte`/`gte`), `network`, capacidades `all`/`none`;
- `time_window` con zona horaria inyectada y ventana nocturna que cruza medianoche;
- documentos inválidos: versión, ID duplicado/inválido, efecto, prioridad, razón, miembro
  desconocido, límites vacíos, `gte > lte`, `HH:mm`, días fuera de rango y zona horaria;
- carga de `config/policy.example.json` y evaluación de sus reglas reales.

Pendiente para el host: recarga en caliente y `policy_hash` por tarea. `time_window` con cambio
de horario se cubrirá con un caso DST cuando el host fije la zona del nodo.

### 1.7 Implementación

- `PolicyDocumentLoader`: JSON estricto (`UnmappedMemberHandling.Disallow`), valida todo y
  **falla cerrado** con `PolicyValidationException`; el host no debe arrancar con un documento roto.
- `PolicyEngine`: recibe `Now` en `PolicyRequest`, ordena por `(priority, declaración)` y evalúa
  sin E/S. `tz` se resuelve con un `Func<string, TimeZoneInfo>` inyectable para pruebas.
- `any_action` vacío no coincide con nada; un array vacío en `kind`/`mode`/`peer` tampoco.
- `limits` vacío es inválido (no “sin restricciones”).

---

## 2. Planner

### 2.1 Entradas

- `TaskFacts`: `kind`, `mode`, `needs` (`gpu`, `min_ram_gb`, `tools`), tamaño estimado
  (`repo_size_mb` desde la tabla `repos`), `max_minutes`.
- `Candidate`: el nodo local y cada par habilitado con `StatusSnapshot` (≤ 30 s de antigüedad).
- `History`: tasa de éxito y latencia p50 por `(peer, kind)` de los últimos 30 días (local).
- `Policy`: efecto que tendría la operación en cada candidato.

### 2.2 Puntuación

Para cada candidato viable:

```
score = w_fit·fit + w_load·(1 − load) + w_trust·trust + w_hist·success_rate
        + w_locality·locality − w_transfer·transfer_cost − w_queue·queue_penalty
```

| Factor | Cálculo | Peso |
|---|---|---|
| `fit` | 1 si cumple `needs`; 0 si no (candidato descartado) | — |
| `load` | `load_score` del candidato (0..1), suavizado con media móvil local | 0.30 |
| `trust` | 1 trusted, 0.5 limited, 0 blocked | 0.20 |
| `success_rate` | Laplace `(ok+1)/(total+2)`; 0.5 si no hay historia | 0.15 |
| `locality` | 1 local, 0.6 mismo tailnet, 0.3 otra red | 0.12 |
| `transfer_cost` | `min(1, repo_size_mb / 2048)` (clon/snapshot) | 0.13 |
| `queue_penalty` | `min(1, (active+queued)/capacity)` | 0.20 |

- La suma de pesos se normaliza; el resultado queda en `[0,1]`.
- Viable = `fit == 1` **y** política ≠ `DENY` **y** `status == online`.
- `recommendation` = candidato viable con mayor `score`; solo se sugiere delegar si
  `score_remote − score_local >= 0.15` **y** el ahorro estimado ≥ 5 minutos. Si no, `LOCAL`.
- `eta_minutes` = `(queue_penalty · 30) + (transfer_cost · 15) + baseline(kind)`, redondeado.
- `reasons` incluye siempre los tres factores de mayor contribución (explicabilidad).

### 2.3 Divisibilidad

- Una tarea es divisible **solo** si el contrato trae `split_units` explícitas (lista de unidades
  independientes con sus rutas e instrucciones) **y** la política permite `code_change`/`test_run`
  en el destino **y** no hay solapamiento de `allowed_paths` entre unidades.
  (`split_units` se agrega al contrato en H5 como campo opcional retrocompatible; en v1 su
  ausencia significa "no divisible".)
- El planner **no inventa** unidades a partir de texto libre. Si el agente propone unidades, el
  humano las ve en la aprobación.
- Máximo 4 unidades; cada una es una tarea hija con `parent_task_id` y `lineage.max_depth`
  heredado.
- Integración: el padre queda en `ReviewRequired` hasta que el humano integre las ramas. ODW
  nunca fusiona.

### 2.4 Determinismo y pruebas

- Sin aleatoriedad. Empates: se resuelve por `node_id` ascendente, luego por menor carga.
- Golden tests con 10 escenarios fijos (valores exactos de `score` con tolerancia 1e-9).
- Un candidato `offline` nunca se recomienda, aunque tenga mejor score histórico.
- Un paquete de 5 GB con enlace lento debe preferir local (prueba de regresión).

---

## 3. Cómo se combinan

```
odw_estimate_task
  ├── planner.Options(taskFacts)            → ranking + reasons
  ├── policy.Evaluate(direction=out,...)    → effect + matched_rule
  └── resultado: recommendation, options, policy_hint, divisible
odw_propose_task
  ├── contract.Validate + hash
  ├── policy.Evaluate(direction=out,...)
  └── AUTO → Approved ; ASK → WaitingApproval ; DENY → error POLICY_DENIED
```

El planner **nunca** autoriza: solo ordena. La autorización es del Policy Engine + humano.
