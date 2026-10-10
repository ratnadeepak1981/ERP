BEGIN TRANSACTION;
GO

CREATE TABLE [BillingLedgerEntries] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [EntryType] int NOT NULL,
    [Direction] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [PostedAtUtc] datetime2 NOT NULL,
    [SourceDocumentType] nvarchar(50) NOT NULL,
    [SourceDocumentId] uniqueidentifier NOT NULL,
    [ReferenceNumber] nvarchar(100) NOT NULL,
    [Description] nvarchar(250) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_BillingLedgerEntries] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_BillingLedgerEntries_Amount_Positive] CHECK ([Amount] > 0),
    CONSTRAINT [FK_BillingLedgerEntries_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PaymentTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [PaymentProvider] nvarchar(50) NOT NULL,
    [ProviderTransactionId] nvarchar(100) NOT NULL,
    [IdempotencyKey] nvarchar(100) NOT NULL,
    [PaymentMethod] int NOT NULL,
    [Status] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [AllocatedAmount] decimal(18,2) NOT NULL,
    [RefundedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [ProcessedAtUtc] datetime2 NOT NULL,
    [FailureReason] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PaymentTransactions_AllocatedAmount_NonNegative] CHECK ([AllocatedAmount] >= 0),
    CONSTRAINT [CK_PaymentTransactions_Amount_Positive] CHECK ([Amount] > 0),
    CONSTRAINT [CK_PaymentTransactions_RefundedAmount_NonNegative] CHECK ([RefundedAmount] >= 0),
    CONSTRAINT [FK_PaymentTransactions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SubscriptionInvoices] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [SubscriptionId] uniqueidentifier NOT NULL,
    [InvoiceNumber] nvarchar(50) NOT NULL,
    [Status] int NOT NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaidAmount] decimal(18,2) NOT NULL,
    [CreditedAmount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [IssueDate] datetime2 NOT NULL,
    [DueDate] datetime2 NOT NULL,
    [PaidAt] datetime2 NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SubscriptionInvoices] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_SubscriptionInvoices_CreditedAmount_NonNegative] CHECK ([CreditedAmount] >= 0),
    CONSTRAINT [CK_SubscriptionInvoices_PaidAmount_NonNegative] CHECK ([PaidAmount] >= 0),
    CONSTRAINT [CK_SubscriptionInvoices_SubTotal_NonNegative] CHECK ([SubTotal] >= 0),
    CONSTRAINT [CK_SubscriptionInvoices_TaxAmount_NonNegative] CHECK ([TaxAmount] >= 0),
    CONSTRAINT [CK_SubscriptionInvoices_TotalAmount_NonNegative] CHECK ([TotalAmount] >= 0),
    CONSTRAINT [FK_SubscriptionInvoices_Subscriptions_SubscriptionId] FOREIGN KEY ([SubscriptionId]) REFERENCES [Subscriptions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SubscriptionInvoices_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [TenantBillingProfiles] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [BillingEmail] nvarchar(256) NOT NULL,
    [CompanyName] nvarchar(200) NOT NULL,
    [TaxId] nvarchar(50) NULL,
    [Currency] nvarchar(3) NOT NULL,
    [AddressLine1] nvarchar(250) NOT NULL,
    [AddressLine2] nvarchar(250) NULL,
    [City] nvarchar(100) NOT NULL,
    [StateOrProvince] nvarchar(100) NULL,
    [PostalCode] nvarchar(20) NOT NULL,
    [CountryCode] nvarchar(2) NOT NULL,
    [IsTaxExempt] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_TenantBillingProfiles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TenantBillingProfiles_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [RefundTransactions] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [PaymentTransactionId] uniqueidentifier NOT NULL,
    [ProviderRefundId] nvarchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Reason] nvarchar(250) NOT NULL,
    [ProcessedAtUtc] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_RefundTransactions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_RefundTransactions_Amount_Positive] CHECK ([Amount] > 0),
    CONSTRAINT [FK_RefundTransactions_PaymentTransactions_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransactions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RefundTransactions_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [CreditNotes] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [SubscriptionInvoiceId] uniqueidentifier NOT NULL,
    [CreditNoteNumber] nvarchar(50) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(3) NOT NULL,
    [Reason] nvarchar(250) NOT NULL,
    [IssuedAtUtc] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_CreditNotes] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_CreditNotes_Amount_Positive] CHECK ([Amount] > 0),
    CONSTRAINT [FK_CreditNotes_SubscriptionInvoices_SubscriptionInvoiceId] FOREIGN KEY ([SubscriptionInvoiceId]) REFERENCES [SubscriptionInvoices] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_CreditNotes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [PaymentAllocations] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [PaymentTransactionId] uniqueidentifier NOT NULL,
    [SubscriptionInvoiceId] uniqueidentifier NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [AllocatedAtUtc] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_PaymentAllocations] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PaymentAllocations_Amount_Positive] CHECK ([Amount] > 0),
    CONSTRAINT [FK_PaymentAllocations_PaymentTransactions_PaymentTransactionId] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransactions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PaymentAllocations_SubscriptionInvoices_SubscriptionInvoiceId] FOREIGN KEY ([SubscriptionInvoiceId]) REFERENCES [SubscriptionInvoices] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_PaymentAllocations_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE TABLE [SubscriptionInvoiceLines] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [SubscriptionInvoiceId] uniqueidentifier NOT NULL,
    [Description] nvarchar(250) NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [TaxRate] decimal(18,4) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [CreatedBy] uniqueidentifier NOT NULL,
    [ModifiedAt] datetime2 NULL,
    [ModifiedBy] uniqueidentifier NULL,
    CONSTRAINT [PK_SubscriptionInvoiceLines] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_SubscriptionInvoiceLines_Quantity_Positive] CHECK ([Quantity] > 0),
    CONSTRAINT [CK_SubscriptionInvoiceLines_SubTotal_NonNegative] CHECK ([SubTotal] >= 0),
    CONSTRAINT [CK_SubscriptionInvoiceLines_TaxAmount_NonNegative] CHECK ([TaxAmount] >= 0),
    CONSTRAINT [CK_SubscriptionInvoiceLines_TaxRate_NonNegative] CHECK ([TaxRate] >= 0),
    CONSTRAINT [CK_SubscriptionInvoiceLines_TotalAmount_NonNegative] CHECK ([TotalAmount] >= 0),
    CONSTRAINT [CK_SubscriptionInvoiceLines_UnitPrice_NonNegative] CHECK ([UnitPrice] >= 0),
    CONSTRAINT [FK_SubscriptionInvoiceLines_SubscriptionInvoices_SubscriptionInvoiceId] FOREIGN KEY ([SubscriptionInvoiceId]) REFERENCES [SubscriptionInvoices] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SubscriptionInvoiceLines_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE NO ACTION
);
GO

