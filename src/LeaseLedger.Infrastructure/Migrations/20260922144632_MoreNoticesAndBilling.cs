using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaseLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoreNoticesAndBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCareHome",
                table: "Units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CardCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LandlordUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StripeSessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardCheckouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InsuranceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    BrokerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    LandlordListed = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InsuranceRecords_Tenancies_TenancyId",
                        column: x => x.TenancyId,
                        principalTable: "Tenancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandlordPlans",
                columns: table => new
                {
                    LandlordUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StripeCustomerId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordPlans", x => x.LandlordUserId);
                });

            migrationBuilder.CreateTable(
                name: "LegalContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandlordUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Firm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalContacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NoticeAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeAcknowledgements_NoticeRecords_NoticeRecordId",
                        column: x => x.NoticeRecordId,
                        principalTable: "NoticeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeReferrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeReferrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeReferrals_NoticeRecords_NoticeRecordId",
                        column: x => x.NoticeRecordId,
                        principalTable: "NoticeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoticeServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoticeRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ServedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoticeServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoticeServices_NoticeRecords_NoticeRecordId",
                        column: x => x.NoticeRecordId,
                        principalTable: "NoticeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PadAuthorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenancyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PadAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PadAuthorizations_Tenancies_TenancyId",
                        column: x => x.TenancyId,
                        principalTable: "Tenancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Trade = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ScheduledFor = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CardCheckouts_StripeSessionId",
                table: "CardCheckouts",
                column: "StripeSessionId",
                unique: true,
                filter: "[StripeSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceRecords_TenancyId",
                table: "InsuranceRecords",
                column: "TenancyId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalContacts_LandlordUserId",
                table: "LegalContacts",
                column: "LandlordUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeAcknowledgements_NoticeRecordId",
                table: "NoticeAcknowledgements",
                column: "NoticeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeReferrals_NoticeRecordId",
                table: "NoticeReferrals",
                column: "NoticeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_NoticeServices_NoticeRecordId",
                table: "NoticeServices",
                column: "NoticeRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PadAuthorizations_TenancyId",
                table: "PadAuthorizations",
                column: "TenancyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenanceRequestId",
                table: "WorkOrders",
                column: "MaintenanceRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CardCheckouts");

            migrationBuilder.DropTable(
                name: "InsuranceRecords");

            migrationBuilder.DropTable(
                name: "LandlordPlans");

            migrationBuilder.DropTable(
                name: "LegalContacts");

            migrationBuilder.DropTable(
                name: "NoticeAcknowledgements");

            migrationBuilder.DropTable(
                name: "NoticeReferrals");

            migrationBuilder.DropTable(
                name: "NoticeServices");

            migrationBuilder.DropTable(
                name: "PadAuthorizations");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsCareHome",
                table: "Units");
        }
    }
}
