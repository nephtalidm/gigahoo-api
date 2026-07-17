SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 047-drop-more-dead-account-columns.sql
-- Second dead-column sweep on Account:
--   ServiceArea       - editable but consumed by nothing (agent ignores it).
--   AgentInstruct     - CosyVoice/Voice-Lab era instruct context; UI no longer sets it,
--                       the voice agent never read it.
--   TelephonyProvider - redundant: PhoneNumber.ProviderId (FK -> Provider) owns which
--                       carrier a number belongs to; this was written but never read.
--   DisplayName       - captured from Google into a JWT claim that nothing consumes.
-- Idempotent.

DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('[dbo].[Account]')
  AND c.name IN (N'ServiceArea', N'AgentInstruct', N'TelephonyProvider', N'DisplayName');
EXEC sp_executesql @sql;
GO

ALTER TABLE [dbo].[Account] DROP COLUMN IF EXISTS
    [ServiceArea], [AgentInstruct], [TelephonyProvider], [DisplayName];
GO

PRINT '047-drop-more-dead-account-columns applied (ServiceArea, AgentInstruct, TelephonyProvider, DisplayName dropped)';
GO
