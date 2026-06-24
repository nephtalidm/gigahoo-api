-- 008-merge-user-into-account.sql
-- Merges User (auth identity) into Account. They were 1:1 with no reason to be separate.
-- After migration: Account is the only identity table.

-- 1. Add auth identity columns to Account (if not already present)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'NormalizedEmail')
    ALTER TABLE [dbo].[Account] ADD NormalizedEmail NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PhoneNumber')
    ALTER TABLE [dbo].[Account] ADD PhoneNumber NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'NormalizedPhone')
    ALTER TABLE [dbo].[Account] ADD NormalizedPhone NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'GoogleSubjectId')
    ALTER TABLE [dbo].[Account] ADD GoogleSubjectId NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'DisplayName')
    ALTER TABLE [dbo].[Account] ADD DisplayName NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'IsEmailConfirmed')
    ALTER TABLE [dbo].[Account] ADD IsEmailConfirmed BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'IsPhoneConfirmed')
    ALTER TABLE [dbo].[Account] ADD IsPhoneConfirmed BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'LastLoginAt')
    ALTER TABLE [dbo].[Account] ADD LastLoginAt DATETIME2(7) NULL;

-- 2. Migrate data from User into Account (via the 1:1 FK Account.UserId -> User.Id)
UPDATE a SET
    a.NormalizedEmail = u.NormalizedEmail,
    a.PhoneNumber = u.PhoneNumber,
    a.NormalizedPhone = u.NormalizedPhone,
    a.GoogleSubjectId = u.GoogleSubjectId,
    a.DisplayName = u.DisplayName,
    a.IsEmailConfirmed = u.IsEmailConfirmed,
    a.IsPhoneConfirmed = u.IsPhoneConfirmed,
    a.LastLoginAt = u.LastLoginAt
FROM [dbo].[Account] a
INNER JOIN [dbo].[User] u ON u.Id = a.UserId;

-- 3. For accounts that also have email on Account but no User match, ensure normalized email is set
UPDATE a SET
    a.NormalizedEmail = LOWER(a.Email)
WHERE a.NormalizedEmail IS NULL AND a.Email IS NOT NULL;

-- 4. Create unique indexes on Account for auth lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_NormalizedEmail')
    CREATE UNIQUE INDEX [IX_Account_NormalizedEmail] ON [dbo].[Account]([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_NormalizedPhone')
    CREATE UNIQUE INDEX [IX_Account_NormalizedPhone] ON [dbo].[Account]([NormalizedPhone]) WHERE [NormalizedPhone] IS NOT NULL;
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_GoogleSubjectId')
    CREATE UNIQUE INDEX [IX_Account_GoogleSubjectId] ON [dbo].[Account]([GoogleSubjectId]) WHERE [GoogleSubjectId] IS NOT NULL;

-- 5. Drop FK from Account to User
DECLARE @fkName NVARCHAR(256);
SELECT @fkName = name FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID(N'[dbo].[Account]')
  AND referenced_object_id = OBJECT_ID(N'[dbo].[User]');
IF @fkName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + @fkName + ']');
    PRINT 'Dropped FK Account_UserId';
END

-- 6. Drop UserId column from Account
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'UserId')
BEGIN
    ALTER TABLE [dbo].[Account] DROP COLUMN [UserId];
    PRINT 'Dropped Account.UserId column';
END

-- 7. Drop the User trigger
IF OBJECT_ID('dbo.TR_User_UpdatedAt', 'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_User_UpdatedAt];
    PRINT 'Dropped TR_User_UpdatedAt trigger';

-- 8. Drop the User table
IF OBJECT_ID('dbo.User', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[User];
    PRINT 'Dropped User table';
END
ELSE
    PRINT 'User table already dropped';
GO

-- 9. Drop unique index on Account.UserId (if still exists)
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_UserId')
BEGIN
    DROP INDEX [IX_Account_UserId] ON [dbo].[Account];
    PRINT 'Dropped IX_Account_UserId index';
END

PRINT 'User merged into Account successfully';
GO
