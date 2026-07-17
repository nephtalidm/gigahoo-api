SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 054-referenceid-createddate-drop-setting.sql
--   * AgentVoice.ApiName -> ReferenceId (it IS the Fish reference_id).
--   * Conversation had two timestamps written at the same instant (DateTimeUtc + CreatedAt):
--     consolidated into ONE column, CreatedDate (CreatedAt renamed; DateTimeUtc dropped,
--     after backfilling CreatedAt from it for any legacy rows where they could differ).
--   * Setting table dropped — its single row (DefaultGreeting) moved to API configuration.
-- Idempotent.

------------------------------------------------------------------------------
-- 1. AgentVoice.ApiName -> ReferenceId.
------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[AgentVoice]') AND name = 'ApiName')
    EXEC sp_rename '[dbo].[AgentVoice].[ApiName]', 'ReferenceId', 'COLUMN';
GO

------------------------------------------------------------------------------
-- 2. Conversation: one timestamp.
------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Conversation]') AND name = 'DateTimeUtc')
BEGIN
    -- The call time is the authoritative value for existing rows.
    EXEC sp_executesql N'UPDATE [dbo].[Conversation] SET [CreatedAt] = [DateTimeUtc];';
    -- Any index or default constraint on DateTimeUtc goes with it (a legacy auto-named
    -- default from the old "Calls" table blocked the plain drop).
    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'DROP INDEX [' + i.name + N'] ON [dbo].[Conversation];'
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID('[dbo].[Conversation]') AND i.is_primary_key = 0
      AND c.name = N'DateTimeUtc';
    SELECT @sql += N'ALTER TABLE [dbo].[Conversation] DROP CONSTRAINT [' + dc.name + N'];'
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('[dbo].[Conversation]') AND c.name = N'DateTimeUtc';
    EXEC sp_executesql @sql;
    ALTER TABLE [dbo].[Conversation] DROP COLUMN [DateTimeUtc];
END
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Conversation]') AND name = 'CreatedAt')
    EXEC sp_rename '[dbo].[Conversation].[CreatedAt]', 'CreatedDate', 'COLUMN';
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversation_AccountId_CreatedDate' AND object_id = OBJECT_ID('[dbo].[Conversation]'))
    CREATE INDEX [IX_Conversation_AccountId_CreatedDate] ON [dbo].[Conversation]([AccountId], [CreatedDate] DESC);
GO

------------------------------------------------------------------------------
-- 3. Setting table: gone (DefaultGreeting now lives in API configuration).
------------------------------------------------------------------------------
DROP TABLE IF EXISTS [dbo].[Setting];
GO

PRINT '054-referenceid-createddate-drop-setting applied';
GO
