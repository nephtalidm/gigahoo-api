-- Migration: Add phone number provisioning fields to Account
-- Date: 2026-06-22

USE [Gigahoo];
GO

-- Add PhoneNumberSid column
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Account]') 
    AND name = 'PhoneNumberSid'
)
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [PhoneNumberSid] NVARCHAR(100) NULL;
END
GO

-- Add TelephonyProvider column
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[Account]') 
    AND name = 'TelephonyProvider'
)
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [TelephonyProvider] NVARCHAR(50) NULL;
END
GO

PRINT 'Migration completed successfully';
GO
