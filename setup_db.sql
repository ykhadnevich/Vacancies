-- Full database setup: applies all 7 migrations on a fresh DB
-- Run: docker exec -i vakansio-db psql -U postgres -d vakansio < setup_db.sql

-- pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- EF migrations history table
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId"   character varying(150) NOT NULL,
    "ProductVersion" character varying(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- ── Migration 1: InitialCreate ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "Applications" (
    "Id"              uuid                        NOT NULL,
    "UserId"          uuid                        NOT NULL,
    "JobVacancyId"    uuid,
    "Title"           character varying(300)      NOT NULL,
    "Company"         character varying(200)      NOT NULL,
    "Salary"          character varying(100),
    "Url"             character varying(500)      NOT NULL,
    "SeniorityLevel"  text                        NOT NULL,
    "Status"          text                        NOT NULL,
    "Notes"           character varying(2000),
    "AddedAt"         timestamp without time zone NOT NULL,
    "UpdatedAt"       timestamp without time zone NOT NULL,
    "IsManuallyAdded" boolean                     NOT NULL,
    "PipelineSteps"   text                        NOT NULL,
    CONSTRAINT "PK_Applications" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "JobVacancies" (
    "Id"              uuid                        NOT NULL,
    "Title"           character varying(300)      NOT NULL,
    "Company"         character varying(200)      NOT NULL,
    "Location"        character varying(200),
    "Description"     text,
    "SalaryMin"       numeric,
    "SalaryMax"       numeric,
    "SalaryCurrency"  character varying(10),
    "SalaryRaw"       character varying(100),
    "Source"          text                        NOT NULL,
    "WorkFormat"      text                        NOT NULL,
    "SeniorityLevel"  text                        NOT NULL,
    "Category"        character varying(100),
    "Urls"            text                        NOT NULL,
    "RelevanceScore"  real,
    "RelevanceStage"  integer,
    "IsDuplicate"     boolean                     NOT NULL,
    "CanonicalJobId"  uuid,
    "PublishedAt"     timestamp without time zone NOT NULL,
    "AggregatedAt"    timestamp without time zone NOT NULL,
    "IsManuallyAdded" boolean                     NOT NULL,
    CONSTRAINT "PK_JobVacancies" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "UserProfiles" (
    "Id"                  uuid                        NOT NULL,
    "Email"               character varying(256)      NOT NULL,
    "DisplayName"         character varying(100),
    "Category"            character varying(100),
    "Skills"              text                        NOT NULL,
    "ExpectedSalary"      numeric,
    "PreferredWorkFormat" text                        NOT NULL,
    "SeniorityLevel"      text                        NOT NULL,
    "PreferredLocation"   character varying(200),
    "CvFileUrl"           character varying(500),
    "CvRawText"           text,
    "CreatedAt"           timestamp without time zone NOT NULL,
    "UpdatedAt"           timestamp without time zone NOT NULL,
    CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_Applications_Status"      ON "Applications"  ("Status");
CREATE INDEX IF NOT EXISTS "IX_Applications_UserId"      ON "Applications"  ("UserId");
CREATE INDEX IF NOT EXISTS "IX_JobVacancies_Company"     ON "JobVacancies"  ("Company");
CREATE INDEX IF NOT EXISTS "IX_JobVacancies_PublishedAt" ON "JobVacancies"  ("PublishedAt");
CREATE INDEX IF NOT EXISTS "IX_JobVacancies_Source"      ON "JobVacancies"  ("Source");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserProfiles_Email" ON "UserProfiles" ("Email");

-- ── Migration 2: FixTextColumns — already correct types above ───────────────

-- ── Migration 3: AddSavedUrl ────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS "SavedUrls" (
    "Id"              uuid                        NOT NULL,
    "Url"             character varying(2000)     NOT NULL,
    "Alias"           character varying(100),
    "CreatedAt"       timestamp without time zone NOT NULL,
    "LastParsedAt"    timestamp without time zone,
    "LastParsedCount" integer                     NOT NULL,
    CONSTRAINT "PK_SavedUrls" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_SavedUrls_Url" ON "SavedUrls" ("Url");

-- ── Migration 4: UrlsToJsonb — empty no-op ──────────────────────────────────

-- ── Migration 5: AddPasswordHash ────────────────────────────────────────────
ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "PasswordHash" character varying(100) NOT NULL DEFAULT '';

-- ── Migration 6: AddMlFeatures ──────────────────────────────────────────────
ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "Embedding" vector(768);

CREATE INDEX IF NOT EXISTS idx_jobvacancies_embedding
    ON "JobVacancies" USING hnsw ("Embedding" vector_cosine_ops);

ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvEmbedding" vector(768),
    ADD COLUMN IF NOT EXISTS "CvVersionId" uuid NOT NULL DEFAULT gen_random_uuid();

CREATE TABLE IF NOT EXISTS "RelevanceExplanations" (
    "CvVersionId"  uuid         NOT NULL,
    "JobId"        uuid         NOT NULL,
    "Reason"       text         NOT NULL,
    "ModelVersion" varchar(50)  NOT NULL DEFAULT '',
    "Score"        real         NOT NULL,
    "GeneratedAt"  timestamp    NOT NULL,
    PRIMARY KEY ("CvVersionId", "JobId")
);

CREATE INDEX IF NOT EXISTS idx_re_cv_version ON "RelevanceExplanations" ("CvVersionId");
CREATE INDEX IF NOT EXISTS idx_re_job_id     ON "RelevanceExplanations" ("JobId");

-- ── Migration 7: AddCvSummary ────────────────────────────────────────────────
ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSummary" text;

-- ── Record all migrations as applied ────────────────────────────────────────
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
    ('20260314130002_InitialCreate',     '8.0.0'),
    ('20260322185641_FixTextColumns',    '8.0.0'),
    ('20260322203204_AddSavedUrl',       '8.0.0'),
    ('20260322204859_UrlsToJsonb',       '8.0.0'),
    ('20260427000000_AddPasswordHash',   '8.0.0'),
    ('20260430000000_AddMlFeatures',     '8.0.0'),
    ('20260503000000_AddCvSummary',      '8.0.0')
ON CONFLICT DO NOTHING;
