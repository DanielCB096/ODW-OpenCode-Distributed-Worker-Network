-- ODW schema 0001
-- Copia implementable de docs/PERSISTENCE.md §2.
-- Los estados y roles se guardan en su forma de contrato (UPPER_SNAKE / lowercase).

CREATE TABLE schema_version (
  version     INTEGER NOT NULL,
  applied_at  TEXT    NOT NULL,
  app_version TEXT    NOT NULL
);

CREATE TABLE peers (
  node_id         TEXT PRIMARY KEY,
  enabled         INTEGER NOT NULL DEFAULT 1 CHECK (enabled IN (0,1)),
  address         TEXT NOT NULL,
  key_id          TEXT NOT NULL UNIQUE,
  token_hash      BLOB NOT NULL,
  trust           TEXT NOT NULL CHECK (trust IN ('trusted','limited','blocked')),
  notes           TEXT,
  added_at        TEXT NOT NULL,
  last_seen_at    TEXT,
  last_status     TEXT,
  token_not_after TEXT
);

CREATE TABLE repos (
  name             TEXT PRIMARY KEY,
  path             TEXT NOT NULL UNIQUE,
  default_base_ref TEXT NOT NULL DEFAULT 'main',
  allowed          INTEGER NOT NULL DEFAULT 1 CHECK (allowed IN (0,1))
);

CREATE TABLE tasks (
  task_id          TEXT NOT NULL,
  role             TEXT NOT NULL CHECK (role IN ('origin','target')),
  counterpart_node TEXT,
  parent_task_id   TEXT,
  kind             TEXT NOT NULL CHECK (kind IN ('analysis','test_run','code_change')),
  mode             TEXT NOT NULL CHECK (mode IN ('read_only','read_write_worktree')),
  state            TEXT NOT NULL,
  state_seq        INTEGER NOT NULL DEFAULT 0 CHECK (state_seq >= 0),
  task_hash        TEXT NOT NULL,
  request_json     TEXT NOT NULL,
  origin_node      TEXT NOT NULL,
  target_node      TEXT NOT NULL,
  created_at       TEXT NOT NULL,
  updated_at       TEXT NOT NULL,
  expires_at       TEXT,
  lease_expires_at TEXT,
  cancel_requested INTEGER NOT NULL DEFAULT 0 CHECK (cancel_requested IN (0,1)),
  cancel_reason    TEXT,
  attempts         INTEGER NOT NULL DEFAULT 0 CHECK (attempts >= 0),
  workspace_path   TEXT,
  executor_ref     TEXT,
  result_json      TEXT,
  finished_at      TEXT,
  PRIMARY KEY (task_id, role),
  CHECK (role = 'origin' OR counterpart_node IS NOT NULL)
);

CREATE INDEX ix_tasks_state_role  ON tasks(role, state);
CREATE INDEX ix_tasks_counterpart ON tasks(counterpart_node, state);
CREATE INDEX ix_tasks_lease       ON tasks(lease_expires_at) WHERE state = 'RUNNING';
CREATE INDEX ix_tasks_parent      ON tasks(parent_task_id) WHERE parent_task_id IS NOT NULL;

CREATE TABLE task_events (
  task_id      TEXT NOT NULL,
  role         TEXT NOT NULL,
  seq          INTEGER NOT NULL CHECK (seq >= 0),
  event_id     TEXT NOT NULL UNIQUE,
  type         TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at   TEXT NOT NULL,
  prev_hash    TEXT,
  hash         TEXT NOT NULL,
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
  approval_id TEXT PRIMARY KEY,
  task_id     TEXT NOT NULL,
  role        TEXT NOT NULL,
  task_hash   TEXT NOT NULL,
  decision    TEXT NOT NULL CHECK (decision IN ('approved','denied')),
  approver    TEXT NOT NULL,
  channel     TEXT NOT NULL CHECK (channel IN ('telegram','cli_pipe')),
  nonce       TEXT NOT NULL UNIQUE,
  reason      TEXT,
  decided_at  TEXT NOT NULL,
  expires_at  TEXT NOT NULL,
  used_at     TEXT,
  signature   TEXT NOT NULL
);

CREATE INDEX ix_approvals_task_hash ON approvals(task_hash);

CREATE TABLE pending_actions (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  action          TEXT NOT NULL,
  task_id         TEXT,
  role            TEXT,
  payload_json    TEXT NOT NULL,
  status          TEXT NOT NULL CHECK (status IN ('pending','running','done','dead')),
  attempts        INTEGER NOT NULL DEFAULT 0 CHECK (attempts >= 0),
  next_attempt_at TEXT NOT NULL,
  last_error      TEXT,
  created_at      TEXT NOT NULL,
  updated_at      TEXT NOT NULL
);

CREATE INDEX ix_pending_due ON pending_actions(status, next_attempt_at);

CREATE TABLE audit_log (
  seq          INTEGER PRIMARY KEY AUTOINCREMENT,
  entry_type   TEXT NOT NULL,
  subject      TEXT NOT NULL,
  actor        TEXT NOT NULL,
  payload_json TEXT NOT NULL,
  created_at   TEXT NOT NULL,
  prev_hash    TEXT NOT NULL,
  hash         TEXT NOT NULL
);

CREATE TABLE artifacts (
  task_id      TEXT NOT NULL,
  role         TEXT NOT NULL,
  name         TEXT NOT NULL,
  bytes        INTEGER NOT NULL CHECK (bytes >= 0),
  sha256       TEXT NOT NULL,
  content_type TEXT NOT NULL,
  stored_path  TEXT NOT NULL,
  created_at   TEXT NOT NULL,
  expires_at   TEXT NOT NULL,
  PRIMARY KEY (task_id, role, name)
);

CREATE TRIGGER trg_audit_no_update BEFORE UPDATE ON audit_log
BEGIN
  SELECT RAISE(ABORT, 'audit_log is append-only');
END;

CREATE TRIGGER trg_audit_no_delete BEFORE DELETE ON audit_log
BEGIN
  SELECT RAISE(ABORT, 'audit_log is append-only');
END;

CREATE TRIGGER trg_task_events_no_update BEFORE UPDATE ON task_events
BEGIN
  SELECT RAISE(ABORT, 'task_events is append-only');
END;

CREATE TRIGGER trg_task_events_no_delete BEFORE DELETE ON task_events
BEGIN
  SELECT RAISE(ABORT, 'task_events is append-only');
END;
