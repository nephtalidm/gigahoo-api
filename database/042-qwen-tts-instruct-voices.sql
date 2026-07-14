SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 042-qwen-tts-instruct-voices.sql
-- Fix 041: it seeded 7 voices (Jennifer, Ryan, Katerina, Jada, Dylan, Sunny, Marcus) that do NOT
-- support the instruct models — they'd ignore the `instructions` directive entirely, defeating the
-- whole Qwen3-TTS-Instruct pivot. Replace them with instruct-capable, English-speaking voices
-- (per the Qwen-TTS voice list). Also correct Elias's label (it's female). Idempotent.

DECLARE @qwentts int = (SELECT [ProviderId] FROM [dbo].[Provider] WHERE [Code] = 'qwen-tts' AND [ProviderTypeId] = 1);

-- The final instruct-capable set, in picker order (ApiName = API voice value, Label = picker text).
DECLARE @voices TABLE ([ApiName] nvarchar(64), [Label] nvarchar(128), [DisplayOrder] int);
INSERT INTO @voices ([ApiName],[Label],[DisplayOrder]) VALUES
  (N'Cherry',  N'Cherry (female)',  0),
  (N'Ethan',   N'Ethan (male)',     1),
  (N'Serena',  N'Serena (female)',  2),
  (N'Bella',   N'Bella (female)',   3),
  (N'Maia',    N'Maia (female)',    4),
  (N'Elias',   N'Elias (female)',   5),
  (N'Moon',    N'Moon (male)',      6),
  (N'Kai',     N'Kai (male)',       7),
  (N'Neil',    N'Neil (male)',      8),
  (N'Vincent', N'Vincent (male)',   9);

-- 1. Insert any not yet present.
INSERT INTO [dbo].[Voice] ([ProviderId],[ApiName],[Label],[DisplayOrder],[IsDefault],[IsActive])
SELECT @qwentts, v.[ApiName], v.[Label], v.[DisplayOrder], 0, 1
FROM @voices v
WHERE @qwentts IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [dbo].[Voice] x WHERE x.[ProviderId] = @qwentts AND x.[ApiName] = v.[ApiName]);

-- 2. Reactivate + relabel + reorder the whole instruct-capable set.
UPDATE tgt SET tgt.[Label] = v.[Label], tgt.[DisplayOrder] = v.[DisplayOrder], tgt.[IsActive] = 1
FROM [dbo].[Voice] tgt
JOIN @voices v ON v.[ApiName] = tgt.[ApiName]
WHERE tgt.[ProviderId] = @qwentts;

-- 3. Deactivate anything NOT in the instruct-capable set (the 7 bad voices from 041).
UPDATE [dbo].[Voice] SET [IsActive] = 0
WHERE [ProviderId] = @qwentts AND [ApiName] NOT IN (SELECT [ApiName] FROM @voices);

-- 4. Cherry is the (single) default, and active.
UPDATE [dbo].[Voice] SET [IsDefault] = 0 WHERE [ProviderId] = @qwentts;
UPDATE [dbo].[Voice] SET [IsDefault] = 1, [IsActive] = 1 WHERE [ProviderId] = @qwentts AND [ApiName] = N'Cherry';
GO

PRINT '042-qwen-tts-instruct-voices applied (10 instruct-capable voices active; non-instruct voices deactivated)';
GO
