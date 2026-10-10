BEGIN TRANSACTION;
GO

ALTER TABLE [Products] DROP CONSTRAINT [FK_Products_Categories_CategoryId];
GO

ALTER TABLE [WarehouseLocations] DROP CONSTRAINT [FK_WarehouseLocations_WarehouseZones_WarehouseZoneId];
GO

ALTER TABLE [WarehouseLocations] DROP CONSTRAINT [FK_WarehouseLocations_Warehouses_WarehouseId];
GO

ALTER TABLE [WarehouseZones] DROP CONSTRAINT [FK_WarehouseZones_Warehouses_WarehouseId];
GO

DROP INDEX [IX_WarehouseZones_WarehouseId] ON [WarehouseZones];
GO

DROP INDEX [IX_WarehouseLocations_WarehouseId] ON [WarehouseLocations];
GO

DROP INDEX [IX_WarehouseLocations_WarehouseZoneId] ON [WarehouseLocations];
GO

DROP INDEX [IX_Products_CategoryId] ON [Products];
GO

ALTER TABLE [Warehouses] ADD [BranchId] uniqueidentifier NULL;
GO

ALTER TABLE [Warehouses] ADD [CompanyId] uniqueidentifier NULL;
GO

ALTER TABLE [WarehouseLocations] ADD [LocationTypeId] uniqueidentifier NULL;
GO

ALTER TABLE [WarehouseLocations] ADD [ParentLocationId] uniqueidentifier NULL;
GO

ALTER TABLE [Products] ADD [CanConsumeInMaintenance] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Products] ADD [CanConsumeInProduction] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

ALTER TABLE [Products] ADD [IsStockTracked] bit NOT NULL DEFAULT CAST(1 AS bit);
GO

ALTER TABLE [Products] ADD [ProductType] int NOT NULL DEFAULT 1;
GO

ALTER TABLE [Products] ADD [TrackingMode] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [Products] ADD [UnitOfMeasureId] uniqueidentifier NULL;
GO

ALTER TABLE [WarehouseZones] ADD CONSTRAINT [AK_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId] UNIQUE ([TenantId], [WarehouseId], [WarehouseZoneId]);
GO

ALTER TABLE [Warehouses] ADD CONSTRAINT [AK_Warehouses_TenantId_WarehouseId] UNIQUE ([TenantId], [WarehouseId]);
GO

ALTER TABLE [WarehouseLocations] ADD CONSTRAINT [AK_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId] UNIQUE ([TenantId], [WarehouseId], [WarehouseZoneId], [WarehouseLocationId]);
GO

ALTER TABLE [Suppliers] ADD CONSTRAINT [AK_Suppliers_TenantId_SupplierId] UNIQUE ([TenantId], [SupplierId]);
GO

ALTER TABLE [Products] ADD CONSTRAINT [AK_Products_TenantId_ProductId] UNIQUE ([TenantId], [ProductId]);
GO

ALTER TABLE [Customers] ADD CONSTRAINT [AK_Customers_TenantId_CustomerId] UNIQUE ([TenantId], [CustomerId]);
GO

ALTER TABLE [Companies] ADD CONSTRAINT [AK_Companies_TenantId_Id] UNIQUE ([TenantId], [Id]);
GO

ALTER TABLE [Categories] ADD CONSTRAINT [AK_Categories_TenantId_CategoryId] UNIQUE ([TenantId], [CategoryId]);
GO

CREATE TABLE [AddressTypes] (
    [AddressTypeId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(250) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_AddressTypes] PRIMARY KEY ([AddressTypeId])
);
GO

CREATE TABLE [Contacts] (
    [ContactId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ContactName] nvarchar(200) NOT NULL,
    [JobTitle] nvarchar(100) NULL,
    [Email] nvarchar(200) NULL,
    [Phone] nvarchar(50) NULL,
    [MobileNumber] nvarchar(50) NULL,
    [PreferredContactMethod] nvarchar(20) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_Contacts] PRIMARY KEY ([ContactId]),
    CONSTRAINT [AK_Contacts_TenantId_ContactId] UNIQUE ([TenantId], [ContactId])
);
GO

CREATE TABLE [ContactTypes] (
    [ContactTypeId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(250) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_ContactTypes] PRIMARY KEY ([ContactTypeId])
);
GO

CREATE TABLE [Countries] (
    [CountryId] uniqueidentifier NOT NULL,
    [Alpha2Code] nchar(2) NOT NULL,
    [Alpha3Code] nchar(3) NOT NULL,
    [NumericCode] nvarchar(3) NULL,
    [CountryName] nvarchar(100) NOT NULL,
    [OfficialName] nvarchar(200) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_Countries] PRIMARY KEY ([CountryId])
);
GO

