SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 041-qwen-tts-voices.sql
-- Add the managed Qwen3-TTS-Instruct-Flash provider ('qwen-tts', an LLM-type provider so it shares
-- the Voice table + ProviderTypeId=1 picker plumbing) and seed 10 of its voices. Unlike CosyVoice,
-- Qwen-TTS takes FREE-English instructs, so the creative "speaking context" presets live in code
-- (QwenInstructs) and apply to every voice — no per-voice option seeding needed here.
-- Idempotent: safe to run repeatedly.

------------------------------------------------------------------------------
-- 1. Provider row.
------------------------------------------------------------------------------
INSERT INTO [dbo].[Provider] ([Name],[Code],[ProviderTypeId])
SELECT 'Qwen TTS','qwen-tts',1
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Provider] p WHERE p.[Code] = 'qwen-tts' AND p.[ProviderTypeId] = 1
);
GO

-- NOTE: migration 030 renamed the Provider PK to [ProviderId] (not [Id] as 027 originally created it).

------------------------------------------------------------------------------
-- 2. Seed 10 Qwen-TTS voices (insert only if missing, keyed on ProviderId+ApiName).
------------------------------------------------------------------------------
DECLARE @qwentts int = (SELECT [ProviderId] FROM [dbo].[Provider] WHERE [Code] = 'qwen-tts' AND [ProviderTypeId] = 1);

INSERT INTO [dbo].[Voice] ([ProviderId],[ApiName],[Label],[DisplayOrder],[IsDefault],[IsActive])
SELECT @qwentts, s.[ApiName], s.[Label], s.[DisplayOrder], s.[IsDefault], 1
FROM (VALUES
        ('Cherry',  'Cherry (female)',   0, 1),
        ('Ethan',   'Ethan (male)',      1, 0),
        ('Jennifer','Jennifer (female)', 2, 0),
        ('Ryan',    'Ryan (male)',       3, 0),
        ('Katerina','Katerina (female)', 4, 0),
        ('Elias',   'Elias (male)',      5, 0),
        ('Jada',    'Jada (female)',     6, 0),
        ('Dylan',   'Dylan (male)',      7, 0),
        ('Sunny',   'Sunny (female)',    8, 0),
        ('Marcus',  'Marcus (male)',     9, 0)
     ) AS s([ApiName],[Label],[DisplayOrder],[IsDefault])
WHERE @qwentts IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM [dbo].[Voice] v
        WHERE v.[ProviderId] = @qwentts AND v.[ApiName] = s.[ApiName]
);
GO

PRINT '041-qwen-tts-voices applied (qwen-tts provider + 10 voices seeded)';
GO
