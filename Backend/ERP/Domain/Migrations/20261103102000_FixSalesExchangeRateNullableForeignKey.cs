using System;
using Domain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations;

[DbContext(typeof(DomainDbContext))]
[Migration("20261103102000_FixSalesExchangeRateNullableForeignKey")]
public partial class FixSalesExchangeRateNullableForeignKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Safely drop composite FKs on SalesOrders and SalesInvoices if present
        migrationBuilder.Sql(@"
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesOrders_ExchangeRates_TenantId_ExchangeRateId')
            BEGIN
                ALTER TABLE [SalesOrders] DROP CONSTRAINT [FK_SalesOrders_ExchangeRates_TenantId_ExchangeRateId];
            END
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesInvoices_ExchangeRates_TenantId_ExchangeRateId')
            BEGIN
                ALTER TABLE [SalesInvoices] DROP CONSTRAINT [FK_SalesInvoices_ExchangeRates_TenantId_ExchangeRateId];
            END
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesOrders_ExchangeRates_ExchangeRateId')
            BEGIN
                ALTER TABLE [SalesOrders] ADD CONSTRAINT [FK_SalesOrders_ExchangeRates_ExchangeRateId]
                    FOREIGN KEY ([ExchangeRateId]) REFERENCES [ExchangeRates] ([ExchangeRateId]);
            END
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesInvoices_ExchangeRates_ExchangeRateId')
            BEGIN
                ALTER TABLE [SalesInvoices] ADD CONSTRAINT [FK_SalesInvoices_ExchangeRates_ExchangeRateId]
                    FOREIGN KEY ([ExchangeRateId]) REFERENCES [ExchangeRates] ([ExchangeRateId]);
            END
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesOrders_ExchangeRates_ExchangeRateId')
            BEGIN
                ALTER TABLE [SalesOrders] DROP CONSTRAINT [FK_SalesOrders_ExchangeRates_ExchangeRateId];
            END
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SalesInvoices_ExchangeRates_ExchangeRateId')
            BEGIN
                ALTER TABLE [SalesInvoices] DROP CONSTRAINT [FK_SalesInvoices_ExchangeRates_ExchangeRateId];
            END
        ");
    }
}
