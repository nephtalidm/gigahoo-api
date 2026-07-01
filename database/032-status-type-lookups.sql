-- 032-status-type-lookups.sql
-- Normalize status/type varchar+enum columns into lookup tables + FKs
-- (PhoneNumber/Conversation/Invoice Status, plus a new Conversation Type),
-- drop PhoneNumber audit timestamps, and rename Conversation.CallerPhone ->
-- CallerPhoneNumber. Idempotent.
--
-- Deploy note: apply BEFORE starting the new API build (incompatible columns).

SET XACT_ABORT ON;
GO

------------------------------------------------------------------------------
-- 1. Lookup tables (seeded with explicit ids matching the C# enums).
------------------------------------------------------------------------------
IF OBJECT_ID('dbo.PhoneNumberStatus') IS NULL
    CREATE TABLE dbo.PhoneNumberStatus (
        PhoneNumberStatusId tinyint NOT NULL CONSTRAINT PK_PhoneNumberStatus PRIMARY KEY,
        Name nvarchar(30) NOT NULL CONSTRAINT UX_PhoneNumberStatus_Name UNIQUE);
GO
INSERT INTO dbo.PhoneNumberStatus (PhoneNumberStatusId, Name)
SELECT v.id, v.nm FROM (VALUES (1,'Available'),(2,'Assigned'),(3,'Released')) v(id,nm)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PhoneNumberStatus s WHERE s.PhoneNumberStatusId = v.id);
GO

IF OBJECT_ID('dbo.ConversationStatus') IS NULL
    CREATE TABLE dbo.ConversationStatus (
        ConversationStatusId tinyint NOT NULL CONSTRAINT PK_ConversationStatus PRIMARY KEY,
        Name nvarchar(30) NOT NULL CONSTRAINT UX_ConversationStatus_Name UNIQUE);
GO
INSERT INTO dbo.ConversationStatus (ConversationStatusId, Name)
SELECT v.id, v.nm FROM (VALUES (1,'Missed'),(2,'Answered'),(3,'Completed'),(4,'Live')) v(id,nm)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ConversationStatus s WHERE s.ConversationStatusId = v.id);
GO

IF OBJECT_ID('dbo.ConversationType') IS NULL
    CREATE TABLE dbo.ConversationType (
        ConversationTypeId tinyint NOT NULL CONSTRAINT PK_ConversationType PRIMARY KEY,
        Name nvarchar(30) NOT NULL CONSTRAINT UX_ConversationType_Name UNIQUE);
GO
INSERT INTO dbo.ConversationType (ConversationTypeId, Name)
SELECT v.id, v.nm FROM (VALUES (1,'Phone Call'),(2,'Web Call')) v(id,nm)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ConversationType s WHERE s.ConversationTypeId = v.id);
GO

IF OBJECT_ID('dbo.InvoiceStatus') IS NULL
    CREATE TABLE dbo.InvoiceStatus (
        InvoiceStatusId tinyint NOT NULL CONSTRAINT PK_InvoiceStatus PRIMARY KEY,
        Name nvarchar(30) NOT NULL CONSTRAINT UX_InvoiceStatus_Name UNIQUE);
GO
INSERT INTO dbo.InvoiceStatus (InvoiceStatusId, Name)
SELECT v.id, v.nm FROM (VALUES (1,'Paid'),(2,'Open'),(3,'Failed'),(4,'Void')) v(id,nm)
WHERE NOT EXISTS (SELECT 1 FROM dbo.InvoiceStatus s WHERE s.InvoiceStatusId = v.id);
GO

------------------------------------------------------------------------------
-- Helpers: drop every index on a column, and drop a column's default constraint.
------------------------------------------------------------------------------
-- 2. PhoneNumber: Status(nvarchar) -> PhoneNumberStatusId; drop audit timestamps.
------------------------------------------------------------------------------
IF COL_LENGTH('dbo.PhoneNumber','PhoneNumberStatusId') IS NULL
    ALTER TABLE dbo.PhoneNumber ADD PhoneNumberStatusId tinyint NULL;
