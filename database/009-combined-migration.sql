-- Combined migration: run feature-settings + user merge
-- Assumes 006 already ran (PaymentMethod and RefreshToken dropped)
SET QUOTED_IDENTIFIER ON;
GO

-- Step 1: Add feature setting columns to Account
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'AnswerQuestions')
    ALTER TABLE [dbo].[Account] ADD AnswerQuestions BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'ServicesInfo')
    ALTER TABLE [dbo].[Account] ADD ServicesInfo NVARCHAR(2000) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FeatureServiceAreas')
    ALTER TABLE [dbo].[Account] ADD FeatureServiceAreas NVARCHAR(500) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FeatureBusinessHours')
    ALTER TABLE [dbo].[Account] ADD FeatureBusinessHours NVARCHAR(500) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'EmergencyAvailability')
    ALTER TABLE [dbo].[Account] ADD EmergencyAvailability NVARCHAR(500) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PricingPolicy')
    ALTER TABLE [dbo].[Account] ADD PricingPolicy NVARCHAR(2000) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'WarrantyPolicy')
    ALTER TABLE [dbo].[Account] ADD WarrantyPolicy NVARCHAR(2000) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FrequentlyAskedQuestions')
    ALTER TABLE [dbo].[Account] ADD FrequentlyAskedQuestions NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'AdditionalBusinessInfo')
    ALTER TABLE [dbo].[Account] ADD AdditionalBusinessInfo NVARCHAR(2000) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'ServeArea')
    ALTER TABLE [dbo].[Account] ADD ServeArea BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'DistanceKm')
    ALTER TABLE [dbo].[Account] ADD DistanceKm INT NOT NULL DEFAULT 50;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'QuoteInspection')
    ALTER TABLE [dbo].[Account] ADD QuoteInspection BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PricePerKm')
    ALTER TABLE [dbo].[Account] ADD PricePerKm DECIMAL(10,2) NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FeatureUpdatedAt')
    ALTER TABLE [dbo].[Account] ADD FeatureUpdatedAt DATETIME2(7) NULL;
GO
PRINT 'Feature setting columns added to Account';
GO

-- Step 2: Migrate FeatureSetting data into Account
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'FeatureSetting')
BEGIN
    UPDATE a SET
        a.AnswerQuestions = ISNULL(fs.AnswerQuestions, 0),
        a.ServicesInfo = fs.ServicesInfo,
        a.FeatureServiceAreas = fs.ServiceAreas,
        a.FeatureBusinessHours = fs.BusinessHours,
        a.EmergencyAvailability = fs.EmergencyAvailability,
        a.PricingPolicy = fs.PricingPolicy,
        a.WarrantyPolicy = fs.WarrantyPolicy,
        a.FrequentlyAskedQuestions = fs.FrequentlyAskedQuestions,
        a.AdditionalBusinessInfo = fs.AdditionalBusinessInfo,
        a.ServeArea = ISNULL(fs.ServeArea, 0),
        a.DistanceKm = ISNULL(fs.DistanceKm, 50),
        a.QuoteInspection = ISNULL(fs.QuoteInspection, 0),
        a.PricePerKm = ISNULL(fs.PricePerKm, 0),
        a.FeatureUpdatedAt = fs.UpdatedAt
    FROM [dbo].[Account] a
    INNER JOIN [dbo].[FeatureSetting] fs ON fs.AccountId = a.Id;
    PRINT 'FeatureSetting data migrated to Account';

    DROP TABLE [dbo].[FeatureSetting];
    PRINT 'Dropped FeatureSetting table';
END
ELSE
BEGIN
    PRINT 'FeatureSetting table already dropped, no data to migrate';
END
GO

