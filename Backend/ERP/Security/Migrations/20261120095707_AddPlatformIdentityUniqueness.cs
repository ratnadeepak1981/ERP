using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Security.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformIdentityUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove legacy duplicate platform roles where TenantId IS NULL, keeping the active role with permissions
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.Roles', N'U') IS NOT NULL
                BEGIN
                    ;WITH DuplicateRoles AS (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY Name 
                                   ORDER BY (SELECT COUNT(*) FROM dbo.RolePermissions rp WHERE rp.RoleId = Roles.Id) DESC, CreatedAt ASC
                               ) as rn
                        FROM dbo.Roles
                        WHERE TenantId IS NULL
                    )
                    DELETE FROM dbo.Roles
                    WHERE Id IN (SELECT Id FROM DuplicateRoles WHERE rn > 1);
                END
            ");

            // Remove legacy duplicate platform users where TenantId IS NULL, keeping the canonical bootstrap user
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
                BEGIN
                    ;WITH DuplicateUsersByUsername AS (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY Username 
                                   ORDER BY CreatedAt DESC
                               ) as rn
                        FROM dbo.Users
                        WHERE TenantId IS NULL
                    )
                    DELETE FROM dbo.Users
                    WHERE Id IN (SELECT Id FROM DuplicateUsersByUsername WHERE rn > 1);

                    ;WITH DuplicateUsersByEmail AS (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY Email 
                                   ORDER BY 
                                       CASE WHEN Username NOT LIKE '%@%' THEN 0 ELSE 1 END,
                                       CreatedAt DESC
                               ) as rn
                        FROM dbo.Users
                        WHERE TenantId IS NULL
                    )
                    DELETE FROM dbo.Users
                    WHERE Id IN (SELECT Id FROM DuplicateUsersByEmail WHERE rn > 1);
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true,
                filter: "[TenantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[TenantId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true,
                filter: "[TenantId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Roles_Name",
                table: "Roles");
        }
    }
}
