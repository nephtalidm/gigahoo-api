SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 038-account-mood.sql
-- Add the AI voice tone/mood setting to Account (Friendly/Professional/Cheerful/Calm/Energetic;
-- NULL = default Friendly). Injected into the call prompt by the voice agent. Idempotent.

IF COL_LENGTH('dbo.Account', 'AgentMood') IS NULL
    ALTER TABLE [dbo].[Account] ADD [AgentMood] nvarchar(30) NULL;
GO

PRINT '038-account-mood applied (Account.AgentMood added)';
GO
