using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601030000_AddScoringCacheTable")]
    public partial class AddScoringCacheTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_scoring_cache_vacancy_version"
                    ON "ScoringCache" ("VacancyId", "ScoringVersion");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_scoring_cache_created"
                    ON "ScoringCache" ("CreatedAt");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "ScoringCache";""");
        }
    }
}
