-- Day 5.16 — adds MonoResultJson to ScoringCache for engine=mono caching.
--
-- The same (CvHash, VacancyId, ScoringVersion) primary key is reused. When the
-- engine flag is "mono", ScoringVersion holds the Mono prompt version string
-- (e.g. "scoring_monolithic_v3_8_anchors") and MonoResultJson holds the full
-- serialised ScoringResult. Judge / reason columns stay NULL on Mono rows; Mono
-- columns stay NULL on Linear+Judge rows. A row can hold BOTH paths if the
-- pipeline re-runs the pair under a different engine — that is intentional and
-- the lookup code picks the field that matches the active engine.
--
-- Apply once from a Postgres client:
--   psql -h localhost -U postgres -d vakansio -f manual_mono_cache_column.sql

BEGIN;

ALTER TABLE "ScoringCache"
    ADD COLUMN IF NOT EXISTS "MonoResultJson" jsonb NULL;


-- Partial index: only Mono rows. Speeds up the hot path (Mono engine lookup
-- ignores Judge/Reason-only rows).
CREATE INDEX IF NOT EXISTS "ix_scoring_cache_mono_only"
    ON "ScoringCache" ("CvHash", "VacancyId", "ScoringVersion")
    WHERE "MonoResultJson" IS NOT NULL;

COMMIT;
