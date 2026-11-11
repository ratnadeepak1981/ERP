-- =========================================================================================
-- ApexERP Flow A Step 1 Migration: Tenant Foundation & Subdomain Hosting
-- Database: PlatformDB
-- Target Tables: Tenants, TenantConfigurations
-- Description:
--   1. Adds Subdomain (nvarchar(63)) to [dbo].[Tenants].
--   2. Adds IndustryType, CompanyStructure, ECommerceEnabled to [dbo].[TenantConfigurations].
--   3. Backfills existing tenant subdomains from [dbo].[TenantDomains] (e.g. Lanka Retail Mart -> 'retail').
--   4. Backfills remaining tenants from unique Code (e.g. TEN-7D61559D -> 'ten-7d61559d').
--   5. Enforces NOT NULL on [dbo].[Tenants].[Subdomain].
--   6. Creates UNIQUE index IX_Tenants_Subdomain on [dbo].[Tenants].
-- =========================================================================================

USE [PlatformDB];
GO

SET NOCOUNT ON;

BEGIN TRANSACTION;

-- 1. Add Subdomain column to Tenants
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'Subdomain')
BEGIN
    ALTER TABLE [dbo].[Tenants] ADD [Subdomain] nvarchar(63) NULL;
END
GO

-- 2. Add Flow A configuration fields to TenantConfigurations
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'IndustryType')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] ADD [IndustryType] nvarchar(100) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'CompanyStructure')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] ADD [CompanyStructure] nvarchar(50) NULL;
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] ADD [ECommerceEnabled] bit NOT NULL CONSTRAINT [DF_TenantConfigurations_ECommerceEnabled] DEFAULT 0;
END
GO

-- 3. Backfill Tenants.Subdomain from existing TenantDomains (where a primary domain exists)
UPDATE t
SET t.[Subdomain] = td.[Subdomain]
FROM [dbo].[Tenants] t
INNER JOIN [dbo].[TenantDomains] td ON t.[Id] = td.[TenantId]
WHERE td.[IsPrimary] = 1 AND t.[Subdomain] IS NULL;
GO

-- 4. Backfill any remaining tenants from their unique Code
UPDATE [dbo].[Tenants]
SET [Subdomain] = LOWER(REPLACE(LTRIM(RTRIM([Code])), ' ', '-'))
WHERE [Subdomain] IS NULL;
GO

-- 5. Validate zero nulls or duplicates exist before index creation
DECLARE @NullCount INT;
SELECT @NullCount = COUNT(*) FROM [dbo].[Tenants] WHERE [Subdomain] IS NULL;
IF @NullCount > 0
BEGIN
    RAISERROR('Validation Failed: NULL subdomains remain in Tenants table!', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

DECLARE @DuplicateCount INT;
SELECT @DuplicateCount = COUNT(*) 
FROM (
    SELECT [Subdomain] 
    FROM [dbo].[Tenants] 
    GROUP BY [Subdomain] 
    HAVING COUNT(*) > 1
) dup;
IF @DuplicateCount > 0
BEGIN
    RAISERROR('Validation Failed: Duplicate subdomains found in Tenants table!', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

-- 6. Alter column to NOT NULL
ALTER TABLE [dbo].[Tenants] ALTER COLUMN [Subdomain] nvarchar(63) NOT NULL;
GO

-- 7. Create Unique Index matching EF Core configuration
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Subdomain' AND object_id = OBJECT_ID(N'[dbo].[Tenants]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenants_Subdomain] ON [dbo].[Tenants] ([Subdomain]);
END
GO

COMMIT TRANSACTION;
GO

-- =========================================================================================
-- ROLLBACK SCRIPT (Execute ONLY if reverting this migration)
-- =========================================================================================
/*
USE [PlatformDB];
GO

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tenants_Subdomain' AND object_id = OBJECT_ID(N'[dbo].[Tenants]'))
BEGIN
    DROP INDEX [IX_Tenants_Subdomain] ON [dbo].[Tenants];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Tenants]') AND name = N'Subdomain')
BEGIN
    ALTER TABLE [dbo].[Tenants] DROP COLUMN [Subdomain];
END
GO

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_TenantConfigurations_ECommerceEnabled')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP CONSTRAINT [DF_TenantConfigurations_ECommerceEnabled];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'ECommerceEnabled')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP COLUMN [ECommerceEnabled];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'CompanyStructure')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP COLUMN [CompanyStructure];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TenantConfigurations]') AND name = N'IndustryType')
BEGIN
    ALTER TABLE [dbo].[TenantConfigurations] DROP COLUMN [IndustryType];
END
GO

COMMIT TRANSACTION;
GO
*/
