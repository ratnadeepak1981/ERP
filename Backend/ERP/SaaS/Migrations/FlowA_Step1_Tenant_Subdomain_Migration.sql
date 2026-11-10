-- =========================================================================================
-- ApexERP Flow A Step 1 Migration: Tenant Foundation & Subdomain Hosting
-- Database: PlatformDB
-- Target Tables: Tenants, TenantConfigurations
-- Description:
--   1. Adds Subdomain, IndustryType, CompanyStructure, ECommerceEnabled, and Admin fields to Tenants.
--   2. Adds ECommerceEnabled to TenantConfigurations.
--   3. Safely backfills existing tenants (e.g. Lanka Retail Mart -> 'lanka-retail-mart').
--   4. Alters Subdomain to NOT NULL and creates UNIQUE index IX_Tenants_Subdomain.
--
-- NOTE: Per AGENTS.md, this script is DOCUMENTED for user approval prior to execution.
-- =========================================================================================

BEGIN TRANSACTION;
GO

-- 1. Add columns to Tenants (nullable initially to allow safe backfill)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'Subdomain')
BEGIN
    ALTER TABLE [Tenants] ADD [Subdomain] nvarchar(63) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'IndustryType')
BEGIN
    ALTER TABLE [Tenants] ADD [IndustryType] nvarchar(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'CompanyStructure')
BEGIN
    ALTER TABLE [Tenants] ADD [CompanyStructure] nvarchar(50) NOT NULL CONSTRAINT [DF_Tenants_CompanyStructure] DEFAULT N'Single';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [Tenants] ADD [ECommerceEnabled] bit NOT NULL CONSTRAINT [DF_Tenants_ECommerceEnabled] DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'AdminFullName')
BEGIN
    ALTER TABLE [Tenants] ADD [AdminFullName] nvarchar(150) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'AdminEmail')
BEGIN
    ALTER TABLE [Tenants] ADD [AdminEmail] nvarchar(150) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'AdminPhone')
BEGIN
    ALTER TABLE [Tenants] ADD [AdminPhone] nvarchar(50) NULL;
END
GO

-- 2. Add ECommerceEnabled to TenantConfigurations
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TenantConfigurations]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [TenantConfigurations] ADD [ECommerceEnabled] bit NOT NULL CONSTRAINT [DF_TenantConfigurations_ECommerceEnabled] DEFAULT 0;
END
GO

-- 3. Backfill existing tenant records
-- Known existing tenant: Lanka Retail Mart (Code: RETAIL)
UPDATE [Tenants]
SET [Subdomain] = N'lanka-retail-mart',
    [IndustryType] = N'Retail',
    [CompanyStructure] = N'Single',
    [ECommerceEnabled] = 0
WHERE [Subdomain] IS NULL AND ([Code] = N'RETAIL' OR [Name] = N'Lanka Retail Mart');
GO

-- Fallback for any other existing tenant in testing/development environments
UPDATE [Tenants]
SET [Subdomain] = LOWER(REPLACE(LTRIM(RTRIM([Code])), N' ', N'-'))
WHERE [Subdomain] IS NULL;
GO

-- 4. Alter column Subdomain to NOT NULL now that all rows have valid subdomains
ALTER TABLE [Tenants] ALTER COLUMN [Subdomain] nvarchar(63) NOT NULL;
GO

-- 5. Create unique index on Subdomain
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Subdomain' AND object_id = OBJECT_ID(N'[Tenants]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_Subdomain] ON [Tenants] ([Subdomain]);
END
GO

COMMIT TRANSACTION;
GO

-- =========================================================================================
-- ROLLBACK SCRIPT (Execute ONLY if reverting this migration)
-- =========================================================================================
/*
BEGIN TRANSACTION;
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Subdomain' AND object_id = OBJECT_ID(N'[Tenants]'))
BEGIN
    DROP INDEX [IX_Tenants_Subdomain] ON [Tenants];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'Subdomain')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [Subdomain];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'IndustryType')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [IndustryType];
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Tenants_CompanyStructure')
BEGIN
    ALTER TABLE [Tenants] DROP CONSTRAINT [DF_Tenants_CompanyStructure];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'CompanyStructure')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [CompanyStructure];
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_Tenants_ECommerceEnabled')
BEGIN
    ALTER TABLE [Tenants] DROP CONSTRAINT [DF_Tenants_ECommerceEnabled];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [ECommerceEnabled];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'AdminFullName')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [AdminFullName];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'AdminEmail')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [AdminEmail];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tenants]') AND name = N'AdminPhone')
BEGIN
    ALTER TABLE [Tenants] DROP COLUMN [AdminPhone];
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_TenantConfigurations_ECommerceEnabled')
BEGIN
    ALTER TABLE [TenantConfigurations] DROP CONSTRAINT [DF_TenantConfigurations_ECommerceEnabled];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[TenantConfigurations]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [TenantConfigurations] DROP COLUMN [ECommerceEnabled];
END
GO

COMMIT TRANSACTION;
GO
*/
