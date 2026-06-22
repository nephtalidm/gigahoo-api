-- Migration: Add new fields to FeatureSettings table
-- Date: 2026-06-22

USE [Gigahoo];
GO

-- Add new columns to FeatureSettings table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'ServiceAreas')
    ALTER TABLE [app].[FeatureSettings] ADD ServiceAreas NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'BusinessHours')
    ALTER TABLE [app].[FeatureSettings] ADD BusinessHours NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'EmergencyAvailability')
    ALTER TABLE [app].[FeatureSettings] ADD EmergencyAvailability NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'PricingPolicy')
    ALTER TABLE [app].[FeatureSettings] ADD PricingPolicy NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'WarrantyPolicy')
    ALTER TABLE [app].[FeatureSettings] ADD WarrantyPolicy NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'FrequentlyAskedQuestions')
    ALTER TABLE [app].[FeatureSettings] ADD FrequentlyAskedQuestions NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[app].[FeatureSettings]') AND name = 'AdditionalBusinessInfo')
    ALTER TABLE [app].[FeatureSettings] ADD AdditionalBusinessInfo NVARCHAR(2000) NULL;

PRINT 'FeatureSettings migration completed successfully';
GO
