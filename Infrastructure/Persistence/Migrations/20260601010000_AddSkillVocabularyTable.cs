using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601010000_AddSkillVocabularyTable")]
    public partial class AddSkillVocabularyTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_skill_vocab_domain"
                    ON "SkillVocabulary" ("Domain");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_skill_vocab_created"
                    ON "SkillVocabulary" ("CreatedAt");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "SkillVocabulary";""");
        }
    }
}
