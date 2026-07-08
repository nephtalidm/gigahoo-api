SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 040-account-max-call-minutes-default.sql
-- Default the per-call cap to 10 minutes. Backfill unconfigured (NULL) accounts to 10 and add a
-- DEFAULT constraint so new rows get 10 automatically. NULL remains "Unlimited" going forward (an
-- explicit dashboard opt-out; EF inserts an explicit NULL for that, which overrides the default).
-- Idempotent.

UPDATE dbo.Account SET MaximumCallMinutes = 10 WHERE MaximumCallMinutes IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Account_MaximumCallMinutes')
    ALTER TABLE dbo.Account ADD CONSTRAINT DF_Account_MaximumCallMinutes DEFAULT 10 FOR MaximumCallMinutes;
GO

PRINT '040-account-max-call-minutes-default applied (default 10, NULL backfilled)';
GO
