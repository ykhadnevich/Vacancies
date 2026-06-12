-- Day 7.x — server-side cache of the last v6 search result per (user, query).
-- Lets the candidate-side UI display yesterday's results instantly on app open
-- without paying the full Mono pipeline cost; fresh results only run on explicit
-- "Refresh" action by the user.
--
-- Apply once:
--   psql -h localhost -U postgres -d vakansio -f manual_user_search_snapshot.sql

BEGIN;

CREATE TABLE IF NOT EXISTS "UserSearchSnapshots" (
    "Id"           uuid                        PRIMARY KEY,
    "UserId"       uuid                        NOT NULL,
    "QueryHash"    varchar(64)                 NOT NULL,
    "Keywords"     varchar(512)                NOT NULL,
    "ResponseJson" jsonb                       NOT NULL,
    "ExecutedAt"   timestamp with time zone    NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ux_user_search_snapshots_user_query"
    ON "UserSearchSnapshots" ("UserId", "QueryHash");

CREATE INDEX IF NOT EXISTS "ix_user_search_snapshots_executed"
    ON "UserSearchSnapshots" ("ExecutedAt");

COMMIT;
