SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 056-per-language-voice-defaults.sql
-- IsDefault on AgentVoice now means "the default voice WITHIN its language" (one per
-- language) — it powers the per-language voice map the call agent uses to switch voices
-- when the caller's language changes. English already had Daniel; Spanish gets Lucas and
-- Japanese gets Ethan. Idempotent.

-- The old uniqueness rule allowed ONE default per provider; the new rule is one per
-- (provider, language).
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Voice_OneDefault' AND object_id = OBJECT_ID('[dbo].[AgentVoice]'))
    DROP INDEX [UX_Voice_OneDefault] ON [dbo].[AgentVoice];
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_AgentVoice_OneDefaultPerLanguage' AND object_id = OBJECT_ID('[dbo].[AgentVoice]'))
    CREATE UNIQUE INDEX [UX_AgentVoice_OneDefaultPerLanguage]
        ON [dbo].[AgentVoice]([ProviderId], [LanguageId]) WHERE [IsDefault] = 1;
GO

UPDATE [dbo].[AgentVoice] SET [IsDefault] = 1 WHERE [ReferenceId] = 'c3719ef423f6494f9d0389e4274bb379'; -- Lucas (Spanish)
UPDATE [dbo].[AgentVoice] SET [IsDefault] = 1 WHERE [ReferenceId] = '4df04f6031014e93b3eb6333c4df104e'; -- Ethan (Japanese)
GO

PRINT '056-per-language-voice-defaults applied (Daniel/en, Lucas/es, Ethan/ja)';
GO
