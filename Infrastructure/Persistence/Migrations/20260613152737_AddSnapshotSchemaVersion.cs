using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    public partial class AddSnapshotSchemaVersion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""UserSearchSnapshots""
                ADD COLUMN IF NOT EXISTS ""SchemaVersion"" integer NOT NULL DEFAULT 0;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""UserSearchSnapshots""
                DROP COLUMN IF EXISTS ""SchemaVersion"";
            ");
        }
    }
}
