SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 043-remove-non-fish-tts-voices.sql
-- Retire the non-Fish TTS voices: we are consolidating on Fish Audio (S2) as the only split-voice
-- TTS engine, so the Qwen-TTS and CosyVoice lab/synth voices are no longer offered. Deactivate them
-- (IsActive = 0) rather than DELETE — same reversible idiom as 037/042, so the rows (and any account
-- that ever referenced one) survive and the Fish voices can be seeded fresh later under a new provider.
-- The 'qwen' provider's OMNI REALTIME voices are left untouched: they drive live calls via the voice
-- picker and are part of the unchanged call flow. Idempotent: safe to run repeatedly.

UPDATE v
SET v.[IsActive] = 0,
    v.[IsDefault] = 0
FROM [dbo].[Voice] v
JOIN [dbo].[Provider] p ON p.[ProviderId] = v.[ProviderId]
WHERE p.[Code] IN ('qwen-tts', 'cosyvoice')
  AND p.[ProviderTypeId] = 1;
GO

PRINT '043-remove-non-fish-tts-voices applied (qwen-tts + cosyvoice voices deactivated; qwen omni voices kept)';
GO
