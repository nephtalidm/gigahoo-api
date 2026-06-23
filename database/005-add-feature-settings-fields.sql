-- Migration: Add new fields to FeatureSetting table
-- Date: 2026-06-22

USE [Gigahoo];
GO

-- Add new columns to FeatureSetting table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'ServiceAreas')
    ALTER TABLE [dbo].[FeatureSetting] ADD ServiceAreas NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'BusinessHours')
    ALTER TABLE [dbo].[FeatureSetting] ADD BusinessHours NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'EmergencyAvailability')
    ALTER TABLE [dbo].[FeatureSetting] ADD EmergencyAvailability NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'PricingPolicy')
    ALTER TABLE [dbo].[FeatureSetting] ADD PricingPolicy NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'WarrantyPolicy')
    ALTER TABLE [dbo].[FeatureSetting] ADD WarrantyPolicy NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'FrequentlyAskedQuestions')
    ALTER TABLE [dbo].[FeatureSetting] ADD FrequentlyAskedQuestions NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FeatureSetting]') AND name = 'AdditionalBusinessInfo')
    ALTER TABLE [dbo].[FeatureSetting] ADD AdditionalBusinessInfo NVARCHAR(2000) NULL;

PRINT 'FeatureSetting migration completed successfully';
GO
