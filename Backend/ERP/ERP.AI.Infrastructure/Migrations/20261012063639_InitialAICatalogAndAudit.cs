using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.AI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialAICatalogAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Success"),
                    SafeMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProviders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SupportsChat = table.Column<bool>(type: "bit", nullable: true),
                    SupportsStructuredOutput = table.Column<bool>(type: "bit", nullable: true),
                    SupportsToolCalling = table.Column<bool>(type: "bit", nullable: true),
                    SupportsEmbeddings = table.Column<bool>(type: "bit", nullable: true),
                    SupportsTokenReporting = table.Column<bool>(type: "bit", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIModels_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIProviderKeyReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KeyReferenceCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Production"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIProviderKeyReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIProviderKeyReferences_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AIUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AIProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProviderKeyReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AIApplicationCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AIFeatureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    TotalTokens = table.Column<int>(type: "int", nullable: true),
                    CachedInputTokens = table.Column<int>(type: "int", nullable: true),
                    ReasoningTokens = table.Column<int>(type: "int", nullable: true),
                    UsageSource = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "ProviderReported"),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Success"),
                    LatencyMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIUsageRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIUsageRecords_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIUsageRecords_AIProviderKeyReferences_AIProviderKeyReferenceId",
                        column: x => x.AIProviderKeyReferenceId,
                        principalTable: "AIProviderKeyReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIUsageRecords_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TenantAIConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIProviderKeyReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantAIConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantAIConfigurations_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantAIConfigurations_AIProviderKeyReferences_AIProviderKeyReferenceId",
                        column: x => x.AIProviderKeyReferenceId,
                        principalTable: "AIProviderKeyReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantAIConfigurations_AIProviders_AIProviderId",
                        column: x => x.AIProviderId,
                        principalTable: "AIProviders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIAuditRecords_CorrelationId",
                table: "AIAuditRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIAuditRecords_EntityType_EntityId",
                table: "AIAuditRecords",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AIAuditRecords_OccurredAtUtc",
                table: "AIAuditRecords",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AIAuditRecords_TenantId_OccurredAtUtc",
                table: "AIAuditRecords",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AIModels_AIProviderId_ModelCode",
                table: "AIModels",
                columns: new[] { "AIProviderId", "ModelCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderKeyReferences_AIProviderId_KeyReferenceCode_Environment",
                table: "AIProviderKeyReferences",
                columns: new[] { "AIProviderId", "KeyReferenceCode", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIProviders_ProviderCode",
                table: "AIProviders",
                column: "ProviderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_AIApplicationCode_AIFeatureCode_CreatedAtUtc",
                table: "AIUsageRecords",
                columns: new[] { "AIApplicationCode", "AIFeatureCode", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_AIModelId",
                table: "AIUsageRecords",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_AIProviderId_AIModelId_CreatedAtUtc",
                table: "AIUsageRecords",
                columns: new[] { "AIProviderId", "AIModelId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_AIProviderKeyReferenceId_CreatedAtUtc",
                table: "AIUsageRecords",
                columns: new[] { "AIProviderKeyReferenceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_CorrelationId",
                table: "AIUsageRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageRecords_TenantId_CreatedAtUtc",
                table: "AIUsageRecords",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantAIConfigurations_AIModelId",
                table: "TenantAIConfigurations",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantAIConfigurations_AIProviderId",
                table: "TenantAIConfigurations",
                column: "AIProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantAIConfigurations_AIProviderKeyReferenceId",
                table: "TenantAIConfigurations",
                column: "AIProviderKeyReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantAIConfigurations_TenantId",
                table: "TenantAIConfigurations",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIAuditRecords");

            migrationBuilder.DropTable(
                name: "AIUsageRecords");

            migrationBuilder.DropTable(
                name: "TenantAIConfigurations");

            migrationBuilder.DropTable(
                name: "AIModels");

            migrationBuilder.DropTable(
                name: "AIProviderKeyReferences");

            migrationBuilder.DropTable(
                name: "AIProviders");
        }
    }
}
