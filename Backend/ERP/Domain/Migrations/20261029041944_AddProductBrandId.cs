using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddProductBrandId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Brands_TenantId_BrandId",
                table: "Brands",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_BrandId",
                table: "Products",
                columns: new[] { "TenantId", "BrandId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Brands_TenantId_BrandId",
                table: "Products",
                columns: new[] { "TenantId", "BrandId" },
                principalTable: "Brands",
                principalColumns: new[] { "TenantId", "BrandId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Brands_TenantId_BrandId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_BrandId",
                table: "Products");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Brands_TenantId_BrandId",
                table: "Brands");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "Products");
        }
    }
}
