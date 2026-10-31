using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderPaymentTermAndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierProductPriceId",
                table: "PurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PaymentTermId",
                table: "PurchaseOrders",
                column: "PaymentTermId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_PaymentTerms_PaymentTermId",
                table: "PurchaseOrders",
                column: "PaymentTermId",
                principalTable: "PaymentTerms",
                principalColumn: "PaymentTermId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_PaymentTerms_PaymentTermId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_PaymentTermId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "SupplierProductPriceId",
                table: "PurchaseOrderItems");
        }
    }
}
