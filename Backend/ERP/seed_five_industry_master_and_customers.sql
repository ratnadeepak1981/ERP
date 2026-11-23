-- ==============================================================================
-- ApexERP Five Industry Master Data & Customer Seed Script
-- Idempotent, transaction-safe execution against DomainDB and SecurityDB
-- Preserves all existing records; adds missing industry products and customers.
-- ==============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> [STEP 1] Starting Master Data & Customer Seed Expansion...';

    -- -------------------------------------------------------------------------
    -- Audit & System constants
    -- -------------------------------------------------------------------------
    DECLARE @SysAdminId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
    DECLARE @DefaultPasswordHash NVARCHAR(500) = '100000.3g5Omed6QBYFz1RO5Z9Psg==.BD3pWyB0pDYnQAwKeA9uJptex0N3weUovGL7d6pX3ic='; -- Password: "123"

    -- -------------------------------------------------------------------------
    -- Target Tenants & Companies
    -- -------------------------------------------------------------------------
    DECLARE @TenantMfg UNIQUEIDENTIFIER = '81307250-B0A9-466C-AB9F-0A52AAFBE52C';
    DECLARE @CompanyMfg UNIQUEIDENTIFIER = '6AD8FE26-04B3-4FE2-917B-C9E03F5B7AC6';

    DECLARE @TenantRetail UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
    DECLARE @CompanyRetail UNIQUEIDENTIFIER = 'FFF3A70A-197F-48C6-834B-B8AE891AE3B2';

    DECLARE @TenantWho UNIQUEIDENTIFIER = 'D6BC8282-3048-4799-BA95-C4A0043DA714';
    DECLARE @CompanyWho UNIQUEIDENTIFIER = 'BC936CFE-3441-47AC-BED8-A3870D763B9D';

    DECLARE @TenantPha UNIQUEIDENTIFIER = 'D81B6860-11A5-4804-B888-DD159113D8D1';
    DECLARE @CompanyPha UNIQUEIDENTIFIER = 'ABCABA45-1F57-43C1-9B83-DD8ECEB5DBC1';

    DECLARE @TenantSrv UNIQUEIDENTIFIER = 'E0146DAD-306E-4855-B195-CA70F35B8C26';
    DECLARE @CompanySrv UNIQUEIDENTIFIER = '4CE8CB3C-61E1-4095-864B-6F7618DCA78E';

    -- Helper procedure-like table for categories
    PRINT '>>> [STEP 2] Seeding Manufacturing Categories...';
    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'MACHINERY-COMPONENTS')
        INSERT INTO DomainDB.dbo.Categories (CategoryId, TenantId, CategoryCode, CategoryName, Description, IsActive, CreatedAt, CreatedBy)
        VALUES (NEWID(), @TenantMfg, 'MACHINERY-COMPONENTS', 'Machinery Components', 'Precision engineered machinery components and spindles', 1, SYSUTCDATETIME(), @SysAdminId);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'RAW-HARDWARE')
        INSERT INTO DomainDB.dbo.Categories (CategoryId, TenantId, CategoryCode, CategoryName, Description, IsActive, CreatedAt, CreatedBy)
        VALUES (NEWID(), @TenantMfg, 'RAW-HARDWARE', 'Raw Hardware', 'Industrial fasteners, fixtures, and raw hardware', 1, SYSUTCDATETIME(), @SysAdminId);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'HYDRAULICS')
        INSERT INTO DomainDB.dbo.Categories (CategoryId, TenantId, CategoryCode, CategoryName, Description, IsActive, CreatedAt, CreatedBy)
        VALUES (NEWID(), @TenantMfg, 'HYDRAULICS', 'Hydraulics', 'Hydraulic pressure regulators, pumps, and valves', 1, SYSUTCDATETIME(), @SysAdminId);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'ASSEMBLY-SYSTEMS')
        INSERT INTO DomainDB.dbo.Categories (CategoryId, TenantId, CategoryCode, CategoryName, Description, IsActive, CreatedAt, CreatedBy)
        VALUES (NEWID(), @TenantMfg, 'ASSEMBLY-SYSTEMS', 'Assembly Systems', 'Automated conveyor rollers, drives, and assembly lines', 1, SYSUTCDATETIME(), @SysAdminId);

    -- -------------------------------------------------------------------------
    -- STEP 3: Seeding Manufacturing Products
    -- -------------------------------------------------------------------------
    PRINT '>>> [STEP 3] Seeding Manufacturing Products & Publishing to Ecommerce...';
    DECLARE @CatMach UNIQUEIDENTIFIER = (SELECT CategoryId FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'MACHINERY-COMPONENTS');
    DECLARE @CatHard UNIQUEIDENTIFIER = (SELECT CategoryId FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'RAW-HARDWARE');
    DECLARE @CatHydr UNIQUEIDENTIFIER = (SELECT CategoryId FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'HYDRAULICS');
    DECLARE @CatAsmb UNIQUEIDENTIFIER = (SELECT CategoryId FROM DomainDB.dbo.Categories WHERE TenantId = @TenantMfg AND CategoryCode = 'ASSEMBLY-SYSTEMS');

    -- PROD-AL-001
    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantMfg AND ProductCode = 'PROD-AL-001')
    BEGIN
        DECLARE @p1 UNIQUEIDENTIFIER = NEWID();
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode, LeadTimeDays)
        VALUES (@p1, @TenantMfg, 'PROD-AL-001', 'High Precision CNC Milling Spindle', @CatMach, 'Lead Time: 3-5 Business Days', 'EA', 1, 1, 1, 1, 980.00, 1250.00, 5, 10, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 2, 5);
    END;

    -- PROD-AL-002
    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantMfg AND ProductCode = 'PROD-AL-002')
    BEGIN
        DECLARE @p2 UNIQUEIDENTIFIER = NEWID();
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode, LeadTimeDays)
        VALUES (@p2, @TenantMfg, 'PROD-AL-002', 'Titanium Grade 5 Hex Fasteners (Pack of 100)', @CatHard, 'Lead Time: Immediate Dispatch', 'BOX', 0, 1, 1, 1, 140.00, 185.50, 20, 50, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 1, 1);
    END;

    -- PROD-AL-003
    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantMfg AND ProductCode = 'PROD-AL-003')
    BEGIN
        DECLARE @p3 UNIQUEIDENTIFIER = NEWID();
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode, LeadTimeDays)
        VALUES (@p3, @TenantMfg, 'PROD-AL-003', 'Hydraulic Pressure Regulator Valve 350 Bar', @CatHydr, 'Lead Time: 10-14 Business Days', 'EA', 1, 1, 1, 1, 480.00, 620.00, 5, 10, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 2, 14);
    END;

    -- PROD-AL-004
    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantMfg AND ProductCode = 'PROD-AL-004')
    BEGIN
        DECLARE @p4 UNIQUEIDENTIFIER = NEWID();
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode, LeadTimeDays)
        VALUES (@p4, @TenantMfg, 'PROD-AL-004', 'Automated Conveyor Roller Assembly 2m', @CatAsmb, 'Lead Time: 5-7 Business Days', 'EA', 1, 1, 1, 1, 360.00, 480.00, 5, 10, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 1, 7);
    END;

    -- Additional standard manufacturing products
    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantMfg AND ProductCode = 'MFG-001')
    BEGIN
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode, LeadTimeDays)
        VALUES (NEWID(), @TenantMfg, 'MFG-001', 'Steel Sheet 2mm (Commercial Grade)', @CatHard, 'Lead Time: 2-3 Business Days', 'KG', 0, 1, 1, 1, 950.00, 1250.00, 100, 500, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 1, 3);
    END;

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantMfg AND ProductCode = 'MFG-004')
    BEGIN
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode, LeadTimeDays)
        VALUES (NEWID(), @TenantMfg, 'MFG-004', 'Electric Drive Motor 1HP 3-Phase', @CatMach, 'Lead Time: 5 Business Days', 'EA', 0, 1, 1, 1, 18500.00, 23000.00, 10, 25, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 2, 5);
    END;

    -- Publish all active sellable products for Manufacturing company
    INSERT INTO DomainDB.dbo.EcommerceProducts (Id, TenantId, CompanyId, ProductId, IsPublished, OnlineDescription, DisplayOrder, CreatedAtUtc, UpdatedAtUtc, CreatedAt, CreatedBy)
    SELECT
        NEWID(),
        p.TenantId,
        @CompanyMfg,
        p.ProductId,
        1,
        ISNULL(p.Description, p.ProductName),
        ROW_NUMBER() OVER (ORDER BY p.ProductCode),
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        @SysAdminId
    FROM DomainDB.dbo.Products p
    WHERE p.TenantId = @TenantMfg
      AND p.IsActive = 1
      AND p.IsSellable = 1
      AND NOT EXISTS (
          SELECT 1 FROM DomainDB.dbo.EcommerceProducts ep
          WHERE ep.TenantId = p.TenantId
            AND ep.CompanyId = @CompanyMfg
            AND ep.ProductId = p.ProductId
      );

    -- Ensure all existing are set to IsPublished = 1
    UPDATE DomainDB.dbo.EcommerceProducts
    SET IsPublished = 1
    WHERE TenantId = @TenantMfg AND CompanyId = @CompanyMfg;

    -- -------------------------------------------------------------------------
    -- STEP 4: Seed Retail Products (RET-006 to RET-010) & Publish
    -- -------------------------------------------------------------------------
    PRINT '>>> [STEP 4] Seeding Extended Retail Products & Publishing...';
    DECLARE @CatRetailStaple UNIQUEIDENTIFIER = (SELECT TOP 1 CategoryId FROM DomainDB.dbo.Categories WHERE TenantId = @TenantRetail AND CategoryCode = 'STAPLE-FOODS');
    IF @CatRetailStaple IS NULL SET @CatRetailStaple = (SELECT TOP 1 CategoryId FROM DomainDB.dbo.Categories WHERE TenantId = @TenantRetail);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantRetail AND ProductCode = 'RET-006')
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode)
        VALUES (NEWID(), @TenantRetail, 'RET-006', 'Fresh Dairy Milk 1L', @CatRetailStaple, 'Pasteurized fresh milk 1L bottle', 'BTL', 0, 1, 1, 1, 350.00, 450.00, 20, 100, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 1);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantRetail AND ProductCode = 'RET-007')
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode)
        VALUES (NEWID(), @TenantRetail, 'RET-007', 'All Purpose Wheat Flour 1kg', @CatRetailStaple, 'Premium all-purpose baking flour 1kg', 'PKT', 0, 1, 1, 1, 280.00, 350.00, 30, 150, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 1);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantRetail AND ProductCode = 'RET-008')
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode)
        VALUES (NEWID(), @TenantRetail, 'RET-008', 'Laundry Detergent Powder 1kg', @CatRetailStaple, 'Color-safe concentrated washing powder', 'PKT', 0, 1, 1, 1, 650.00, 800.00, 25, 100, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 0);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantRetail AND ProductCode = 'RET-009')
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode)
        VALUES (NEWID(), @TenantRetail, 'RET-009', 'Toilet Tissue Roll 2-Ply', @CatRetailStaple, 'Soft absorbent bathroom tissue', 'PCS', 0, 1, 1, 1, 120.00, 160.00, 50, 200, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 0);

    IF NOT EXISTS (SELECT 1 FROM DomainDB.dbo.Products WHERE TenantId = @TenantRetail AND ProductCode = 'RET-010')
        INSERT INTO DomainDB.dbo.Products (ProductId, TenantId, ProductCode, ProductName, CategoryId, Description, UnitOfMeasure, IsManufacturable, IsPurchasable, IsSellable, ValuationMethod, StandardCost, SellingPrice, ReorderLevel, ReorderQuantity, IsActive, CreatedAt, CreatedBy, IsStockTracked, ProductType, TrackingMode)
        VALUES (NEWID(), @TenantRetail, 'RET-010', 'Plastic Food Storage Container 1.5L', @CatRetailStaple, 'BPA-free airtight food container', 'PCS', 0, 1, 1, 1, 300.00, 450.00, 20, 80, 1, SYSUTCDATETIME(), @SysAdminId, 1, 1, 0);

    -- Publish all active sellable products for Retail company
    INSERT INTO DomainDB.dbo.EcommerceProducts (Id, TenantId, CompanyId, ProductId, IsPublished, OnlineDescription, DisplayOrder, CreatedAtUtc, UpdatedAtUtc, CreatedAt, CreatedBy)
    SELECT
        NEWID(),
        p.TenantId,
        @CompanyRetail,
        p.ProductId,
        1,
        ISNULL(p.Description, p.ProductName),
        ROW_NUMBER() OVER (ORDER BY p.ProductCode),
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        @SysAdminId
    FROM DomainDB.dbo.Products p
    WHERE p.TenantId = @TenantRetail
      AND p.IsActive = 1
      AND p.IsSellable = 1
      AND NOT EXISTS (
          SELECT 1 FROM DomainDB.dbo.EcommerceProducts ep
          WHERE ep.TenantId = p.TenantId
            AND ep.CompanyId = @CompanyRetail
            AND ep.ProductId = p.ProductId
      );

    UPDATE DomainDB.dbo.EcommerceProducts
    SET IsPublished = 1
    WHERE TenantId = @TenantRetail AND CompanyId = @CompanyRetail;

    -- -------------------------------------------------------------------------
    -- STEP 5: Customer Seed Helper Table
    -- -------------------------------------------------------------------------
    PRINT '>>> [STEP 5] Seeding 25 Customers, Contacts, Addresses & User Accounts...';

    DECLARE @CustomerSeedTable TABLE (
        TenantId UNIQUEIDENTIFIER,
        CustomerCode NVARCHAR(50),
        CustomerName NVARCHAR(200),
        TradingName NVARCHAR(200),
        TaxNumber NVARCHAR(50),
        Email NVARCHAR(150),
        Phone NVARCHAR(50),
        AddressLine1 NVARCHAR(200),
        City NVARCHAR(100),
        PostalCode NVARCHAR(20),
        Country NVARCHAR(100),
        PaymentTerms NVARCHAR(50),
        CustomerType NVARCHAR(50),
        ApprovalStatus NVARCHAR(50),
        ContactPerson NVARCHAR(100),
        UserRole NVARCHAR(100)
    );

    -- 1. Retail Profile Customers
    INSERT INTO @CustomerSeedTable VALUES
    (@TenantRetail, 'CUST-RET-IND01', 'Nuwan Bandara', NULL, NULL, 'nuwan.retail@apexerp.local', '+94 77 234 5601', 'No. 12, Galle Road', 'Colombo', '00300', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Nuwan Bandara', 'CUSTOMER_INDIVIDUAL'),
    (@TenantRetail, 'CUST-RET-IND02', 'Anoma Jayasinghe', NULL, NULL, 'anoma.retail@apexerp.local', '+94 77 234 5602', 'No. 45, Station Road', 'Dehiwala', '10350', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Anoma Jayasinghe', 'CUSTOMER_INDIVIDUAL'),
    (@TenantRetail, 'CUST-RET-001', 'Cargills Supermarkets PLC', 'Cargills Food City', 'V102938475', 'procurement@cargillsretail.lk', '+94 11 456 7890', 'No. 40, York Street, Colombo 01', 'Colombo', '00100', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Nimal Fernando', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantRetail, 'CUST-RET-002', 'Metro Supermarket Chain Ltd', 'Metro Mart', 'V582910472', 'orders@metromart.local', '+94 11 456 7892', 'No. 88, Kandy Road', 'Kadawatha', '11850', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Pradeep Samarasinghe', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantRetail, 'CUST-RET-003', 'Green Garden Cafes (Pvt) Ltd', 'Green Garden Cafe', 'V918273645', 'purchasing@greengarden.local', '+94 11 456 7893', 'No. 15, Park Street', 'Colombo', '00200', 'Sri Lanka', 'NET30', 'Company', 'Pending', 'Malini Senanayake', 'CUSTOMER_COMPANY_ADMIN'),

    -- 2. Manufacturing Profile Customers
    (@TenantMfg, 'CUST-MFG-RET01', 'Kasun Perera', NULL, NULL, 'kasun.mfg@apexerp.local', '+94 77 123 4501', 'No. 18, Industrial Estate Road', 'Moratuwa', '10400', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Kasun Perera', 'CUSTOMER_INDIVIDUAL'),
    (@TenantMfg, 'CUST-MFG-RET02', 'Dilani Fernando', NULL, NULL, 'dilani.mfg@apexerp.local', '+94 77 123 4502', 'No. 32, Factory Lane', 'Panadura', '12500', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Dilani Fernando', 'CUSTOMER_INDIVIDUAL'),
    (@TenantMfg, 'CUST-MFG-001', 'Lanka Engineering Projects (Pvt) Ltd', 'Lanka Engineering', 'V847291038', 'engineering@mfgcorp.local', '+94 11 300 0000', 'No. 25, Main Street', 'Colombo', '00200', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Sunil Jayawardena', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantMfg, 'CUST-MFG-002', 'Apex Precision Dynamics (Pvt) Ltd', 'Apex Precision', 'V192837465', 'procurement@apexprecision.local', '+94 11 300 0002', 'No. 70, Industrial Zone', 'Biyagama', '11672', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Nimal Wickramasinghe', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantMfg, 'CUST-MFG-003', 'Southern Fabrications & Tooling Ltd', 'Southern Tooling', 'V675849302', 'admin@southernfab.local', '+94 11 300 0003', 'No. 12, Heavy Industry Park', 'Sapugaskanda', '11654', 'Sri Lanka', 'NET30', 'Company', 'Pending', 'Roshan Silva', 'CUSTOMER_COMPANY_ADMIN'),

    -- 3. Wholesale Profile Customers
    (@TenantWho, 'CUST-WHO-RET01', 'Suresh Jayakody', NULL, NULL, 'suresh.wholesale@apexerp.local', '+94 77 345 6701', 'No. 55, Main Street', 'Kelaniya', '11600', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Suresh Jayakody', 'CUSTOMER_INDIVIDUAL'),
    (@TenantWho, 'CUST-WHO-RET02', 'Tharanga Mendis', NULL, NULL, 'tharanga.wholesale@apexerp.local', '+94 77 345 6702', 'No. 88, Kandy Road', 'Kiribathgoda', '11600', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Tharanga Mendis', 'CUSTOMER_INDIVIDUAL'),
    (@TenantWho, 'CUST-WHO-001', 'Northern Retail Stores (Pvt) Ltd', 'Northern Stores', 'V384729105', 'orders@cityretail.local', '+94 21 220 0000', 'No. 15, Market Road', 'Jaffna', '40000', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Bandula Gunawardena', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantWho, 'CUST-WHO-002', 'Lanka Wholesale Distributors Ltd', 'Lanka Wholesale', 'V726154839', 'logistics@lankadist.local', '+94 11 410 0002', 'No. 120, Harbor Road', 'Colombo', '01300', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Janaka Alwis', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantWho, 'CUST-WHO-003', 'Southern Grocers Cooperative', 'Southern Grocers', 'V593817264', 'procure@southerngrocers.local', '+94 91 223 4567', 'No. 40, Fort Road', 'Galle', '80000', 'Sri Lanka', 'NET30', 'Company', 'Pending', 'Dammika Karunaratne', 'CUSTOMER_COMPANY_ADMIN'),

    -- 4. Pharmacy Profile Customers
    (@TenantPha, 'CUST-PHA-RET01', 'Ruwan Gunasekara', NULL, NULL, 'ruwan.pharmacy@apexerp.local', '+94 77 456 7801', 'No. 14, Ward Place', 'Colombo', '00700', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Ruwan Gunasekara', 'CUSTOMER_INDIVIDUAL'),
    (@TenantPha, 'CUST-PHA-RET02', 'Sanduni Wijesinghe', NULL, NULL, 'sanduni.pharmacy@apexerp.local', '+94 77 456 7802', 'No. 80, Cotta Road', 'Rajagiriya', '10107', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Sanduni Wijesinghe', 'CUSTOMER_INDIVIDUAL'),
    (@TenantPha, 'CUST-PHA-001', 'Northern Community Pharmacy', 'Community Pharmacy', 'V473829105', 'procure@medicare.local', '+94 21 220 1000', 'No. 25, Hospital Road', 'Jaffna', '40000', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Dr. Asela Peiris', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantPha, 'CUST-PHA-002', 'CareFirst Pharmacy Network Ltd', 'CareFirst Health', 'V847291045', 'admin@carefirst.local', '+94 11 220 1002', 'No. 120, High Level Road', 'Nugegoda', '10250', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Kavinda Dissanayake', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantPha, 'CUST-PHA-003', 'Wellness Clinic & Surgical Care', 'Wellness Clinic', 'V192837456', 'supplies@wellnesscare.local', '+94 11 220 1003', 'No. 55, Havelock Road', 'Colombo', '00500', 'Sri Lanka', 'NET30', 'Company', 'Pending', 'Dr. Kumara Senaratne', 'CUSTOMER_COMPANY_ADMIN'),

    -- 5. Service Profile Customers
    (@TenantSrv, 'CUST-SRV-RET01', 'Mahesh Rodrigo', NULL, NULL, 'mahesh.service@apexerp.local', '+94 77 567 8901', 'No. 22, Havelock City', 'Colombo', '00500', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Mahesh Rodrigo', 'CUSTOMER_INDIVIDUAL'),
    (@TenantSrv, 'CUST-SRV-RET02', 'Nirosha Weerakkody', NULL, NULL, 'nirosha.service@apexerp.local', '+94 77 567 8902', 'No. 70, Nawala Road', 'Nawala', '10107', 'Sri Lanka', 'IMMEDIATE', 'Retail', 'Approved', 'Nirosha Weerakkody', 'CUSTOMER_INDIVIDUAL'),
    (@TenantSrv, 'CUST-SRV-001', 'Lanka Business Solutions (Pvt) Ltd', 'Lanka Business Solutions', 'V847291039', 'facility@colombotowers.local', '+94 11 440 0000', 'No. 75, Business Park', 'Colombo', '00500', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Upul Priyantha', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantSrv, 'CUST-SRV-002', 'Premier Facility Management (Pvt) Ltd', 'Premier Facilities', 'V192837461', 'operations@premierfacilities.local', '+94 11 440 0002', 'No. 120, Galle Road', 'Colombo', '00300', 'Sri Lanka', 'NET30', 'Company', 'Approved', 'Lasantha Abeyratne', 'CUSTOMER_COMPANY_ADMIN'),
    (@TenantSrv, 'CUST-SRV-003', 'Apex Hospitality Services Ltd', 'Apex Hospitality', 'V473829108', 'service@apexhospitality.local', '+94 11 440 0003', 'No. 40, Airport Road', 'Katunayake', '11450', 'Sri Lanka', 'NET30', 'Company', 'Pending', 'Gayan Mendis', 'CUSTOMER_COMPANY_ADMIN');

    -- Iterate and insert customers, contacts, addresses, users, roles
    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT TenantId, CustomerCode, CustomerName, TradingName, TaxNumber, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, CustomerType, ApprovalStatus, ContactPerson, UserRole
        FROM @CustomerSeedTable;

    DECLARE @tId UNIQUEIDENTIFIER, @cCode NVARCHAR(50), @cName NVARCHAR(200), @tName NVARCHAR(200), @tNum NVARCHAR(50), @em NVARCHAR(150), @ph NVARCHAR(50);
    DECLARE @a1 NVARCHAR(200), @city NVARCHAR(100), @post NVARCHAR(20), @ctry NVARCHAR(100), @terms NVARCHAR(50), @cType NVARCHAR(50), @appr NVARCHAR(50), @cp NVARCHAR(100), @uRole NVARCHAR(100);

    OPEN cur;
    FETCH NEXT FROM cur INTO @tId, @cCode, @cName, @tName, @tNum, @em, @ph, @a1, @city, @post, @ctry, @terms, @cType, @appr, @cp, @uRole;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Resolve Currency & Price Card
        DECLARE @currId UNIQUEIDENTIFIER = (SELECT TOP 1 CurrencyId FROM DomainDB.dbo.Currencies WHERE TenantId = @tId AND CurrencyCode = 'LKR');
        DECLARE @spcId UNIQUEIDENTIFIER = (SELECT TOP 1 PriceCardId FROM DomainDB.dbo.PriceCards WHERE TenantId = @tId AND PriceCardType = 2 AND IsActive = 1);

        -- Resolve or create AddressType & ContactType
        DECLARE @addrTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 AddressTypeId FROM DomainDB.dbo.AddressTypes WHERE (TenantId = @tId OR TenantId IS NULL) AND Code = 'PRIMARY');
        IF @addrTypeId IS NULL
        BEGIN
            SET @addrTypeId = NEWID();
            INSERT INTO DomainDB.dbo.AddressTypes (AddressTypeId, TenantId, Code, Name, IsActive, CreatedAt, CreatedBy)
            VALUES (@addrTypeId, @tId, 'PRIMARY', 'Primary Address', 1, SYSUTCDATETIME(), @SysAdminId);
        END;

        DECLARE @contactTypeId UNIQUEIDENTIFIER = (SELECT TOP 1 ContactTypeId FROM DomainDB.dbo.ContactTypes WHERE (TenantId = @tId OR TenantId IS NULL) AND Code = 'PRIMARY');
        IF @contactTypeId IS NULL
        BEGIN
            SET @contactTypeId = NEWID();
            INSERT INTO DomainDB.dbo.ContactTypes (ContactTypeId, TenantId, Code, Name, IsActive, CreatedAt, CreatedBy)
            VALUES (@contactTypeId, @tId, 'PRIMARY', 'Primary Contact', 1, SYSUTCDATETIME(), @SysAdminId);
        END;

        -- 1. Customer
        DECLARE @customerId UNIQUEIDENTIFIER = (SELECT CustomerId FROM DomainDB.dbo.Customers WHERE TenantId = @tId AND CustomerCode = @cCode);
        IF @customerId IS NULL
        BEGIN
            SET @customerId = NEWID();
            INSERT INTO DomainDB.dbo.Customers (CustomerId, TenantId, CustomerCode, CustomerName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, TaxNumber, CreditLimit, PaymentTerms, IsActive, CreatedAt, CreatedBy, DefaultSalesPriceCardId, CurrencyId)
            VALUES (@customerId, @tId, @cCode, @cName, @cp, @em, @ph, @a1, @city, @post, @ctry, @tNum, 500000.00, @terms, CASE WHEN @appr = 'Pending' THEN 0 ELSE 1 END, SYSUTCDATETIME(), @SysAdminId, @spcId, @currId);
        END
        ELSE
        BEGIN
            UPDATE DomainDB.dbo.Customers
            SET CustomerName = @cName, ContactPerson = @cp, Email = @em, Phone = @ph, AddressLine1 = @a1, City = @city, PostalCode = @post, Country = @ctry,
                TaxNumber = ISNULL(@tNum, TaxNumber), PaymentTerms = @terms, DefaultSalesPriceCardId = ISNULL(DefaultSalesPriceCardId, @spcId), CurrencyId = ISNULL(CurrencyId, @currId)
            WHERE CustomerId = @customerId;
        END;

        -- 2. Address & CustomerAddress
        DECLARE @addressId UNIQUEIDENTIFIER = (SELECT TOP 1 a.AddressId FROM DomainDB.dbo.Addresses a JOIN DomainDB.dbo.CustomerAddresses ca ON a.AddressId = ca.AddressId WHERE ca.CustomerId = @customerId);
        IF @addressId IS NULL
        BEGIN
            SET @addressId = NEWID();
            INSERT INTO DomainDB.dbo.Addresses (AddressId, TenantId, AddressName, AddressLine1, City, PostalCode, UnmatchedCountryText, IsActive, CreatedAt, CreatedBy)
            VALUES (@addressId, @tId, @cName + ' - Main Address', @a1, @city, @post, @ctry, 1, SYSUTCDATETIME(), @SysAdminId);

            INSERT INTO DomainDB.dbo.CustomerAddresses (CustomerAddressId, TenantId, CustomerId, AddressId, AddressTypeId, IsDefault, IsActive, CreatedAt, CreatedBy)
            VALUES (NEWID(), @tId, @customerId, @addressId, @addrTypeId, 1, 1, SYSUTCDATETIME(), @SysAdminId);
        END;

        -- 3. Contact & CustomerContact
        DECLARE @contactId UNIQUEIDENTIFIER = (SELECT TOP 1 c.ContactId FROM DomainDB.dbo.Contacts c JOIN DomainDB.dbo.CustomerContacts cc ON c.ContactId = cc.ContactId WHERE cc.CustomerId = @customerId);
        IF @contactId IS NULL
        BEGIN
            SET @contactId = NEWID();
            INSERT INTO DomainDB.dbo.Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, IsActive, CreatedAt, CreatedBy)
            VALUES (@contactId, @tId, @cp, CASE WHEN @cType = 'Retail' THEN 'Individual Account Holder' ELSE 'Commercial Purchasing Officer' END, @em, @ph, 1, SYSUTCDATETIME(), @SysAdminId);

            INSERT INTO DomainDB.dbo.CustomerContacts (CustomerContactId, TenantId, CustomerId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
            VALUES (NEWID(), @tId, @customerId, @contactId, @contactTypeId, 1, 1, SYSUTCDATETIME(), @SysAdminId);
        END;

        -- 4. Security User & Role
        DECLARE @userId UNIQUEIDENTIFIER = (SELECT Id FROM SecurityDB.dbo.Users WHERE Email = @em OR Username = @em);
        IF @userId IS NULL
        BEGIN
            SET @userId = NEWID();
            INSERT INTO SecurityDB.dbo.Users (Id, TenantId, Username, Email, PasswordHash, IsActive, CreatedAt, CreatedBy)
            VALUES (@userId, @tId, @em, @em, @DefaultPasswordHash, 1, SYSUTCDATETIME(), @SysAdminId);
        END;

        -- Resolve Role in SecurityDB
        DECLARE @roleId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM SecurityDB.dbo.Roles WHERE TenantId = @tId AND (TemplateKey = @uRole OR Name = @uRole));
        IF @roleId IS NULL
        BEGIN
            -- Global fallback role
            SET @roleId = (SELECT TOP 1 Id FROM SecurityDB.dbo.Roles WHERE TemplateKey = @uRole OR Name = @uRole);
        END;

        IF @roleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM SecurityDB.dbo.UserRoles WHERE UserId = @userId AND RoleId = @roleId)
        BEGIN
            INSERT INTO SecurityDB.dbo.UserRoles (Id, UserId, RoleId, CreatedAt, CreatedBy)
            VALUES (NEWID(), @userId, @roleId, SYSUTCDATETIME(), @SysAdminId);
        END;

        FETCH NEXT FROM cur INTO @tId, @cCode, @cName, @tName, @tNum, @em, @ph, @a1, @city, @post, @ctry, @terms, @cType, @appr, @cp, @uRole;
    END;

    CLOSE cur;
    DEALLOCATE cur;

    COMMIT TRANSACTION;
    PRINT '>>> [SUCCESS] All Master Data & Customers successfully seeded and committed!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    PRINT '>>> [ERROR] Seeding failed: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, 1);
END CATCH;
