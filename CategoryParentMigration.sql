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

