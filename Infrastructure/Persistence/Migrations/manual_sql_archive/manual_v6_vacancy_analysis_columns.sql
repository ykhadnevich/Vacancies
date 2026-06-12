-- Manual migration — v6 production wiring.
--
-- This file is the PERMANENT SOURCE OF TRUTH for the partial index
-- `IX_JobVacancies_NeedsAnalysis` (declared at lines 27–29 below) — NOT a
-- temporary backup. The fluent config in
-- Infrastructure/Persistence/Configurations/JobVacancyConfiguration.cs
-- intentionally does NOT declare this index via HasIndex(...).HasFilter(...)
-- because doing so re-triggers an EF Core 8 + Npgsql.Pgvector NRE in
-- MigrationsModelDiffer when diff-ing against the Embedding column on the
-- same entity. Until the upstream bug is resolved, the partial index lives
-- in raw SQL only.
--
-- Do NOT delete this file without first adding a Fluent-API equivalent to
-- JobVacancyConfiguration.cs and verifying `dotnet ef migrations add` works.
--
-- The 3 column ADDs below were originally applied through this file because
-- the same upstream NRE blocked `dotnet ef migrations add`. The 3 columns
-- are now also reflected in AppDbContextModelSnapshot.cs (hand-synced) and
-- in the fluent config, so future `migrations add` calls see no drift for
-- those columns. The index, however, remains snapshot-invisible — it exists
-- in the DB and in this file only.
--
-- Schema additions are NULL-safe (existing rows get NULL and are picked up
-- by VacancyAnalysisWorker for backfill). `IF NOT EXISTS` makes this script
-- safe to re-run.

BEGIN;

ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "VacancyAnalysisJson" text NULL;

ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "VacancyAnalyzedAt" timestamp with time zone NULL;

ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "VacancyAnalysisModelVersion" varchar(64) NULL;

-- Partial index on the "needs analysis" queue. Postgres-specific —
-- only indexes rows where the worker still needs to act.
CREATE INDEX IF NOT EXISTS "IX_JobVacancies_NeedsAnalysis"
    ON "JobVacancies" ("VacancyAnalyzedAt")
    WHERE "VacancyAnalysisJson" IS NULL;

COMMIT;
