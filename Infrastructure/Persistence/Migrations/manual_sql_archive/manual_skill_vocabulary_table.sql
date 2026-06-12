-- Manual migration — Global Skill Vocabulary (v6 optimization).
--
-- Creates the "SkillVocabulary" table that backs the deduplicated batch
-- skill expansion path. The v6 scoring handler used to pay one Gemini
-- expansion call per CV + per vacancy (~62 calls on a 100-vacancy cold run,
-- ~$0.23, ~100 s wall). With this table in place those calls collapse into
-- ONE batch call over the unknown subset (typically <50 skills on a fresh
-- corpus, trending to zero as the table fills).
--
-- We use manual SQL here for the same reason as the v6 vacancy-analysis
-- columns: `dotnet ef migrations add` crashes with NullReferenceException at
-- `ColumnBase.ProviderValueComparer` because of an EF Core 8 + Pgvector
-- interaction on the existing `Embedding` column. Until that's resolved
-- upstream, schema bumps live in manual SQL + hand-synced fluent config
-- (Infrastructure/Persistence/Configurations/SkillVocabularyConfiguration.cs).
--
-- `IF NOT EXISTS` makes this script safe to re-run.

BEGIN;

CREATE TABLE IF NOT EXISTS "SkillVocabulary" (
    "CanonicalLower"  varchar(255)             NOT NULL,
    "Canonical"       varchar(255)             NOT NULL,
    "SynonymsJson"    text                     NOT NULL,
    "Domain"          varchar(32)              NOT NULL DEFAULT 'general',
    "Confidence"      numeric(3,2)             NOT NULL DEFAULT 1.00,
    "Source"          varchar(32)              NOT NULL DEFAULT 'llm_batch',
    "ModelVersion"    varchar(64)              NULL,
    "CreatedAt"       timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_SkillVocabulary" PRIMARY KEY ("CanonicalLower")
);

-- Cohort-introspection indexes: lets us answer "how many tech vs marketing
-- entries did we add this week" without sequential scans.
CREATE INDEX IF NOT EXISTS "ix_skill_vocab_domain"
    ON "SkillVocabulary" ("Domain");

CREATE INDEX IF NOT EXISTS "ix_skill_vocab_created"
    ON "SkillVocabulary" ("CreatedAt");

COMMIT;
