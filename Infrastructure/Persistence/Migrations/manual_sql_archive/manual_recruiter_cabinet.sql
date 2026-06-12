-- Day 6 — Recruiter cabinet schema.
--
-- Adds:
--   * UserProfiles.Role           : int (0=Candidate, 1=Recruiter, 2=Both) default 0
--   * JobVacancies.OwnerUserId    : uuid nullable, filtered index
--   * CandidateLists              : recruiter-owned named pools
--   * RecruiterCandidates         : CVs uploaded by recruiters
--   * CandidateListMemberships    : M:N list ↔ candidate
--   * CandidateScores             : per (vacancy × candidate) Mono scoring result
--
-- Mirrors the manual_mono_cache_column.sql pattern: idempotent (IF NOT EXISTS) +
-- BEGIN/COMMIT. Apply once from a Postgres client:
--   psql -h localhost -U postgres -d vakansio -f manual_recruiter_cabinet.sql

BEGIN;

-- 1. UserProfiles.Role -------------------------------------------------------

ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "Role" int NOT NULL DEFAULT 0;


-- 2. JobVacancies.OwnerUserId ------------------------------------------------

ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "OwnerUserId" uuid NULL;

-- Filtered index: only recruiter-posted vacancies show up here, so the index stays small.
CREATE INDEX IF NOT EXISTS "ix_job_vacancies_owner"
    ON "JobVacancies" ("OwnerUserId")
    WHERE "OwnerUserId" IS NOT NULL;


-- 3. CandidateLists ----------------------------------------------------------

CREATE TABLE IF NOT EXISTS "CandidateLists" (
    "Id"              uuid                        PRIMARY KEY,
    "RecruiterUserId" uuid                        NOT NULL,
    "Name"            varchar(200)                NOT NULL,
    "Description"     text                        NULL,
    "CreatedAt"       timestamp with time zone    NOT NULL,
    "UpdatedAt"       timestamp with time zone    NOT NULL
);

CREATE INDEX IF NOT EXISTS "ix_candidate_lists_recruiter_created"
    ON "CandidateLists" ("RecruiterUserId", "CreatedAt");


-- 4. RecruiterCandidates -----------------------------------------------------

CREATE TABLE IF NOT EXISTS "RecruiterCandidates" (
    "Id"                          uuid                        PRIMARY KEY,
    "RecruiterUserId"             uuid                        NOT NULL,
    "CandidateName"               varchar(200)                NULL,
    "CvRawText"                   text                        NOT NULL,
    "CvNormalizedJson"            jsonb                       NULL,
    "CvHash"                      varchar(64)                 NULL,
    "NormalizationModelVersion"   varchar(64)                 NULL,
    "Status"                      int                         NOT NULL,
    "LastError"                   varchar(500)                NULL,
    "AddedAt"                     timestamp with time zone    NOT NULL,
    "UpdatedAt"                   timestamp with time zone    NOT NULL
);

CREATE INDEX IF NOT EXISTS "ix_recruiter_candidates_recruiter_added"
    ON "RecruiterCandidates" ("RecruiterUserId", "AddedAt");

CREATE INDEX IF NOT EXISTS "ix_recruiter_candidates_recruiter_hash"
    ON "RecruiterCandidates" ("RecruiterUserId", "CvHash");


-- 5. CandidateListMemberships (M:N) -----------------------------------------

CREATE TABLE IF NOT EXISTS "CandidateListMemberships" (
    "CandidateListId"      uuid                        NOT NULL,
    "RecruiterCandidateId" uuid                        NOT NULL,
    "AddedAt"              timestamp with time zone    NOT NULL,
    PRIMARY KEY ("CandidateListId", "RecruiterCandidateId")
);

CREATE INDEX IF NOT EXISTS "ix_candidate_list_memberships_candidate"
    ON "CandidateListMemberships" ("RecruiterCandidateId");


-- 6. CandidateScores --------------------------------------------------------

CREATE TABLE IF NOT EXISTS "CandidateScores" (
    "Id"                   uuid                        PRIMARY KEY,
    "VacancyId"            uuid                        NOT NULL,
    "RecruiterCandidateId" uuid                        NOT NULL,
    "Score"                double precision            NOT NULL,
    "ScoringVersion"       varchar(256)                NOT NULL,
    "ScoringResultJson"    jsonb                       NOT NULL,
    "ScoredAt"             timestamp with time zone    NOT NULL
);

-- Re-analyse upserts on this natural key.
CREATE UNIQUE INDEX IF NOT EXISTS "ux_candidate_scores_vacancy_candidate"
    ON "CandidateScores" ("VacancyId", "RecruiterCandidateId");

-- Ranking query: ORDER BY score DESC for a vacancy.
CREATE INDEX IF NOT EXISTS "ix_candidate_scores_vacancy_score"
    ON "CandidateScores" ("VacancyId", "Score");

COMMIT;
