using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedPriceCardTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceCards",
                columns: table => new
                {
                    PriceCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceCardCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PriceCardName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PriceCardType = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceCards", x => x.PriceCardId);
                });

            migrationBuilder.CreateTable(
                name: "PriceCardLines",
                columns: table => new
                {
                    PriceCardLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriceCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 1m),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceCardLines", x => x.PriceCardLineId);
                    table.ForeignKey(
                        name: "FK_PriceCardLines_PriceCards_PriceCardId",
                        column: x => x.PriceCardId,
                        principalTable: "PriceCards",
                        principalColumn: "PriceCardId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PriceCardLines_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceCardLines_UnitsOfMeasure_TenantId_UnitOfMeasureId",
                        columns: x => new { x.TenantId, x.UnitOfMeasureId },
                        principalTable: "UnitsOfMeasure",
                        principalColumns: new[] { "TenantId", "UnitOfMeasureId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PriceCards_TenantId_PriceCardCode",
                table: "PriceCards",
                columns: new[] { "TenantId", "PriceCardCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceCards_TenantId_PriceCardId",
                table: "PriceCards",
                columns: new[] { "TenantId", "PriceCardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceCards_TenantId_PriceCardType_IsActive",
                table: "PriceCards",
                columns: new[] { "TenantId", "PriceCardType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceCardLines_TenantId_PriceCardId_ProductId",
                table: "PriceCardLines",
                columns: new[] { "TenantId", "PriceCardId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceCardLines_TenantId_PriceCardId_ProductId_UnitOfMeasureId",
                table: "PriceCardLines",
                columns: new[] { "TenantId", "PriceCardId", "ProductId", "UnitOfMeasureId" },
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultPurchasePriceCardId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_DefaultPurchasePriceCardId",
                table: "Suppliers",
                columns: new[] { "TenantId", "DefaultPurchasePriceCardId" });

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultSalesPriceCardId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_DefaultSalesPriceCardId",
                table: "Customers",
                columns: new[] { "TenantId", "DefaultSalesPriceCardId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_DefaultSalesPriceCardId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DefaultSalesPriceCardId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_DefaultPurchasePriceCardId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DefaultPurchasePriceCardId",
                table: "Suppliers");

            migrationBuilder.DropTable(
                name: "PriceCardLines");

            migrationBuilder.DropTable(
                name: "PriceCards");
        }
    }
}
