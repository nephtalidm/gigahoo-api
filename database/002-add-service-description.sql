-- ============================================================
-- Migration: Add ServiceDescription to BusinessCategory
-- Purpose: Support generalized voice agent prompts for all service types
-- ============================================================

USE [Gigahoo];
GO

-- Add ServiceDescription column to BusinessCategory
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[BusinessCategory]') 
    AND name = 'ServiceDescription'
)
BEGIN
    ALTER TABLE [dbo].[BusinessCategory]
    ADD [ServiceDescription] NVARCHAR(200) NOT NULL DEFAULT N'service need';
END
GO

-- Update existing categories with appropriate service descriptions
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'plumbing issue' WHERE [Name] = N'Plumbing';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'electrical issue' WHERE [Name] = N'Electrical';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'heating or cooling issue' WHERE [Name] = N'HVAC';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'appliance repair need' WHERE [Name] = N'Appliance Repair';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'cleaning service need' WHERE [Name] = N'Cleaning';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'garage door issue' WHERE [Name] = N'Garage Door Repair';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'lock or key issue' WHERE [Name] = N'Locksmith';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'roofing issue' WHERE [Name] = N'Roofing';
UPDATE [dbo].[BusinessCategory] SET [ServiceDescription] = N'service need' WHERE [Name] = N'Other';
GO
