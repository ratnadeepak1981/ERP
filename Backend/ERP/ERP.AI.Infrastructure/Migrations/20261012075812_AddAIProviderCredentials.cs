using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.AI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAIProviderCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIProviderCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EncryptedSecret = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncryptionAlgorithm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "RSA-4096-OAEP-SHA256"),
                    KeyVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Production"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProviderCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIProviderCredentials_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderCredentials_AIProviderId_KeyReferenceCode_Environment",
                table: "AIProviderCredentials",
                columns: new[] { "AIProviderId", "KeyReferenceCode", "Environment" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIProviderCredentials");
        }
    }
}
