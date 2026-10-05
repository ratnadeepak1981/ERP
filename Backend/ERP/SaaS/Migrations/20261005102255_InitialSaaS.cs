using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class InitialSaaS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformAuditRecords",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAuditRecords", x => x.AuditId);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SubscriptionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StorageMode = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantConfigurations",
                columns: table => new
                {
                    TenantConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BranchEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WarehouseZoneEnabled = table.Column<bool>(type: "bit", nullable: false),
                    WarehouseBinEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfigurations", x => x.TenantConfigurationId);
                    table.ForeignKey(
                        name: "FK_TenantConfigurations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantDatabases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DatabaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DatabaseServer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDatabases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantDatabases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionLimits_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionLimitParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionLimitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParameterKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_SubscriptionLimitParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionLimitParameters_SubscriptionLimits_SubscriptionLimitId",
                        column: x => x.SubscriptionLimitId,
                        principalTable: "SubscriptionLimits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditRecords_TenantId_EntityName_EntityId",
                table: "PlatformAuditRecords",
                columns: new[] { "TenantId", "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAuditRecords_Timestamp",
                table: "PlatformAuditRecords",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimitParameters_SubscriptionLimitId",
                table: "SubscriptionLimitParameters",
                column: "SubscriptionLimitId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimitParameters_SubscriptionLimitId_ParameterKey",
                table: "SubscriptionLimitParameters",
                columns: new[] { "SubscriptionLimitId", "ParameterKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLimits_SubscriptionId",
                table: "SubscriptionLimits",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId",
                table: "Subscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_TenantId_SubscriptionName",
                table: "Subscriptions",
                columns: new[] { "TenantId", "SubscriptionName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfigurations_TenantId",
                table: "TenantConfigurations",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantDatabases_TenantId",
                table: "TenantDatabases",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformAuditRecords");

            migrationBuilder.DropTable(
                name: "SubscriptionLimitParameters");

            migrationBuilder.DropTable(
                name: "TenantConfigurations");

            migrationBuilder.DropTable(
                name: "TenantDatabases");

            migrationBuilder.DropTable(
                name: "SubscriptionLimits");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
