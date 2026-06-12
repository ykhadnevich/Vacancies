-- ────────────────────────────────────────────────────────────────────────
-- v2 of the tracker analysis snapshot — adds the per-section bilingual reason
-- fields, the full sub-score breakdown, and the CV file name used for the
-- analysis. Builds on top of manual_tracker_analysis_columns.sql.
--
-- Idempotent — safe to run twice. Postgres-specific (jsonb).
-- ────────────────────────────────────────────────────────────────────────

ALTER TABLE "Applications"
    ADD COLUMN IF NOT EXISTS "StrengthsEn"      varchar(2000) NULL,
    ADD COLUMN IF NOT EXISTS "StrengthsUk"      varchar(2000) NULL,
    ADD COLUMN IF NOT EXISTS "GapsEn"           varchar(2000) NULL,
    ADD COLUMN IF NOT EXISTS "GapsUk"           varchar(2000) NULL,
    ADD COLUMN IF NOT EXISTS "RecommendationEn" varchar(2000) NULL,
    ADD COLUMN IF NOT EXISTS "RecommendationUk" varchar(2000) NULL,
    ADD COLUMN IF NOT EXISTS "SubScores"        jsonb         NULL,
    ADD COLUMN IF NOT EXISTS "CvFileName"       varchar(255)  NULL;
