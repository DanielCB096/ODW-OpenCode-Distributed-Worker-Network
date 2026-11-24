-- ODW schema 0002: solicitudes de aprobación humana (origen)
CREATE TABLE approval_requests (
  approval_id TEXT PRIMARY KEY,
  task_id     TEXT NOT NULL,
  role        TEXT NOT NULL,
  task_hash   TEXT NOT NULL,
  summary     TEXT NOT NULL,
  nonce       TEXT NOT NULL UNIQUE,
  status      TEXT NOT NULL CHECK (status IN ('pending','approved','denied','expired')),
  created_at  TEXT NOT NULL,
  expires_at  TEXT NOT NULL,
  decided_at  TEXT,
  decided_by  TEXT,
  reason      TEXT,
  used_at     TEXT
);

CREATE INDEX ix_approval_requests_pending ON approval_requests(status, expires_at);
CREATE INDEX ix_approval_requests_task ON approval_requests(task_id, role);
