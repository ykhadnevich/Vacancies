using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601070000_AddTrackerAnalysisV2Columns")]
    public partial class AddTrackerAnalysisV2Columns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Applications"
                    ADD COLUMN IF NOT EXISTS "StrengthsEn"      varchar(2000) NULL,
                    ADD COLUMN IF NOT EXISTS "StrengthsUk"      varchar(2000) NULL,
                    ADD COLUMN IF NOT EXISTS "GapsEn"           varchar(2000) NULL,
                    ADD COLUMN IF NOT EXISTS "GapsUk"           varchar(2000) NULL,
                    ADD COLUMN IF NOT EXISTS "RecommendationEn" varchar(2000) NULL,
                    ADD COLUMN IF NOT EXISTS "RecommendationUk" varchar(2000) NULL,
                    ADD COLUMN IF NOT EXISTS "SubScores"        jsonb         NULL,
                    ADD COLUMN IF NOT EXISTS "CvFileName"       varchar(255)  NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Applications"
                    DROP COLUMN IF EXISTS "StrengthsEn",
                    DROP COLUMN IF EXISTS "StrengthsUk",
                    DROP COLUMN IF EXISTS "GapsEn",
                    DROP COLUMN IF EXISTS "GapsUk",
                    DROP COLUMN IF EXISTS "RecommendationEn",
                    DROP COLUMN IF EXISTS "RecommendationUk",
                    DROP COLUMN IF EXISTS "SubScores",
                    DROP COLUMN IF EXISTS "CvFileName";
                """);
        }
    }
}
