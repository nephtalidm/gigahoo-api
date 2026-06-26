-- 022-account-voice-greeting.sql
-- Per-account AI voice agent settings: the custom greeting spoken when a call is
-- answered, and the selected TTS voice (Qwen Realtime voice name). NULL = default.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'GreetingMessage')
    ALTER TABLE [dbo].[Account] ADD [GreetingMessage] nvarchar(500) NULL;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'AgentVoice')
    ALTER TABLE [dbo].[Account] ADD [AgentVoice] nvarchar(50) NULL;
GO
PRINT 'Account.GreetingMessage + AgentVoice added';
GO
