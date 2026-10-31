using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAndLocationIdentifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocationIdentifiers",
                columns: table => new
                {
                    LocationIdentifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentifierValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentifierType = table.Column<int>(type: "int", nullable: false),
                    Symbology = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationIdentifiers", x => x.LocationIdentifierId);
                    table.ForeignKey(
                        name: "FK_LocationIdentifiers_WarehouseLocations_WarehouseLocationId",
                        column: x => x.WarehouseLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "WarehouseLocationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocationIdentifiers_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductIdentifiers",
                columns: table => new
                {
                    ProductIdentifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdentifierValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentifierType = table.Column<int>(type: "int", nullable: false),
                    Symbology = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIdentifiers", x => x.ProductIdentifierId);
                    table.ForeignKey(
                        name: "FK_ProductIdentifiers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductIdentifiers_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "UnitOfMeasureId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationIdentifiers_TenantId_IdentifierValue",
                table: "LocationIdentifiers",
                columns: new[] { "TenantId", "IdentifierValue" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LocationIdentifiers_TenantId_WarehouseLocationId",
                table: "LocationIdentifiers",
                columns: new[] { "TenantId", "WarehouseLocationId" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LocationIdentifiers_WarehouseId",
                table: "LocationIdentifiers",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationIdentifiers_WarehouseLocationId",
                table: "LocationIdentifiers",
                column: "WarehouseLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentifiers_ProductId",
                table: "ProductIdentifiers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentifiers_TenantId_IdentifierValue",
                table: "ProductIdentifiers",
                columns: new[] { "TenantId", "IdentifierValue" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentifiers_TenantId_ProductId",
                table: "ProductIdentifiers",
                columns: new[] { "TenantId", "ProductId" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1 AND [UnitOfMeasureId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentifiers_TenantId_ProductId_UnitOfMeasureId",
                table: "ProductIdentifiers",
                columns: new[] { "TenantId", "ProductId", "UnitOfMeasureId" },
                unique: true,
                filter: "[IsPrimary] = 1 AND [IsActive] = 1 AND [UnitOfMeasureId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIdentifiers_UnitOfMeasureId",
                table: "ProductIdentifiers",
                column: "UnitOfMeasureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocationIdentifiers");

            migrationBuilder.DropTable(
                name: "ProductIdentifiers");
        }
    }
}
