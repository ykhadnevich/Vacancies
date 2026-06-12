using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260611120000_AddAuditEntries")]
    public partial class AddAuditEntries : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AuditEntries" (
                    "Id"          uuid PRIMARY KEY,
                    "UserId"      uuid NULL,
                    "Action"      varchar(128) NOT NULL,
                    "EntityType"  varchar(64) NULL,
                    "EntityId"    uuid NULL,
                    "PayloadJson" jsonb NULL,
                    "Outcome"     varchar(32) NOT NULL,
                    "Timestamp"   timestamp with time zone NOT NULL,
                    "IpAddress"   varchar(64) NULL,
                    "UserAgent"   varchar(512) NULL
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_audit_entries_user_timestamp"
                    ON "AuditEntries" ("UserId", "Timestamp");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_audit_entries_entity_timestamp"
                    ON "AuditEntries" ("EntityType", "EntityId", "Timestamp");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "ix_audit_entries_timestamp"
                    ON "AuditEntries" ("Timestamp");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "AuditEntries";""");
        }
    }
}