GO
IF COL_LENGTH('dbo.PhoneNumber','Status') IS NOT NULL
    UPDATE dbo.PhoneNumber SET PhoneNumberStatusId =
        CASE WHEN Status='Assigned' THEN 2 WHEN Status='Released' THEN 3 ELSE 1 END
    WHERE PhoneNumberStatusId IS NULL;
GO
-- Drop any index referencing the old Status column (so the column can be dropped).
DECLARE @sql nvarchar(max) = '';
SELECT @sql = @sql + 'DROP INDEX ' + QUOTENAME(i.name) + ' ON dbo.PhoneNumber;'
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID('dbo.PhoneNumber') AND c.name='Status' AND i.is_primary_key=0;
IF @sql <> '' EXEC(@sql);
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.PhoneNumber') AND name='PhoneNumberStatusId' AND is_nullable=1)
   AND NOT EXISTS (SELECT 1 FROM dbo.PhoneNumber WHERE PhoneNumberStatusId IS NULL)
    ALTER TABLE dbo.PhoneNumber ALTER COLUMN PhoneNumberStatusId tinyint NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_PhoneNumber_PhoneNumberStatus')
    ALTER TABLE dbo.PhoneNumber ADD CONSTRAINT FK_PhoneNumber_PhoneNumberStatus
        FOREIGN KEY (PhoneNumberStatusId) REFERENCES dbo.PhoneNumberStatus(PhoneNumberStatusId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PhoneNumber_PhoneNumberStatusId' AND object_id=OBJECT_ID('dbo.PhoneNumber'))
    CREATE INDEX IX_PhoneNumber_PhoneNumberStatusId ON dbo.PhoneNumber(PhoneNumberStatusId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PhoneNumber_CountryId_PhoneNumberStatusId' AND object_id=OBJECT_ID('dbo.PhoneNumber'))
    CREATE INDEX IX_PhoneNumber_CountryId_PhoneNumberStatusId ON dbo.PhoneNumber(CountryId, PhoneNumberStatusId);
GO
-- Drop old Status + audit-timestamp columns (dropping default constraints first).
DECLARE @t sysname = 'dbo.PhoneNumber', @col sysname, @dc sysname, @drop nvarchar(max);
DECLARE cols CURSOR LOCAL FAST_FORWARD FOR SELECT c FROM (VALUES ('Status'),('ReleasedAt'),('CreatedAt'),('UpdatedAt')) v(c);
OPEN cols; FETCH NEXT FROM cols INTO @col;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(@t, @col) IS NOT NULL
    BEGIN
        SET @dc = NULL;
        SELECT @dc = dc.name FROM sys.default_constraints dc
            JOIN sys.columns c ON c.default_object_id=dc.object_id
            WHERE c.object_id=OBJECT_ID(@t) AND c.name=@col;
        IF @dc IS NOT NULL EXEC('ALTER TABLE '+@t+' DROP CONSTRAINT ['+@dc+']');
        SET @drop = 'ALTER TABLE '+@t+' DROP COLUMN ['+@col+']'; EXEC(@drop);
    END
    FETCH NEXT FROM cols INTO @col;
END
CLOSE cols; DEALLOCATE cols;
GO

------------------------------------------------------------------------------
-- 3. Conversation: Status -> ConversationStatusId, add ConversationTypeId,
--    rename CallerPhone -> CallerPhoneNumber.
------------------------------------------------------------------------------
IF COL_LENGTH('dbo.Conversation','ConversationStatusId') IS NULL
    ALTER TABLE dbo.Conversation ADD ConversationStatusId tinyint NULL;
GO
IF COL_LENGTH('dbo.Conversation','Status') IS NOT NULL
    UPDATE dbo.Conversation SET ConversationStatusId =
        CASE WHEN Status='Answered' THEN 2 WHEN Status='Completed' THEN 3 WHEN Status='Live' THEN 4 ELSE 1 END
    WHERE ConversationStatusId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Conversation') AND name='ConversationStatusId' AND is_nullable=1)
   AND NOT EXISTS (SELECT 1 FROM dbo.Conversation WHERE ConversationStatusId IS NULL)
    ALTER TABLE dbo.Conversation ALTER COLUMN ConversationStatusId tinyint NOT NULL;
GO
IF COL_LENGTH('dbo.Conversation','ConversationTypeId') IS NULL
    ALTER TABLE dbo.Conversation ADD ConversationTypeId tinyint NOT NULL
        CONSTRAINT DF_Conversation_ConversationTypeId DEFAULT 1;  -- 1 = Phone Call
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Conversation_ConversationStatus')
    ALTER TABLE dbo.Conversation ADD CONSTRAINT FK_Conversation_ConversationStatus
        FOREIGN KEY (ConversationStatusId) REFERENCES dbo.ConversationStatus(ConversationStatusId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Conversation_ConversationType')
    ALTER TABLE dbo.Conversation ADD CONSTRAINT FK_Conversation_ConversationType
        FOREIGN KEY (ConversationTypeId) REFERENCES dbo.ConversationType(ConversationTypeId);
GO
-- Drop indexes referencing old Status, then the column; add (AccountId, ConversationStatusId).
DECLARE @sql2 nvarchar(max) = '';
SELECT @sql2 = @sql2 + 'DROP INDEX ' + QUOTENAME(i.name) + ' ON dbo.Conversation;'
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
WHERE i.object_id=OBJECT_ID('dbo.Conversation') AND c.name='Status' AND i.is_primary_key=0;
IF @sql2 <> '' EXEC(@sql2);
GO
DECLARE @dc2 sysname;
SELECT @dc2 = dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.default_object_id=dc.object_id
    WHERE c.object_id=OBJECT_ID('dbo.Conversation') AND c.name='Status';
IF @dc2 IS NOT NULL EXEC('ALTER TABLE dbo.Conversation DROP CONSTRAINT ['+@dc2+']');
IF COL_LENGTH('dbo.Conversation','Status') IS NOT NULL ALTER TABLE dbo.Conversation DROP COLUMN Status;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Conversation_AccountId_ConversationStatusId' AND object_id=OBJECT_ID('dbo.Conversation'))
    CREATE INDEX IX_Conversation_AccountId_ConversationStatusId ON dbo.Conversation(AccountId, ConversationStatusId);
GO
IF COL_LENGTH('dbo.Conversation','CallerPhone') IS NOT NULL
   AND COL_LENGTH('dbo.Conversation','CallerPhoneNumber') IS NULL
    EXEC sp_rename 'dbo.Conversation.CallerPhone', 'CallerPhoneNumber', 'COLUMN';
GO

------------------------------------------------------------------------------
-- 4. Invoice: Status(nvarchar) -> InvoiceStatusId.
------------------------------------------------------------------------------
IF COL_LENGTH('dbo.Invoice','InvoiceStatusId') IS NULL
    ALTER TABLE dbo.Invoice ADD InvoiceStatusId tinyint NULL;
GO
IF COL_LENGTH('dbo.Invoice','Status') IS NOT NULL
    UPDATE dbo.Invoice SET InvoiceStatusId =
        CASE WHEN Status='Open' THEN 2 WHEN Status='Failed' THEN 3 WHEN Status='Void' THEN 4 ELSE 1 END
    WHERE InvoiceStatusId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('dbo.Invoice') AND name='InvoiceStatusId' AND is_nullable=1)
   AND NOT EXISTS (SELECT 1 FROM dbo.Invoice WHERE InvoiceStatusId IS NULL)
    ALTER TABLE dbo.Invoice ALTER COLUMN InvoiceStatusId tinyint NOT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name='FK_Invoice_InvoiceStatus')
    ALTER TABLE dbo.Invoice ADD CONSTRAINT FK_Invoice_InvoiceStatus
        FOREIGN KEY (InvoiceStatusId) REFERENCES dbo.InvoiceStatus(InvoiceStatusId);
GO
DECLARE @dc3 sysname;
SELECT @dc3 = dc.name FROM sys.default_constraints dc JOIN sys.columns c ON c.default_object_id=dc.object_id
    WHERE c.object_id=OBJECT_ID('dbo.Invoice') AND c.name='Status';
IF @dc3 IS NOT NULL EXEC('ALTER TABLE dbo.Invoice DROP CONSTRAINT ['+@dc3+']');
IF COL_LENGTH('dbo.Invoice','Status') IS NOT NULL ALTER TABLE dbo.Invoice DROP COLUMN Status;
GO

PRINT '032-status-type-lookups.sql complete.';
GO
