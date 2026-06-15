using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace frontida4baby.Web.Migrations
{
    /// <inheritdoc />
    public partial class CodeReviewFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostReactions_AspNetUsers_UserId",
                table: "PostReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedPosts_AspNetUsers_UserId",
                table: "SavedPosts");

            migrationBuilder.AlterColumn<string>(
                name: "StripeSubscriptionId",
                table: "Subscriptions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "Subscriptions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationAttempts",
                table: "Replies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Posts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationAttempts",
                table: "Posts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProcessedStripeEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedStripeEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_Status_ModerationStatus_CreatedAt",
                table: "Posts",
                columns: new[] { "Status", "ModerationStatus", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_PostReactions_AspNetUsers_UserId",
                table: "PostReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SavedPosts_AspNetUsers_UserId",
                table: "SavedPosts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PostReactions_AspNetUsers_UserId",
                table: "PostReactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedPosts_AspNetUsers_UserId",
                table: "SavedPosts");

            migrationBuilder.DropTable(
                name: "ProcessedStripeEvents");

            migrationBuilder.DropIndex(
                name: "IX_Posts_Status_ModerationStatus_CreatedAt",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ModerationAttempts",
                table: "Replies");

            migrationBuilder.DropColumn(
                name: "ModerationAttempts",
                table: "Posts");

            migrationBuilder.AlterColumn<string>(
                name: "StripeSubscriptionId",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCustomerId",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PostReactions_AspNetUsers_UserId",
                table: "PostReactions",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedPosts_AspNetUsers_UserId",
                table: "SavedPosts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
