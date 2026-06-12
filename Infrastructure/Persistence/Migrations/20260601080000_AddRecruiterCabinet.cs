using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601080000_AddRecruiterCabinet")]
    public partial class AddRecruiterCabinet : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                    ADD COLUMN IF NOT EXISTS "Role" int NOT NULL DEFAULT 0;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                    ADD COLUMN IF NOT EXISTS "OwnerUserId" uuid NULL;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_job_vacancies_owner"
                    ON "JobVacancies" ("OwnerUserId")
                    WHERE "OwnerUserId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "CandidateLists" (
                    "Id"              uuid                        PRIMARY KEY,
                    "RecruiterUserId" uuid                        NOT NULL,
                    "Name"            varchar(200)                NOT NULL,
                    "Description"     text                        NULL,
                    "CreatedAt"       timestamp with time zone    NOT NULL,
                    "UpdatedAt"       timestamp with time zone    NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_candidate_lists_recruiter_created"
                    ON "CandidateLists" ("RecruiterUserId", "CreatedAt");
                """);

            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_recruiter_candidates_recruiter_added"
                    ON "RecruiterCandidates" ("RecruiterUserId", "AddedAt");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_recruiter_candidates_recruiter_hash"
                    ON "RecruiterCandidates" ("RecruiterUserId", "CvHash");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "CandidateListMemberships" (
                    "CandidateListId"      uuid                        NOT NULL,
                    "RecruiterCandidateId" uuid                        NOT NULL,
                    "AddedAt"              timestamp with time zone    NOT NULL,
                    PRIMARY KEY ("CandidateListId", "RecruiterCandidateId")
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_candidate_list_memberships_candidate"
                    ON "CandidateListMemberships" ("RecruiterCandidateId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "CandidateScores" (
                    "Id"                   uuid                        PRIMARY KEY,
                    "VacancyId"            uuid                        NOT NULL,
                    "RecruiterCandidateId" uuid                        NOT NULL,
                    "Score"                double precision            NOT NULL,
                    "ScoringVersion"       varchar(256)                NOT NULL,
                    "ScoringResultJson"    jsonb                       NOT NULL,
                    "ScoredAt"             timestamp with time zone    NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "ux_candidate_scores_vacancy_candidate"
                    ON "CandidateScores" ("VacancyId", "RecruiterCandidateId");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_candidate_scores_vacancy_score"
                    ON "CandidateScores" ("VacancyId", "Score");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "CandidateScores";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "CandidateListMemberships";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "RecruiterCandidates";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "CandidateLists";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "ix_job_vacancies_owner";""");
            migrationBuilder.Sql("""ALTER TABLE "JobVacancies" DROP COLUMN IF EXISTS "OwnerUserId";""");
            migrationBuilder.Sql("""ALTER TABLE "UserProfiles" DROP COLUMN IF EXISTS "Role";""");
        }
    }
}
