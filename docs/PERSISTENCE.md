# ODW — Persistencia (SQLite)

> Un archivo por nodo: `%PROGRAMDATA%\ODW\odw.db` (ACL: servicio lectura/escritura; usuarios solo lectura si acaso).
> Un único escritor (ADR-008). Migraciones embebidas y versionadas.

---

## 1. PRAGMAs de conexión

```sql
PRAGMA journal_mode = WAL;          -- persistente en el archivo
PRAGMA synchronous = NORMAL;        -- durabilidad suficiente con WAL; FULL solo en auditoría crítica
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;
PRAGMA wal_autocheckpoint = 1000;   -- o 4 MiB
PRAGMA temp_store = MEMORY;
PRAGMA trusted_schema = OFF;
```

- Conexión **escritora**: una sola, propiedad del actor `SqliteWriter`.
- Conexiones **lectoras**: `Mode=ReadOnly` + `PRAGMA query_only=ON`, una por lectura (ADO pooling
  las reutiliza). No compiten con el escritor gracias a WAL.
- Apagado ordenado: `PRAGMA wal_checkpoint(TRUNCATE)` antes de cerrar.
- Nombres y valores siempre parametrizados. Cero SQL concatenado.

---

## 2. Esquema (migración 0001)

```sql
CREATE TABLE schema_version (
  version     INTEGER NOT NULL,
  applied_at  TEXT    NOT NULL,
  app_version TEXT    NOT NULL
);

CREATE TABLE peers (
  node_id        TEXT PRIMARY KEY,
  enabled        INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0,1)),
  address        TEXT NOT NULL,                -- host:puerto explicitamente, sin inferencia DNS
  key_id         TEXT NOT NULL UNIQUE,
  token_hash     BLOB NOT NULL,                -- SHA-256(secret)
  trust          TEXT NOT NULL CHECK (trust IN ('trusted','limited','blocked')),
  notes          TEXT,
  added_at       TEXT NOT NULL,
  last_seen_at   TEXT,
  last_status    TEXT,                          -- JSON saneado, no confiable para decisiones
  token_not_after TEXT
);

CREATE TABLE repos (
  name             TEXT PRIMARY KEY,
  path             TEXT NOT NULL UNIQUE,        -- ruta local absoluta (nunca viaja)
  default_base_ref TEXT NOT NULL DEFAULT 'main',
  allowed          INTEGER NOT NULL DEFAULT 1 CHECK (allowed IN (0,1))
);

CREATE TABLE tasks (
  task_id          TEXT NOT NULL,
  role             TEXT NOT NULL CHECK (role IN ('origin','target')),
  counterpart_node TEXT,                        -- target si role=origin; origin si role=target
  parent_task_id   TEXT,
  kind             TEXT NOT NULL,
  mode             TEXT NOT NULL,
  state            TEXT NOT NULL,
  state_seq        INTEGER NOT NULL DEFAULT 0,
  task_hash        TEXT NOT NULL,
  request_json     TEXT NOT NULL,               -- TaskRequest tal como se recibio/creo
  origin_node      TEXT NOT NULL,
  target_node      TEXT NOT NULL,
  created_at       TEXT NOT NULL,
  updated_at       TEXT NOT NULL,
  expires_at       TEXT,
  lease_expires_at TEXT,
  cancel_requested INTEGER NOT NULL DEFAULT 0 CHECK (cancel_requested IN (0,1)),
  cancel_reason    TEXT,
  attempts         INTEGER NOT NULL DEFAULT 0,
  workspace_path   TEXT,                        -- ruta local del workspace (target)
  executor_ref     TEXT,                        -- session id de OpenCode
  result_json      TEXT,
  finished_at      TEXT,
  PRIMARY KEY (task_id, role),
  CHECK (role = 'origin' OR counterpart_node IS NOT NULL)
);

CREATE INDEX ix_tasks_state_role   ON tasks(role, state);
CREATE INDEX ix_tasks_counterpart  ON tasks(counterpart_node, state);
CREATE INDEX ix_tasks_lease        ON tasks(lease_expires_at) WHERE state = 'Running';
CREATE INDEX ix_tasks_parent       ON tasks(parent_task_id) WHERE parent_task_id IS NOT NULL;

CREATE TABLE task_events (
  task_id    TEXT NOT NULL,
  role       TEXT NOT NULL,
  seq        INTEGER NOT NULL,
  event_id   TEXT NOT NULL UNIQUE,
  type       TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at TEXT NOT NULL,
  prev_hash  TEXT,
  hash       TEXT NOT NULL,                     -- SHA-256(prev_hash || canonical(payload))
  PRIMARY KEY (task_id, role, seq),
  FOREIGN KEY (task_id, role) REFERENCES tasks(task_id, role) ON DELETE CASCADE
);

CREATE TABLE messages (
  message_id   TEXT PRIMARY KEY,
  task_id      TEXT NOT NULL,
  role         TEXT NOT NULL,
  direction    TEXT NOT NULL CHECK (direction IN ('in','out')),
  delivery     TEXT NOT NULL CHECK (delivery IN ('queue','steer')),
  body         TEXT NOT NULL,
  created_at   TEXT NOT NULL,
  delivered_at TEXT
);
CREATE INDEX ix_messages_task ON messages(task_id, role, created_at);

CREATE TABLE approvals (
  approval_id  TEXT PRIMARY KEY,
  task_id      TEXT NOT NULL,
  role         TEXT NOT NULL,
  task_hash    TEXT NOT NULL,
  decision     TEXT NOT NULL CHECK (decision IN ('approved','denied')),
  approver     TEXT NOT NULL,                   -- human:telegram:<id> | human:cli:<cuenta>
  channel      TEXT NOT NULL CHECK (channel IN ('telegram','cli_pipe')),
  nonce        TEXT NOT NULL UNIQUE,
  reason       TEXT,
  decided_at   TEXT NOT NULL,
  expires_at   TEXT NOT NULL,
  used_at      TEXT,                            -- single-use
  signature    TEXT NOT NULL
);
CREATE INDEX ix_approvals_task_hash ON approvals(task_hash);

CREATE TABLE pending_actions (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  action          TEXT NOT NULL,                -- send_task | poll_task | cancel_task | fetch_result | notify_approval
  task_id         TEXT,
  role            TEXT,
  payload_json    TEXT NOT NULL,
  status          TEXT NOT NULL CHECK (status IN ('pending','running','done','dead')),
  attempts        INTEGER NOT NULL DEFAULT 0,
  next_attempt_at TEXT NOT NULL,
  last_error      TEXT,
  created_at      TEXT NOT NULL,
  updated_at      TEXT NOT NULL
);
CREATE INDEX ix_pending_due ON pending_actions(status, next_attempt_at);

CREATE TABLE audit_log (
  seq         INTEGER PRIMARY KEY AUTOINCREMENT,
  entry_type  TEXT NOT NULL,
  subject     TEXT NOT NULL,
  actor       TEXT NOT NULL,                   -- service | human:... | peer:<node>
  payload_json TEXT NOT NULL,
  created_at  TEXT NOT NULL,
  prev_hash   TEXT NOT NULL,
  hash        TEXT NOT NULL
);

CREATE TABLE artifacts (
  task_id     TEXT NOT NULL,
  role        TEXT NOT NULL,
  name        TEXT NOT NULL,
  bytes       INTEGER NOT NULL,
  sha256      TEXT NOT NULL,
  content_type TEXT NOT NULL,
  stored_path TEXT NOT NULL,                   -- fuera de la BD; la BD solo referencia
  created_at  TEXT NOT NULL,
  expires_at  TEXT NOT NULL,
  PRIMARY KEY (task_id, role, name)
);
```

