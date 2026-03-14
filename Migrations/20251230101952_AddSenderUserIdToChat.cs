using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace web_jobs.Migrations
{
    /// <inheritdoc />
    public partial class AddSenderUserIdToChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderRole",
                table: "Chats");

            migrationBuilder.AddColumn<Guid>(
                name: "SenderUser_ID",
                table: "Chats",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderUser_ID",
                table: "Chats");

            migrationBuilder.AddColumn<string>(
                name: "SenderRole",
                table: "Chats",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
