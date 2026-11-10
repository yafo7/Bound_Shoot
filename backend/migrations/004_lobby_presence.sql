ALTER TABLE auth_sessions ADD COLUMN last_seen INTEGER NOT NULL DEFAULT 0;
INSERT INTO schema_migrations(version) VALUES(4);