CREATE INDEX [IX_BillingLedgerEntries_TenantId_PostedAtUtc] ON [BillingLedgerEntries] ([TenantId], [PostedAtUtc]);
GO

CREATE UNIQUE INDEX [IX_BillingLedgerEntries_TenantId_SourceDocumentType_SourceDocumentId_EntryType_Direction] ON [BillingLedgerEntries] ([TenantId], [SourceDocumentType], [SourceDocumentId], [EntryType], [Direction]);
GO

CREATE UNIQUE INDEX [IX_CreditNotes_CreditNoteNumber] ON [CreditNotes] ([CreditNoteNumber]);
GO

CREATE INDEX [IX_CreditNotes_SubscriptionInvoiceId] ON [CreditNotes] ([SubscriptionInvoiceId]);
GO

CREATE INDEX [IX_CreditNotes_TenantId] ON [CreditNotes] ([TenantId]);
GO

CREATE INDEX [IX_PaymentAllocations_PaymentTransactionId] ON [PaymentAllocations] ([PaymentTransactionId]);
GO

CREATE INDEX [IX_PaymentAllocations_SubscriptionInvoiceId] ON [PaymentAllocations] ([SubscriptionInvoiceId]);
GO

CREATE INDEX [IX_PaymentAllocations_TenantId] ON [PaymentAllocations] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_PaymentTransactions_PaymentProvider_ProviderTransactionId] ON [PaymentTransactions] ([PaymentProvider], [ProviderTransactionId]);
GO

