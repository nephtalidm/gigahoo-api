-- 013-make-account-email-nullable.sql
-- Phone-only signup creates accounts without an email, but the Account.Email
-- column was left NOT NULL after the User->Account merge. Make it nullable to
-- match the entity (string? Email) and the filtered unique index on NormalizedEmail.

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'Email' AND is_nullable = 0)
    ALTER TABLE [dbo].[Account] ALTER COLUMN [Email] NVARCHAR(256) NULL;
GO

PRINT 'Account.Email made nullable';
GO
