-- 012-add-password-hash-to-account.sql
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PasswordHash')
    ALTER TABLE [dbo].[Account] ADD PasswordHash NVARCHAR(512) NULL;
GO
PRINT 'Added PasswordHash column to Account';
GO
