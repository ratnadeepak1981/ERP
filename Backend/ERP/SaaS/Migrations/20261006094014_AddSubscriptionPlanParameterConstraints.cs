using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlanParameterConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlanParameters_LimitValue_NonNegative",
                table: "SubscriptionPlanParameters",
                sql: "[LimitValue] IS NULL OR [LimitValue] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SubscriptionPlanParameters_Unlimited_LimitValue",
                table: "SubscriptionPlanParameters",
                sql: "([IsUnlimited] = 1 AND [LimitValue] IS NULL) OR ([IsUnlimited] = 0 AND [LimitValue] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlanParameters_LimitValue_NonNegative",
                table: "SubscriptionPlanParameters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SubscriptionPlanParameters_Unlimited_LimitValue",
                table: "SubscriptionPlanParameters");
        }
    }
}
