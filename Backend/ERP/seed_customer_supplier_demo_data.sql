-- ==============================================================================
-- ApexERP — Development Seed Data Update: Customers, Suppliers & Customer Users
-- Idempotent script for development database (DomainDB & SecurityDB)
-- ==============================================================================

USE DomainDB;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '25ADAB1D-6E1A-4820-A078-DD7AAE6AA7F0';
DECLARE @CompanyId UNIQUEIDENTIFIER = 'B3BA45AE-D214-43C6-A9A3-05CB37661F9F';
DECLARE @CurrencyId UNIQUEIDENTIFIER = '44786ED6-B83A-4A12-A3E4-BBB4977759FB';
DECLARE @DefaultSalesPriceCardId UNIQUEIDENTIFIER = 'D5D50698-8D35-4E64-91B8-8740D77CEB46';
DECLARE @DefaultPurchasePriceCardId UNIQUEIDENTIFIER = 'CA5918A4-906D-4883-B4B5-F27D1FE32C27';
DECLARE @ContactTypeId UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

-- -----------------------------------------------------------------------------
-- 1. SUPPLIER MASTER RECORDS (3 Meaningful Suppliers)
-- -----------------------------------------------------------------------------

-- Supplier 1: Lanka Retail Distributors (Pvt) Ltd
IF EXISTS (SELECT 1 FROM Suppliers WHERE TenantId = @TenantId AND (SupplierId = '8F25F678-FF9C-4BA1-ABF3-63DADA843B00' OR SupplierCode = 'SUP-RET-001'))
BEGIN
    UPDATE Suppliers
    SET SupplierCode = 'SUP-RET-001',
        SupplierName = 'Lanka Retail Distributors (Pvt) Ltd',
        ContactPerson = 'Deepak Ratna',
        Email = 'ratnadeepak1981@gmail.com',
        Phone = '+94 11 234 5678',
        AddressLine1 = 'No. 250, Industrial Zone, Orugodawatta',
        City = 'Colombo',
        PostalCode = '00300',
        Country = 'Sri Lanka',
        PaymentTerms = 'NET30',
        DefaultPurchasePriceCardId = @DefaultPurchasePriceCardId,
        CurrencyId = @CurrencyId,
        IsActive = 1
    WHERE TenantId = @TenantId AND (SupplierId = '8F25F678-FF9C-4BA1-ABF3-63DADA843B00' OR SupplierCode = 'SUP-RET-001');
END
ELSE
BEGIN
    INSERT INTO Suppliers (SupplierId, TenantId, SupplierCode, SupplierName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, DefaultPurchasePriceCardId, CurrencyId, IsActive, CreatedAt, CreatedBy)
    VALUES ('8F25F678-FF9C-4BA1-ABF3-63DADA843B00', @TenantId, 'SUP-RET-001', 'Lanka Retail Distributors (Pvt) Ltd', 'Deepak Ratna', 'ratnadeepak1981@gmail.com', '+94 11 234 5678', 'No. 250, Industrial Zone, Orugodawatta', 'Colombo', '00300', 'Sri Lanka', 'NET30', @DefaultPurchasePriceCardId, @CurrencyId, 1, SYSUTCDATETIME(), @SystemUserId);
END

