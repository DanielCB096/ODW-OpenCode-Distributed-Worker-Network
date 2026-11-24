-- ODW schema 0003: firma HMAC y notificación de aprobaciones
ALTER TABLE approval_requests ADD COLUMN signature TEXT;
ALTER TABLE approval_requests ADD COLUMN notified_at TEXT;
