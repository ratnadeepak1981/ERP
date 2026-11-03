using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Security.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionCategorisationAndRoleTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "Roles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "Roles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Permissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Tenant");

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "Permissions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_TenantId_TemplateKey",
                table: "Roles",
                columns: new[] { "TenantId", "TemplateKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Category_Module",
                table: "Permissions",
                columns: new[] { "Category", "Module" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Roles_TenantId_TemplateKey",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Category_Module",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Permissions");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "Permissions");
        }
    }
}
