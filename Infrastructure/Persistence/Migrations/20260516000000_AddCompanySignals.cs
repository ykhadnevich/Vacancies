using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Persistence;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260516000000_AddCompanySignals")]
    public partial class AddCompanySignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // P5: Company signals — applicant competition + recruiter reachability
            // Both nullable: populated only for Djinni jobs, null for other sources.
            migrationBuilder.AddColumn<int>(
                name: "ApplicantCount",
                table: "JobVacancies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecruiterRespondsQuickly",
                table: "JobVacancies",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApplicantCount",           table: "JobVacancies");
            migrationBuilder.DropColumn(name: "RecruiterRespondsQuickly", table: "JobVacancies");
        }
    }
}
