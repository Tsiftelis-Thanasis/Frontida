using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace frontida4baby.Web.Migrations
{
    /// <inheritdoc />
    public partial class LegalAndSafetyFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "PostReactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApprovedByOP",
                table: "PostReactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "BlacklistReason",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlacklistedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasAcceptedTerms",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlacklisted",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PostReactions");

            migrationBuilder.DropColumn(
                name: "IsApprovedByOP",
                table: "PostReactions");

            migrationBuilder.DropColumn(
                name: "BlacklistReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BlacklistedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HasAcceptedTerms",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsBlacklisted",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "AspNetUsers");
        }
    }
}
