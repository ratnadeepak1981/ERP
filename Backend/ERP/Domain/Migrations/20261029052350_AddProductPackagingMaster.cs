using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddProductPackagingMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductPackagings",
                columns: table => new
                {
                    ProductPackagingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackagingCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackagingName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PackagingUomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    PurchaseAllowed = table.Column<bool>(type: "bit", nullable: false),
                    SalesAllowed = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPackagings", x => x.ProductPackagingId);
                    table.ForeignKey(
                        name: "FK_ProductPackagings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductPackagings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductPackagings_UnitsOfMeasure_PackagingUomId",
                        column: x => x.PackagingUomId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "UnitOfMeasureId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPackagings_CompanyId",
                table: "ProductPackagings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPackagings_PackagingUomId",
                table: "ProductPackagings",
                column: "PackagingUomId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPackagings_ProductId",
                table: "ProductPackagings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPackagings_TenantId_CompanyId",
                table: "ProductPackagings",
                columns: new[] { "TenantId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPackagings_TenantId_PackagingCode",
                table: "ProductPackagings",
                columns: new[] { "TenantId", "PackagingCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductPackagings_TenantId_ProductId_PackagingUomId",
                table: "ProductPackagings",
                columns: new[] { "TenantId", "ProductId", "PackagingUomId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductPackagings");
        }
    }
}
