-- Migration: Add PhoneNumber pool table for reuse
-- Date: 2026-06-22

USE [Gigahoo];
GO

-- Create PhoneNumber pool table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PhoneNumbers' AND schema_id = SCHEMA_ID('app'))
BEGIN
    CREATE TABLE [app].[PhoneNumbers] (
        [Id]                UNIQUEIDENTIFIER    NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Sid]               NVARCHAR(100)       NOT NULL UNIQUE,
        [Number]            NVARCHAR(20)        NOT NULL,
        [CountryCode]       CHAR(2)             NOT NULL,
        [Provider]          NVARCHAR(50)        NOT NULL DEFAULT 'twilio',
        [Status]            NVARCHAR(20)        NOT NULL DEFAULT 'Available', -- Available, Assigned, Released
        [AssignedAccountId] UNIQUEIDENTIFIER    NULL,
        [MonthlyCost]       DECIMAL(10,2)       NOT NULL DEFAULT 1.15,
        [PurchasedAt]       DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        [AssignedAt]        DATETIME2           NULL,
        [ReleasedAt]        DATETIME2           NULL,
        [CreatedAt]         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt]         DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX [IX_PhoneNumbers_Status] ON [app].[PhoneNumbers] ([Status]);
    CREATE INDEX [IX_PhoneNumbers_CountryCode_Status] ON [app].[PhoneNumbers] ([CountryCode], [Status]);
    CREATE INDEX [IX_PhoneNumbers_AssignedAccountId] ON [app].[PhoneNumbers] ([AssignedAccountId]);
END
GO

PRINT 'Migration completed successfully';
GO
