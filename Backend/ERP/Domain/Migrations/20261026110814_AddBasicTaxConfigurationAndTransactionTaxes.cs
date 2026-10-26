using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddBasicTaxConfigurationAndTransactionTaxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "SalesOrderItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxInclusive",
                table: "SalesOrderItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxCodeId",
                table: "SalesOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "SalesInvoiceItems",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxInclusive",
                table: "SalesInvoiceItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxCodeId",
                table: "SalesInvoiceItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "PurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxInclusive",
                table: "PurchaseOrderItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PurchaseOrderItems",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SubTotal",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "PurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxCodeId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "PurchaseOrderItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TaxCodes",
                columns: table => new
                {
                    TaxCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Applicability = table.Column<int>(type: "int", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxCodes", x => x.TaxCodeId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxCodes_TenantId_Code",
                table: "TaxCodes",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxCodes_TenantId_TaxCodeId",
                table: "TaxCodes",
                columns: new[] { "TenantId", "TaxCodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxCodes");

            migrationBuilder.DropColumn(
                name: "IsTaxInclusive",
                table: "SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxCodeId",
                table: "SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "IsTaxInclusive",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "TaxCodeId",
                table: "SalesInvoiceItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "IsTaxInclusive",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "SubTotal",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxCodeId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "PurchaseOrderItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "SalesOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                table: "SalesInvoiceItems",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");
        }
    }
}
