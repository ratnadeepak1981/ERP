using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddEcommerceProductCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EcommerceProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    OnlineDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceProducts_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_CompanyId",
                table: "EcommerceProducts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_ProductId",
                table: "EcommerceProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_TenantId_CompanyId_DisplayOrder",
                table: "EcommerceProducts",
                columns: new[] { "TenantId", "CompanyId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_TenantId_CompanyId_IsPublished",
                table: "EcommerceProducts",
                columns: new[] { "TenantId", "CompanyId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceProducts_TenantId_CompanyId_ProductId",
                table: "EcommerceProducts",
                columns: new[] { "TenantId", "CompanyId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EcommerceProducts");
        }
    }
}
