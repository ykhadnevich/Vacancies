using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    // No-op marker migration. Its purpose is to refresh AppDbContextModelSnapshot
    // so future `dotnet ef migrations add` calls diff against the current model
    // state. The schema is already present from the prior chain of migrations;
    // there is nothing to apply here.
    public partial class SnapshotBaseline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }

        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
