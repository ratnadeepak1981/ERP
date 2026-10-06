using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionArchitectureUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionLimits_Subscriptions_SubscriptionId",
                table: "SubscriptionLimits");

            migrationBuilder.DropTable(
                name: "SubscriptionLimitParameters");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionLimits_SubscriptionId",
                table: "SubscriptionLimits");

            migrationBuilder.RenameColumn(
                name: "SubscriptionId",
                table: "SubscriptionLimits",
                newName: "SubscriptionPlanId");

            migrationBuilder.AlterColumn<string>(
                name: "SubscriptionType",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "SubscriptionName",
                table: "Subscriptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateTable(
                name: "SubscriptionParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParameterType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionParameters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionLimitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionParameterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LimitValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsUnlimited = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanParameters_SubscriptionLimits_SubscriptionLimitId",
                        column: x => x.SubscriptionLimitId,
                        principalTable: "SubscriptionLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanParameters_SubscriptionParameters_SubscriptionParameterId",
                        column: x => x.SubscriptionParameterId,
                        principalTable: "SubscriptionParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanParameterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionUsages_SubscriptionPlanParameters_SubscriptionPlanParameterId",
                        column: x => x.SubscriptionPlanParameterId,
                        principalTable: "SubscriptionPlanParameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionUsages_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimits_SubscriptionPlanId",
                table: "SubscriptionLimits",
                column: "SubscriptionPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionParameters_ParameterKey",
                table: "SubscriptionParameters",
                column: "ParameterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanParameters_SubscriptionLimitId",
                table: "SubscriptionPlanParameters",
                column: "SubscriptionLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanParameters_SubscriptionLimitId_SubscriptionParameterId",
                table: "SubscriptionPlanParameters",
                columns: new[] { "SubscriptionLimitId", "SubscriptionParameterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanParameters_SubscriptionParameterId",
                table: "SubscriptionPlanParameters",
                column: "SubscriptionParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_SubscriptionId",
                table: "SubscriptionUsages",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_SubscriptionId_SubscriptionPlanParameterId_PeriodStart_PeriodEnd",
                table: "SubscriptionUsages",
                columns: new[] { "SubscriptionId", "SubscriptionPlanParameterId", "PeriodStart", "PeriodEnd" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_SubscriptionPlanParameterId",
                table: "SubscriptionUsages",
                column: "SubscriptionPlanParameterId");

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionLimits_SubscriptionPlans_SubscriptionPlanId",
                table: "SubscriptionLimits",
                column: "SubscriptionPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubscriptionLimits_SubscriptionPlans_SubscriptionPlanId",
                table: "SubscriptionLimits");

            migrationBuilder.DropTable(
                name: "SubscriptionUsages");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanParameters");

            migrationBuilder.DropTable(
                name: "SubscriptionParameters");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionLimits_SubscriptionPlanId",
                table: "SubscriptionLimits");

            migrationBuilder.RenameColumn(
                name: "SubscriptionPlanId",
                table: "SubscriptionLimits",
                newName: "SubscriptionId");

            migrationBuilder.AlterColumn<string>(
                name: "SubscriptionType",
                table: "Subscriptions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "SubscriptionName",
                table: "Subscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateTable(
                name: "SubscriptionLimitParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionLimitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsUnlimited = table.Column<bool>(type: "bit", nullable: false),
                    LimitValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParameterKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParameterType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionLimitParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionLimitParameters_SubscriptionLimits_SubscriptionLimitId",
                        column: x => x.SubscriptionLimitId,
                        principalTable: "SubscriptionLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimits_SubscriptionId",
                table: "SubscriptionLimits",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimitParameters_SubscriptionLimitId",
                table: "SubscriptionLimitParameters",
                column: "SubscriptionLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimitParameters_SubscriptionLimitId_ParameterKey_ParameterType",
                table: "SubscriptionLimitParameters",
                columns: new[] { "SubscriptionLimitId", "ParameterKey", "ParameterType" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubscriptionLimits_Subscriptions_SubscriptionId",
                table: "SubscriptionLimits",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
