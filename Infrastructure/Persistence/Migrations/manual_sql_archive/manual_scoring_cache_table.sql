-- v6.7 — manual SQL migration for the (CV, vacancy, prompt-version) cache.
--
-- Manual SQL because `dotnet ef migrations add` currently blows up with a
-- NullReferenceException at ColumnBase.ProviderValueComparer in this solution
-- (same EF Core 8 + Pgvector interaction documented in
-- manual_skill_vocabulary_table.sql). The runtime EF model still applies via
-- ScoringCacheConfiguration; this script just creates the physical schema.
--
-- Apply from a Postgres client (psql, pgAdmin, Rider):
--   psql -h localhost -U postgres -d vakansio -f manual_scoring_cache_table.sql

BEGIN;

CREATE TABLE IF NOT EXISTS "ScoringCache" (
    "CvHash"            varchar(64)              NOT NULL,
    "VacancyId"         uuid                     NOT NULL,
    "ScoringVersion"    varchar(256)             NOT NULL,

    -- Judge payload (nullable so a Judge-only write is representable).
    "JudgeScore"        double precision         NULL,
    "JudgeVerdict"      int                      NULL,

    -- Reason payload (six bilingual sections; all-or-nothing semantics
    -- enforced by ScoringCacheEntry.WriteReason at the domain layer).
    "StrengthsEn"       text                     NULL,
    "StrengthsUk"       text                     NULL,
    "GapsEn"            text                     NULL,
    "GapsUk"            text                     NULL,
    "RecommendationEn"  text                     NULL,
    "RecommendationUk"  text                     NULL,

    "CreatedAt"         timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt"         timestamp with time zone NOT NULL DEFAULT NOW(),

    CONSTRAINT "PK_ScoringCache"
        PRIMARY KEY ("CvHash", "VacancyId", "ScoringVersion")
);

-- Secondary indexes:
--   * vacancy_version lets a future cleanup job enumerate cache rows when a
--     vacancy is permanently deleted from the JobVacancy table.
--   * created_at supports a future TTL job that drops rows older than the
--     operator's retention window.
CREATE INDEX IF NOT EXISTS "ix_scoring_cache_vacancy_version"
    ON "ScoringCache" ("VacancyId", "ScoringVersion");

CREATE INDEX IF NOT EXISTS "ix_scoring_cache_created"
    ON "ScoringCache" ("CreatedAt");

COMMIT;