-- Step 3: Add auth identity columns to Account
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'NormalizedEmail')
    ALTER TABLE [dbo].[Account] ADD NormalizedEmail NVARCHAR(256) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PhoneNumber')
    ALTER TABLE [dbo].[Account] ADD PhoneNumber NVARCHAR(50) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'NormalizedPhone')
    ALTER TABLE [dbo].[Account] ADD NormalizedPhone NVARCHAR(50) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'GoogleSubjectId')
    ALTER TABLE [dbo].[Account] ADD GoogleSubjectId NVARCHAR(256) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'DisplayName')
    ALTER TABLE [dbo].[Account] ADD DisplayName NVARCHAR(256) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'IsEmailConfirmed')
    ALTER TABLE [dbo].[Account] ADD IsEmailConfirmed BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'IsPhoneConfirmed')
    ALTER TABLE [dbo].[Account] ADD IsPhoneConfirmed BIT NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'LastLoginAt')
    ALTER TABLE [dbo].[Account] ADD LastLoginAt DATETIME2(7) NULL;
GO
PRINT 'Auth identity columns added to Account';
GO

-- Step 4: Migrate User data into Account
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'User')
BEGIN
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
    PRINT 'User data migrated to Account';

    -- For accounts with email but no User match
    UPDATE a SET
        a.NormalizedEmail = LOWER(a.Email)
    WHERE a.NormalizedEmail IS NULL AND a.Email IS NOT NULL;
    PRINT 'Normalized email set for remaining accounts';
END
ELSE
BEGIN
    PRINT 'User table already dropped, no data to migrate';
    -- Still need to set normalized emails if missing
    UPDATE a SET
        a.NormalizedEmail = LOWER(a.Email)
    WHERE a.NormalizedEmail IS NULL AND a.Email IS NOT NULL;
    PRINT 'Normalized email set for remaining accounts';
END
GO

-- Step 5: Create unique indexes on Account for auth lookups
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_NormalizedEmail')
    CREATE UNIQUE INDEX [IX_Account_NormalizedEmail] ON [dbo].[Account]([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_NormalizedPhone')
    CREATE UNIQUE INDEX [IX_Account_NormalizedPhone] ON [dbo].[Account]([NormalizedPhone]) WHERE [NormalizedPhone] IS NOT NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_GoogleSubjectId')
    CREATE UNIQUE INDEX [IX_Account_GoogleSubjectId] ON [dbo].[Account]([GoogleSubjectId]) WHERE [GoogleSubjectId] IS NOT NULL;
GO
PRINT 'Auth indexes created on Account';
GO

-- Step 6: Drop unique index on Account.UserId (must drop before column)
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Accounts_UserId')
BEGIN
    DROP INDEX [IX_Accounts_UserId] ON [dbo].[Account];
    PRINT 'Dropped IX_Accounts_UserId index';
END
GO

-- Step 7: Drop FK from Account to User
DECLARE @fkName NVARCHAR(256);
SELECT @fkName = name FROM sys.foreign_keys
WHERE parent_object_id = OBJECT_ID(N'[dbo].[Account]')
  AND referenced_object_id = OBJECT_ID(N'[dbo].[User]');
IF @fkName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + @fkName + ']');
    PRINT 'Dropped FK Account_UserId';
END
ELSE
BEGIN
    PRINT 'No FK to drop';
END
GO

-- Step 8: Drop UserId column from Account
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'UserId')
BEGIN
    ALTER TABLE [dbo].[Account] DROP COLUMN [UserId];
    PRINT 'Dropped Account.UserId column';
END
GO

-- Step 9: Drop the User trigger
IF OBJECT_ID('dbo.TR_User_UpdatedAt', 'TR') IS NOT NULL
BEGIN
    DROP TRIGGER [dbo].[TR_User_UpdatedAt];
    PRINT 'Dropped TR_User_UpdatedAt trigger';
END
GO

-- Step 10: Drop the User table
IF OBJECT_ID('dbo.User', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[User];
    PRINT 'Dropped User table';
END
ELSE
BEGIN
    PRINT 'User table already dropped';
END
GO

-- Step 11: Drop unique index on Account.UserId (cleanup if still exists)
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Account_UserId')
BEGIN
    DROP INDEX [IX_Account_UserId] ON [dbo].[Account];
    PRINT 'Dropped IX_Account_UserId index';
END
GO

PRINT 'All migrations completed successfully';
GO
