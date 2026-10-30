CREATE TABLE user_cosmetics_v3(
 user_id TEXT PRIMARY KEY REFERENCES users(id),
 skin INTEGER NOT NULL DEFAULT 0 CHECK(skin BETWEEN 0 AND 7));
INSERT INTO user_cosmetics_v3(user_id,skin) SELECT user_id,skin FROM user_cosmetics;
DROP TABLE user_cosmetics;
ALTER TABLE user_cosmetics_v3 RENAME TO user_cosmetics;
INSERT INTO schema_migrations(version) VALUES(3);
