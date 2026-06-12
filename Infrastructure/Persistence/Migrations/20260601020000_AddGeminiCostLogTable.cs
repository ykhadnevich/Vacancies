using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601020000_AddGeminiCostLogTable")]
    public partial class AddGeminiCostLogTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "GeminiCostLog" (
                    "Id"           uuid             NOT NULL PRIMARY KEY,
                    "Timestamp"    timestamp        NOT NULL,
                    "UserId"       uuid             NULL,
                    "RequestId"    uuid             NOT NULL,
                    "RequestKind"  varchar(64)      NOT NULL,
                    "Stage"        varchar(64)      NOT NULL,
                    "Calls"        integer          NOT NULL,
                    "DurationMs"   double precision NOT NULL,
                    "InputTokens"  bigint           NOT NULL,
                    "OutputTokens" bigint           NOT NULL,
                    "CostUsd"      double precision NOT NULL,
                    "Keywords"     varchar(256)     NULL
                );
                """);

            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_GeminiCostLog_Timestamp" ON "GeminiCostLog" ("Timestamp");""");
            migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_GeminiCostLog_RequestId" ON "GeminiCostLog" ("RequestId");""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "GeminiCostLog";""");
        }
    }
}
