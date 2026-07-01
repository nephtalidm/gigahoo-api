-- 036-drop-account-regioncustom.sql
-- Region is now always a dropdown selection stored as Account.RegionId; the
-- free-text Account.RegionCustom column is retired. Idempotent.

IF COL_LENGTH('dbo.Account', 'RegionCustom') IS NOT NULL
BEGIN
    DECLARE @dc sysname;
    SELECT @dc = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE c.object_id = OBJECT_ID('dbo.Account') AND c.name = 'RegionCustom';
    IF @dc IS NOT NULL EXEC('ALTER TABLE dbo.Account DROP CONSTRAINT [' + @dc + ']');

    ALTER TABLE dbo.Account DROP COLUMN RegionCustom;
END
GO

PRINT '036-drop-account-regioncustom.sql complete.';
GO
