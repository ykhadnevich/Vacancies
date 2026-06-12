using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260430000000_AddMlFeatures")]
    public partial class AddMlFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Вмикаємо pgvector extension
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

            // 2. Додаємо Embedding до JobVacancies (vector 768-dim)
            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                ADD COLUMN IF NOT EXISTS "Embedding" vector(768);
                """);

            // 3. HNSW індекс для швидкого cosine similarity пошуку
            //    Дозволяє scoring 100K вакансій за < 50мс
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_jobvacancies_embedding
                ON "JobVacancies"
                USING hnsw ("Embedding" vector_cosine_ops);
                """);

            // 4. Додаємо CvEmbedding та CvVersionId до UserProfiles
            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                ADD COLUMN IF NOT EXISTS "CvEmbedding" vector(768),
                ADD COLUMN IF NOT EXISTS "CvVersionId" uuid NOT NULL DEFAULT gen_random_uuid();
                """);

            // 5. Створюємо таблицю RelevanceExplanations
            //    Кеш LLM-пояснень: ключ (CvVersionId, JobId) — вічний кеш
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "RelevanceExplanations" (
                    "CvVersionId"   uuid         NOT NULL,
                    "JobId"         uuid         NOT NULL,
                    "Reason"        text         NOT NULL,
                    "ModelVersion"  varchar(50)  NOT NULL DEFAULT '',
                    "Score"         real         NOT NULL,
                    "GeneratedAt"   timestamp    NOT NULL,
                    PRIMARY KEY ("CvVersionId", "JobId")
                );

                CREATE INDEX IF NOT EXISTS idx_re_cv_version
                    ON "RelevanceExplanations" ("CvVersionId");

                CREATE INDEX IF NOT EXISTS idx_re_job_id
                    ON "RelevanceExplanations" ("JobId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "RelevanceExplanations";""");

            migrationBuilder.Sql("""
                ALTER TABLE "UserProfiles"
                DROP COLUMN IF EXISTS "CvEmbedding",
                DROP COLUMN IF EXISTS "CvVersionId";
                """);

            migrationBuilder.Sql("""DROP INDEX IF EXISTS idx_jobvacancies_embedding;""");

            migrationBuilder.Sql("""
                ALTER TABLE "JobVacancies"
                DROP COLUMN IF EXISTS "Embedding";
                """);
        }
    }
}
