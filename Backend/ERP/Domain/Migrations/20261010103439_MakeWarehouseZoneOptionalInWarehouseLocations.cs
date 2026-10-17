using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domain.Migrations
{
    /// <inheritdoc />
    public partial class MakeWarehouseZoneOptionalInWarehouseLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseLocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseZones");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_LocationCode",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseZoneId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WarehouseLocations_TenantId_WarehouseId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_LocationCode",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "LocationCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_ParentLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "ParentLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_WarehouseZoneId",
                table: "WarehouseLocations",
                column: "WarehouseZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_ParentLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "ParentLocationId" },
                principalTable: "WarehouseLocations",
                principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseLocationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_WarehouseZoneId",
                table: "WarehouseLocations",
                column: "WarehouseZoneId",
                principalTable: "WarehouseZones",
                principalColumn: "WarehouseZoneId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_WarehouseZoneId",
                table: "WarehouseLocations");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WarehouseLocations_TenantId_WarehouseId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_LocationCode",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_ParentLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseLocationId",
                table: "WarehouseLocations");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseLocations_WarehouseZoneId",
                table: "WarehouseLocations");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseZoneId",
                table: "WarehouseLocations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseZones",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "WarehouseLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_LocationCode",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "LocationCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "ParentLocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "WarehouseLocationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "ParentLocationId" },
                principalTable: "WarehouseLocations",
                principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId", "WarehouseLocationId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseLocations_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId",
                table: "WarehouseLocations",
                columns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" },
                principalTable: "WarehouseZones",
                principalColumns: new[] { "TenantId", "WarehouseId", "WarehouseZoneId" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
