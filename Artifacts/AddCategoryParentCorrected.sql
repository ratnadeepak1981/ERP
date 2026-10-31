BEGIN TRANSACTION;
GO

ALTER TABLE [Products] ADD [BrandId] uniqueidentifier NULL;
GO

ALTER TABLE [Brands] ADD CONSTRAINT [AK_Brands_TenantId_BrandId] UNIQUE ([TenantId], [BrandId]);
GO

CREATE INDEX [IX_Products_TenantId_BrandId] ON [Products] ([TenantId], [BrandId]);
GO

ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Brands_TenantId_BrandId] FOREIGN KEY ([TenantId], [BrandId]) REFERENCES [Brands] ([TenantId], [BrandId]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029041944_AddProductBrandId', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [ProductPackagings] (
    [ProductPackagingId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [PackagingCode] nvarchar(50) NOT NULL,
    [PackagingName] nvarchar(100) NOT NULL,
    [PackagingUomId] uniqueidentifier NOT NULL,
    [BaseQuantity] decimal(18,6) NOT NULL,
    [PurchaseAllowed] bit NOT NULL,
    [SalesAllowed] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [RowVersion] rowversion NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_ProductPackagings] PRIMARY KEY ([ProductPackagingId]),
    CONSTRAINT [FK_ProductPackagings_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_ProductPackagings_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductPackagings_UnitsOfMeasure_PackagingUomId] FOREIGN KEY ([PackagingUomId]) REFERENCES [UnitsOfMeasure] ([UnitOfMeasureId]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_ProductPackagings_CompanyId] ON [ProductPackagings] ([CompanyId]);
GO

CREATE INDEX [IX_ProductPackagings_PackagingUomId] ON [ProductPackagings] ([PackagingUomId]);
GO

CREATE INDEX [IX_ProductPackagings_ProductId] ON [ProductPackagings] ([ProductId]);
GO

CREATE INDEX [IX_ProductPackagings_TenantId_CompanyId] ON [ProductPackagings] ([TenantId], [CompanyId]);
GO

CREATE UNIQUE INDEX [IX_ProductPackagings_TenantId_PackagingCode] ON [ProductPackagings] ([TenantId], [PackagingCode]);
GO

CREATE INDEX [IX_ProductPackagings_TenantId_ProductId_PackagingUomId] ON [ProductPackagings] ([TenantId], [ProductId], [PackagingUomId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029052350_AddProductPackagingMaster', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Companies] ADD [SpecialUomConversionEnabled] bit NOT NULL DEFAULT CAST(1 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029075402_AddCompanySpecialUomConversionSetting', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Categories] ADD [ParentCategoryId] uniqueidentifier NULL;
GO

CREATE INDEX [IX_Categories_TenantId_ParentCategoryId] ON [Categories] ([TenantId], [ParentCategoryId]);
GO

ALTER TABLE [Categories] ADD CONSTRAINT [FK_Categories_Categories_TenantId_ParentCategoryId] FOREIGN KEY ([TenantId], [ParentCategoryId]) REFERENCES [Categories] ([TenantId], [CategoryId]) ON DELETE NO ACTION;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261029112701_AddCategoryParentCorrected', N'8.0.0');
GO

COMMIT;
GO

