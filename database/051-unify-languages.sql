SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 051-unify-languages.sql
-- ONE language system: the Language table now carries the BCP-47-ish locale Code for every
-- row, the five UI-only locales become real rows, and Account's dashboard-locale string
-- (AccountLanguage) becomes an AccountLanguageId FK into the same table used by
-- Conversation.LanguageId and AgentVoice.LanguageId. Keep the Code list in sync with the
-- UI's shipped dictionaries (lib/i18n). Idempotent.

------------------------------------------------------------------------------
-- 1. Language.Code (+ backfill for the original nine rows).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Language]') AND name = 'Code')
    ALTER TABLE [dbo].[Language] ADD [Code] nvarchar(10) NULL;
GO
UPDATE [dbo].[Language] SET [Code] = CASE [Name]
    WHEN N'English'   THEN 'en'  WHEN N'French'   THEN 'fr'  WHEN N'Mandarin' THEN 'zh'
    WHEN N'Cantonese' THEN 'yue' WHEN N'Spanish'  THEN 'es'  WHEN N'Japanese' THEN 'ja'
    WHEN N'Hindi'     THEN 'hi'  WHEN N'Korean'   THEN 'ko'  WHEN N'Tagalog'  THEN 'tl'
    ELSE [Code] END
WHERE [Code] IS NULL;
GO

------------------------------------------------------------------------------
-- 2. The UI-only locales become Language rows.
------------------------------------------------------------------------------
INSERT INTO [dbo].[Language] ([Name], [Code])
SELECT v.[Name], v.[Code]
FROM (VALUES (N'Arabic','ar'), (N'Persian','fa'), (N'Punjabi','pa'), (N'Russian','ru'), (N'Ukrainian','uk')) v([Name],[Code])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[Language] l WHERE l.[Code] = v.[Code]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Language_Code' AND object_id = OBJECT_ID('[dbo].[Language]'))
    CREATE UNIQUE INDEX [UX_Language_Code] ON [dbo].[Language]([Code]) WHERE [Code] IS NOT NULL;
GO

------------------------------------------------------------------------------
-- 3. Account.AccountLanguageId FK, backfilled from the old locale string.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'AccountLanguageId')
    ALTER TABLE [dbo].[Account] ADD [AccountLanguageId] tinyint NULL
        CONSTRAINT FK_Account_AccountLanguage REFERENCES [dbo].[Language]([LanguageId]);
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'AccountLanguage')
BEGIN
    EXEC sp_executesql N'
        UPDATE a SET a.[AccountLanguageId] = l.[LanguageId]
        FROM [dbo].[Account] a
        JOIN [dbo].[Language] l ON l.[Code] = a.[AccountLanguage]
        WHERE a.[AccountLanguage] IS NOT NULL;';
    ALTER TABLE [dbo].[Account] DROP COLUMN [AccountLanguage];
END
GO

PRINT '051-unify-languages applied (Language.Code + 5 locale rows; Account.AccountLanguageId FK)';
GO
