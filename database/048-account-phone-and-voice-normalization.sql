SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 048-account-phone-and-voice-normalization.sql
-- Account keeps ONE reference per related fact instead of denormalized copies:
--   * AssignedPhoneNumberId (FK -> PhoneNumber) replaces PhoneNumberSid + ForwardingPhone
--     (the PhoneNumber row already owns Sid + Number + ProviderId).
--   * The personal PhoneNumber/NormalizedPhone columns are dropped — SMS auth now keys on
--     the business phone.
--   * BusinessPhone is renamed BusinessPhoneNumber.
--   * The Voice table is renamed AgentVoice (PK VoiceId -> AgentVoiceId), and Account's
--     AgentVoice ApiName string becomes an AgentVoiceId FK.
-- Idempotent: each step guards on current schema state.

------------------------------------------------------------------------------
-- 1. Voice -> AgentVoice table + PK rename.
------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Voice')
    EXEC sp_rename '[dbo].[Voice]', 'AgentVoice';
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[AgentVoice]') AND name = 'VoiceId')
    EXEC sp_rename '[dbo].[AgentVoice].[VoiceId]', 'AgentVoiceId', 'COLUMN';
GO

------------------------------------------------------------------------------
-- 2. Account.AgentVoiceId FK, backfilled from the old ApiName string.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'AgentVoiceId')
BEGIN
    ALTER TABLE [dbo].[Account] ADD [AgentVoiceId] int NULL
        CONSTRAINT FK_Account_AgentVoice REFERENCES [dbo].[AgentVoice]([AgentVoiceId]);
END
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'AgentVoice')
BEGIN
    EXEC sp_executesql N'
        UPDATE a SET a.[AgentVoiceId] = v.[AgentVoiceId]
        FROM [dbo].[Account] a
        JOIN [dbo].[AgentVoice] v ON v.[ApiName] = a.[AgentVoice]
        WHERE a.[AgentVoice] IS NOT NULL;';
    ALTER TABLE [dbo].[Account] DROP COLUMN [AgentVoice];
END
GO

------------------------------------------------------------------------------
-- 3. Account.AssignedPhoneNumberId FK, backfilled from the old Sid copy.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'AssignedPhoneNumberId')
BEGIN
    ALTER TABLE [dbo].[Account] ADD [AssignedPhoneNumberId] uniqueidentifier NULL
        CONSTRAINT FK_Account_AssignedPhoneNumber REFERENCES [dbo].[PhoneNumber]([PhoneNumberId]);
END
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'PhoneNumberSid')
BEGIN
    EXEC sp_executesql N'
        UPDATE a SET a.[AssignedPhoneNumberId] = p.[PhoneNumberId]
        FROM [dbo].[Account] a
        JOIN [dbo].[PhoneNumber] p ON p.[Sid] = a.[PhoneNumberSid]
        WHERE a.[PhoneNumberSid] IS NOT NULL;';
END
GO

------------------------------------------------------------------------------
-- 4. Drop the denormalized / personal phone columns (indexes + defaults first).
------------------------------------------------------------------------------
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'DROP INDEX [' + i.name + N'] ON [dbo].[Account];'
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('[dbo].[Account]') AND i.is_primary_key = 0
  AND c.name IN (N'PhoneNumber', N'NormalizedPhone', N'PhoneNumberSid', N'ForwardingPhone');
SELECT @sql += N'ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('[dbo].[Account]')
  AND c.name IN (N'PhoneNumber', N'NormalizedPhone', N'PhoneNumberSid', N'ForwardingPhone');
EXEC sp_executesql @sql;
GO
ALTER TABLE [dbo].[Account] DROP COLUMN IF EXISTS
    [PhoneNumber], [NormalizedPhone], [PhoneNumberSid], [ForwardingPhone];
GO

------------------------------------------------------------------------------
-- 5. BusinessPhone -> BusinessPhoneNumber.
------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'BusinessPhone')
    EXEC sp_rename '[dbo].[Account].[BusinessPhone]', 'BusinessPhoneNumber', 'COLUMN';
GO

PRINT '048-account-phone-and-voice-normalization applied';
GO
