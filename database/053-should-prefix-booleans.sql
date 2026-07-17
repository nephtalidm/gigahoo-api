SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 053-should-prefix-booleans.sql
-- Behavior-toggle booleans get the grammatical Should prefix (states keep Is):
--   CollectName/Phone/Address/Emergency -> ShouldCollect...
--   EmailCallNotifications              -> ShouldSendCallSummaryEmail
--   SmsCallNotifications                -> ShouldSendCallSummarySms
-- (These gate exactly the per-call owner summary; minutes alerts are unconditional.)
-- Idempotent: each rename guards on the old column existing.

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'CollectName')
    EXEC sp_rename '[dbo].[Account].[CollectName]', 'ShouldCollectName', 'COLUMN';
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'CollectPhone')
    EXEC sp_rename '[dbo].[Account].[CollectPhone]', 'ShouldCollectPhone', 'COLUMN';
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'CollectAddress')
    EXEC sp_rename '[dbo].[Account].[CollectAddress]', 'ShouldCollectAddress', 'COLUMN';
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'CollectEmergency')
    EXEC sp_rename '[dbo].[Account].[CollectEmergency]', 'ShouldCollectEmergency', 'COLUMN';
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'EmailCallNotifications')
    EXEC sp_rename '[dbo].[Account].[EmailCallNotifications]', 'ShouldSendCallSummaryEmail', 'COLUMN';
GO
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'SmsCallNotifications')
    EXEC sp_rename '[dbo].[Account].[SmsCallNotifications]', 'ShouldSendCallSummarySms', 'COLUMN';
GO

PRINT '053-should-prefix-booleans applied';
GO
