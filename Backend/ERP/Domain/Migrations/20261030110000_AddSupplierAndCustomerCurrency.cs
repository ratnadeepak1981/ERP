using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierAndCustomerCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId_CurrencyId",
                table: "Suppliers",
                columns: new[] { "TenantId", "CurrencyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Currencies_TenantId_CurrencyId",
                table: "Suppliers",
                columns: new[] { "TenantId", "CurrencyId" },
                principalTable: "Currencies",
                principalColumns: new[] { "TenantId", "CurrencyId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_CurrencyId",
                table: "Customers",
                columns: new[] { "TenantId", "CurrencyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Currencies_TenantId_CurrencyId",
                table: "Customers",
                columns: new[] { "TenantId", "CurrencyId" },
                principalTable: "Currencies",
                principalColumns: new[] { "TenantId", "CurrencyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Currencies_TenantId_CurrencyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId_CurrencyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Currencies_TenantId_CurrencyId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_CurrencyId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Customers");
        }
    }
}
