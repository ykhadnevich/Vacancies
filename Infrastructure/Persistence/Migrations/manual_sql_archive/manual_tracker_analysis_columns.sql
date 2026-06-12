-- ────────────────────────────────────────────────────────────────────────
-- Manual migration: extend ApplicationTrackers with the v6 analysis snapshot.
--
-- Adds 8 nullable columns that capture *why* a vacancy was added to the
-- tracker (composite score, verdict, evidence, reason text, pipeline version).
-- All fields stay null for manually-added entries.
--
-- Idempotent — safe to run twice. Postgres-specific (jsonb).
-- ────────────────────────────────────────────────────────────────────────

ALTER TABLE "Applications"
    ADD COLUMN IF NOT EXISTS "Score"              double precision NULL,
    ADD COLUMN IF NOT EXISTS "Verdict"            varchar(20)      NULL,
    ADD COLUMN IF NOT EXISTS "MatchedSkills"      jsonb            NULL,
    ADD COLUMN IF NOT EXISTS "MissingMustHaves"   jsonb            NULL,
    ADD COLUMN IF NOT EXISTS "TriggeredAntiFlags" jsonb            NULL,
    ADD COLUMN IF NOT EXISTS "ReasonShort"        text             NULL,
    ADD COLUMN IF NOT EXISTS "PipelineVersion"    varchar(100)     NULL,
    ADD COLUMN IF NOT EXISTS "AnalyzedAt"         timestamptz      NULL;