CREATE UNIQUE INDEX [IX_PaymentTransactions_TenantId_PaymentProvider_IdempotencyKey] ON [PaymentTransactions] ([TenantId], [PaymentProvider], [IdempotencyKey]);
GO

CREATE INDEX [IX_PaymentTransactions_TenantId_Status] ON [PaymentTransactions] ([TenantId], [Status]);
GO

CREATE INDEX [IX_RefundTransactions_PaymentTransactionId] ON [RefundTransactions] ([PaymentTransactionId]);
GO

CREATE UNIQUE INDEX [IX_RefundTransactions_ProviderRefundId] ON [RefundTransactions] ([ProviderRefundId]);
GO

CREATE INDEX [IX_RefundTransactions_TenantId] ON [RefundTransactions] ([TenantId]);
GO

CREATE INDEX [IX_SubscriptionInvoiceLines_SubscriptionInvoiceId] ON [SubscriptionInvoiceLines] ([SubscriptionInvoiceId]);
GO

CREATE INDEX [IX_SubscriptionInvoiceLines_TenantId] ON [SubscriptionInvoiceLines] ([TenantId]);
GO

CREATE UNIQUE INDEX [IX_SubscriptionInvoices_InvoiceNumber] ON [SubscriptionInvoices] ([InvoiceNumber]);
GO

CREATE INDEX [IX_SubscriptionInvoices_SubscriptionId] ON [SubscriptionInvoices] ([SubscriptionId]);
GO

CREATE INDEX [IX_SubscriptionInvoices_TenantId_Status] ON [SubscriptionInvoices] ([TenantId], [Status]);
GO

