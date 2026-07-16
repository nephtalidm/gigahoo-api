SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 044-fish-voices.sql
-- The voice catalog becomes Fish Audio only: every existing Voice row is DELETED (accounts
-- reference voices by ApiName string — no FK — so a hard wipe is safe), and the 7 Fish voices
-- are seeded, each tagged with the NEW [Gender] column and the NEW [LanguageId] FK so the
-- dashboard picker can group by language and gender. Idempotent: the wipe+reseed converges.

------------------------------------------------------------------------------
-- 1. New columns: Gender + LanguageId (FK -> Language).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Voice]') AND name = 'Gender')
    ALTER TABLE [dbo].[Voice] ADD [Gender] nvarchar(10) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Voice]') AND name = 'LanguageId')
    ALTER TABLE [dbo].[Voice] ADD [LanguageId] tinyint NULL
        CONSTRAINT FK_Voice_Language REFERENCES [dbo].[Language]([LanguageId]);
GO

------------------------------------------------------------------------------
-- 2. Fish Audio provider row (LLM type, so it shares the Voice picker plumbing).
------------------------------------------------------------------------------
INSERT INTO [dbo].[Provider] ([Name],[Code],[ProviderTypeId])
SELECT 'Fish Audio','fish',1
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Provider] p WHERE p.[Code] = 'fish' AND p.[ProviderTypeId] = 1
);
GO

------------------------------------------------------------------------------
-- 3. Wipe ALL voices and seed the Fish catalog (language + gender).
------------------------------------------------------------------------------
DELETE FROM [dbo].[Voice];
GO

DECLARE @fish int    = (SELECT [ProviderId] FROM [dbo].[Provider] WHERE [Code] = 'fish' AND [ProviderTypeId] = 1);
DECLARE @en  tinyint = (SELECT [LanguageId] FROM [dbo].[Language] WHERE [Name] = N'English');
DECLARE @es  tinyint = (SELECT [LanguageId] FROM [dbo].[Language] WHERE [Name] = N'Spanish');
DECLARE @ja  tinyint = (SELECT [LanguageId] FROM [dbo].[Language] WHERE [Name] = N'Japanese');

INSERT INTO [dbo].[Voice] ([ProviderId],[ApiName],[Label],[Gender],[LanguageId],[DisplayOrder],[IsDefault],[IsActive])
VALUES
    (@fish, 'c0f7fea11f2d4b13b89a55a000b02c1b', N'Male',     N'male',   @en, 0, 1, 1),
    (@fish, '56b4cf7513704a33a074996859639cfd', N'Female',   N'female', @en, 1, 0, 1),
    (@fish, 'c3719ef423f6494f9d0389e4274bb379', N'Male',     N'male',   @es, 2, 0, 1),
    (@fish, '4df04f6031014e93b3eb6333c4df104e', N'Male 1',   N'male',   @ja, 3, 0, 1),
    (@fish, '098f0f275e8d41a0bc13598f70f46337', N'Male 2',   N'male',   @ja, 4, 0, 1),
    (@fish, '5161d41404314212af1254556477c17d', N'Female 1', N'female', @ja, 5, 0, 1),
    (@fish, 'f662e62acfb949958043ba29058fe282', N'Female 2', N'female', @ja, 6, 0, 1);
GO

PRINT '044-fish-voices applied (Voice wiped; 7 Fish voices seeded with Gender + LanguageId)';
GO
