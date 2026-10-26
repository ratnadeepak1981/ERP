using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandPaymentTermAndSoDeliveryAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryAddressId",
                table: "SalesOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddressSnapshot",
                table: "SalesOrders",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    BrandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrandCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BrandName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.BrandId);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                columns: table => new
                {
                    PaymentTermId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TermCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TermName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.PaymentTermId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Brands_TenantId_BrandCode",
                table: "Brands",
                columns: new[] { "TenantId", "BrandCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Brands_TenantId_BrandId",
                table: "Brands",
                columns: new[] { "TenantId", "BrandId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_TenantId_PaymentTermId",
                table: "PaymentTerms",
                columns: new[] { "TenantId", "PaymentTermId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerms_TenantId_TermCode",
                table: "PaymentTerms",
                columns: new[] { "TenantId", "TermCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropTable(
                name: "PaymentTerms");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressSnapshot",
                table: "SalesOrders");
        }
    }
}
