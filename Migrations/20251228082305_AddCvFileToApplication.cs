using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_jobs.Migrations
{
    /// <inheritdoc />
    public partial class AddCvFileToApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CvFile",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CvFile",
                table: "Applications");
        }
    }
}
