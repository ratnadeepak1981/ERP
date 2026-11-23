-- ==============================================================================
-- ApexERP — Dynamic E-commerce Storefront Product Publication Backfill Script
-- Safe, idempotent script for development database (DomainDB)
-- Makes all existing active and sellable Product Master items available in the
-- public storefront for each tenant's active selling company without duplicates.
-- ==============================================================================

USE DomainDB;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

PRINT 'Starting E-commerce product publication backfill...';

-- -----------------------------------------------------------------------------
-- Safe, Idempotent Backfill: Products -> EcommerceProducts
-- Preserves tenant and company ownership (p.TenantId = c.TenantId)
-- Excludes inactive, non-sellable, or archived products
-- Prevents duplicate publication records per (TenantId, CompanyId, ProductId)
-- -----------------------------------------------------------------------------
INSERT INTO EcommerceProducts (
    Id,
    TenantId,
    CompanyId,
    ProductId,
    IsPublished,
    OnlineDescription,
    DisplayOrder,
    CreatedAtUtc,
    UpdatedAtUtc,
    CreatedAt,
    CreatedBy,
    ModifiedAt,
    ModifiedBy
)
SELECT 
    NEWID() AS Id,
    p.TenantId,
    c.Id AS CompanyId,
    p.ProductId,
    1 AS IsPublished,
    ISNULL(p.Description, p.ProductName) AS OnlineDescription,
    ROW_NUMBER() OVER (PARTITION BY c.Id ORDER BY p.ProductCode) AS DisplayOrder,
    SYSUTCDATETIME() AS CreatedAtUtc,
    NULL AS UpdatedAtUtc,
    SYSUTCDATETIME() AS CreatedAt,
    '00000000-0000-0000-0000-000000000000' AS CreatedBy,
    NULL AS ModifiedAt,
    NULL AS ModifiedBy
FROM Products p
INNER JOIN Companies c ON p.TenantId = c.TenantId
WHERE c.IsActive = 1
  AND p.IsActive = 1
  AND p.IsSellable = 1
  AND NOT EXISTS (
      SELECT 1 
      FROM EcommerceProducts ep 
      WHERE ep.TenantId = p.TenantId 
        AND ep.CompanyId = c.Id 
        AND ep.ProductId = p.ProductId
  );

DECLARE @InsertedCount INT = @@ROWCOUNT;

-- Ensure any existing unpublished products that are active and sellable are set to IsPublished = 1
UPDATE ep
SET ep.IsPublished = 1,
    ep.UpdatedAtUtc = SYSUTCDATETIME(),
    ep.ModifiedAt = SYSUTCDATETIME(),
    ep.ModifiedBy = '00000000-0000-0000-0000-000000000000'
FROM EcommerceProducts ep
INNER JOIN Products p ON ep.ProductId = p.ProductId
WHERE p.IsActive = 1 AND p.IsSellable = 1 AND ep.IsPublished = 0;

DECLARE @UpdatedCount INT = @@ROWCOUNT;

PRINT CONCAT('Backfill completed successfully. Newly published products count: ', @InsertedCount, ', Updated to published count: ', @UpdatedCount);
GO
