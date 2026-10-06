using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaseLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PdfTemplateNoticeKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoticeKind",
                table: "PdfFormTemplates",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfFormTemplates_LandlordUserId_NoticeKind",
                table: "PdfFormTemplates",
                columns: new[] { "LandlordUserId", "NoticeKind" },
                unique: true,
                filter: "[NoticeKind] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PdfFormTemplates_LandlordUserId_NoticeKind",
                table: "PdfFormTemplates");

            migrationBuilder.DropColumn(
                name: "NoticeKind",
                table: "PdfFormTemplates");
        }
    }
}
