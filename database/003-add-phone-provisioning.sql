-- Migration: Add phone number provisioning fields to Accounts
-- Date: 2026-06-22

USE [Gigahoo];
GO

-- Add PhoneNumberSid column
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[app].[Accounts]') 
    AND name = 'PhoneNumberSid'
)
BEGIN
    ALTER TABLE [app].[Accounts]
    ADD [PhoneNumberSid] NVARCHAR(100) NULL;
END
GO

-- Add TelephonyProvider column
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[app].[Accounts]') 
    AND name = 'TelephonyProvider'
)
BEGIN
    ALTER TABLE [app].[Accounts]
    ADD [TelephonyProvider] NVARCHAR(50) NULL;
END
GO

PRINT 'Migration completed successfully';
GO
