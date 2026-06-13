CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE TABLE "Applications" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "JobVacancyId" uuid,
        "Title" character varying(300) NOT NULL,
        "Company" character varying(200) NOT NULL,
        "Salary" character varying(100),
        "Url" character varying(500) NOT NULL,
        "SeniorityLevel" text NOT NULL,
        "Status" text NOT NULL,
        "Notes" character varying(2000),
        "AddedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "IsManuallyAdded" boolean NOT NULL,
        "PipelineSteps" text NOT NULL,
        CONSTRAINT "PK_Applications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE TABLE "JobVacancies" (
        "Id" uuid NOT NULL,
        "Title" character varying(300) NOT NULL,
        "Company" character varying(200) NOT NULL,
        "Location" character varying(200),
        "Description" text,
        "SalaryMin" numeric,
        "SalaryMax" numeric,
        "SalaryCurrency" character varying(10),
        "SalaryRaw" character varying(100),
        "Source" text NOT NULL,
        "WorkFormat" text NOT NULL,
        "SeniorityLevel" text NOT NULL,
        "Category" character varying(100),
        "Urls" character varying(2000) NOT NULL,
        "RelevanceScore" real,
        "RelevanceStage" integer,
        "IsDuplicate" boolean NOT NULL,
        "CanonicalJobId" uuid,
        "PublishedAt" timestamp with time zone NOT NULL,
        "AggregatedAt" timestamp with time zone NOT NULL,
        "IsManuallyAdded" boolean NOT NULL,
        CONSTRAINT "PK_JobVacancies" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE TABLE "UserProfiles" (
        "Id" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "DisplayName" character varying(100),
        "Category" character varying(100),
        "Skills" text NOT NULL,
        "ExpectedSalary" numeric,
        "PreferredWorkFormat" text NOT NULL,
        "SeniorityLevel" text NOT NULL,
        "PreferredLocation" character varying(200),
        "CvFileUrl" character varying(500),
        "CvRawText" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserProfiles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE INDEX "IX_Applications_Status" ON "Applications" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE INDEX "IX_Applications_UserId" ON "Applications" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE INDEX "IX_JobVacancies_Company" ON "JobVacancies" ("Company");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE INDEX "IX_JobVacancies_PublishedAt" ON "JobVacancies" ("PublishedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE INDEX "IX_JobVacancies_Source" ON "JobVacancies" ("Source");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_UserProfiles_Email" ON "UserProfiles" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314130002_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260314130002_InitialCreate', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "UserProfiles" ALTER COLUMN "UpdatedAt" TYPE timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "UserProfiles" ALTER COLUMN "CreatedAt" TYPE timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "JobVacancies" ALTER COLUMN "Urls" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "JobVacancies" ALTER COLUMN "PublishedAt" TYPE timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "JobVacancies" ALTER COLUMN "AggregatedAt" TYPE timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "Applications" ALTER COLUMN "UpdatedAt" TYPE timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    ALTER TABLE "Applications" ALTER COLUMN "AddedAt" TYPE timestamp without time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322185641_FixTextColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260322185641_FixTextColumns', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322203204_AddSavedUrl') THEN
    CREATE TABLE "SavedUrls" (
        "Id" uuid NOT NULL,
        "Url" character varying(2000) NOT NULL,
        "Alias" character varying(100),
        "CreatedAt" timestamp without time zone NOT NULL,
        "LastParsedAt" timestamp without time zone,
        "LastParsedCount" integer NOT NULL,
        CONSTRAINT "PK_SavedUrls" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322203204_AddSavedUrl') THEN
    CREATE UNIQUE INDEX "IX_SavedUrls_Url" ON "SavedUrls" ("Url");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322203204_AddSavedUrl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260322203204_AddSavedUrl', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322204859_UrlsToJsonb') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260322204859_UrlsToJsonb', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427000000_AddPasswordHash') THEN
    ALTER TABLE "UserProfiles" ADD "PasswordHash" character varying(100) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260427000000_AddPasswordHash') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260427000000_AddPasswordHash', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMlFeatures') THEN
    CREATE EXTENSION IF NOT EXISTS vector;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMlFeatures') THEN
    ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "Embedding" vector(768);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMlFeatures') THEN
    CREATE INDEX IF NOT EXISTS idx_jobvacancies_embedding
    ON "JobVacancies"
    USING hnsw ("Embedding" vector_cosine_ops);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMlFeatures') THEN
    ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvEmbedding" vector(768),
    ADD COLUMN IF NOT EXISTS "CvVersionId" uuid NOT NULL DEFAULT gen_random_uuid();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMlFeatures') THEN
    CREATE TABLE IF NOT EXISTS "RelevanceExplanations" (
        "CvVersionId"   uuid         NOT NULL,
        "JobId"         uuid         NOT NULL,
        "Reason"        text         NOT NULL,
        "ModelVersion"  varchar(50)  NOT NULL DEFAULT '',
        "Score"         real         NOT NULL,
        "GeneratedAt"   timestamp    NOT NULL,
        PRIMARY KEY ("CvVersionId", "JobId")
    );

    CREATE INDEX IF NOT EXISTS idx_re_cv_version
        ON "RelevanceExplanations" ("CvVersionId");

    CREATE INDEX IF NOT EXISTS idx_re_job_id
        ON "RelevanceExplanations" ("JobId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMlFeatures') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260430000000_AddMlFeatures', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddCvSummary') THEN
    ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSummary" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddCvSummary') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260503000000_AddCvSummary', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516000000_AddCompanySignals') THEN
    ALTER TABLE "JobVacancies" ADD "ApplicantCount" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516000000_AddCompanySignals') THEN
    ALTER TABLE "JobVacancies" ADD "RecruiterRespondsQuickly" boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260516000000_AddCompanySignals') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260516000000_AddCompanySignals', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddCvSummaryModelVersion') THEN
    ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSummaryModelVersion" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddCvSummaryModelVersion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260521000000_AddCvSummaryModelVersion', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528160000_AddSkillExpansionColumns') THEN
    ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvSkillsExpanded" text,
    ADD COLUMN IF NOT EXISTS "CvSkillsExpansionVersion" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528160000_AddSkillExpansionColumns') THEN
    ALTER TABLE "JobVacancies"
    ADD COLUMN IF NOT EXISTS "VacancyMustHavesExpanded" text,
    ADD COLUMN IF NOT EXISTS "VacancyExpansionVersion" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528160000_AddSkillExpansionColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260528160000_AddSkillExpansionColumns', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddVacancyAnalysisColumns') THEN
    ALTER TABLE "JobVacancies"
        ADD COLUMN IF NOT EXISTS "VacancyAnalysisJson" text NULL,
        ADD COLUMN IF NOT EXISTS "VacancyAnalyzedAt" timestamp with time zone NULL,
        ADD COLUMN IF NOT EXISTS "VacancyAnalysisModelVersion" varchar(64) NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddVacancyAnalysisColumns') THEN
    CREATE INDEX IF NOT EXISTS "IX_JobVacancies_NeedsAnalysis"
        ON "JobVacancies" ("VacancyAnalyzedAt")
        WHERE "VacancyAnalysisJson" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddVacancyAnalysisColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601000000_AddVacancyAnalysisColumns', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601010000_AddSkillVocabularyTable') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601010000_AddSkillVocabularyTable') THEN
    CREATE INDEX IF NOT EXISTS "ix_skill_vocab_domain"
        ON "SkillVocabulary" ("Domain");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601010000_AddSkillVocabularyTable') THEN
    CREATE INDEX IF NOT EXISTS "ix_skill_vocab_created"
        ON "SkillVocabulary" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601010000_AddSkillVocabularyTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601010000_AddSkillVocabularyTable', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601020000_AddGeminiCostLogTable') THEN
    CREATE TABLE IF NOT EXISTS "GeminiCostLog" (
        "Id"           uuid             NOT NULL PRIMARY KEY,
        "Timestamp"    timestamp        NOT NULL,
        "UserId"       uuid             NULL,
        "RequestId"    uuid             NOT NULL,
        "RequestKind"  varchar(64)      NOT NULL,
        "Stage"        varchar(64)      NOT NULL,
        "Calls"        integer          NOT NULL,
        "DurationMs"   double precision NOT NULL,
        "InputTokens"  bigint           NOT NULL,
        "OutputTokens" bigint           NOT NULL,
        "CostUsd"      double precision NOT NULL,
        "Keywords"     varchar(256)     NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601020000_AddGeminiCostLogTable') THEN
    CREATE INDEX IF NOT EXISTS "IX_GeminiCostLog_Timestamp" ON "GeminiCostLog" ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601020000_AddGeminiCostLogTable') THEN
    CREATE INDEX IF NOT EXISTS "IX_GeminiCostLog_RequestId" ON "GeminiCostLog" ("RequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601020000_AddGeminiCostLogTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601020000_AddGeminiCostLogTable', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601030000_AddScoringCacheTable') THEN
    CREATE TABLE IF NOT EXISTS "ScoringCache" (
        "CvHash"            varchar(64)              NOT NULL,
        "VacancyId"         uuid                     NOT NULL,
        "ScoringVersion"    varchar(256)             NOT NULL,
        "JudgeScore"        double precision         NULL,
        "JudgeVerdict"      int                      NULL,
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601030000_AddScoringCacheTable') THEN
    CREATE INDEX IF NOT EXISTS "ix_scoring_cache_vacancy_version"
        ON "ScoringCache" ("VacancyId", "ScoringVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601030000_AddScoringCacheTable') THEN
    CREATE INDEX IF NOT EXISTS "ix_scoring_cache_created"
        ON "ScoringCache" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601030000_AddScoringCacheTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601030000_AddScoringCacheTable', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601040000_AddMonoCacheColumn') THEN
    ALTER TABLE "ScoringCache"
        ADD COLUMN IF NOT EXISTS "MonoResultJson" jsonb NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601040000_AddMonoCacheColumn') THEN
    CREATE INDEX IF NOT EXISTS "ix_scoring_cache_mono_only"
        ON "ScoringCache" ("CvHash", "VacancyId", "ScoringVersion")
        WHERE "MonoResultJson" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601040000_AddMonoCacheColumn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601040000_AddMonoCacheColumn', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601050000_AddTrackerLocationColumn') THEN
    ALTER TABLE "Applications"
        ADD COLUMN IF NOT EXISTS "Location" varchar(200) NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601050000_AddTrackerLocationColumn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601050000_AddTrackerLocationColumn', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601060000_AddTrackerAnalysisColumns') THEN
    ALTER TABLE "Applications"
        ADD COLUMN IF NOT EXISTS "Score"              double precision NULL,
        ADD COLUMN IF NOT EXISTS "Verdict"            varchar(20)      NULL,
        ADD COLUMN IF NOT EXISTS "MatchedSkills"      jsonb            NULL,
        ADD COLUMN IF NOT EXISTS "MissingMustHaves"   jsonb            NULL,
        ADD COLUMN IF NOT EXISTS "TriggeredAntiFlags" jsonb            NULL,
        ADD COLUMN IF NOT EXISTS "ReasonShort"        text             NULL,
        ADD COLUMN IF NOT EXISTS "PipelineVersion"    varchar(100)     NULL,
        ADD COLUMN IF NOT EXISTS "AnalyzedAt"         timestamptz      NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601060000_AddTrackerAnalysisColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601060000_AddTrackerAnalysisColumns', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601070000_AddTrackerAnalysisV2Columns') THEN
    ALTER TABLE "Applications"
        ADD COLUMN IF NOT EXISTS "StrengthsEn"      varchar(2000) NULL,
        ADD COLUMN IF NOT EXISTS "StrengthsUk"      varchar(2000) NULL,
        ADD COLUMN IF NOT EXISTS "GapsEn"           varchar(2000) NULL,
        ADD COLUMN IF NOT EXISTS "GapsUk"           varchar(2000) NULL,
        ADD COLUMN IF NOT EXISTS "RecommendationEn" varchar(2000) NULL,
        ADD COLUMN IF NOT EXISTS "RecommendationUk" varchar(2000) NULL,
        ADD COLUMN IF NOT EXISTS "SubScores"        jsonb         NULL,
        ADD COLUMN IF NOT EXISTS "CvFileName"       varchar(255)  NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601070000_AddTrackerAnalysisV2Columns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601070000_AddTrackerAnalysisV2Columns', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    ALTER TABLE "UserProfiles"
        ADD COLUMN IF NOT EXISTS "Role" int NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    ALTER TABLE "JobVacancies"
        ADD COLUMN IF NOT EXISTS "OwnerUserId" uuid NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE INDEX IF NOT EXISTS "ix_job_vacancies_owner"
        ON "JobVacancies" ("OwnerUserId")
        WHERE "OwnerUserId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE TABLE IF NOT EXISTS "CandidateLists" (
        "Id"              uuid                        PRIMARY KEY,
        "RecruiterUserId" uuid                        NOT NULL,
        "Name"            varchar(200)                NOT NULL,
        "Description"     text                        NULL,
        "CreatedAt"       timestamp with time zone    NOT NULL,
        "UpdatedAt"       timestamp with time zone    NOT NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE INDEX IF NOT EXISTS "ix_candidate_lists_recruiter_created"
        ON "CandidateLists" ("RecruiterUserId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE INDEX IF NOT EXISTS "ix_recruiter_candidates_recruiter_added"
        ON "RecruiterCandidates" ("RecruiterUserId", "AddedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE INDEX IF NOT EXISTS "ix_recruiter_candidates_recruiter_hash"
        ON "RecruiterCandidates" ("RecruiterUserId", "CvHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE TABLE IF NOT EXISTS "CandidateListMemberships" (
        "CandidateListId"      uuid                        NOT NULL,
        "RecruiterCandidateId" uuid                        NOT NULL,
        "AddedAt"              timestamp with time zone    NOT NULL,
        PRIMARY KEY ("CandidateListId", "RecruiterCandidateId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE INDEX IF NOT EXISTS "ix_candidate_list_memberships_candidate"
        ON "CandidateListMemberships" ("RecruiterCandidateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE TABLE IF NOT EXISTS "CandidateScores" (
        "Id"                   uuid                        PRIMARY KEY,
        "VacancyId"            uuid                        NOT NULL,
        "RecruiterCandidateId" uuid                        NOT NULL,
        "Score"                double precision            NOT NULL,
        "ScoringVersion"       varchar(256)                NOT NULL,
        "ScoringResultJson"    jsonb                       NOT NULL,
        "ScoredAt"             timestamp with time zone    NOT NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "ux_candidate_scores_vacancy_candidate"
        ON "CandidateScores" ("VacancyId", "RecruiterCandidateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    CREATE INDEX IF NOT EXISTS "ix_candidate_scores_vacancy_score"
        ON "CandidateScores" ("VacancyId", "Score");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601080000_AddRecruiterCabinet') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601080000_AddRecruiterCabinet', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601090000_AddUserSearchSnapshotsTable') THEN
    CREATE TABLE IF NOT EXISTS "UserSearchSnapshots" (
        "Id"           uuid                        PRIMARY KEY,
        "UserId"       uuid                        NOT NULL,
        "QueryHash"    varchar(64)                 NOT NULL,
        "Keywords"     varchar(512)                NOT NULL,
        "ResponseJson" jsonb                       NOT NULL,
        "ExecutedAt"   timestamp with time zone    NOT NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601090000_AddUserSearchSnapshotsTable') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "ux_user_search_snapshots_user_query"
        ON "UserSearchSnapshots" ("UserId", "QueryHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601090000_AddUserSearchSnapshotsTable') THEN
    CREATE INDEX IF NOT EXISTS "ix_user_search_snapshots_executed"
        ON "UserSearchSnapshots" ("ExecutedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601090000_AddUserSearchSnapshotsTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260601090000_AddUserSearchSnapshotsTable', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611120000_AddAuditEntries') THEN
    CREATE TABLE IF NOT EXISTS "AuditEntries" (
        "Id"          uuid PRIMARY KEY,
        "UserId"      uuid NULL,
        "Action"      varchar(128) NOT NULL,
        "EntityType"  varchar(64) NULL,
        "EntityId"    uuid NULL,
        "PayloadJson" jsonb NULL,
        "Outcome"     varchar(32) NOT NULL,
        "Timestamp"   timestamp with time zone NOT NULL,
        "IpAddress"   varchar(64) NULL,
        "UserAgent"   varchar(512) NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611120000_AddAuditEntries') THEN
    CREATE INDEX IF NOT EXISTS "ix_audit_entries_user_timestamp"
        ON "AuditEntries" ("UserId", "Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611120000_AddAuditEntries') THEN
    CREATE INDEX IF NOT EXISTS "ix_audit_entries_entity_timestamp"
        ON "AuditEntries" ("EntityType", "EntityId", "Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611120000_AddAuditEntries') THEN
    CREATE INDEX IF NOT EXISTS "ix_audit_entries_timestamp"
        ON "AuditEntries" ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260611120000_AddAuditEntries') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260611120000_AddAuditEntries', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260612000000_AddCvFileKey') THEN
    ALTER TABLE "UserProfiles"
        ADD COLUMN IF NOT EXISTS "CvFileKey" VARCHAR(512) NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260612000000_AddCvFileKey') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260612000000_AddCvFileKey', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260612181139_SnapshotBaseline') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260612181139_SnapshotBaseline', '8.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613100000_AddSnapshotSchemaVersion') THEN
    ALTER TABLE "UserSearchSnapshots" ADD "SchemaVersion" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613100000_AddSnapshotSchemaVersion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260613100000_AddSnapshotSchemaVersion', '8.0.10');
    END IF;
END $EF$;
COMMIT;

