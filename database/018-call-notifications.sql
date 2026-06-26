-- 018-call-notifications.sql
-- Per-account post-call notification preferences. After each call the owner can be
-- sent a summary by email and/or SMS; these flags let them toggle each channel.
-- Both default ON so existing accounts keep receiving summaries.

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'EmailCallNotifications')
    ALTER TABLE [dbo].[Account] ADD [EmailCallNotifications] bit NOT NULL CONSTRAINT DF_Account_EmailCallNotifications DEFAULT 1;
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'SmsCallNotifications')
    ALTER TABLE [dbo].[Account] ADD [SmsCallNotifications] bit NOT NULL CONSTRAINT DF_Account_SmsCallNotifications DEFAULT 1;
GO

PRINT 'Account.EmailCallNotifications and Account.SmsCallNotifications added';
GO
