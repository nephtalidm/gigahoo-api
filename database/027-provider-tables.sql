-- 027-provider-tables.sql
-- Make providers fully data-driven: no more "stripe"/"qwen" magic strings in columns.
-- Introduces ProviderType + Provider lookup tables and repoints PlanPrice,
-- PaymentCustomer, and Account at Provider.Id (FKs) instead of nvarchar codes.
-- Idempotent: safe to run repeatedly.

------------------------------------------------------------------------------
-- 1. ProviderType lookup (LLM / Payment / Phone / SMS / Email).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProviderType')
BEGIN
    CREATE TABLE [dbo].[ProviderType] (
        [Id]   tinyint      NOT NULL CONSTRAINT PK_ProviderType PRIMARY KEY,
        [Name] nvarchar(20) NOT NULL CONSTRAINT UX_ProviderType_Name UNIQUE
    );
END
GO

MERGE [dbo].[ProviderType] AS tgt
USING (VALUES (1,'LLM'),(2,'Payment'),(3,'Phone'),(4,'SMS'),(5,'Email')) AS src([Id],[Name])
    ON tgt.[Id] = src.[Id]
WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id],[Name]) VALUES (src.[Id], src.[Name]);
GO

------------------------------------------------------------------------------
-- 2. Provider lookup (name + code + provider type).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Provider')
BEGIN
    CREATE TABLE [dbo].[Provider] (
        [Id]             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Provider PRIMARY KEY,
        [Name]           nvarchar(50) NOT NULL,
        [Code]           nvarchar(30) NOT NULL,
        [ProviderTypeId] tinyint      NOT NULL,
        CONSTRAINT FK_Provider_ProviderType FOREIGN KEY ([ProviderTypeId]) REFERENCES [dbo].[ProviderType]([Id]),
        CONSTRAINT UX_Provider_Code_Type UNIQUE ([Code],[ProviderTypeId])
    );
END
GO

-- Seed providers (insert only if missing, keyed on Code+ProviderTypeId).
INSERT INTO [dbo].[Provider] ([Name],[Code],[ProviderTypeId])
SELECT s.[Name], s.[Code], s.[ProviderTypeId]
FROM (VALUES
        ('Stripe','stripe',2),
        ('Qwen','qwen',1),
        ('Twilio','twilio',3),
        ('Twilio','twilio',4),
        ('SendGrid','sendgrid',5)
     ) AS s([Name],[Code],[ProviderTypeId])
WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[Provider] p
        WHERE p.[Code] = s.[Code] AND p.[ProviderTypeId] = s.[ProviderTypeId]
);
GO

------------------------------------------------------------------------------
-- 3. PlanPrice: string Provider -> ProviderId FK.
------------------------------------------------------------------------------
-- Fix the ReplacedOn trigger: migration 024 renamed StripePriceId -> ProviderPriceId
-- but TR_PlanPrice_ReplacedOn still referenced the old name, so every UPDATE on
-- PlanPrice errored. Recreate it against ProviderPriceId before we touch the table.
IF OBJECT_ID('dbo.TR_PlanPrice_ReplacedOn', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_PlanPrice_ReplacedOn;
GO
CREATE TRIGGER dbo.TR_PlanPrice_ReplacedOn ON [dbo].[PlanPrice]
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    UPDATE pp
        SET ReplacedOn = SYSUTCDATETIME()
    FROM [dbo].[PlanPrice] pp
    INNER JOIN inserted i ON pp.[Id] = i.[Id]
    INNER JOIN deleted  d ON d.[Id] = i.[Id]
    WHERE i.[Amount] <> d.[Amount]
       OR ISNULL(i.[ProviderPriceId], '') <> ISNULL(d.[ProviderPriceId], '');
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'ProviderId')
    ALTER TABLE [dbo].[PlanPrice] ADD [ProviderId] int NULL;
GO

-- All existing PlanPrice rows are Stripe payment prices.
UPDATE [dbo].[PlanPrice]
SET [ProviderId] = (SELECT [Id] FROM [dbo].[Provider] WHERE [Code] = 'stripe' AND [ProviderTypeId] = 2)
WHERE [ProviderId] IS NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PlanPrice_Provider')
    ALTER TABLE [dbo].[PlanPrice]
        ADD CONSTRAINT FK_PlanPrice_Provider FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider]([Id]);
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_PlanPrice_Plan_Currency_Provider' AND object_id = OBJECT_ID(N'[dbo].[PlanPrice]'))
    DROP INDEX UX_PlanPrice_Plan_Currency_Provider ON [dbo].[PlanPrice];
