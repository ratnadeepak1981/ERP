using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddStoresPutawayCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Putaways",
                columns: table => new
                {
                    PutawayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PutawayNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PutawayDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceDocumentType = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Putaways", x => x.PutawayId);
                });

            migrationBuilder.CreateTable(
                name: "PutawayLines",
                columns: table => new
                {
                    PutawayLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PutawayId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDocumentLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceWarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PreviouslyPutawayQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OutstandingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PutawayQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BatchNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawayLines", x => x.PutawayLineId);
                    table.ForeignKey(
                        name: "FK_PutawayLines_Putaways_PutawayId",
                        column: x => x.PutawayId,
                        principalTable: "Putaways",
                        principalColumn: "PutawayId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PutawayLineAllocations",
                columns: table => new
                {
                    PutawayLineAllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PutawayLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationWarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MovementSequence = table.Column<int>(type: "int", nullable: false),
                    InventoryTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PutawayLineAllocations", x => x.PutawayLineAllocationId);
                    table.ForeignKey(
                        name: "FK_PutawayLineAllocations_PutawayLines_PutawayLineId",
                        column: x => x.PutawayLineId,
                        principalTable: "PutawayLines",
                        principalColumn: "PutawayLineId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PutawayLineAllocations_DestinationWarehouseLocationId",
                table: "PutawayLineAllocations",
                column: "DestinationWarehouseLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayLineAllocations_InventoryTransactionId",
                table: "PutawayLineAllocations",
                column: "InventoryTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayLineAllocations_PutawayLineId",
                table: "PutawayLineAllocations",
                column: "PutawayLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayLines_ProductId",
                table: "PutawayLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayLines_PutawayId",
                table: "PutawayLines",
                column: "PutawayId");

            migrationBuilder.CreateIndex(
                name: "IX_PutawayLines_SourceDocumentLineId",
                table: "PutawayLines",
                column: "SourceDocumentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Putaways_TenantId_CompanyId_PutawayNumber",
                table: "Putaways",
                columns: new[] { "TenantId", "CompanyId", "PutawayNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Putaways_TenantId_SourceDocumentId",
                table: "Putaways",
                columns: new[] { "TenantId", "SourceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Putaways_TenantId_Status",
                table: "Putaways",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Putaways_TenantId_WarehouseId",
                table: "Putaways",
                columns: new[] { "TenantId", "WarehouseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PutawayLineAllocations");

            migrationBuilder.DropTable(
                name: "PutawayLines");

            migrationBuilder.DropTable(
                name: "Putaways");
        }
    }
}