-- Supplier 1 Contact
IF NOT EXISTS (SELECT 1 FROM Contacts WHERE TenantId = @TenantId AND ContactId = 'C0010001-25AD-4820-A078-000000000001')
BEGIN
    INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
    VALUES ('C0010001-25AD-4820-A078-000000000001', @TenantId, 'Deepak Ratna', 'Key Account Manager', 'ratnadeepak1981@gmail.com', '+94 11 234 5678', '+94 77 123 4567', 'Email', 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Contacts
    SET ContactName = 'Deepak Ratna',
        Email = 'ratnadeepak1981@gmail.com',
        Phone = '+94 11 234 5678',
        JobTitle = 'Key Account Manager',
        IsActive = 1
    WHERE TenantId = @TenantId AND ContactId = 'C0010001-25AD-4820-A078-000000000001';
END

-- Supplier 1 Contact Link
IF NOT EXISTS (SELECT 1 FROM SupplierContacts WHERE TenantId = @TenantId AND SupplierId = '8F25F678-FF9C-4BA1-ABF3-63DADA843B00' AND ContactId = 'C0010001-25AD-4820-A078-000000000001')
BEGIN
    INSERT INTO SupplierContacts (SupplierContactId, TenantId, SupplierId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), @TenantId, '8F25F678-FF9C-4BA1-ABF3-63DADA843B00', 'C0010001-25AD-4820-A078-000000000001', @ContactTypeId, 1, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE SupplierContacts
    SET IsPrimary = 1, IsActive = 1
    WHERE TenantId = @TenantId AND SupplierId = '8F25F678-FF9C-4BA1-ABF3-63DADA843B00' AND ContactId = 'C0010001-25AD-4820-A078-000000000001';
END

-- Supplier 2: Prime Ceylon Provisions (Pvt) Ltd
IF EXISTS (SELECT 1 FROM Suppliers WHERE TenantId = @TenantId AND (SupplierId = '9F2E1CB1-FE26-4D29-B908-1A73D1B113A1' OR SupplierCode = 'SUP-RET-002' OR SupplierCode = '23232'))
BEGIN
    UPDATE Suppliers
    SET SupplierCode = 'SUP-RET-002',
        SupplierName = 'Prime Ceylon Provisions (Pvt) Ltd',
        ContactPerson = 'Pathmanathan Deepak',
        Email = 'pathmanathanratnadeepak@gmail.com',
        Phone = '+94 11 345 6789',
        AddressLine1 = 'No. 85, Baseline Road, Dematagoda',
        City = 'Colombo',
        PostalCode = '00900',
        Country = 'Sri Lanka',
        PaymentTerms = 'NET30',
        DefaultPurchasePriceCardId = @DefaultPurchasePriceCardId,
        CurrencyId = @CurrencyId,
        IsActive = 1
    WHERE TenantId = @TenantId AND (SupplierId = '9F2E1CB1-FE26-4D29-B908-1A73D1B113A1' OR SupplierCode = 'SUP-RET-002' OR SupplierCode = '23232');
END
ELSE
BEGIN
    INSERT INTO Suppliers (SupplierId, TenantId, SupplierCode, SupplierName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, DefaultPurchasePriceCardId, CurrencyId, IsActive, CreatedAt, CreatedBy)
    VALUES ('9F2E1CB1-FE26-4D29-B908-1A73D1B113A1', @TenantId, 'SUP-RET-002', 'Prime Ceylon Provisions (Pvt) Ltd', 'Pathmanathan Deepak', 'pathmanathanratnadeepak@gmail.com', '+94 11 345 6789', 'No. 85, Baseline Road, Dematagoda', 'Colombo', '00900', 'Sri Lanka', 'NET30', @DefaultPurchasePriceCardId, @CurrencyId, 1, SYSUTCDATETIME(), @SystemUserId);
END

-- Supplier 2 Contact
IF NOT EXISTS (SELECT 1 FROM Contacts WHERE TenantId = @TenantId AND ContactId = '09B6FCDA-FB13-48A5-A86B-3AAD92562386')
BEGIN
    INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
    VALUES ('09B6FCDA-FB13-48A5-A86B-3AAD92562386', @TenantId, 'Pathmanathan Deepak', 'Sales Director', 'pathmanathanratnadeepak@gmail.com', '+94 11 345 6789', '+94 77 234 5678', 'Email', 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Contacts
    SET ContactName = 'Pathmanathan Deepak',
        Email = 'pathmanathanratnadeepak@gmail.com',
        Phone = '+94 11 345 6789',
        JobTitle = 'Sales Director',
        IsActive = 1
    WHERE TenantId = @TenantId AND ContactId = '09B6FCDA-FB13-48A5-A86B-3AAD92562386';
END

-- Supplier 2 Contact Link
IF NOT EXISTS (SELECT 1 FROM SupplierContacts WHERE TenantId = @TenantId AND SupplierId = '9F2E1CB1-FE26-4D29-B908-1A73D1B113A1' AND ContactId = '09B6FCDA-FB13-48A5-A86B-3AAD92562386')
BEGIN
    INSERT INTO SupplierContacts (SupplierContactId, TenantId, SupplierId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), @TenantId, '9F2E1CB1-FE26-4D29-B908-1A73D1B113A1', '09B6FCDA-FB13-48A5-A86B-3AAD92562386', @ContactTypeId, 1, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE SupplierContacts
    SET IsPrimary = 1, IsActive = 1
    WHERE TenantId = @TenantId AND SupplierId = '9F2E1CB1-FE26-4D29-B908-1A73D1B113A1' AND ContactId = '09B6FCDA-FB13-48A5-A86B-3AAD92562386';
END

-- Supplier 3: Apex Wholesale Importers PLC
IF EXISTS (SELECT 1 FROM Suppliers WHERE TenantId = @TenantId AND (SupplierId = '998533B0-D221-4877-9AD9-D831B61E8466' OR SupplierCode = 'SUP-RET-003' OR SupplierCode = '12212'))
BEGIN
    UPDATE Suppliers
    SET SupplierCode = 'SUP-RET-003',
        SupplierName = 'Apex Wholesale Importers PLC',
        ContactPerson = 'Deepak Pathmanathan',
        Email = 'ratnadeepak@yahoo.com',
        Phone = '+94 11 567 8901',
        AddressLine1 = 'No. 120, Kandy Road, Peliyagoda',
        City = 'Peliyagoda',
        PostalCode = '11830',
        Country = 'Sri Lanka',
        PaymentTerms = 'NET30',
        DefaultPurchasePriceCardId = @DefaultPurchasePriceCardId,
        CurrencyId = @CurrencyId,
        IsActive = 1
    WHERE TenantId = @TenantId AND (SupplierId = '998533B0-D221-4877-9AD9-D831B61E8466' OR SupplierCode = 'SUP-RET-003' OR SupplierCode = '12212');
END
ELSE
BEGIN
    INSERT INTO Suppliers (SupplierId, TenantId, SupplierCode, SupplierName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, DefaultPurchasePriceCardId, CurrencyId, IsActive, CreatedAt, CreatedBy)
    VALUES ('998533B0-D221-4877-9AD9-D831B61E8466', @TenantId, 'SUP-RET-003', 'Apex Wholesale Importers PLC', 'Deepak Pathmanathan', 'ratnadeepak@yahoo.com', '+94 11 567 8901', 'No. 120, Kandy Road, Peliyagoda', 'Peliyagoda', '11830', 'Sri Lanka', 'NET30', @DefaultPurchasePriceCardId, @CurrencyId, 1, SYSUTCDATETIME(), @SystemUserId);
END

-- Supplier 3 Contact
IF NOT EXISTS (SELECT 1 FROM Contacts WHERE TenantId = @TenantId AND ContactId = '6201CE67-8709-4013-BB6F-E749A6B092CD')
BEGIN
    INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
    VALUES ('6201CE67-8709-4013-BB6F-E749A6B092CD', @TenantId, 'Deepak Pathmanathan', 'Commercial Manager', 'ratnadeepak@yahoo.com', '+94 11 567 8901', '+94 77 345 6789', 'Email', 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Contacts
    SET ContactName = 'Deepak Pathmanathan',
        Email = 'ratnadeepak@yahoo.com',
        Phone = '+94 11 567 8901',
        JobTitle = 'Commercial Manager',
        IsActive = 1
    WHERE TenantId = @TenantId AND ContactId = '6201CE67-8709-4013-BB6F-E749A6B092CD';
END

-- Supplier 3 Contact Link
IF NOT EXISTS (SELECT 1 FROM SupplierContacts WHERE TenantId = @TenantId AND SupplierId = '998533B0-D221-4877-9AD9-D831B61E8466' AND ContactId = '6201CE67-8709-4013-BB6F-E749A6B092CD')
BEGIN
    INSERT INTO SupplierContacts (SupplierContactId, TenantId, SupplierId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), @TenantId, '998533B0-D221-4877-9AD9-D831B61E8466', '6201CE67-8709-4013-BB6F-E749A6B092CD', @ContactTypeId, 1, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE SupplierContacts
    SET IsPrimary = 1, IsActive = 1
    WHERE TenantId = @TenantId AND SupplierId = '998533B0-D221-4877-9AD9-D831B61E8466' AND ContactId = '6201CE67-8709-4013-BB6F-E749A6B092CD';
END

-- -----------------------------------------------------------------------------
-- 2. CUSTOMER MASTER RECORDS (3 Meaningful Customers with exact notification emails)
-- -----------------------------------------------------------------------------

-- Customer 1: Cargills Supermarkets PLC
IF EXISTS (SELECT 1 FROM Customers WHERE TenantId = @TenantId AND (CustomerId = '81D0AA22-6376-4EA5-AB6E-F69B38F38E6A' OR CustomerCode = 'CUST-RET-001'))
BEGIN
    UPDATE Customers
    SET CustomerCode = 'CUST-RET-001',
        CustomerName = 'Cargills Supermarkets PLC',
        ContactPerson = 'Deepak Ratna',
        Email = 'ratnadeepak1981@gmail.com1',
        Phone = '+94 11 456 7890',
        AddressLine1 = 'No. 40, York Street, Colombo 01',
        City = 'Colombo',
        PostalCode = '00100',
        Country = 'Sri Lanka',
        PaymentTerms = 'NET30',
        CreditLimit = 500000.00,
        DefaultSalesPriceCardId = @DefaultSalesPriceCardId,
        CurrencyId = @CurrencyId,
        IsActive = 1
    WHERE TenantId = @TenantId AND (CustomerId = '81D0AA22-6376-4EA5-AB6E-F69B38F38E6A' OR CustomerCode = 'CUST-RET-001');
END
ELSE
BEGIN
    INSERT INTO Customers (CustomerId, TenantId, CustomerCode, CustomerName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, CreditLimit, DefaultSalesPriceCardId, CurrencyId, IsActive, CreatedAt, CreatedBy)
    VALUES ('81D0AA22-6376-4EA5-AB6E-F69B38F38E6A', @TenantId, 'CUST-RET-001', 'Cargills Supermarkets PLC', 'Deepak Ratna', 'ratnadeepak1981@gmail.com1', '+94 11 456 7890', 'No. 40, York Street, Colombo 01', 'Colombo', '00100', 'Sri Lanka', 'NET30', 500000.00, @DefaultSalesPriceCardId, @CurrencyId, 1, SYSUTCDATETIME(), @SystemUserId);
END

-- Customer 1 Contact (ContactId matches User 1 Id: 44444444-25ad-4820-a078-111111111111)
IF NOT EXISTS (SELECT 1 FROM Contacts WHERE TenantId = @TenantId AND ContactId = '44444444-25AD-4820-A078-111111111111')
BEGIN
    INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
    VALUES ('44444444-25AD-4820-A078-111111111111', @TenantId, 'Deepak Ratna', 'Procurement Director', 'ratnadeepak1981@gmail.com1', '+94 11 456 7890', '+94 77 456 7890', 'Email', 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Contacts
    SET ContactName = 'Deepak Ratna',
        Email = 'ratnadeepak1981@gmail.com1',
        Phone = '+94 11 456 7890',
        JobTitle = 'Procurement Director',
        IsActive = 1
    WHERE TenantId = @TenantId AND ContactId = '44444444-25AD-4820-A078-111111111111';
END

-- Customer 1 Contact Link
IF NOT EXISTS (SELECT 1 FROM CustomerContacts WHERE TenantId = @TenantId AND CustomerId = '81D0AA22-6376-4EA5-AB6E-F69B38F38E6A' AND ContactId = '44444444-25AD-4820-A078-111111111111')
BEGIN
    INSERT INTO CustomerContacts (CustomerContactId, TenantId, CustomerId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), @TenantId, '81D0AA22-6376-4EA5-AB6E-F69B38F38E6A', '44444444-25AD-4820-A078-111111111111', @ContactTypeId, 1, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE CustomerContacts
    SET IsPrimary = 1, IsActive = 1
    WHERE TenantId = @TenantId AND CustomerId = '81D0AA22-6376-4EA5-AB6E-F69B38F38E6A' AND ContactId = '44444444-25AD-4820-A078-111111111111';
END

-- Customer 2: Keells Food Products PLC
IF EXISTS (SELECT 1 FROM Customers WHERE TenantId = @TenantId AND (CustomerId = 'C0020002-25AD-4820-A078-000000000002' OR CustomerCode = 'CUST-RET-002'))
BEGIN
    UPDATE Customers
    SET CustomerCode = 'CUST-RET-002',
        CustomerName = 'Keells Food Products PLC',
        ContactPerson = 'Pathmanathan Deepak',
        Email = 'pathmanathanratnadeepak@gmail.com1',
        Phone = '+94 11 567 8901',
        AddressLine1 = 'No. 117, Sir Chittampalam A. Gardiner Mawatha',
        City = 'Colombo',
        PostalCode = '00200',
        Country = 'Sri Lanka',
        PaymentTerms = 'NET30',
        CreditLimit = 500000.00,
        DefaultSalesPriceCardId = @DefaultSalesPriceCardId,
        CurrencyId = @CurrencyId,
        IsActive = 1
    WHERE TenantId = @TenantId AND (CustomerId = 'C0020002-25AD-4820-A078-000000000002' OR CustomerCode = 'CUST-RET-002');
END
ELSE
BEGIN
    INSERT INTO Customers (CustomerId, TenantId, CustomerCode, CustomerName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, CreditLimit, DefaultSalesPriceCardId, CurrencyId, IsActive, CreatedAt, CreatedBy)
    VALUES ('C0020002-25AD-4820-A078-000000000002', @TenantId, 'CUST-RET-002', 'Keells Food Products PLC', 'Pathmanathan Deepak', 'pathmanathanratnadeepak@gmail.com1', '+94 11 567 8901', 'No. 117, Sir Chittampalam A. Gardiner Mawatha', 'Colombo', '00200', 'Sri Lanka', 'NET30', 500000.00, @DefaultSalesPriceCardId, @CurrencyId, 1, SYSUTCDATETIME(), @SystemUserId);
END

-- Customer 2 Contact (ContactId matches User 2 Id: 44444444-25ad-4820-a078-222222222222)
IF NOT EXISTS (SELECT 1 FROM Contacts WHERE TenantId = @TenantId AND ContactId = '44444444-25AD-4820-A078-222222222222')
BEGIN
    INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
    VALUES ('44444444-25AD-4820-A078-222222222222', @TenantId, 'Pathmanathan Deepak', 'Supply Chain Manager', 'pathmanathanratnadeepak@gmail.com1', '+94 11 567 8901', '+94 77 567 8901', 'Email', 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Contacts
    SET ContactName = 'Pathmanathan Deepak',
        Email = 'pathmanathanratnadeepak@gmail.com1',
        Phone = '+94 11 567 8901',
        JobTitle = 'Supply Chain Manager',
        IsActive = 1
    WHERE TenantId = @TenantId AND ContactId = '44444444-25AD-4820-A078-222222222222';
END

-- Customer 2 Contact Link
IF NOT EXISTS (SELECT 1 FROM CustomerContacts WHERE TenantId = @TenantId AND CustomerId = 'C0020002-25AD-4820-A078-000000000002' AND ContactId = '44444444-25AD-4820-A078-222222222222')
BEGIN
    INSERT INTO CustomerContacts (CustomerContactId, TenantId, CustomerId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), @TenantId, 'C0020002-25AD-4820-A078-000000000002', '44444444-25AD-4820-A078-222222222222', @ContactTypeId, 1, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE CustomerContacts
    SET IsPrimary = 1, IsActive = 1
    WHERE TenantId = @TenantId AND CustomerId = 'C0020002-25AD-4820-A078-000000000002' AND ContactId = '44444444-25AD-4820-A078-222222222222';
END

-- Customer 3: Ceylon Beverage Holdings PLC
IF EXISTS (SELECT 1 FROM Customers WHERE TenantId = @TenantId AND (CustomerId = 'C0030003-25AD-4820-A078-000000000003' OR CustomerCode = 'CUST-RET-003'))
BEGIN
    UPDATE Customers
    SET CustomerCode = 'CUST-RET-003',
        CustomerName = 'Ceylon Beverage Holdings PLC',
        ContactPerson = 'Deepak Pathmanathan',
        Email = 'ratnadeepak@yahoo.com1',
        Phone = '+94 11 678 9012',
        AddressLine1 = 'No. 254, Colombo Road, Biyagama',
        City = 'Biyagama',
        PostalCode = '11600',
        Country = 'Sri Lanka',
        PaymentTerms = 'NET30',
        CreditLimit = 500000.00,
        DefaultSalesPriceCardId = @DefaultSalesPriceCardId,
        CurrencyId = @CurrencyId,
        IsActive = 1
    WHERE TenantId = @TenantId AND (CustomerId = 'C0030003-25AD-4820-A078-000000000003' OR CustomerCode = 'CUST-RET-003');
END
ELSE
BEGIN
    INSERT INTO Customers (CustomerId, TenantId, CustomerCode, CustomerName, ContactPerson, Email, Phone, AddressLine1, City, PostalCode, Country, PaymentTerms, CreditLimit, DefaultSalesPriceCardId, CurrencyId, IsActive, CreatedAt, CreatedBy)
    VALUES ('C0030003-25AD-4820-A078-000000000003', @TenantId, 'CUST-RET-003', 'Ceylon Beverage Holdings PLC', 'Deepak Pathmanathan', 'ratnadeepak@yahoo.com1', '+94 11 678 9012', 'No. 254, Colombo Road, Biyagama', 'Biyagama', '11600', 'Sri Lanka', 'NET30', 500000.00, @DefaultSalesPriceCardId, @CurrencyId, 1, SYSUTCDATETIME(), @SystemUserId);
END

-- Customer 3 Contact (ContactId matches User 3 Id: 44444444-25ad-4820-a078-333333333333)
IF NOT EXISTS (SELECT 1 FROM Contacts WHERE TenantId = @TenantId AND ContactId = '44444444-25AD-4820-A078-333333333333')
BEGIN
    INSERT INTO Contacts (ContactId, TenantId, ContactName, JobTitle, Email, Phone, MobileNumber, PreferredContactMethod, IsActive, CreatedAt, CreatedBy)
    VALUES ('44444444-25AD-4820-A078-333333333333', @TenantId, 'Deepak Pathmanathan', 'Purchasing Lead', 'ratnadeepak@yahoo.com1', '+94 11 678 9012', '+94 77 678 9012', 'Email', 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Contacts
    SET ContactName = 'Deepak Pathmanathan',
        Email = 'ratnadeepak@yahoo.com1',
        Phone = '+94 11 678 9012',
        JobTitle = 'Purchasing Lead',
        IsActive = 1
    WHERE TenantId = @TenantId AND ContactId = '44444444-25AD-4820-A078-333333333333';
END

-- Customer 3 Contact Link
IF NOT EXISTS (SELECT 1 FROM CustomerContacts WHERE TenantId = @TenantId AND CustomerId = 'C0030003-25AD-4820-A078-000000000003' AND ContactId = '44444444-25AD-4820-A078-333333333333')
BEGIN
    INSERT INTO CustomerContacts (CustomerContactId, TenantId, CustomerId, ContactId, ContactTypeId, IsPrimary, IsActive, CreatedAt, CreatedBy)
    VALUES (NEWID(), @TenantId, 'C0030003-25AD-4820-A078-000000000003', '44444444-25AD-4820-A078-333333333333', @ContactTypeId, 1, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE CustomerContacts
    SET IsPrimary = 1, IsActive = 1
    WHERE TenantId = @TenantId AND CustomerId = 'C0030003-25AD-4820-A078-000000000003' AND ContactId = '44444444-25AD-4820-A078-333333333333';
END

PRINT 'DomainDB seed completed successfully.';
GO

-- =============================================================================
-- 3. SECURITY DATABASE (3 Separate Customer Accounts with password: 123)
-- =============================================================================
USE SecurityDB;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @TenantId UNIQUEIDENTIFIER = '25ADAB1D-6E1A-4820-A078-DD7AAE6AA7F0';
DECLARE @CustomerRoleId UNIQUEIDENTIFIER;
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
-- PBKDF2 hash of "123" with 100,000 iterations:
DECLARE @PasswordHash NVARCHAR(MAX) = '100000.538BSvbSxWUDzUeIsrIj2w==.puXyL70mVnwVzNheE/OT7OKIz8lFjKA2thoAXQEETJ4=';

-- Ensure CUSTOMER_COMPANY_ADMIN role exists for this tenant
SELECT @CustomerRoleId = Id FROM Roles WHERE TenantId = @TenantId AND Name = 'CUSTOMER_COMPANY_ADMIN';
IF (@CustomerRoleId IS NULL)
BEGIN
    SET @CustomerRoleId = '55555555-25AD-4820-A078-111111111111';
    INSERT INTO Roles (Id, TenantId, Name, Description, IsActive, CreatedAt, CreatedBy, IsSystemRole, TemplateKey, TemplateVersion)
    VALUES (@CustomerRoleId, @TenantId, 'CUSTOMER_COMPANY_ADMIN', 'Customer Company Administrator (Portal)', 1, SYSUTCDATETIME(), @SystemUserId, 0, 'CUSTOMER_COMPANY_ADMIN', 1);
END

-- Customer Account 1: cargills.portal@cargillsretail.lk
IF NOT EXISTS (SELECT 1 FROM Users WHERE TenantId = @TenantId AND (Id = '44444444-25AD-4820-A078-111111111111' OR Email = 'cargills.portal@cargillsretail.lk' OR Username = 'cargills.portal@cargillsretail.lk'))
BEGIN
    INSERT INTO Users (Id, TenantId, Username, Email, PasswordHash, IsActive, CreatedAt, CreatedBy)
    VALUES ('44444444-25AD-4820-A078-111111111111', @TenantId, 'cargills.portal@cargillsretail.lk', 'cargills.portal@cargillsretail.lk', @PasswordHash, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Users
    SET IsActive = 1
    WHERE TenantId = @TenantId AND (Id = '44444444-25AD-4820-A078-111111111111' OR Email = 'cargills.portal@cargillsretail.lk' OR Username = 'cargills.portal@cargillsretail.lk');
END

-- Customer Account 1 Role Assignment
IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = '44444444-25AD-4820-A078-111111111111' AND RoleId = @CustomerRoleId)
BEGIN
    INSERT INTO UserRoles (Id, UserId, RoleId, CreatedAt, CreatedBy)
    VALUES (NEWID(), '44444444-25AD-4820-A078-111111111111', @CustomerRoleId, SYSUTCDATETIME(), @SystemUserId);
END

-- Customer Account 2: keells.portal@keells.lk
IF NOT EXISTS (SELECT 1 FROM Users WHERE TenantId = @TenantId AND (Id = '44444444-25AD-4820-A078-222222222222' OR Email = 'keells.portal@keells.lk' OR Username = 'keells.portal@keells.lk'))
BEGIN
    INSERT INTO Users (Id, TenantId, Username, Email, PasswordHash, IsActive, CreatedAt, CreatedBy)
    VALUES ('44444444-25AD-4820-A078-222222222222', @TenantId, 'keells.portal@keells.lk', 'keells.portal@keells.lk', @PasswordHash, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Users
    SET IsActive = 1
    WHERE TenantId = @TenantId AND (Id = '44444444-25AD-4820-A078-222222222222' OR Email = 'keells.portal@keells.lk' OR Username = 'keells.portal@keells.lk');
END

-- Customer Account 2 Role Assignment
IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = '44444444-25AD-4820-A078-222222222222' AND RoleId = @CustomerRoleId)
BEGIN
    INSERT INTO UserRoles (Id, UserId, RoleId, CreatedAt, CreatedBy)
    VALUES (NEWID(), '44444444-25AD-4820-A078-222222222222', @CustomerRoleId, SYSUTCDATETIME(), @SystemUserId);
END

-- Customer Account 3: ceylonbev.portal@ceylonbev.lk
IF NOT EXISTS (SELECT 1 FROM Users WHERE TenantId = @TenantId AND (Id = '44444444-25AD-4820-A078-333333333333' OR Email = 'ceylonbev.portal@ceylonbev.lk' OR Username = 'ceylonbev.portal@ceylonbev.lk'))
BEGIN
    INSERT INTO Users (Id, TenantId, Username, Email, PasswordHash, IsActive, CreatedAt, CreatedBy)
    VALUES ('44444444-25AD-4820-A078-333333333333', @TenantId, 'ceylonbev.portal@ceylonbev.lk', 'ceylonbev.portal@ceylonbev.lk', @PasswordHash, 1, SYSUTCDATETIME(), @SystemUserId);
END
ELSE
BEGIN
    UPDATE Users
    SET IsActive = 1
    WHERE TenantId = @TenantId AND (Id = '44444444-25AD-4820-A078-333333333333' OR Email = 'ceylonbev.portal@ceylonbev.lk' OR Username = 'ceylonbev.portal@ceylonbev.lk');
END

-- Customer Account 3 Role Assignment
IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = '44444444-25AD-4820-A078-333333333333' AND RoleId = @CustomerRoleId)
BEGIN
    INSERT INTO UserRoles (Id, UserId, RoleId, CreatedAt, CreatedBy)
    VALUES (NEWID(), '44444444-25AD-4820-A078-333333333333', @CustomerRoleId, SYSUTCDATETIME(), @SystemUserId);
END

PRINT 'SecurityDB seed completed successfully.';
GO
