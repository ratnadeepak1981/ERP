using System;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations;

[DbContext(typeof(DomainDbContext))]
[Migration("20261031130000_AddExchangeRateAndPurchaseOrderCurrencySnapshots")]
public partial class AddExchangeRateAndPurchaseOrderCurrencySnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ExchangeRates",
            columns: table => new
            {
                ExchangeRateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FromCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ToCurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RateType = table.Column<int>(type: "int", nullable: false),
                EffectiveDate = table.Column<DateTime>(type: "date", nullable: false),
                Rate = table.Column<decimal>(type: "decimal(28,12)", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExchangeRates", x => x.ExchangeRateId);
                table.UniqueConstraint(
                    "AK_ExchangeRates_TenantId_ExchangeRateId",
                    x => new { x.TenantId, x.ExchangeRateId });
                table.ForeignKey(
                    name: "FK_ExchangeRates_Currencies_TenantId_FromCurrencyId",
                    columns: x => new { x.TenantId, x.FromCurrencyId },
                    principalTable: "Currencies",
                    principalColumns: new[] { "TenantId", "CurrencyId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ExchangeRates_Currencies_TenantId_ToCurrencyId",
                    columns: x => new { x.TenantId, x.ToCurrencyId },
                    principalTable: "Currencies",
                    principalColumns: new[] { "TenantId", "CurrencyId" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "TransactionCurrencyId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "BaseCurrencyId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "BaseCurrencyCode",
            table: "PurchaseOrders",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "BaseCurrencyDecimalPlaces",
            table: "PurchaseOrders",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "AppliedExchangeRate",
            table: "PurchaseOrders",
            type: "decimal(28,12)",
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "ExchangeRateSource",
            table: "PurchaseOrders",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "ExchangeRateId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "ExchangeRateEffectiveDate",
            table: "PurchaseOrders",
            type: "date",
            nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "PriceCardId",
            table: "PurchaseOrders",
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "BaseSubTotal",
            table: "PurchaseOrders",
            type: "decimal(18,4)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "BaseDiscountAmount",
            table: "PurchaseOrders",
            type: "decimal(18,4)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "BaseTaxAmount",
            table: "PurchaseOrders",
            type: "decimal(18,4)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "BaseTotalAmount",
            table: "PurchaseOrders",
            type: "decimal(18,4)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_FromCurrencyId_ToCurrencyId_RateType_EffectiveDate",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "FromCurrencyId", "ToCurrencyId", "RateType", "EffectiveDate" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_FromCurrencyId_ToCurrencyId_RateType_IsActive_EffectiveDate",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "FromCurrencyId", "ToCurrencyId", "RateType", "IsActive", "EffectiveDate" });
        migrationBuilder.CreateIndex(
            name: "IX_ExchangeRates_TenantId_ToCurrencyId",
            table: "ExchangeRates",
            columns: new[] { "TenantId", "ToCurrencyId" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_TenantId_TransactionCurrencyId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "TransactionCurrencyId" });
        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_TenantId_BaseCurrencyId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "BaseCurrencyId" });
        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_TenantId_ExchangeRateId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "ExchangeRateId" });

        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseOrders_Currencies_TenantId_TransactionCurrencyId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "TransactionCurrencyId" },
            principalTable: "Currencies",
            principalColumns: new[] { "TenantId", "CurrencyId" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseOrders_Currencies_TenantId_BaseCurrencyId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "BaseCurrencyId" },
            principalTable: "Currencies",
            principalColumns: new[] { "TenantId", "CurrencyId" },
            onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(
            name: "FK_PurchaseOrders_ExchangeRates_TenantId_ExchangeRateId",
            table: "PurchaseOrders",
            columns: new[] { "TenantId", "ExchangeRateId" },
            principalTable: "ExchangeRates",
            principalColumns: new[] { "TenantId", "ExchangeRateId" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseOrders_Currencies_TenantId_TransactionCurrencyId",
            table: "PurchaseOrders");
        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseOrders_Currencies_TenantId_BaseCurrencyId",
            table: "PurchaseOrders");
        migrationBuilder.DropForeignKey(
            name: "FK_PurchaseOrders_ExchangeRates_TenantId_ExchangeRateId",
            table: "PurchaseOrders");
        migrationBuilder.DropIndex("IX_PurchaseOrders_TenantId_TransactionCurrencyId", "PurchaseOrders");
        migrationBuilder.DropIndex("IX_PurchaseOrders_TenantId_BaseCurrencyId", "PurchaseOrders");
        migrationBuilder.DropIndex("IX_PurchaseOrders_TenantId_ExchangeRateId", "PurchaseOrders");

        migrationBuilder.DropColumn("TransactionCurrencyId", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseCurrencyId", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseCurrencyCode", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseCurrencyDecimalPlaces", "PurchaseOrders");
        migrationBuilder.DropColumn("AppliedExchangeRate", "PurchaseOrders");
        migrationBuilder.DropColumn("ExchangeRateSource", "PurchaseOrders");
        migrationBuilder.DropColumn("ExchangeRateId", "PurchaseOrders");
        migrationBuilder.DropColumn("ExchangeRateEffectiveDate", "PurchaseOrders");
        migrationBuilder.DropColumn("PriceCardId", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseSubTotal", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseDiscountAmount", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseTaxAmount", "PurchaseOrders");
        migrationBuilder.DropColumn("BaseTotalAmount", "PurchaseOrders");

        migrationBuilder.DropTable("ExchangeRates");
    }
}
