-- 014-add-limit-notified-at.sql
-- Minutes metering sends a one-time SMS+email when an account crosses its included
-- minutes for the period. LimitNotifiedAt guards that notification (set once per
-- billing period, cleared on renewal) so we don't re-notify on every subsequent call.

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'LimitNotifiedAt')
    ALTER TABLE [dbo].[Account] ADD [LimitNotifiedAt] datetime2 NULL;
GO

PRINT 'Account.LimitNotifiedAt added';
GO
