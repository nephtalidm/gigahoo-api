-- ============================================================
-- Migration: Add ServiceDescription to BusinessCategories
-- Purpose: Support generalized voice agent prompts for all service types
-- ============================================================

USE [Gigahoo];
GO

-- Add ServiceDescription column to BusinessCategories
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[app].[BusinessCategories]') 
    AND name = 'ServiceDescription'
)
BEGIN
    ALTER TABLE [app].[BusinessCategories]
    ADD [ServiceDescription] NVARCHAR(200) NOT NULL DEFAULT N'service need';
END
GO

-- Update existing categories with appropriate service descriptions
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'plumbing issue' WHERE [Name] = N'Plumbing';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'electrical issue' WHERE [Name] = N'Electrical';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'heating or cooling issue' WHERE [Name] = N'HVAC';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'appliance repair need' WHERE [Name] = N'Appliance Repair';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'cleaning service need' WHERE [Name] = N'Cleaning';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'garage door issue' WHERE [Name] = N'Garage Door Repair';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'lock or key issue' WHERE [Name] = N'Locksmith';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'roofing issue' WHERE [Name] = N'Roofing';
UPDATE [app].[BusinessCategories] SET [ServiceDescription] = N'service need' WHERE [Name] = N'Other';
GO
