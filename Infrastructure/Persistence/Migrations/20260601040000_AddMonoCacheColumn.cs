using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601040000_AddMonoCacheColumn")]
    public partial class AddMonoCacheColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "ScoringCache"
                    ADD COLUMN IF NOT EXISTS "MonoResultJson" jsonb NULL;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_scoring_cache_mono_only"
                    ON "ScoringCache" ("CvHash", "VacancyId", "ScoringVersion")
                    WHERE "MonoResultJson" IS NOT NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "ix_scoring_cache_mono_only";""");
            migrationBuilder.Sql("""
                ALTER TABLE "ScoringCache"
                    DROP COLUMN IF EXISTS "MonoResultJson";
                """);
        }
    }
}
