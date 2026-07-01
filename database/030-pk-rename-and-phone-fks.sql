-- 030-pk-rename-and-phone-fks.sql
-- Two coordinated schema changes that finish the entity refactor:
--   A. Rename every table's [Id] primary-key column to [<Table>Id], matching the
--      FK columns (which already use the <Target>Id convention) and the C# entities.
--   B. PhoneNumber: replace the [Provider]/[CountryCode] varchar columns with
--      [ProviderId]/[CountryId] foreign keys (finishing what 027 did for the other
--      tables), and seed Telnyx as a Phone/SMS provider.
-- Idempotent: safe to run repeatedly.
--
-- Deploy note: apply this BEFORE starting the new API build. The renamed columns
-- are incompatible with the currently-running (old) API, so stop it first.

SET XACT_ABORT ON;
GO

------------------------------------------------------------------------------
-- A. Rename PK columns  [Id] -> [<Table>Id].
--    FK constraints, PK constraints and indexes follow the column automatically
--    (SQL Server tracks them by column_id, not name), so nothing else needs
--    touching except the two triggers below that reference [Id] by name.
------------------------------------------------------------------------------
DECLARE @renames TABLE (TableName sysname, NewCol sysname);
INSERT INTO @renames (TableName, NewCol) VALUES
    ('Plan','PlanId'), ('BusinessCategory','BusinessCategoryId'), ('Country','CountryId'),
    ('Language','LanguageId'), ('Region','RegionId'), ('Account','AccountId'),
    ('Conversation','ConversationId'), ('Invoice','InvoiceId'), ('OtpCode','OtpCodeId'),
    ('ContactSubmission','ContactSubmissionId'), ('PhoneNumber','PhoneNumberId'),
    ('PlanPrice','PlanPriceId'), ('PaymentCustomer','PaymentCustomerId'),
    ('ProviderType','ProviderTypeId'), ('Provider','ProviderId'), ('Voice','VoiceId');

DECLARE @t sysname, @nc sysname, @sql nvarchar(max);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT TableName, NewCol FROM @renames;
OPEN cur;
FETCH NEXT FROM cur INTO @t, @nc;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH('dbo.' + @t, 'Id') IS NOT NULL
       AND COL_LENGTH('dbo.' + @t, @nc) IS NULL
    BEGIN
        SET @sql = 'EXEC sp_rename ''dbo.' + @t + '.Id'', ''' + @nc + ''', ''COLUMN''';
        EXEC sp_executesql @sql;
        PRINT 'Renamed ' + @t + '.Id -> ' + @nc;
    END
    FETCH NEXT FROM cur INTO @t, @nc;
END
CLOSE cur; DEALLOCATE cur;
GO

------------------------------------------------------------------------------
-- A.1  Recreate the two triggers that referenced [Id] by name.
------------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_Account_UpdatedAt]
ON [dbo].[Account]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE a SET [UpdatedAt] = SYSUTCDATETIME()
    FROM [dbo].[Account] a
    INNER JOIN inserted i ON a.[AccountId] = i.[AccountId];
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_PlanPrice_ReplacedOn ON [dbo].[PlanPrice]
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    UPDATE pp
        SET ReplacedOn = SYSUTCDATETIME()
    FROM [dbo].[PlanPrice] pp
    INNER JOIN inserted i ON pp.[PlanPriceId] = i.[PlanPriceId]
    INNER JOIN deleted  d ON d.[PlanPriceId] = i.[PlanPriceId]
    WHERE i.[Amount] <> d.[Amount]
       OR ISNULL(i.[ProviderPriceId], '') <> ISNULL(d.[ProviderPriceId], '');
END
GO

------------------------------------------------------------------------------
-- B. PhoneNumber: Provider/CountryCode varchars -> ProviderId/CountryId FKs.
------------------------------------------------------------------------------
-- B.1  Seed Telnyx as a Phone (3) and SMS (4) provider (Twilio seeded in 027).
INSERT INTO [dbo].[Provider] ([Name],[Code],[ProviderTypeId])
SELECT s.[Name], s.[Code], s.[ProviderTypeId]
FROM (VALUES
        ('Telnyx','telnyx',3),
        ('Telnyx','telnyx',4)
     ) AS s([Name],[Code],[ProviderTypeId])
WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[Provider] p
        WHERE p.[Code] = s.[Code] AND p.[ProviderTypeId] = s.[ProviderTypeId]
);
GO

-- B.2  Add the new FK columns (nullable during backfill).
IF COL_LENGTH('dbo.PhoneNumber','ProviderId') IS NULL
    ALTER TABLE [dbo].[PhoneNumber] ADD [ProviderId] INT NULL;
GO
IF COL_LENGTH('dbo.PhoneNumber','CountryId') IS NULL
    ALTER TABLE [dbo].[PhoneNumber] ADD [CountryId] SMALLINT NULL;
GO

-- B.3  Backfill from the old varchar columns (while they still exist).
IF COL_LENGTH('dbo.PhoneNumber','Provider') IS NOT NULL
    UPDATE pn SET [ProviderId] = p.[ProviderId]
    FROM [dbo].[PhoneNumber] pn
    INNER JOIN [dbo].[Provider] p
        ON p.[Code] = pn.[Provider] AND p.[ProviderTypeId] = 3   -- Phone
    WHERE pn.[ProviderId] IS NULL;
GO
IF COL_LENGTH('dbo.PhoneNumber','CountryCode') IS NOT NULL
    UPDATE pn SET [CountryId] = c.[CountryId]
    FROM [dbo].[PhoneNumber] pn
    INNER JOIN [dbo].[Country] c ON c.[Code] = pn.[CountryCode]
    WHERE pn.[CountryId] IS NULL;
GO

-- B.4  Enforce NOT NULL + FKs once fully backfilled.
IF COL_LENGTH('dbo.PhoneNumber','ProviderId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PhoneNumber') AND name = 'ProviderId' AND is_nullable = 1)
   AND NOT EXISTS (SELECT 1 FROM [dbo].[PhoneNumber] WHERE [ProviderId] IS NULL)
    ALTER TABLE [dbo].[PhoneNumber] ALTER COLUMN [ProviderId] INT NOT NULL;
GO
IF COL_LENGTH('dbo.PhoneNumber','CountryId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PhoneNumber') AND name = 'CountryId' AND is_nullable = 1)
   AND NOT EXISTS (SELECT 1 FROM [dbo].[PhoneNumber] WHERE [CountryId] IS NULL)
    ALTER TABLE [dbo].[PhoneNumber] ALTER COLUMN [CountryId] SMALLINT NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PhoneNumber_Provider')
    ALTER TABLE [dbo].[PhoneNumber] ADD CONSTRAINT [FK_PhoneNumber_Provider]
        FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider]([ProviderId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PhoneNumber_Country')
    ALTER TABLE [dbo].[PhoneNumber] ADD CONSTRAINT [FK_PhoneNumber_Country]
        FOREIGN KEY ([CountryId]) REFERENCES [dbo].[Country]([CountryId]);
GO

-- B.5  Swap the (CountryCode,Status) index for (CountryId,Status).
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PhoneNumbers_CountryCode_Status' AND object_id = OBJECT_ID('dbo.PhoneNumber'))
    DROP INDEX [IX_PhoneNumbers_CountryCode_Status] ON [dbo].[PhoneNumber];
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PhoneNumber_CountryId_Status' AND object_id = OBJECT_ID('dbo.PhoneNumber'))
    CREATE INDEX [IX_PhoneNumber_CountryId_Status] ON [dbo].[PhoneNumber] ([CountryId], [Status]);
GO

-- B.6  Drop the old varchar columns (drop the Provider default constraint first).
DECLARE @df sysname;
SELECT @df = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
WHERE c.object_id = OBJECT_ID('dbo.PhoneNumber') AND c.name = 'Provider';
IF @df IS NOT NULL EXEC('ALTER TABLE [dbo].[PhoneNumber] DROP CONSTRAINT [' + @df + ']');

IF COL_LENGTH('dbo.PhoneNumber','Provider') IS NOT NULL
    ALTER TABLE [dbo].[PhoneNumber] DROP COLUMN [Provider];
GO
IF COL_LENGTH('dbo.PhoneNumber','CountryCode') IS NOT NULL
    ALTER TABLE [dbo].[PhoneNumber] DROP COLUMN [CountryCode];
GO

PRINT '030-pk-rename-and-phone-fks.sql complete.';
GO
