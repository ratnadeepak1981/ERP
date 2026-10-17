using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLedgerAndBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                columns: table => new
                {
                    InventoryBalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityQuarantine = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityOnOrder = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => x.InventoryBalanceId);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_WarehouseLocations_TenantId_WarehouseId_WarehouseLocationId",
                        columns: x => new { x.TenantId, x.WarehouseId, x.WarehouseLocationId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_Warehouses_TenantId_WarehouseId",
                        columns: x => new { x.TenantId, x.WarehouseId },
                        principalTable: "Warehouses",
                        principalColumns: new[] { "TenantId", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    InventoryTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MovementType = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentType = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementSequence = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReversalOfTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.InventoryTransactionId);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryTransactions_ReversalOfTransactionId",
                        column: x => x.ReversalOfTransactionId,
                        principalTable: "InventoryTransactions",
                        principalColumn: "InventoryTransactionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_WarehouseLocations_TenantId_WarehouseId_WarehouseLocationId",
                        columns: x => new { x.TenantId, x.WarehouseId, x.WarehouseLocationId },
                        principalTable: "WarehouseLocations",
                        principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Warehouses_TenantId_WarehouseId",
                        columns: x => new { x.TenantId, x.WarehouseId },
                        principalTable: "Warehouses",
                        principalColumns: new[] { "TenantId", "WarehouseId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_TenantId_ProductId",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_TenantId_WarehouseId_WarehouseLocationId",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBalances_TenantId_WarehouseLocationId_ProductId_BatchNumber_SerialNumber",
                table: "InventoryBalances",
                columns: new[] { "TenantId", "WarehouseLocationId", "ProductId", "BatchNumber", "SerialNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_ReversalOfTransactionId",
                table: "InventoryTransactions",
                column: "ReversalOfTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_ProductId",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_SourceDocumentType_SourceDocumentId_SourceDocumentLineId_MovementType_MovementSequence",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "SourceDocumentType", "SourceDocumentId", "SourceDocumentLineId", "MovementType", "MovementSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_TransactionNumber",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "TransactionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_TenantId_WarehouseId_WarehouseLocationId",
                table: "InventoryTransactions",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryBalances");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");
        }
    }
}
