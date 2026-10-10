using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringBillingSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add columns to Subscriptions as nullable or with temporary defaults
            migrationBuilder.AddColumn<bool>(
                name: "AutoRenew",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingAnchorDate",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                table: "Subscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BillingPlanPriceSnapshot",
                table: "Subscriptions",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodStart",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInvoiceGeneratedAt",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextBillingDate",
                table: "Subscriptions",
                type: "datetime2",
                nullable: true);

            // 2. Deterministic backfill based on plan terms, current subscription status, and StartDate
            migrationBuilder.Sql(@"
                -- Backfill BillingCycle, PriceSnapshot, Anchor, and Periods from linked SubscriptionPlans
                UPDATE s
                SET 
                    s.[BillingCycle] = ISNULL(p.[BillingCycle], 2),
                    s.[BillingPlanPriceSnapshot] = ISNULL(p.[Price], 0.0),
                    s.[BillingAnchorDate] = s.[StartDate],
                    s.[CurrentPeriodStart] = s.[StartDate],
                    s.[CurrentPeriodEnd] = CASE 
                        WHEN ISNULL(p.[BillingCycle], 2) = 0 THEN '9999-12-31T23:59:59.0000000' -- Lifetime (0)
                        WHEN ISNULL(p.[BillingCycle], 2) = 1 THEN DATEADD(day, 7, s.[StartDate]) -- Weekly (1)
                        WHEN ISNULL(p.[BillingCycle], 2) = 2 THEN DATEADD(month, 1, s.[StartDate]) -- Monthly (2)
                        WHEN ISNULL(p.[BillingCycle], 2) = 3 THEN DATEADD(month, 3, s.[StartDate]) -- Quarterly (3)
                        WHEN ISNULL(p.[BillingCycle], 2) = 4 THEN DATEADD(year, 1, s.[StartDate]) -- Yearly (4)
                        ELSE DATEADD(month, 1, s.[StartDate])
                    END,
                    -- NextBillingDate: Only Active recurring subscriptions schedule future billing;
                    -- Lifetime (0), Expired (5), Cancelled (6), and Suspended (4) have NULL NextBillingDate.
                    -- Active subscriptions have NextBillingDate set to CurrentPeriodEnd.
                    s.[NextBillingDate] = CASE 
                        WHEN ISNULL(p.[BillingCycle], 2) = 0 THEN NULL -- Lifetime does not bill
                        WHEN s.[Status] = 2 AND (s.[EndDate] IS NULL OR s.[EndDate] > GETUTCDATE()) 
                            THEN CASE 
                                WHEN ISNULL(p.[BillingCycle], 2) = 1 THEN DATEADD(day, 7, s.[StartDate])
                                WHEN ISNULL(p.[BillingCycle], 2) = 2 THEN DATEADD(month, 1, s.[StartDate])
                                WHEN ISNULL(p.[BillingCycle], 2) = 3 THEN DATEADD(month, 3, s.[StartDate])
                                WHEN ISNULL(p.[BillingCycle], 2) = 4 THEN DATEADD(year, 1, s.[StartDate])
                                ELSE DATEADD(month, 1, s.[StartDate])
                            END
                        ELSE NULL
                    END,
                    s.[AutoRenew] = CASE 
                        WHEN ISNULL(p.[BillingCycle], 2) = 0 THEN CAST(0 AS bit) -- Lifetime does not auto-renew
                        WHEN s.[Status] IN (5, 6) THEN CAST(0 AS bit) -- Expired/Cancelled do not auto-renew
                        ELSE CAST(1 AS bit)
                    END
                FROM [Subscriptions] s
                LEFT JOIN [SubscriptionPlans] p ON s.[SubscriptionPlanId] = p.[Id]
                WHERE s.[BillingAnchorDate] IS NULL;
            ");

            // 3. Alter Subscriptions columns to NOT NULL without permanent defaults
            migrationBuilder.AlterColumn<DateTime>(
                name: "BillingAnchorDate",
                table: "Subscriptions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BillingCycle",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "BillingPlanPriceSnapshot",
                table: "Subscriptions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CurrentPeriodStart",
                table: "Subscriptions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Subscriptions",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            // 4. Drop permanent default constraint on AutoRenew if added
            migrationBuilder.Sql(@"
                DECLARE @var0 sysname;
                SELECT @var0 = [d].[name]
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'AutoRenew');
                IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var0 + '];');
            ");

            migrationBuilder.AddColumn<int>(
                name: "BillingCycle",
                table: "SubscriptionInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingPeriodEnd",
                table: "SubscriptionInvoices",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingPeriodStart",
                table: "SubscriptionInvoices",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "SubscriptionInvoices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_SubscriptionId_BillingPeriodStart_BillingPeriodEnd",
                table: "SubscriptionInvoices",
                columns: new[] { "SubscriptionId", "BillingPeriodStart", "BillingPeriodEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SubscriptionInvoices_SubscriptionId_BillingPeriodStart_BillingPeriodEnd",
                table: "SubscriptionInvoices");

            migrationBuilder.DropColumn(
                name: "AutoRenew",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingAnchorDate",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingPlanPriceSnapshot",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStart",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "LastInvoiceGeneratedAt",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "NextBillingDate",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingCycle",
                table: "SubscriptionInvoices");

            migrationBuilder.DropColumn(
                name: "BillingPeriodEnd",
                table: "SubscriptionInvoices");

            migrationBuilder.DropColumn(
                name: "BillingPeriodStart",
                table: "SubscriptionInvoices");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "SubscriptionInvoices");
        }
    }
}
