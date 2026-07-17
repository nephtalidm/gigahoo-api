SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 049-drop-normalized-email.sql
-- The database collation is SQL_Latin1_General_CP1_CI_AS (case-INsensitive), so the
-- lowercased NormalizedEmail lookup copy is redundant: comparing/indexing Email directly
-- behaves identically. Drop the copy; uniqueness moves to Email itself. Idempotent.

-- 1. Drop any index on NormalizedEmail.
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'DROP INDEX [' + i.name + N'] ON [dbo].[Account];'
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('[dbo].[Account]') AND i.is_primary_key = 0
  AND c.name = N'NormalizedEmail';
EXEC sp_executesql @sql;
GO

-- 2. Drop the column.
ALTER TABLE [dbo].[Account] DROP COLUMN IF EXISTS [NormalizedEmail];
GO

-- 3. Unique (filtered) index directly on Email.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_Account_Email' AND object_id = OBJECT_ID('[dbo].[Account]'))
    CREATE UNIQUE INDEX [UX_Account_Email] ON [dbo].[Account]([Email]) WHERE [Email] IS NOT NULL;
GO

PRINT '049-drop-normalized-email applied (NormalizedEmail dropped; unique index on Email)';
GO
