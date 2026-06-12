using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the v6.2 skill-expansion columns to UserProfiles and JobVacancies.
    ///
    /// <para>
    /// Motivation: <c>SkillMatchCalculator</c> previously did exact-string
    /// intersection of <c>cv.skills</c> against <c>vacancy.must_have_skills</c>
    /// through a small canonical-alias map. Production data showed senior CVs
    /// with 44 strong skills scoring skill_match = 0.12-0.36 against generic
    /// PM postings because their vocabulary differed (Cohort analysis vs
    /// product analytics, Stripe Billing vs billing, A/B-тестів vs A/B testing).
    /// </para>
    ///
    /// <para>
    /// Fix: <c>ISkillExpansionService</c> calls Gemini once per CV (cached in
    /// <c>CvSkillsExpanded</c>) and once per vacancy (cached in
    /// <c>VacancyMustHavesExpanded</c>) to produce typed expansions, then
    /// SkillMatchCalculator intersects the expanded sets. Stored as JSONB
    /// `{original: [{term, relation, confidence}, ...], ...}`.
    /// </para>
    ///
    /// <para>
    /// Existing rows: columns nullable; expansion happens lazily on next
    /// score / worker pass. No backfill required.
    /// </para>
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260528160000_AddSkillExpansionColumns")]
    public partial class AddSkillExpansionColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                ADD COLUMN IF NOT EXISTS "CvSkillsExpanded" text,
                ADD COLUMN IF NOT EXISTS "CvSkillsExpansionVersion" text;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                ADD COLUMN IF NOT EXISTS "VacancyMustHavesExpanded" text,
                ADD COLUMN IF NOT EXISTS "VacancyExpansionVersion" text;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                DROP COLUMN IF EXISTS "CvSkillsExpanded",
                DROP COLUMN IF EXISTS "CvSkillsExpansionVersion";
                """);
            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                DROP COLUMN IF EXISTS "VacancyMustHavesExpanded",
                DROP COLUMN IF EXISTS "VacancyExpansionVersion";
                """);
        }
    }
}
