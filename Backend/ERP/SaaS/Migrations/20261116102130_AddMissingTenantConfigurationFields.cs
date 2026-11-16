using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingTenantConfigurationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'AdminEmail')
BEGIN
    ALTER TABLE [dbo].[Tenants] ADD [AdminEmail] nvarchar(200) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'AdminUsername')
BEGIN
    ALTER TABLE [dbo].[Tenants] ADD [AdminUsername] nvarchar(100) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'Subdomain')
BEGIN
    ALTER TABLE [dbo].[Tenants] ADD [Subdomain] nvarchar(63) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'CompanyStructure')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] ADD [CompanyStructure] nvarchar(50) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] ADD [ECommerceEnabled] bit NOT NULL CONSTRAINT [DF_TenantConfigurations_ECommerceEnabled] DEFAULT 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'IndustryType')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] ADD [IndustryType] nvarchar(100) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Subdomain' AND object_id = OBJECT_ID(N'[dbo].[Tenants]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_Subdomain] ON [dbo].[Tenants] ([Subdomain]) WHERE [Subdomain] IS NOT NULL;
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Subdomain' AND object_id = OBJECT_ID(N'[dbo].[Tenants]'))
BEGIN
    DROP INDEX [IX_Tenants_Subdomain] ON [dbo].[Tenants];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'AdminEmail')
BEGIN
    ALTER TABLE [dbo].[Tenants] DROP COLUMN [AdminEmail];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'AdminUsername')
BEGIN
    ALTER TABLE [dbo].[Tenants] DROP COLUMN [AdminUsername];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'Subdomain')
BEGIN
    ALTER TABLE [dbo].[Tenants] DROP COLUMN [Subdomain];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'CompanyStructure')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP COLUMN [CompanyStructure];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP CONSTRAINT IF EXISTS [DF_TenantConfigurations_ECommerceEnabled];
    ALTER TABLE [dbo].[TenantConfigurations] DROP COLUMN [ECommerceEnabled];
END

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'IndustryType')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP COLUMN [IndustryType];
END
");
        }
    }
}
