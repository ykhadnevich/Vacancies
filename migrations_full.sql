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

