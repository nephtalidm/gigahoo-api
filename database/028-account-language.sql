-- 028-account-language.sql
-- The account's preferred website language (ISO locale, e.g. 'en','es'). Set at
-- signup from the locale the user signed up in; switchable in General Settings.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'AccountLanguage')
    ALTER TABLE [dbo].[Account] ADD [AccountLanguage] nvarchar(10) NULL;
GO
PRINT 'Account.AccountLanguage added';
GO