GO

-- Make ProviderId NOT NULL before building the unique index on it (a unique index
-- on the column would otherwise block the ALTER COLUMN).
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'ProviderId' AND is_nullable = 1)
    ALTER TABLE [dbo].[PlanPrice] ALTER COLUMN [ProviderId] int NOT NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_PlanPrice_Plan_Currency_ProviderId' AND object_id = OBJECT_ID(N'[dbo].[PlanPrice]'))
    CREATE UNIQUE INDEX UX_PlanPrice_Plan_Currency_ProviderId ON [dbo].[PlanPrice]([PlanId],[Currency],[ProviderId]);
GO

-- Drop the old nvarchar Provider column (drop its default constraint first if present).
DECLARE @ppDef sysname = (
    SELECT dc.name FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND c.name = 'Provider');
IF @ppDef IS NOT NULL EXEC('ALTER TABLE [dbo].[PlanPrice] DROP CONSTRAINT ' + @ppDef);
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'Provider')
    ALTER TABLE [dbo].[PlanPrice] DROP COLUMN [Provider];
GO

------------------------------------------------------------------------------
-- 4. PaymentCustomer: string Provider -> ProviderId FK.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PaymentCustomer]') AND name = 'ProviderId')
    ALTER TABLE [dbo].[PaymentCustomer] ADD [ProviderId] int NULL;
GO

UPDATE [dbo].[PaymentCustomer]
SET [ProviderId] = (SELECT [Id] FROM [dbo].[Provider] WHERE [Code] = 'stripe' AND [ProviderTypeId] = 2)
WHERE [Provider] = 'stripe' AND [ProviderId] IS NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_PaymentCustomer_Provider')
    ALTER TABLE [dbo].[PaymentCustomer]
        ADD CONSTRAINT FK_PaymentCustomer_Provider FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider]([Id]);
GO

-- Drop old UNIQUE(AccountId, Provider) and the nvarchar Provider column (+ its default).
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'UX_PaymentCustomer_Account_Provider' AND parent_object_id = OBJECT_ID(N'[dbo].[PaymentCustomer]'))
    ALTER TABLE [dbo].[PaymentCustomer] DROP CONSTRAINT UX_PaymentCustomer_Account_Provider;
GO
IF EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_PaymentCustomer_Provider')
    ALTER TABLE [dbo].[PaymentCustomer] DROP CONSTRAINT DF_PaymentCustomer_Provider;
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PaymentCustomer]') AND name = 'Provider')
    ALTER TABLE [dbo].[PaymentCustomer] DROP COLUMN [Provider];
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PaymentCustomer]') AND name = 'ProviderId' AND is_nullable = 1)
    ALTER TABLE [dbo].[PaymentCustomer] ALTER COLUMN [ProviderId] int NOT NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'UX_PaymentCustomer_Account_ProviderId' AND parent_object_id = OBJECT_ID(N'[dbo].[PaymentCustomer]'))
    ALTER TABLE [dbo].[PaymentCustomer] ADD CONSTRAINT UX_PaymentCustomer_Account_ProviderId UNIQUE ([AccountId],[ProviderId]);
GO

------------------------------------------------------------------------------
-- 5. Account: string LlmProvider -> LlmProviderId FK (nullable).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'LlmProviderId')
    ALTER TABLE [dbo].[Account] ADD [LlmProviderId] int NULL;
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Account_LlmProvider')
    ALTER TABLE [dbo].[Account]
        ADD CONSTRAINT FK_Account_LlmProvider FOREIGN KEY ([LlmProviderId]) REFERENCES [dbo].[Provider]([Id]);
GO

UPDATE [dbo].[Account]
SET [LlmProviderId] = (SELECT [Id] FROM [dbo].[Provider] WHERE [Code] = 'qwen' AND [ProviderTypeId] = 1)
WHERE [LlmProvider] = 'qwen' AND [LlmProviderId] IS NULL;
GO

-- Drop the old nvarchar LlmProvider column (+ its default constraint).
IF EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_Account_LlmProvider')
    ALTER TABLE [dbo].[Account] DROP CONSTRAINT DF_Account_LlmProvider;
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'LlmProvider')
    ALTER TABLE [dbo].[Account] DROP COLUMN [LlmProvider];
GO

PRINT '027-provider-tables migration applied (ProviderType, Provider; PlanPrice/PaymentCustomer/Account repointed to ProviderId)';
GO
