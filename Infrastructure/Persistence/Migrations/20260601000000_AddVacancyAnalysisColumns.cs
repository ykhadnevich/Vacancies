using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601000000_AddVacancyAnalysisColumns")]
    public partial class AddVacancyAnalysisColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                    ADD COLUMN IF NOT EXISTS "VacancyAnalysisJson" text NULL,
                    ADD COLUMN IF NOT EXISTS "VacancyAnalyzedAt" timestamp with time zone NULL,
                    ADD COLUMN IF NOT EXISTS "VacancyAnalysisModelVersion" varchar(64) NULL;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_JobVacancies_NeedsAnalysis"
                    ON "JobVacancies" ("VacancyAnalyzedAt")
                    WHERE "VacancyAnalysisJson" IS NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_JobVacancies_NeedsAnalysis";""");
            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                    DROP COLUMN IF EXISTS "VacancyAnalysisJson",
                    DROP COLUMN IF EXISTS "VacancyAnalyzedAt",
                    DROP COLUMN IF EXISTS "VacancyAnalysisModelVersion";
                """);
        }
    }
}
