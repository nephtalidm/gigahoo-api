-- 026-plan-display-order.sql
-- Plan ordering (for upgrade/downgrade comparisons + card order) is data-driven
-- from the table, not a hardcoded list in the app.
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Plan]') AND name = 'DisplayOrder')
    ALTER TABLE [dbo].[Plan] ADD [DisplayOrder] int NOT NULL CONSTRAINT DF_Plan_DisplayOrder DEFAULT 0;
GO
UPDATE [dbo].[Plan] SET [DisplayOrder] = 1 WHERE [Name] = 'Free';
UPDATE [dbo].[Plan] SET [DisplayOrder] = 2 WHERE [Name] = 'Starter';
UPDATE [dbo].[Plan] SET [DisplayOrder] = 3 WHERE [Name] = 'Business';
GO
PRINT 'Plan.DisplayOrder added';
GO
