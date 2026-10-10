using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add Status as nullable column to allow deterministic backfill without arbitrary database defaults
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Subscriptions",
                type: "int",
                nullable: true);

            // 2. Deterministic backfill based strictly on verified data
            // 2A. Verified Active: IsActive = 1 and (EndDate is null or in the future)
            migrationBuilder.Sql(@"
                UPDATE [Subscriptions]
                SET [Status] = 2 -- Active
                WHERE [Status] IS NULL
                  AND [IsActive] = 1 
                  AND ([EndDate] IS NULL OR [EndDate] >= GETUTCDATE());
            ");

            // 2B. Inactive with an EndDate in the past: Classified as Expired (5)
            // (Note: PastDue represents a payment-failure lifecycle that cannot be inferred from legacy IsActive/EndDate alone)
            migrationBuilder.Sql(@"
                UPDATE [Subscriptions]
                SET [Status] = 5 -- Expired
                WHERE [Status] IS NULL
                  AND [EndDate] IS NOT NULL 
                  AND [EndDate] < GETUTCDATE();
            ");

            // 2C. Inactive with NULL EndDate: Classified as Provisionally Suspended (4) per policy
            migrationBuilder.Sql(@"
                UPDATE [Subscriptions]
                SET [Status] = 4 -- Suspended (Provisional)
                WHERE [Status] IS NULL
                  AND [IsActive] = 0 
                  AND [EndDate] IS NULL;
            ");

            // 2D. Fallback for any remaining unclassified records
            migrationBuilder.Sql(@"
                UPDATE [Subscriptions]
                SET [Status] = 5 -- Expired
                WHERE [Status] IS NULL;
            ");

            // 3. Make column NOT NULL without a permanent default constraint
            // Application entity initialization strictly supplies the explicit Status value
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Subscriptions");
        }
    }
}
