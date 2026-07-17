SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 050-drop-agentstyle-agentmood.sql
-- AgentStyle: CosyVoice-era voice emotion. The dashboard can no longer set it (the Voice
-- Lab is gone and the voice-settings save stopped sending it) and its only effect was a
-- legacy tone phrase. AgentMood: added by 038 but never even mapped by the entity — pure
-- DB-only dead weight. Both dropped. Idempotent.

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('[dbo].[Account]')
  AND c.name IN (N'AgentStyle', N'AgentMood');
EXEC sp_executesql @sql;
GO

ALTER TABLE [dbo].[Account] DROP COLUMN IF EXISTS [AgentStyle], [AgentMood];
GO

PRINT '050-drop-agentstyle-agentmood applied';
GO
