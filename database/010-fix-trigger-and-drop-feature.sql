SET QUOTED_IDENTIFIER ON;
GO

-- Fix the Account trigger (was referencing app.Accounts)
IF OBJECT_ID('dbo.TR_Accounts_UpdatedAt', 'TR') IS NOT NULL
    DROP TRIGGER [dbo].[TR_Accounts_UpdatedAt];
GO

CREATE OR ALTER TRIGGER [dbo].[TR_Account_UpdatedAt]
ON [dbo].[Account]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE a SET [UpdatedAt] = SYSUTCDATETIME()
    FROM [dbo].[Account] a
    INNER JOIN inserted i ON a.[Id] = i.[Id];
END;
GO

-- Drop FeatureSetting table (data already migrated, just need to drop)
IF OBJECT_ID('dbo.FeatureSetting', 'U') IS NOT NULL
    DROP TABLE [dbo].[FeatureSetting];
GO

PRINT 'Trigger fixed and FeatureSetting dropped';
GO
