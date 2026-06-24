-- 011-make-account-business-columns-nullable.sql
-- Makes business profile columns nullable so auth can create minimal accounts

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'BusinessName' AND is_nullable = 0)
    ALTER TABLE [dbo].[Account] ALTER COLUMN [BusinessName] NVARCHAR(256) NULL;
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'CategoryId' AND is_nullable = 0)
    ALTER TABLE [dbo].[Account] ALTER COLUMN [CategoryId] TINYINT NULL;
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'BusinessPhone' AND is_nullable = 0)
    ALTER TABLE [dbo].[Account] ALTER COLUMN [BusinessPhone] NVARCHAR(50) NULL;
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'CountryId' AND is_nullable = 0)
    ALTER TABLE [dbo].[Account] ALTER COLUMN [CountryId] SMALLINT NULL;
GO

PRINT 'Account business columns made nullable';
GO
