using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_jobs.Migrations
{
    /// <inheritdoc />
    public partial class AddCvLayoutAndTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvColorTheme",
                table: "CandidateProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CvLayout",
                table: "CandidateProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvColorTheme",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "CvLayout",
                table: "CandidateProfiles");
        }
    }
}
