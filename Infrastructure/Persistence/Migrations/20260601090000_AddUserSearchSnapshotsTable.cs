using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260601090000_AddUserSearchSnapshotsTable")]
    public partial class AddUserSearchSnapshotsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "UserSearchSnapshots" (
                    "Id"           uuid                        PRIMARY KEY,
                    "UserId"       uuid                        NOT NULL,
                    "QueryHash"    varchar(64)                 NOT NULL,
                    "Keywords"     varchar(512)                NOT NULL,
                    "ResponseJson" jsonb                       NOT NULL,
                    "ExecutedAt"   timestamp with time zone    NOT NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "ux_user_search_snapshots_user_query"
                    ON "UserSearchSnapshots" ("UserId", "QueryHash");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_user_search_snapshots_executed"
                    ON "UserSearchSnapshots" ("ExecutedAt");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "UserSearchSnapshots";""");
        }
    }
}
