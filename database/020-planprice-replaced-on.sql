-- 020-planprice-replaced-on.sql
-- Track when a plan price was last replaced. NULL = never replaced (the original
-- seeded price); a timestamp = the last time the Amount/StripePriceId changed.
-- Stamped automatically by a trigger so it always reflects the last replacement.

-- Drop the earlier always-set UpdatedAt variant if it exists.
IF OBJECT_ID('dbo.TR_PlanPrice_UpdatedAt', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_PlanPrice_UpdatedAt;
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'UpdatedAt')
BEGIN
    DECLARE @df sysname = (
        SELECT dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND c.name = 'UpdatedAt');
    IF @df IS NOT NULL EXEC('ALTER TABLE [dbo].[PlanPrice] DROP CONSTRAINT ' + @df);
    ALTER TABLE [dbo].[PlanPrice] DROP COLUMN [UpdatedAt];
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'ReplacedOn')
    ALTER TABLE [dbo].[PlanPrice] ADD [ReplacedOn] datetime2 NULL;
GO

IF OBJECT_ID('dbo.TR_PlanPrice_ReplacedOn', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_PlanPrice_ReplacedOn;
GO
CREATE TRIGGER dbo.TR_PlanPrice_ReplacedOn ON [dbo].[PlanPrice]
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    UPDATE pp
        SET ReplacedOn = SYSUTCDATETIME()
    FROM [dbo].[PlanPrice] pp
    INNER JOIN inserted i ON pp.[Id] = i.[Id]
    INNER JOIN deleted  d ON d.[Id] = i.[Id]
    WHERE i.[Amount] <> d.[Amount]
       OR ISNULL(i.[StripePriceId], '') <> ISNULL(d.[StripePriceId], '');
END
GO

PRINT 'PlanPrice.ReplacedOn added (NULL until a price is replaced)';
GO
