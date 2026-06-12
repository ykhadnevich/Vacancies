-- Fix for: EF MigrateAsync reports "database is already up to date" even though
-- only 4 of the 10 migrations are recorded in __EFMigrationsHistory.
--
-- Root cause: migrations 20260427-20260528 were authored without their
-- companion `.Designer.cs` files, so the EF migrator's discovery scan
-- doesn't surface them when MigrateAsync runs at startup.
--
-- This script applies the Up() SQL for each of the missing 6 migrations
-- (all idempotent — every column / table / index uses IF NOT EXISTS) and
-- inserts a matching __EFMigrationsHistory row so future runs of
-- MigrateAsync see them as applied.

BEGIN;

-- ---------------------------------------------------------------------------
-- 20260427000000_AddPasswordHash
-- ---------------------------------------------------------------------------
ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "PasswordHash" varchar(100) NOT NULL DEFAULT '';

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260427000000_AddPasswordHash', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 20260430000000_AddMlFeatures
-- (pgvector extension already created manually earlier in this session)
-- ---------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS vector;

ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "Embedding" vector(768);

CREATE INDEX IF NOT EXISTS idx_jobvacancies_embedding
    ON "JobVacancies"
    USING hnsw ("Embedding" vector_cosine_ops);

ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvEmbedding" vector(768),
    ADD COLUMN IF NOT EXISTS "CvVersionId" uuid NOT NULL DEFAULT gen_random_uuid();

CREATE TABLE IF NOT EXISTS "RelevanceExplanations" (
    "CvVersionId"   uuid         NOT NULL,
    "JobId"         uuid         NOT NULL,
    "Reason"        text         NOT NULL,
    "ModelVersion"  varchar(50)  NOT NULL DEFAULT '',
    "Score"         real         NOT NULL,
    "GeneratedAt"   timestamp    NOT NULL,
    PRIMARY KEY ("CvVersionId", "JobId")
);
CREATE INDEX IF NOT EXISTS idx_re_cv_version ON "RelevanceExplanations" ("CvVersionId");
CREATE INDEX IF NOT EXISTS idx_re_job_id     ON "RelevanceExplanations" ("JobId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260430000000_AddMlFeatures', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 20260503000000_AddCvSummary
-- ---------------------------------------------------------------------------
ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSummary" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260503000000_AddCvSummary', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 20260516000000_AddCompanySignals
-- ---------------------------------------------------------------------------
ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "ApplicantCount" integer,
    ADD COLUMN IF NOT EXISTS "RecruiterRespondsQuickly" boolean;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260516000000_AddCompanySignals', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 20260521000000_AddCvSummaryModelVersion
-- ---------------------------------------------------------------------------
ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSummaryModelVersion" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260521000000_AddCvSummaryModelVersion', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

-- ---------------------------------------------------------------------------
-- 20260528160000_AddSkillExpansionColumns
-- ---------------------------------------------------------------------------
ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSkillsExpanded" text,
    ADD COLUMN IF NOT EXISTS "CvSkillsExpansionVersion" text;
ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "VacancyMustHavesExpanded" text,
    ADD COLUMN IF NOT EXISTS "VacancyExpansionVersion" text;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260528160000_AddSkillExpansionColumns', '8.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;

-- Verify final state
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
