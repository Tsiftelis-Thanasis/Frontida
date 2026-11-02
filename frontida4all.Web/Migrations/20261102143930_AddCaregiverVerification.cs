using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace frontida4all.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCaregiverVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    IdentityStatus = table.Column<int>(type: "integer", nullable: false),
                    IdentityVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StripeIdentitySessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PhoneCodeSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneCodesSentToday = table.Column<int>(type: "integer", nullable: false),
                    PhoneCodesCountDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PhoneCodeFailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    AddressApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddressExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriminalRecordIssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriminalRecordUploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriminalRecordApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriminalRecordExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriminalRecordWarningSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CriminalRecordExpiredNoticeSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddressWarningSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AddressExpiredNoticeSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVerifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserVerifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VerificationDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataPurgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationDocuments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserVerifications_UserId",
                table: "UserVerifications",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationDocuments_Status",
                table: "VerificationDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationDocuments_UserId_Type_Status",
                table: "VerificationDocuments",
                columns: new[] { "UserId", "Type", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserVerifications");

            migrationBuilder.DropTable(
                name: "VerificationDocuments");
        }
    }
}