CREATE UNIQUE INDEX [IX_TenantBillingProfiles_TenantId] ON [TenantBillingProfiles] ([TenantId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009093946_AddTenantBillingAndLedger', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Subscriptions] ADD [AutoRenew] bit NOT NULL DEFAULT CAST(1 AS bit);
GO

ALTER TABLE [Subscriptions] ADD [BillingAnchorDate] datetime2 NULL;
GO

ALTER TABLE [Subscriptions] ADD [BillingCycle] int NULL;
GO

ALTER TABLE [Subscriptions] ADD [BillingPlanPriceSnapshot] decimal(18,2) NULL;
GO

ALTER TABLE [Subscriptions] ADD [CurrentPeriodStart] datetime2 NULL;
GO

ALTER TABLE [Subscriptions] ADD [CurrentPeriodEnd] datetime2 NULL;
GO

ALTER TABLE [Subscriptions] ADD [LastInvoiceGeneratedAt] datetime2 NULL;
GO

ALTER TABLE [Subscriptions] ADD [NextBillingDate] datetime2 NULL;
GO

                -- Backfill BillingCycle, PriceSnapshot, Anchor, and Periods from linked SubscriptionPlans
                UPDATE s
                SET 
                    s.[BillingCycle] = ISNULL(p.[BillingCycle], 2),
                    s.[BillingPlanPriceSnapshot] = ISNULL(p.[Price], 0.0),
                    s.[BillingAnchorDate] = s.[StartDate],
                    s.[CurrentPeriodStart] = s.[StartDate],
                    s.[CurrentPeriodEnd] = CASE 
                        WHEN ISNULL(p.[BillingCycle], 2) = 0 THEN '9999-12-31T23:59:59.0000000' -- Lifetime (0)
                        WHEN ISNULL(p.[BillingCycle], 2) = 1 THEN DATEADD(day, 7, s.[StartDate]) -- Weekly (1)
                        WHEN ISNULL(p.[BillingCycle], 2) = 2 THEN DATEADD(month, 1, s.[StartDate]) -- Monthly (2)
                        WHEN ISNULL(p.[BillingCycle], 2) = 3 THEN DATEADD(month, 3, s.[StartDate]) -- Quarterly (3)
                        WHEN ISNULL(p.[BillingCycle], 2) = 4 THEN DATEADD(year, 1, s.[StartDate]) -- Yearly (4)
                        ELSE DATEADD(month, 1, s.[StartDate])
                    END,
                    -- NextBillingDate: Only Active recurring subscriptions schedule future billing;
                    -- Lifetime (0), Expired (5), Cancelled (6), and Suspended (4) have NULL NextBillingDate.
                    -- Active subscriptions have NextBillingDate set to CurrentPeriodEnd.
                    s.[NextBillingDate] = CASE 
                        WHEN ISNULL(p.[BillingCycle], 2) = 0 THEN NULL -- Lifetime does not bill
                        WHEN s.[Status] = 2 AND (s.[EndDate] IS NULL OR s.[EndDate] > GETUTCDATE()) 
                            THEN CASE 
                                WHEN ISNULL(p.[BillingCycle], 2) = 1 THEN DATEADD(day, 7, s.[StartDate])
                                WHEN ISNULL(p.[BillingCycle], 2) = 2 THEN DATEADD(month, 1, s.[StartDate])
                                WHEN ISNULL(p.[BillingCycle], 2) = 3 THEN DATEADD(month, 3, s.[StartDate])
                                WHEN ISNULL(p.[BillingCycle], 2) = 4 THEN DATEADD(year, 1, s.[StartDate])
                                ELSE DATEADD(month, 1, s.[StartDate])
                            END
                        ELSE NULL
                    END,
                    s.[AutoRenew] = CASE 
                        WHEN ISNULL(p.[BillingCycle], 2) = 0 THEN CAST(0 AS bit) -- Lifetime does not auto-renew
                        WHEN s.[Status] IN (5, 6) THEN CAST(0 AS bit) -- Expired/Cancelled do not auto-renew
                        ELSE CAST(1 AS bit)
                    END
                FROM [Subscriptions] s
                LEFT JOIN [SubscriptionPlans] p ON s.[SubscriptionPlanId] = p.[Id]
                WHERE s.[BillingAnchorDate] IS NULL;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'BillingAnchorDate');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Subscriptions] ALTER COLUMN [BillingAnchorDate] datetime2 NOT NULL;
GO

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'BillingCycle');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Subscriptions] ALTER COLUMN [BillingCycle] int NOT NULL;
GO

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'BillingPlanPriceSnapshot');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Subscriptions] ALTER COLUMN [BillingPlanPriceSnapshot] decimal(18,2) NOT NULL;
GO

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'CurrentPeriodStart');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Subscriptions] ALTER COLUMN [CurrentPeriodStart] datetime2 NOT NULL;
GO

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'CurrentPeriodEnd');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Subscriptions] ALTER COLUMN [CurrentPeriodEnd] datetime2 NOT NULL;
GO

                DECLARE @var0 sysname;
                SELECT @var0 = [d].[name]
                FROM [sys].[default_constraints] [d]
                INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
                WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Subscriptions]') AND [c].[name] = N'AutoRenew');
                IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Subscriptions] DROP CONSTRAINT [' + @var0 + '];');
GO

ALTER TABLE [SubscriptionInvoices] ADD [BillingCycle] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [SubscriptionInvoices] ADD [BillingPeriodEnd] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [SubscriptionInvoices] ADD [BillingPeriodStart] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
GO

ALTER TABLE [SubscriptionInvoices] ADD [SequenceNumber] int NOT NULL DEFAULT 0;
GO

CREATE UNIQUE INDEX [IX_SubscriptionInvoices_SubscriptionId_BillingPeriodStart_BillingPeriodEnd] ON [SubscriptionInvoices] ([SubscriptionId], [BillingPeriodStart], [BillingPeriodEnd]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261009100016_AddRecurringBillingSchedule', N'8.0.0');
GO

COMMIT;
GO

