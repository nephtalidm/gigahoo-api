-- 023-settings.sql
-- General website settings as simple key/value pairs (e.g. the default AI voice
-- agent greeting used to pre-fill the dashboard input for un-customized accounts).
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Setting]') AND type = N'U')
    CREATE TABLE [dbo].[Setting] (
        [SettingKey] nvarchar(100) NOT NULL,
        [SettingValue] nvarchar(max) NULL,
        CONSTRAINT [PK_Setting] PRIMARY KEY ([SettingKey])
    );
GO
IF NOT EXISTS (SELECT * FROM [dbo].[Setting] WHERE [SettingKey] = N'DefaultGreeting')
    INSERT INTO [dbo].[Setting] ([SettingKey], [SettingValue])
    VALUES (N'DefaultGreeting', N'Hi, thanks for calling! How can I help you today?');
GO
PRINT 'Setting table created + DefaultGreeting seeded';
GO
