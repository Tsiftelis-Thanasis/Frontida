using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace frontida4baby.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPostCloseReasonAndAdminUserFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedByUserId",
                table: "Posts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosedReason",
                table: "Posts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ClosedReason",
                table: "Posts");
        }
    }
}
