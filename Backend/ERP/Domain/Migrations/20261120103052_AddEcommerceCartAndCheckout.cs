using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddEcommerceCartAndCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EcommerceCarts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SalesOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CheckoutAttemptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceCarts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcommerceCarts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCarts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCarts_SalesOrders_SalesOrderId",
                        column: x => x.SalesOrderId,
                        principalTable: "SalesOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EcommerceCartLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitOfMeasureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductPackagingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EcommerceCartLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_EcommerceCarts_CartId",
                        column: x => x.CartId,
                        principalTable: "EcommerceCarts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_ProductPackagings_ProductPackagingId",
                        column: x => x.ProductPackagingId,
                        principalTable: "ProductPackagings",
                        principalColumn: "ProductPackagingId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EcommerceCartLines_UnitsOfMeasure_UnitOfMeasureId",
                        column: x => x.UnitOfMeasureId,
                        principalTable: "UnitsOfMeasure",
                        principalColumn: "UnitOfMeasureId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CartId",
                table: "EcommerceCartLines",
                column: "CartId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_CartId_ProductId",
                table: "EcommerceCartLines",
                columns: new[] { "CartId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_ProductId",
                table: "EcommerceCartLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_ProductPackagingId",
                table: "EcommerceCartLines",
                column: "ProductPackagingId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCartLines_UnitOfMeasureId",
                table: "EcommerceCartLines",
                column: "UnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_CompanyId",
                table: "EcommerceCarts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_CustomerId",
                table: "EcommerceCarts",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_SalesOrderId",
                table: "EcommerceCarts",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_TenantId_CompanyId_IdempotencyKey",
                table: "EcommerceCarts",
                columns: new[] { "TenantId", "CompanyId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_TenantId_CompanyId_Status",
                table: "EcommerceCarts",
                columns: new[] { "TenantId", "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_TenantId_CustomerId_Status",
                table: "EcommerceCarts",
                columns: new[] { "TenantId", "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EcommerceCarts_TenantId_SessionId_Status",
                table: "EcommerceCarts",
                columns: new[] { "TenantId", "SessionId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EcommerceCartLines");

            migrationBuilder.DropTable(
                name: "EcommerceCarts");
        }
    }
}