CREATE TABLE [LocationTypes] (
    [LocationTypeId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(250) NULL,
    [CanStoreInventory] bit NOT NULL,
    [CanPick] bit NOT NULL,
    [CanPutAway] bit NOT NULL,
    [CanContainChildren] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_LocationTypes] PRIMARY KEY ([LocationTypeId])
);
GO

CREATE TABLE [UnitsOfMeasure] (
    [UnitOfMeasureId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Code] nvarchar(50) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(250) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_UnitsOfMeasure] PRIMARY KEY ([UnitOfMeasureId]),
    CONSTRAINT [AK_UnitsOfMeasure_TenantId_UnitOfMeasureId] UNIQUE ([TenantId], [UnitOfMeasureId])
);
GO

CREATE TABLE [CustomerContacts] (
    [CustomerContactId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [ContactTypeId] uniqueidentifier NOT NULL,
    [IsPrimary] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_CustomerContacts] PRIMARY KEY ([CustomerContactId]),
    CONSTRAINT [FK_CustomerContacts_ContactTypes_ContactTypeId] FOREIGN KEY ([ContactTypeId]) REFERENCES [ContactTypes] ([ContactTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustomerContacts_Contacts_TenantId_ContactId] FOREIGN KEY ([TenantId], [ContactId]) REFERENCES [Contacts] ([TenantId], [ContactId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustomerContacts_Customers_TenantId_CustomerId] FOREIGN KEY ([TenantId], [CustomerId]) REFERENCES [Customers] ([TenantId], [CustomerId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SupplierContacts] (
    [SupplierContactId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [ContactTypeId] uniqueidentifier NOT NULL,
    [IsPrimary] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SupplierContacts] PRIMARY KEY ([SupplierContactId]),
    CONSTRAINT [FK_SupplierContacts_ContactTypes_ContactTypeId] FOREIGN KEY ([ContactTypeId]) REFERENCES [ContactTypes] ([ContactTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplierContacts_Contacts_TenantId_ContactId] FOREIGN KEY ([TenantId], [ContactId]) REFERENCES [Contacts] ([TenantId], [ContactId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplierContacts_Suppliers_TenantId_SupplierId] FOREIGN KEY ([TenantId], [SupplierId]) REFERENCES [Suppliers] ([TenantId], [SupplierId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [Addresses] (
    [AddressId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AddressName] nvarchar(100) NOT NULL,
    [AddressLine1] nvarchar(250) NOT NULL,
    [AddressLine2] nvarchar(250) NULL,
    [City] nvarchar(100) NOT NULL,
    [StateProvince] nvarchar(100) NULL,
    [PostalCode] nvarchar(20) NULL,
    [CountryId] uniqueidentifier NULL,
    [UnmatchedCountryText] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_Addresses] PRIMARY KEY ([AddressId]),
    CONSTRAINT [AK_Addresses_TenantId_AddressId] UNIQUE ([TenantId], [AddressId]),
    CONSTRAINT [FK_Addresses_Countries_CountryId] FOREIGN KEY ([CountryId]) REFERENCES [Countries] ([CountryId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SupplierProductPrices] (
    [SupplierProductPriceId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [ProductId] uniqueidentifier NOT NULL,
    [SupplierItemCode] nvarchar(50) NULL,
    [UnitPrice] decimal(18,4) NOT NULL,
    [Currency] nchar(3) NOT NULL,
    [UnitOfMeasureId] uniqueidentifier NULL,
    [MinimumOrderQuantity] decimal(18,4) NOT NULL,
    [EffectiveFrom] datetime2 NOT NULL,
    [EffectiveTo] datetime2 NULL,
    [LeadTimeDays] int NULL,
    [IsPreferred] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SupplierProductPrices] PRIMARY KEY ([SupplierProductPriceId]),
    CONSTRAINT [FK_SupplierProductPrices_Products_TenantId_ProductId] FOREIGN KEY ([TenantId], [ProductId]) REFERENCES [Products] ([TenantId], [ProductId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplierProductPrices_Suppliers_TenantId_SupplierId] FOREIGN KEY ([TenantId], [SupplierId]) REFERENCES [Suppliers] ([TenantId], [SupplierId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplierProductPrices_UnitsOfMeasure_TenantId_UnitOfMeasureId] FOREIGN KEY ([TenantId], [UnitOfMeasureId]) REFERENCES [UnitsOfMeasure] ([TenantId], [UnitOfMeasureId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [UnitOfMeasureConversions] (
    [ConversionId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [FromUnitId] uniqueidentifier NOT NULL,
    [ToUnitId] uniqueidentifier NOT NULL,
    [ConversionFactor] decimal(18,6) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_UnitOfMeasureConversions] PRIMARY KEY ([ConversionId]),
    CONSTRAINT [FK_UnitOfMeasureConversions_UnitsOfMeasure_TenantId_FromUnitId] FOREIGN KEY ([TenantId], [FromUnitId]) REFERENCES [UnitsOfMeasure] ([TenantId], [UnitOfMeasureId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_UnitOfMeasureConversions_UnitsOfMeasure_TenantId_ToUnitId] FOREIGN KEY ([TenantId], [ToUnitId]) REFERENCES [UnitsOfMeasure] ([TenantId], [UnitOfMeasureId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [BranchAddresses] (
    [BranchAddressId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [BranchId] uniqueidentifier NOT NULL,
    [AddressId] uniqueidentifier NOT NULL,
    [AddressTypeId] uniqueidentifier NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_BranchAddresses] PRIMARY KEY ([BranchAddressId]),
    CONSTRAINT [FK_BranchAddresses_AddressTypes_AddressTypeId] FOREIGN KEY ([AddressTypeId]) REFERENCES [AddressTypes] ([AddressTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BranchAddresses_Addresses_TenantId_AddressId] FOREIGN KEY ([TenantId], [AddressId]) REFERENCES [Addresses] ([TenantId], [AddressId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_BranchAddresses_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [CompanyAddresses] (
    [CompanyAddressId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NOT NULL,
    [AddressId] uniqueidentifier NOT NULL,
    [AddressTypeId] uniqueidentifier NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_CompanyAddresses] PRIMARY KEY ([CompanyAddressId]),
    CONSTRAINT [FK_CompanyAddresses_AddressTypes_AddressTypeId] FOREIGN KEY ([AddressTypeId]) REFERENCES [AddressTypes] ([AddressTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompanyAddresses_Addresses_TenantId_AddressId] FOREIGN KEY ([TenantId], [AddressId]) REFERENCES [Addresses] ([TenantId], [AddressId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CompanyAddresses_Companies_TenantId_CompanyId] FOREIGN KEY ([TenantId], [CompanyId]) REFERENCES [Companies] ([TenantId], [Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [CustomerAddresses] (
    [CustomerAddressId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CustomerId] uniqueidentifier NOT NULL,
    [AddressId] uniqueidentifier NOT NULL,
    [AddressTypeId] uniqueidentifier NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_CustomerAddresses] PRIMARY KEY ([CustomerAddressId]),
    CONSTRAINT [FK_CustomerAddresses_AddressTypes_AddressTypeId] FOREIGN KEY ([AddressTypeId]) REFERENCES [AddressTypes] ([AddressTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustomerAddresses_Addresses_TenantId_AddressId] FOREIGN KEY ([TenantId], [AddressId]) REFERENCES [Addresses] ([TenantId], [AddressId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CustomerAddresses_Customers_TenantId_CustomerId] FOREIGN KEY ([TenantId], [CustomerId]) REFERENCES [Customers] ([TenantId], [CustomerId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SupplierAddresses] (
    [SupplierAddressId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [AddressId] uniqueidentifier NOT NULL,
    [AddressTypeId] uniqueidentifier NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SupplierAddresses] PRIMARY KEY ([SupplierAddressId]),
    CONSTRAINT [FK_SupplierAddresses_AddressTypes_AddressTypeId] FOREIGN KEY ([AddressTypeId]) REFERENCES [AddressTypes] ([AddressTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplierAddresses_Addresses_TenantId_AddressId] FOREIGN KEY ([TenantId], [AddressId]) REFERENCES [Addresses] ([TenantId], [AddressId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SupplierAddresses_Suppliers_TenantId_SupplierId] FOREIGN KEY ([TenantId], [SupplierId]) REFERENCES [Suppliers] ([TenantId], [SupplierId]) ON DELETE NO ACTION
);
GO

CREATE TABLE [WarehouseAddresses] (
    [WarehouseAddressId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [WarehouseId] uniqueidentifier NOT NULL,
    [AddressId] uniqueidentifier NOT NULL,
    [AddressTypeId] uniqueidentifier NOT NULL,
    [IsDefault] bit NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_WarehouseAddresses] PRIMARY KEY ([WarehouseAddressId]),
    CONSTRAINT [FK_WarehouseAddresses_AddressTypes_AddressTypeId] FOREIGN KEY ([AddressTypeId]) REFERENCES [AddressTypes] ([AddressTypeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseAddresses_Addresses_TenantId_AddressId] FOREIGN KEY ([TenantId], [AddressId]) REFERENCES [Addresses] ([TenantId], [AddressId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WarehouseAddresses_Warehouses_TenantId_WarehouseId] FOREIGN KEY ([TenantId], [WarehouseId]) REFERENCES [Warehouses] ([TenantId], [WarehouseId]) ON DELETE NO ACTION
);
GO

CREATE UNIQUE INDEX [IX_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId] ON [WarehouseZones] ([TenantId], [WarehouseId], [WarehouseZoneId]);
GO

CREATE UNIQUE INDEX [IX_WarehouseZones_TenantId_WarehouseZoneId] ON [WarehouseZones] ([TenantId], [WarehouseZoneId]);
GO

CREATE INDEX [IX_Warehouses_BranchId] ON [Warehouses] ([BranchId]);
GO

CREATE INDEX [IX_Warehouses_TenantId_CompanyId] ON [Warehouses] ([TenantId], [CompanyId]);
GO

CREATE UNIQUE INDEX [IX_Warehouses_TenantId_WarehouseId] ON [Warehouses] ([TenantId], [WarehouseId]);
GO

CREATE INDEX [IX_WarehouseLocations_LocationTypeId] ON [WarehouseLocations] ([LocationTypeId]);
GO

CREATE INDEX [IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId] ON [WarehouseLocations] ([TenantId], [WarehouseId], [WarehouseZoneId], [ParentLocationId]);
GO

CREATE UNIQUE INDEX [IX_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_WarehouseLocationId] ON [WarehouseLocations] ([TenantId], [WarehouseId], [WarehouseZoneId], [WarehouseLocationId]);
GO

CREATE UNIQUE INDEX [IX_WarehouseLocations_TenantId_WarehouseLocationId] ON [WarehouseLocations] ([TenantId], [WarehouseLocationId]);
GO

CREATE UNIQUE INDEX [IX_Suppliers_TenantId_SupplierId] ON [Suppliers] ([TenantId], [SupplierId]);
GO

CREATE INDEX [IX_Products_TenantId_CategoryId] ON [Products] ([TenantId], [CategoryId]);
GO

CREATE UNIQUE INDEX [IX_Products_TenantId_ProductId] ON [Products] ([TenantId], [ProductId]);
GO

CREATE INDEX [IX_Products_TenantId_UnitOfMeasureId] ON [Products] ([TenantId], [UnitOfMeasureId]);
GO

CREATE UNIQUE INDEX [IX_Customers_TenantId_CustomerId] ON [Customers] ([TenantId], [CustomerId]);
GO

CREATE UNIQUE INDEX [IX_Companies_TenantId_Id] ON [Companies] ([TenantId], [Id]);
GO

CREATE UNIQUE INDEX [IX_Categories_TenantId_CategoryId] ON [Categories] ([TenantId], [CategoryId]);
GO

CREATE INDEX [IX_Addresses_CountryId] ON [Addresses] ([CountryId]);
GO

CREATE UNIQUE INDEX [IX_Addresses_TenantId_AddressId] ON [Addresses] ([TenantId], [AddressId]);
GO

CREATE UNIQUE INDEX [IX_AddressTypes_TenantId_Code] ON [AddressTypes] ([TenantId], [Code]) WHERE [TenantId] IS NOT NULL;
GO

CREATE INDEX [IX_BranchAddresses_AddressTypeId] ON [BranchAddresses] ([AddressTypeId]);
GO

CREATE INDEX [IX_BranchAddresses_BranchId] ON [BranchAddresses] ([BranchId]);
GO

CREATE INDEX [IX_BranchAddresses_TenantId_AddressId] ON [BranchAddresses] ([TenantId], [AddressId]);
GO

CREATE UNIQUE INDEX [IX_BranchAddresses_TenantId_BranchId_AddressId_AddressTypeId] ON [BranchAddresses] ([TenantId], [BranchId], [AddressId], [AddressTypeId]);
GO

CREATE UNIQUE INDEX [IX_BranchAddresses_TenantId_BranchId_AddressTypeId] ON [BranchAddresses] ([TenantId], [BranchId], [AddressTypeId]) WHERE [IsDefault] = 1 AND [IsActive] = 1;
GO

CREATE INDEX [IX_CompanyAddresses_AddressTypeId] ON [CompanyAddresses] ([AddressTypeId]);
GO

CREATE INDEX [IX_CompanyAddresses_TenantId_AddressId] ON [CompanyAddresses] ([TenantId], [AddressId]);
GO

CREATE UNIQUE INDEX [IX_CompanyAddresses_TenantId_CompanyId_AddressId_AddressTypeId] ON [CompanyAddresses] ([TenantId], [CompanyId], [AddressId], [AddressTypeId]);
GO

CREATE UNIQUE INDEX [IX_CompanyAddresses_TenantId_CompanyId_AddressTypeId] ON [CompanyAddresses] ([TenantId], [CompanyId], [AddressTypeId]) WHERE [IsDefault] = 1 AND [IsActive] = 1;
GO

CREATE UNIQUE INDEX [IX_Contacts_TenantId_ContactId] ON [Contacts] ([TenantId], [ContactId]);
GO

CREATE UNIQUE INDEX [IX_ContactTypes_TenantId_Code] ON [ContactTypes] ([TenantId], [Code]) WHERE [TenantId] IS NOT NULL;
GO

CREATE UNIQUE INDEX [IX_Countries_Alpha2Code] ON [Countries] ([Alpha2Code]);
GO

CREATE UNIQUE INDEX [IX_Countries_Alpha3Code] ON [Countries] ([Alpha3Code]);
GO

CREATE UNIQUE INDEX [IX_Countries_CountryName] ON [Countries] ([CountryName]);
GO

CREATE INDEX [IX_CustomerAddresses_AddressTypeId] ON [CustomerAddresses] ([AddressTypeId]);
GO

CREATE INDEX [IX_CustomerAddresses_TenantId_AddressId] ON [CustomerAddresses] ([TenantId], [AddressId]);
GO

CREATE UNIQUE INDEX [IX_CustomerAddresses_TenantId_CustomerId_AddressId_AddressTypeId] ON [CustomerAddresses] ([TenantId], [CustomerId], [AddressId], [AddressTypeId]);
GO

CREATE UNIQUE INDEX [IX_CustomerAddresses_TenantId_CustomerId_AddressTypeId] ON [CustomerAddresses] ([TenantId], [CustomerId], [AddressTypeId]) WHERE [IsDefault] = 1 AND [IsActive] = 1;
GO

CREATE INDEX [IX_CustomerContacts_ContactTypeId] ON [CustomerContacts] ([ContactTypeId]);
GO

CREATE INDEX [IX_CustomerContacts_TenantId_ContactId] ON [CustomerContacts] ([TenantId], [ContactId]);
GO

CREATE UNIQUE INDEX [IX_CustomerContacts_TenantId_CustomerId_ContactId_ContactTypeId] ON [CustomerContacts] ([TenantId], [CustomerId], [ContactId], [ContactTypeId]);
GO

CREATE UNIQUE INDEX [IX_CustomerContacts_TenantId_CustomerId_ContactTypeId] ON [CustomerContacts] ([TenantId], [CustomerId], [ContactTypeId]) WHERE [IsPrimary] = 1 AND [IsActive] = 1;
GO

CREATE UNIQUE INDEX [IX_LocationTypes_TenantId_Code] ON [LocationTypes] ([TenantId], [Code]) WHERE [TenantId] IS NOT NULL;
GO

CREATE INDEX [IX_SupplierAddresses_AddressTypeId] ON [SupplierAddresses] ([AddressTypeId]);
GO

CREATE INDEX [IX_SupplierAddresses_TenantId_AddressId] ON [SupplierAddresses] ([TenantId], [AddressId]);
GO

CREATE UNIQUE INDEX [IX_SupplierAddresses_TenantId_SupplierId_AddressId_AddressTypeId] ON [SupplierAddresses] ([TenantId], [SupplierId], [AddressId], [AddressTypeId]);
GO

CREATE UNIQUE INDEX [IX_SupplierAddresses_TenantId_SupplierId_AddressTypeId] ON [SupplierAddresses] ([TenantId], [SupplierId], [AddressTypeId]) WHERE [IsDefault] = 1 AND [IsActive] = 1;
GO

CREATE INDEX [IX_SupplierContacts_ContactTypeId] ON [SupplierContacts] ([ContactTypeId]);
GO

CREATE INDEX [IX_SupplierContacts_TenantId_ContactId] ON [SupplierContacts] ([TenantId], [ContactId]);
GO

CREATE UNIQUE INDEX [IX_SupplierContacts_TenantId_SupplierId_ContactId_ContactTypeId] ON [SupplierContacts] ([TenantId], [SupplierId], [ContactId], [ContactTypeId]);
GO

CREATE UNIQUE INDEX [IX_SupplierContacts_TenantId_SupplierId_ContactTypeId] ON [SupplierContacts] ([TenantId], [SupplierId], [ContactTypeId]) WHERE [IsPrimary] = 1 AND [IsActive] = 1;
GO

CREATE INDEX [IX_SupplierProductPrices_TenantId_ProductId] ON [SupplierProductPrices] ([TenantId], [ProductId]);
GO

CREATE INDEX [IX_SupplierProductPrices_TenantId_SupplierId_ProductId] ON [SupplierProductPrices] ([TenantId], [SupplierId], [ProductId]);
GO

CREATE INDEX [IX_SupplierProductPrices_TenantId_UnitOfMeasureId] ON [SupplierProductPrices] ([TenantId], [UnitOfMeasureId]);
GO

CREATE UNIQUE INDEX [IX_UnitOfMeasureConversions_TenantId_FromUnitId_ToUnitId] ON [UnitOfMeasureConversions] ([TenantId], [FromUnitId], [ToUnitId]);
GO

CREATE INDEX [IX_UnitOfMeasureConversions_TenantId_ToUnitId] ON [UnitOfMeasureConversions] ([TenantId], [ToUnitId]);
GO

CREATE UNIQUE INDEX [IX_UnitsOfMeasure_TenantId_Code] ON [UnitsOfMeasure] ([TenantId], [Code]);
GO

CREATE UNIQUE INDEX [IX_UnitsOfMeasure_TenantId_UnitOfMeasureId] ON [UnitsOfMeasure] ([TenantId], [UnitOfMeasureId]);
GO

CREATE INDEX [IX_WarehouseAddresses_AddressTypeId] ON [WarehouseAddresses] ([AddressTypeId]);
GO

CREATE INDEX [IX_WarehouseAddresses_TenantId_AddressId] ON [WarehouseAddresses] ([TenantId], [AddressId]);
GO

CREATE UNIQUE INDEX [IX_WarehouseAddresses_TenantId_WarehouseId_AddressId_AddressTypeId] ON [WarehouseAddresses] ([TenantId], [WarehouseId], [AddressId], [AddressTypeId]);
GO

CREATE UNIQUE INDEX [IX_WarehouseAddresses_TenantId_WarehouseId_AddressTypeId] ON [WarehouseAddresses] ([TenantId], [WarehouseId], [AddressTypeId]) WHERE [IsDefault] = 1 AND [IsActive] = 1;
GO

ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_Categories_TenantId_CategoryId] FOREIGN KEY ([TenantId], [CategoryId]) REFERENCES [Categories] ([TenantId], [CategoryId]) ON DELETE NO ACTION;
GO

ALTER TABLE [Products] ADD CONSTRAINT [FK_Products_UnitsOfMeasure_TenantId_UnitOfMeasureId] FOREIGN KEY ([TenantId], [UnitOfMeasureId]) REFERENCES [UnitsOfMeasure] ([TenantId], [UnitOfMeasureId]) ON DELETE NO ACTION;
GO

ALTER TABLE [WarehouseLocations] ADD CONSTRAINT [FK_WarehouseLocations_LocationTypes_LocationTypeId] FOREIGN KEY ([LocationTypeId]) REFERENCES [LocationTypes] ([LocationTypeId]) ON DELETE NO ACTION;
GO

ALTER TABLE [WarehouseLocations] ADD CONSTRAINT [FK_WarehouseLocations_WarehouseLocations_TenantId_WarehouseId_WarehouseZoneId_ParentLocationId] FOREIGN KEY ([TenantId], [WarehouseId], [WarehouseZoneId], [ParentLocationId]) REFERENCES [WarehouseLocations] ([TenantId], [WarehouseId], [WarehouseZoneId], [WarehouseLocationId]) ON DELETE NO ACTION;
GO

ALTER TABLE [WarehouseLocations] ADD CONSTRAINT [FK_WarehouseLocations_WarehouseZones_TenantId_WarehouseId_WarehouseZoneId] FOREIGN KEY ([TenantId], [WarehouseId], [WarehouseZoneId]) REFERENCES [WarehouseZones] ([TenantId], [WarehouseId], [WarehouseZoneId]) ON DELETE NO ACTION;
GO

ALTER TABLE [WarehouseLocations] ADD CONSTRAINT [FK_WarehouseLocations_Warehouses_TenantId_WarehouseId] FOREIGN KEY ([TenantId], [WarehouseId]) REFERENCES [Warehouses] ([TenantId], [WarehouseId]) ON DELETE NO ACTION;
GO

ALTER TABLE [Warehouses] ADD CONSTRAINT [FK_Warehouses_Branches_BranchId] FOREIGN KEY ([BranchId]) REFERENCES [Branches] ([Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [Warehouses] ADD CONSTRAINT [FK_Warehouses_Companies_TenantId_CompanyId] FOREIGN KEY ([TenantId], [CompanyId]) REFERENCES [Companies] ([TenantId], [Id]) ON DELETE NO ACTION;
GO

ALTER TABLE [WarehouseZones] ADD CONSTRAINT [FK_WarehouseZones_Warehouses_TenantId_WarehouseId] FOREIGN KEY ([TenantId], [WarehouseId]) REFERENCES [Warehouses] ([TenantId], [WarehouseId]) ON DELETE NO ACTION;
GO

-- Seed Countries
IF NOT EXISTS (SELECT 1 FROM Countries WHERE Alpha2Code = 'LK')
BEGIN
    INSERT INTO Countries (CountryId, Alpha2Code, Alpha3Code, NumericCode, CountryName, OfficialName, IsActive, CreatedAt, CreatedBy)
    VALUES 
    (NEWID(), 'LK', 'LKA', '144', 'Sri Lanka', 'Democratic Socialist Republic of Sri Lanka', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'US', 'USA', '840', 'United States', 'United States of America', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'GB', 'GBR', '826', 'United Kingdom', 'United Kingdom of Great Britain and Northern Ireland', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'IN', 'IND', '356', 'India', 'Republic of India', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'DE', 'DEU', '276', 'Germany', 'Federal Republic of Germany', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'SG', 'SGP', '702', 'Singapore', 'Republic of Singapore', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'AU', 'AUS', '036', 'Australia', 'Commonwealth of Australia', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'AE', 'ARE', '784', 'United Arab Emirates', 'United Arab Emirates', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'JP', 'JPN', '392', 'Japan', 'Japan', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    (NEWID(), 'CN', 'CHN', '156', 'China', 'People''s Republic of China', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END
-- Seed Global AddressTypes
IF NOT EXISTS (SELECT 1 FROM AddressTypes WHERE TenantId IS NULL AND Code = 'GENERAL')
BEGIN
    INSERT INTO AddressTypes (AddressTypeId, TenantId, Code, Name, Description, IsActive, CreatedAt, CreatedBy)
    VALUES
    ('11111111-0000-0000-0000-000000000001', NULL, 'GENERAL', 'General', 'General business address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000002', NULL, 'REG_OFFICE', 'Registered Office', 'Official registered office', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000003', NULL, 'BILLING', 'Billing', 'Billing address for invoices', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000004', NULL, 'SHIPPING', 'Shipping', 'Shipping and delivery address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000005', NULL, 'FACTORY', 'Factory', 'Manufacturing plant address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000006', NULL, 'WAREHOUSE', 'Warehouse', 'Storage warehouse address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000007', NULL, 'BRANCH', 'Branch', 'Branch office address', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000008', NULL, 'PROJECT_SITE', 'Project Site', 'Project operating site', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('11111111-0000-0000-0000-000000000009', NULL, 'OTHER', 'Other', 'Other address type', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END
-- Seed Global ContactTypes
IF NOT EXISTS (SELECT 1 FROM ContactTypes WHERE TenantId IS NULL AND Code = 'GENERAL')
BEGIN
    INSERT INTO ContactTypes (ContactTypeId, TenantId, Code, Name, Description, IsActive, CreatedAt, CreatedBy)
    VALUES
    ('22222222-0000-0000-0000-000000000001', NULL, 'GENERAL', 'General', 'General contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000002', NULL, 'PURCHASING', 'Purchasing', 'Purchasing representative', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000003', NULL, 'SALES', 'Sales', 'Sales representative', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000004', NULL, 'ACCOUNTS', 'Accounts', 'Finance and accounting contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000005', NULL, 'WAREHOUSE', 'Warehouse', 'Warehouse contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('22222222-0000-0000-0000-000000000006', NULL, 'TECHNICAL', 'Technical', 'Technical contact', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END
-- Seed Global LocationTypes
IF NOT EXISTS (SELECT 1 FROM LocationTypes WHERE TenantId IS NULL AND Code = 'RACK')
BEGIN
    INSERT INTO LocationTypes (LocationTypeId, TenantId, Code, Name, Description, CanStoreInventory, CanPick, CanPutAway, CanContainChildren, IsActive, CreatedAt, CreatedBy)
    VALUES
    ('33333333-0000-0000-0000-000000000001', NULL, 'RACK', 'Rack', 'Warehouse rack storage', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000002', NULL, 'SHELF', 'Shelf', 'Shelf in a rack or bay', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000003', NULL, 'BIN', 'Bin', 'Small parts bin', 1, 1, 1, 0, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000004', NULL, 'FLOOR', 'Floor Location', 'Floor area storage', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000005', NULL, 'STAGING', 'Staging Area', 'Staging and buffer area', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'),
    ('33333333-0000-0000-0000-000000000006', NULL, 'CUPBOARD', 'Cupboard', 'Enclosed cupboard or locker', 1, 1, 1, 1, 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000');
END
-- Backfill Product UOM
INSERT INTO UnitsOfMeasure (UnitOfMeasureId, TenantId, Code, Name, Description, IsActive, CreatedAt, CreatedBy)
SELECT NEWID(), p.TenantId, UPPER(LTRIM(RTRIM(p.UnitOfMeasure))), LTRIM(RTRIM(p.UnitOfMeasure)), 'Backfilled from existing products', 1, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000000'
FROM Products p
WHERE p.UnitOfMeasure IS NOT NULL AND LEN(LTRIM(RTRIM(p.UnitOfMeasure))) > 0
  AND NOT EXISTS (
      SELECT 1 FROM UnitsOfMeasure u
      WHERE u.TenantId = p.TenantId AND u.Code = UPPER(LTRIM(RTRIM(p.UnitOfMeasure)))
  )
GROUP BY p.TenantId, UPPER(LTRIM(RTRIM(p.UnitOfMeasure))), LTRIM(RTRIM(p.UnitOfMeasure));
UPDATE p
SET p.UnitOfMeasureId = u.UnitOfMeasureId
FROM Products p
INNER JOIN UnitsOfMeasure u ON u.TenantId = p.TenantId AND u.Code = UPPER(LTRIM(RTRIM(p.UnitOfMeasure)))
WHERE p.UnitOfMeasureId IS NULL;
-- Backfill Customer Addresses
INSERT INTO Addresses (AddressId, TenantId, AddressName, AddressLine1, AddressLine2, City, StateProvince, PostalCode, CountryId, UnmatchedCountryText, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    c.CustomerName + ' - Default Address',
    c.AddressLine1,
    c.AddressLine2,
    c.City,
    NULL,
    c.PostalCode,
    cntry.CountryId,
    CASE WHEN cntry.CountryId IS NULL AND c.Country IS NOT NULL AND LEN(LTRIM(RTRIM(c.Country))) > 0 THEN LTRIM(RTRIM(c.Country)) ELSE NULL END,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
LEFT JOIN Countries cntry ON (cntry.Alpha2Code = UPPER(LTRIM(RTRIM(c.Country))) OR cntry.Alpha3Code = UPPER(LTRIM(RTRIM(c.Country))) OR UPPER(cntry.CountryName) = UPPER(LTRIM(RTRIM(c.Country))))
WHERE c.AddressLine1 IS NOT NULL AND LEN(LTRIM(RTRIM(c.AddressLine1))) > 0
  AND NOT EXISTS (SELECT 1 FROM CustomerAddresses ca WHERE ca.CustomerId = c.CustomerId AND ca.TenantId = c.TenantId);
INSERT INTO CustomerAddresses (CustomerAddressId, TenantId, CustomerId, AddressId, AddressTypeId, IsDefault, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    c.CustomerId,
    a.AddressId,
    '11111111-0000-0000-0000-000000000001', -- GENERAL
    1,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
INNER JOIN Addresses a ON a.TenantId = c.TenantId AND a.AddressName = (c.CustomerName + ' - Default Address')
WHERE NOT EXISTS (SELECT 1 FROM CustomerAddresses ca WHERE ca.CustomerId = c.CustomerId AND ca.TenantId = c.TenantId);
-- Backfill Customer Contacts
INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    CASE WHEN c.ContactPerson IS NOT NULL AND LEN(LTRIM(RTRIM(c.ContactPerson))) > 0 THEN LTRIM(RTRIM(c.ContactPerson)) ELSE c.CustomerName + ' Contact' END,
    NULL,
    c.Email,
    c.Phone,
    NULL,
    NULL,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
WHERE (c.ContactPerson IS NOT NULL AND LEN(LTRIM(RTRIM(c.ContactPerson))) > 0)
   OR (c.Email IS NOT NULL AND LEN(LTRIM(RTRIM(c.Email))) > 0)
   OR (c.Phone IS NOT NULL AND LEN(LTRIM(RTRIM(c.Phone))) > 0)
  AND NOT EXISTS (SELECT 1 FROM CustomerContacts cc WHERE cc.CustomerId = c.CustomerId AND cc.TenantId = c.TenantId);
INSERT INTO CustomerContacts (CustomerContactId, TenantId, CustomerId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
SELECT 
    NEWID(),
    c.TenantId,
    c.CustomerId,
    ct.ContactId,
    '22222222-0000-0000-0000-000000000001', -- GENERAL
    1,
    1,
    SYSUTCDATETIME(),
    '00000000-0000-0000-0000-000000000000'
FROM Customers c
INNER JOIN Contacts ct ON ct.TenantId = c.TenantId AND (ct.ContactName = LTRIM(RTRIM(c.ContactPerson)) OR ct.ContactName = (c.CustomerName + ' Contact'))
WHERE NOT EXISTS (SELECT 1 FROM CustomerContacts cc WHERE cc.CustomerId = c.CustomerId AND cc.TenantId = c.TenantId);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261010045936_AddMasterDataFoundationAndAddressContact', N'8.0.0');
GO

COMMIT;
GO

