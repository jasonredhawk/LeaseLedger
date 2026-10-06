using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaseLedger.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PdfFormTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfFormTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LandlordUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfFormTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdfFormTemplates_AspNetUsers_LandlordUserId",
                        column: x => x.LandlordUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PdfFormFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Options = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DataKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConstantValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Format = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfFormFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PdfFormFields_PdfFormTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PdfFormTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfFormFields_TemplateId",
                table: "PdfFormFields",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PdfFormTemplates_LandlordUserId",
                table: "PdfFormTemplates",
                column: "LandlordUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfFormFields");

            migrationBuilder.DropTable(
                name: "PdfFormTemplates");
        }
    }
}
