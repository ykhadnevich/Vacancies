using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601060000_AddTrackerAnalysisColumns")]
    public partial class AddTrackerAnalysisColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Applications"
                    ADD COLUMN IF NOT EXISTS "Score"              double precision NULL,
                    ADD COLUMN IF NOT EXISTS "Verdict"            varchar(20)      NULL,
                    ADD COLUMN IF NOT EXISTS "MatchedSkills"      jsonb            NULL,
                    ADD COLUMN IF NOT EXISTS "MissingMustHaves"   jsonb            NULL,
                    ADD COLUMN IF NOT EXISTS "TriggeredAntiFlags" jsonb            NULL,
                    ADD COLUMN IF NOT EXISTS "ReasonShort"        text             NULL,
                    ADD COLUMN IF NOT EXISTS "PipelineVersion"    varchar(100)     NULL,
                    ADD COLUMN IF NOT EXISTS "AnalyzedAt"         timestamptz      NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Applications"
                    DROP COLUMN IF EXISTS "Score",
                    DROP COLUMN IF EXISTS "Verdict",
                    DROP COLUMN IF EXISTS "MatchedSkills",
                    DROP COLUMN IF EXISTS "MissingMustHaves",
                    DROP COLUMN IF EXISTS "TriggeredAntiFlags",
                    DROP COLUMN IF EXISTS "ReasonShort",
                    DROP COLUMN IF EXISTS "PipelineVersion",
                    DROP COLUMN IF EXISTS "AnalyzedAt";
                """);
        }
    }
}