### 2.1 Notas de diseño

- **`(task_id, role)` como PK**: un nodo puede ser origen y destino de tareas distintas; nunca
  hay dos filas con el mismo `(task_id, role)`. `role` es inmutable.
- **`state_seq`** se incrementa dentro de la **misma transacción** que inserta `task_events`:
  no puede haber evento sin transición ni transición sin evento.
- **`request_json`** guarda el contrato recibido (para auditoría y recomputación del hash).
  No se usa como estado operativo.
- **Artefactos** se guardan en disco (`%PROGRAMDATA%\ODW\artifacts\<task_id>\`), no como BLOB:
  la BD se mantiene pequeña y el respaldo de la BD es rápido.
- **Sin `ON DELETE CASCADE` en `tasks`**: las tareas no se borran por accidente; se purgan con
  un job explícito y auditado.
- **Formato de valores**: `role` se guarda `origin`/`target`; `state` en su forma de contrato
  (`WAITING_APPROVAL`, `REVIEW_REQUIRED`, …); `kind` y `mode` en minúsculas (`test_run`,
  `read_write_worktree`). Facilita leer la BD sin conocer el enum de C#.
- **Append-only de eventos**: además de la auditoría, `task_events` tiene triggers que abortan
  `UPDATE`/`DELETE` (la cadena de hash se verifica con `TaskStore.VerifyEvents`).
- **`tasks.attempts`** queda reservado; el conteo de reintentos de transporte vive en
  `pending_actions.attempts`.

---

## 3. Concurrencia y transacciones

- El actor `SqliteWriter` recibe `WriteCommand` por `Channel<WriteCommand>` (bounded 1024,
  `FullMode=Wait`). Cada comando se traduce a **una** transacción `BEGIN IMMEDIATE … COMMIT`.
- La transición de estado se implementa como compare-and-set:

```sql
UPDATE tasks
   SET state = @next, state_seq = state_seq + 1, updated_at = @now
 WHERE task_id = @id AND role = @role AND state = @expected AND state_seq = @expectedSeq;
-- rows affected must be 1; si es 0 → TASK_STATE_CONFLICT
```

- El evento se inserta después del `UPDATE` y en la misma transacción, con
  `seq = @expectedSeq + 1`.
- Las lecturas nunca bloquean al escritor (WAL).
- `SqliteWriter` expone `QueueDepth`, `CommittedCount` y `FailedCount`. La latencia p95 se
  medirá cuando exista el host con métricas (H1); no se inventa aquí.

---

## 4. Migraciones

- Migraciones embebidas como recursos `Odw.Node.Persistence.Migrations.0001_init.sql`, etc.
  El nombre del recurso codifica la versión (`0001`) y el nombre (`init`); se rechazan duplicados
  y huecos.
- Al arrancar: `PRAGMA user_version` + `schema_version`. Si hay migraciones pendientes:
  1. Copia de seguridad consistente solo si ya había esquema (`BackupDatabase`, no copia de archivo).
  2. Migración dentro de una transacción, incluida la fila de `schema_version` y `user_version`.
  3. Si algo falla, revierte todo: esquema, fila y `user_version` quedan intactos (probado).
- Una migración que falla **no deja el servicio arrancado**; el error se reporta por el pipe de
  CLI (`odw doctor`).
- Prohibido editar una migración ya publicada.

---

## 5. Outbox, reintentos y recuperación

### 5.1 Outbox

Toda acción con efecto remoto se persiste en `pending_actions` **antes** de intentarse.
El despachador reclama en una transacción: selecciona hasta `batchSize` acciones `pending` con
`next_attempt_at <= now` y, por cada una, `UPDATE … SET status='running', attempts=attempts+1
WHERE id=… AND status='pending'`. El `WHERE` es el reclamo atómico: dos despachadores nunca
obtienen la misma fila.

- `attempts` cuenta intentos iniciados (el reclamo ya lo incrementa).
- Fallo: `pending` con `next_attempt_at = now + 2^(attempts-1)` acotado a 16 s;
  al llegar a 5 intentos ⇒ `dead` con `last_error` (no se pierde: se inspecciona).
- `status='running'` al arrancar = proceso caído a mitad ⇒ se devuelve a `pending`.
- `done` con `CompleteAsync`; una acción `dead` requiere intervención y queda auditada.

### 5.2 Arranque (recuperación)

Implementado en `StartupRecovery.RunAsync`, en **una sola transacción**:

```
1. Abrir BD y aplicar migraciones pendientes (DatabaseMigrator).
2. Cargar config y verificar binds (ADR-002) — fuera de esta capa.
3. target con state=RUNNING  → INTERRUPTED (state_seq+1), evento task.interrupted,
   auditoría recovery.interrupted. El Job Object huérfano muere con el servicio
   (KILL_ON_JOB_CLOSE); no se reejecuta nada.
4. origin con state IN (ACCEPTED, RUNNING) y lease_expires_at < now
   → UNREACHABLE (state_seq+1), evento task.unreachable, auditoría y encolado
   de poll_task para reconciliar con el par.
5. pending_actions con status=RUNNING → PENDING (next_attempt_at=now).
6. audit_log: service.start con los contadores (interrupted, unreachable, reset).
```

- Nunca se reejecuta nada automáticamente.
- Las tareas del origen que ya estaban `UNREACHABLE` no cambian en el arranque: siguen
  esperando `task.progress`, `task.completed` o su `expires_at`.
- Los artefactos huérfanos (tarea inexistente) se purgan a los 7 días.

---

## 6. Retención y purga

| Dato | Retención | Purga |
|---|---|---|
| `tasks` terminales | 30 días (config) | Job diario, auditado |
| `task_events` | Igual que la tarea | Cascada al purgar |
| `messages` | 30 días | Job diario |
| `artifacts` | 7 días tras entrega, 30 si `review_required` | Job diario |
| `pending_actions` `done` | 7 días | Job diario |
| `audit_log` | 1 año | Archivado firmado, nunca borrado silencioso |
| `peers` | Permanente hasta revocación | Manual |

Purga = `DELETE` + `VACUUM` **fuera** de horario; el `VACUUM` no se ejecuta con tareas activas.

---

## 7. Cadena de auditoría

- `audit_log.hash = SHA-256(prev_hash ‖ entry)`, con `prev_hash` = hash de la fila `seq-1`
  (génesis: 64 ceros).
- `entry` es una concatenación **con longitud prefijada por campo** para eliminar ambigüedades:
  `seq`, `entry_type`, `subject`, `actor`, `payload_json`, `created_at`, separados por `\n`.
  Cada campo se escribe como `<largo>:<valor>`. No es JCS: es una cadena interna, determinista
  y verificable (misma construcción para `task_events`, que además incluye `task_id`, `role`,
  `seq` y `event_id`).
- Se escribe en la **misma transacción** que el cambio auditado.
- `odw audit verify` recalcula la cadena y reporta la primera divergencia
  (`TaskStore.VerifyAudit` / `VerifyEvents`).
- Ancla diaria: se escribe `<hash>  odw-audit.db` en `%PROGRAMDATA%\ODW\anchors\YYYY-MM-DD.txt`
  con ACL de solo anexado para el servicio.
- `DELETE` o `UPDATE` en `audit_log` **y** en `task_events` están prohibidos por triggers:

```sql
CREATE TRIGGER trg_audit_no_update BEFORE UPDATE ON audit_log
BEGIN SELECT RAISE(ABORT, 'audit_log is append-only'); END;
CREATE TRIGGER trg_audit_no_delete BEFORE DELETE ON audit_log
BEGIN SELECT RAISE(ABORT, 'audit_log is append-only'); END;
CREATE TRIGGER trg_task_events_no_update BEFORE UPDATE ON task_events
BEGIN SELECT RAISE(ABORT, 'task_events is append-only'); END;
CREATE TRIGGER trg_task_events_no_delete BEFORE DELETE ON task_events
BEGIN SELECT RAISE(ABORT, 'task_events is append-only'); END;
```

---

## 8. Respaldo

- Respaldo diario con la API de backup de SQLite (consistente con WAL), comprimido, cifrado
  con DPAPI/`ProtectedData` para la clave, retenido 14 días.
- Verificación semanal: abrir la copia, `PRAGMA integrity_check`, `SELECT count(*)` de tablas clave.
- Restauración documentada y probada una vez por hito (ver `skill backup-recovery` del proyecto
  cuando exista la solución .NET).

---

## 9. Consultas de referencia

```sql
-- Tareas que requieren acción humana en este nodo
SELECT task_id, role, state, updated_at
  FROM tasks
 WHERE state IN ('WaitingApproval','AwaitingLocalApproval')
 ORDER BY updated_at;

-- Leases vencidos (candidatos a UNREACHABLE en el origen)
SELECT task_id FROM tasks
 WHERE role='origin' AND state IN ('Accepted','Running')
   AND lease_expires_at < @now;

-- Outbox vencido
SELECT id, action, attempts FROM pending_actions
 WHERE status='pending' AND next_attempt_at <= @now
 ORDER BY next_attempt_at LIMIT 20;

-- Integridad de la cadena de eventos de una tarea
SELECT seq, hash FROM task_events
 WHERE task_id=@id AND role=@role ORDER BY seq;
```

---

## 10. Pruebas de persistencia (obligatorias)

| Prueba | Esperado |
|---|---|---|
| Transición con `state_seq` desactualizado | `TASK_STATE_CONFLICT`, sin escritura | ✅ |
| Fallo dentro de una escritura | rollback completo; escritor sigue usable | ✅ |
| Reinicio con tarea `Running` | pasa a `Interrupted`; no se reejecuta | ✅ |
| Lease vencido al arrancar | `Unreachable` + `poll_task` encolado | ✅ |
| Outbox `running` tras caída | vuelve a `pending` | ✅ |
| Migración fallida | BD intacta y versión sin avanzar | ✅ |
| `UPDATE`/`DELETE` de auditoría o eventos | error del trigger | ✅ |
| 100 escrituras concurrentes | todas serializadas; sin `SQLITE_BUSY` | ✅ |
| Respaldo con WAL activo | copia íntegra verificable | ⬜ H2 |
| Purga con tarea activa | no purga la activa | ⬜ H3 |
