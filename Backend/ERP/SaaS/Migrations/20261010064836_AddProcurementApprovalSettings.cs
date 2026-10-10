using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementApprovalSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProcurementApprovalMode",
                table: "TenantConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "ProcurementApprovalRequired",
                table: "TenantConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcurementApprovalMode",
                table: "TenantConfigurations");

            migrationBuilder.DropColumn(
                name: "ProcurementApprovalRequired",
                table: "TenantConfigurations");
        }
    }
}
