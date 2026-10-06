using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlansAndParameterTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove existing indexes that will be replaced.
            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId_SubscriptionName",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_SubscriptionLimitParameters_SubscriptionLimitId_ParameterKey",
                table: "SubscriptionLimitParameters");

            // Create SubscriptionPlans table.
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),

                    Name = table.Column<string>(
                        type: "nvarchar(150)",
                        maxLength: 150,
                        nullable: false),

                    Code = table.Column<string>(
                        type: "nvarchar(50)",
                        maxLength: 50,
                        nullable: false),

                    Description = table.Column<string>(
                        type: "nvarchar(500)",
                        maxLength: 500,
                        nullable: false),

                    Price = table.Column<decimal>(
                        type: "decimal(18,2)",
                        nullable: false),

                    BillingCycle = table.Column<int>(
                        type: "int",
                        nullable: false),

                    IsActive = table.Column<bool>(
                        type: "bit",
                        nullable: false),

                    CreatedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: false),

                    CreatedBy = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: false),

                    ModifiedAt = table.Column<DateTime>(
                        type: "datetime2",
                        nullable: true),

                    ModifiedBy = table.Column<Guid>(
                        type: "uniqueidentifier",
                        nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_SubscriptionPlans",
                        x => x.Id);

                    table.CheckConstraint(
                        "CK_SubscriptionPlans_Price_NonNegative",
                        "[Price] >= 0");
                });

            // Initial Free Manufacturing plan.
            //
            // This is the platform-level catalogue/package.
            // Tenants will receive a Subscription pointing to this plan.
            Guid freePlanId =
                new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[]
                {
                    "Id",
                    "Name",
                    "Code",
                    "Description",
                    "Price",
                    "BillingCycle",
                    "IsActive",
                    "CreatedAt",
                    "CreatedBy"
                },
                values: new object[]
                {
                    freePlanId,
                    "Free Manufacturing",
                    "FREE",
                    "Free Manufacturing ERP subscription plan",
                    0m,
                    2, // Monthly
                    true,
                    DateTime.UtcNow,
                    Guid.Empty
                });

            // Add SubscriptionPlanId to existing subscriptions.
            //
            // Existing subscriptions are temporarily assigned the
            // Free Manufacturing plan so the new FK can be created safely.
            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPlanId",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: freePlanId);

            // Add ParameterType.
            //
            // Existing parameters are treated as Fixed parameters.
            // Fixed = 1
            // Usage = 2
            migrationBuilder.AddColumn<int>(
                name: "ParameterType",
                table: "SubscriptionLimitParameters",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Explicitly backfill existing subscriptions.
            migrationBuilder.Sql($"""
                UPDATE Subscriptions
                SET SubscriptionPlanId = '{freePlanId}'
                WHERE SubscriptionPlanId = '{Guid.Empty}';
                """);

            // SubscriptionPlan lookup index.
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_SubscriptionPlanId",
                table: "Subscriptions",
                column: "SubscriptionPlanId");

            // Only one active subscription is allowed per tenant.
            //
            // Historical inactive subscriptions remain allowed.
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId",
                unique: true,
                filter: "[IsActive] = 1");

            // Parameter uniqueness:
            //
            // SubscriptionLimit + ParameterKey + ParameterType
            migrationBuilder.CreateIndex(
                name:
                    "IX_SubscriptionLimitParameters_SubscriptionLimitId_ParameterKey_ParameterType",
                table: "SubscriptionLimitParameters",
                columns: new[]
                {
                    "SubscriptionLimitId",
                    "ParameterKey",
                    "ParameterType"
                },
                unique: true);

            // Subscription plan code must be unique.
            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Code",
                table: "SubscriptionPlans",
                column: "Code",
                unique: true);

            // Subscription -> SubscriptionPlan relationship.
            migrationBuilder.AddForeignKey(
                name:
                    "FK_Subscriptions_SubscriptionPlans_SubscriptionPlanId",
                table: "Subscriptions",
                column: "SubscriptionPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name:
                    "FK_Subscriptions_SubscriptionPlans_SubscriptionPlanId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_SubscriptionPlanId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name:
                    "IX_SubscriptionLimitParameters_SubscriptionLimitId_ParameterKey_ParameterType",
                table: "SubscriptionLimitParameters");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ParameterType",
                table: "SubscriptionLimitParameters");

            // Restore the original TenantId index.
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            // Restore the original subscription uniqueness rule.
            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_SubscriptionName",
                table: "Subscriptions",
                columns: new[]
                {
                    "TenantId",
                    "SubscriptionName"
                },
                unique: true);

            // Restore the original parameter uniqueness rule.
            migrationBuilder.CreateIndex(
                name:
                    "IX_SubscriptionLimitParameters_SubscriptionLimitId_ParameterKey",
                table: "SubscriptionLimitParameters",
                columns: new[]
                {
                    "SubscriptionLimitId",
                    "ParameterKey"
                },
                unique: true);
        }
    }
}

