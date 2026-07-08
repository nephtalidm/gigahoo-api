SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 039-account-max-call-minutes.sql
-- Add a per-call hard cap (kill switch) to Account: the longest a single call may run before the
-- voice agent forcibly ends it, regardless of how the conversation is going. NULL = no cap.
-- Enforced by the VoiceAgent (call-session). Idempotent.

IF COL_LENGTH('dbo.Account', 'MaximumCallMinutes') IS NULL
    ALTER TABLE [dbo].[Account] ADD [MaximumCallMinutes] int NULL;
GO

PRINT '039-account-max-call-minutes applied (Account.MaximumCallMinutes added)';
GO
