-- 021-plan-concurrent-calls.sql
-- Per-plan concurrent-call cap. Free & Starter answer one call at a time; Business
-- allows simultaneous calls (NULL = no app-imposed limit). Enforced when a new
-- inbound call arrives: if the account already has MaxConcurrentCalls live calls,
-- the extra call is turned away.

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Plan]') AND name = 'MaxConcurrentCalls')
    ALTER TABLE [dbo].[Plan] ADD [MaxConcurrentCalls] int NULL;
GO

UPDATE [dbo].[Plan] SET [MaxConcurrentCalls] = 1    WHERE [Name] IN ('Free', 'Starter');
UPDATE [dbo].[Plan] SET [MaxConcurrentCalls] = NULL WHERE [Name] = 'Business';   -- unlimited
GO

PRINT 'Plan.MaxConcurrentCalls added (Free/Starter=1, Business=unlimited)';
GO
