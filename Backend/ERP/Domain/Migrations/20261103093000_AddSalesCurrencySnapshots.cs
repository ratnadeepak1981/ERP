using System;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations;

[DbContext(typeof(DomainDbContext))]
[Migration("20261103093000_AddSalesCurrencySnapshots")]
public partial class AddSalesCurrencySnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SalesOrders columns
        migrationBuilder.AddColumn<Guid>(
            name: "TransactionCurrencyId",
            table: "SalesOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "BaseCurrencyId",
            table: "SalesOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BaseCurrencyCode",
            table: "SalesOrders",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BaseCurrencyDecimalPlaces",
            table: "SalesOrders",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "AppliedExchangeRate",
            table: "SalesOrders",
            type: "decimal(28,12)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ExchangeRateSource",
            table: "SalesOrders",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ExchangeRateId",
            table: "SalesOrders",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExchangeRateEffectiveDate",
            table: "SalesOrders",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseSubTotal",
            table: "SalesOrders",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseDiscountAmount",
            table: "SalesOrders",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseTaxAmount",
            table: "SalesOrders",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseTotalAmount",
            table: "SalesOrders",
            type: "decimal(18,4)",
            nullable: true);

        // SalesInvoices columns
        migrationBuilder.AddColumn<Guid>(
            name: "TransactionCurrencyId",
            table: "SalesInvoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "BaseCurrencyId",
            table: "SalesInvoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BaseCurrencyCode",
            table: "SalesInvoices",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BaseCurrencyDecimalPlaces",
            table: "SalesInvoices",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "AppliedExchangeRate",
            table: "SalesInvoices",
            type: "decimal(28,12)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ExchangeRateSource",
            table: "SalesInvoices",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ExchangeRateId",
            table: "SalesInvoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ExchangeRateEffectiveDate",
            table: "SalesInvoices",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseSubTotal",
            table: "SalesInvoices",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseDiscountAmount",
            table: "SalesInvoices",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseTaxAmount",
            table: "SalesInvoices",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "BaseTotalAmount",
            table: "SalesInvoices",
            type: "decimal(18,4)",
            nullable: true);

        // Foreign keys and indexes for SalesOrders
        migrationBuilder.CreateIndex(
            name: "IX_SalesOrders_TenantId_TransactionCurrencyId",
            table: "SalesOrders",
            columns: new[] { "TenantId", "TransactionCurrencyId" });

        migrationBuilder.CreateIndex(
            name: "IX_SalesOrders_TenantId_BaseCurrencyId",
            table: "SalesOrders",
            columns: new[] { "TenantId", "BaseCurrencyId" });

        migrationBuilder.CreateIndex(
            name: "IX_SalesOrders_TenantId_ExchangeRateId",
            table: "SalesOrders",
            columns: new[] { "TenantId", "ExchangeRateId" });

        migrationBuilder.AddForeignKey(
            name: "FK_SalesOrders_Currencies_TenantId_TransactionCurrencyId",
            table: "SalesOrders",
            columns: new[] { "TenantId", "TransactionCurrencyId" },
            principalTable: "Currencies",
            principalColumns: new[] { "TenantId", "CurrencyId" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SalesOrders_Currencies_TenantId_BaseCurrencyId",
            table: "SalesOrders",
            columns: new[] { "TenantId", "BaseCurrencyId" },
            principalTable: "Currencies",
            principalColumns: new[] { "TenantId", "CurrencyId" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SalesOrders_ExchangeRates_TenantId_ExchangeRateId",
            table: "SalesOrders",
            columns: new[] { "TenantId", "ExchangeRateId" },
            principalTable: "ExchangeRates",
            principalColumns: new[] { "TenantId", "ExchangeRateId" },
            onDelete: ReferentialAction.Restrict);

        // Foreign keys and indexes for SalesInvoices
        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_TenantId_TransactionCurrencyId",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "TransactionCurrencyId" });

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_TenantId_BaseCurrencyId",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "BaseCurrencyId" });

        migrationBuilder.CreateIndex(
            name: "IX_SalesInvoices_TenantId_ExchangeRateId",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "ExchangeRateId" });

        migrationBuilder.AddForeignKey(
            name: "FK_SalesInvoices_Currencies_TenantId_TransactionCurrencyId",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "TransactionCurrencyId" },
            principalTable: "Currencies",
            principalColumns: new[] { "TenantId", "CurrencyId" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SalesInvoices_Currencies_TenantId_BaseCurrencyId",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "BaseCurrencyId" },
            principalTable: "Currencies",
            principalColumns: new[] { "TenantId", "CurrencyId" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_SalesInvoices_ExchangeRates_TenantId_ExchangeRateId",
            table: "SalesInvoices",
            columns: new[] { "TenantId", "ExchangeRateId" },
            principalTable: "ExchangeRates",
            principalColumns: new[] { "TenantId", "ExchangeRateId" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop foreign keys
        migrationBuilder.DropForeignKey(
            name: "FK_SalesInvoices_ExchangeRates_TenantId_ExchangeRateId",
            table: "SalesInvoices");

        migrationBuilder.DropForeignKey(
            name: "FK_SalesInvoices_Currencies_TenantId_BaseCurrencyId",
            table: "SalesInvoices");

        migrationBuilder.DropForeignKey(
            name: "FK_SalesInvoices_Currencies_TenantId_TransactionCurrencyId",
            table: "SalesInvoices");

        migrationBuilder.DropForeignKey(
            name: "FK_SalesOrders_ExchangeRates_TenantId_ExchangeRateId",
            table: "SalesOrders");

        migrationBuilder.DropForeignKey(
            name: "FK_SalesOrders_Currencies_TenantId_BaseCurrencyId",
            table: "SalesOrders");

        migrationBuilder.DropForeignKey(
            name: "FK_SalesOrders_Currencies_TenantId_TransactionCurrencyId",
            table: "SalesOrders");

        // Drop indexes
        migrationBuilder.DropIndex(
            name: "IX_SalesInvoices_TenantId_ExchangeRateId",
            table: "SalesInvoices");

        migrationBuilder.DropIndex(
            name: "IX_SalesInvoices_TenantId_BaseCurrencyId",
            table: "SalesInvoices");

        migrationBuilder.DropIndex(
            name: "IX_SalesInvoices_TenantId_TransactionCurrencyId",
            table: "SalesInvoices");

        migrationBuilder.DropIndex(
            name: "IX_SalesOrders_TenantId_ExchangeRateId",
            table: "SalesOrders");

        migrationBuilder.DropIndex(
            name: "IX_SalesOrders_TenantId_BaseCurrencyId",
            table: "SalesOrders");

        migrationBuilder.DropIndex(
            name: "IX_SalesOrders_TenantId_TransactionCurrencyId",
            table: "SalesOrders");

        // Drop columns from SalesInvoices
        migrationBuilder.DropColumn(name: "BaseTotalAmount", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "BaseTaxAmount", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "BaseDiscountAmount", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "BaseSubTotal", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "ExchangeRateEffectiveDate", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "ExchangeRateId", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "ExchangeRateSource", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "AppliedExchangeRate", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "BaseCurrencyDecimalPlaces", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "BaseCurrencyCode", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "BaseCurrencyId", table: "SalesInvoices");
        migrationBuilder.DropColumn(name: "TransactionCurrencyId", table: "SalesInvoices");

        // Drop columns from SalesOrders
        migrationBuilder.DropColumn(name: "BaseTotalAmount", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "BaseTaxAmount", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "BaseDiscountAmount", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "BaseSubTotal", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "ExchangeRateEffectiveDate", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "ExchangeRateId", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "ExchangeRateSource", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "AppliedExchangeRate", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "BaseCurrencyDecimalPlaces", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "BaseCurrencyCode", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "BaseCurrencyId", table: "SalesOrders");
        migrationBuilder.DropColumn(name: "TransactionCurrencyId", table: "SalesOrders");
    }
}
