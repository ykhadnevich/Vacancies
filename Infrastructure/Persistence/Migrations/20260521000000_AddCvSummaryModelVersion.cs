using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the <c>CvSummaryModelVersion</c> column that tracks the composite
    /// prompt version (e.g. "gemini-cv-normalization-v3+tech_v2") which
    /// produced each user's <c>CvSummary</c>.
    ///
    /// <para>
    /// Why: previously a prompt bump did not trigger automatic re-normalization
    /// — <c>CvSummaryWorker</c> only re-ran when <c>CvSummary IS NULL</c>, so
    /// users with stale summaries from an older prompt version kept seeing
    /// downstream scoring based on outdated extraction. With this column the
    /// worker compares stored version against the current expected prefix and
    /// re-normalizes whenever they diverge.
    /// </para>
    ///
    /// <para>
    /// Existing rows are backfilled to NULL; the worker treats NULL as "stale"
    /// and re-normalizes on the next pass — equivalent to a one-time
    /// <c>UPDATE user_profiles SET cv_summary = NULL</c> but without losing
    /// the existing summary text until the new one is ready.
    /// </para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521000000_AddCvSummaryModelVersion")]
    public partial class AddCvSummaryModelVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                ADD COLUMN IF NOT EXISTS "CvSummaryModelVersion" text;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                DROP COLUMN IF EXISTS "CvSummaryModelVersion";
                """);
        }
    }
}
