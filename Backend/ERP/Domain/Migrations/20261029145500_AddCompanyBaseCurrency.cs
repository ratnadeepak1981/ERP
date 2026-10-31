using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyBaseCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BaseCurrencyId",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill existing companies with the tenant's primary active currency
            migrationBuilder.Sql(@"
                UPDATE c
                SET c.BaseCurrencyId = (
                    SELECT TOP 1 cur.CurrencyId
                    FROM Currencies cur
                    WHERE cur.TenantId = c.TenantId AND cur.IsActive = 1
                    ORDER BY CASE WHEN cur.CurrencyCode = 'USD' THEN 1 WHEN cur.CurrencyCode = 'LKR' THEN 2 ELSE 3 END
                )
                FROM Companies c
                WHERE c.BaseCurrencyId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_BaseCurrencyId",
                table: "Companies",
                columns: new[] { "TenantId", "BaseCurrencyId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Currencies_TenantId_BaseCurrencyId",
                table: "Companies",
                columns: new[] { "TenantId", "BaseCurrencyId" },
                principalTable: "Currencies",
                principalColumns: new[] { "TenantId", "CurrencyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Currencies_TenantId_BaseCurrencyId",
                table: "Companies");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_BaseCurrencyId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BaseCurrencyId",
                table: "Companies");
        }
    }
}
