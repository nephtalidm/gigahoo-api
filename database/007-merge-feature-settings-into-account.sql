-- 007-merge-feature-settings-into-account.sql
-- Merges FeatureSetting columns into Account (1:1 relationship, no reason to be separate).
-- Drops the FeatureSetting table afterwards.

-- Add feature setting columns to Account (all nullable, default to NULL/0)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'AnswerQuestions')
    ALTER TABLE [dbo].[Account] ADD AnswerQuestions BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'ServicesInfo')
    ALTER TABLE [dbo].[Account] ADD ServicesInfo NVARCHAR(2000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FeatureServiceAreas')
    ALTER TABLE [dbo].[Account] ADD FeatureServiceAreas NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FeatureBusinessHours')
    ALTER TABLE [dbo].[Account] ADD FeatureBusinessHours NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'EmergencyAvailability')
    ALTER TABLE [dbo].[Account] ADD EmergencyAvailability NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PricingPolicy')
    ALTER TABLE [dbo].[Account] ADD PricingPolicy NVARCHAR(2000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'WarrantyPolicy')
    ALTER TABLE [dbo].[Account] ADD WarrantyPolicy NVARCHAR(2000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FrequentlyAskedQuestions')
    ALTER TABLE [dbo].[Account] ADD FrequentlyAskedQuestions NVARCHAR(MAX) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'AdditionalBusinessInfo')
    ALTER TABLE [dbo].[Account] ADD AdditionalBusinessInfo NVARCHAR(2000) NULL;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'ServeArea')
    ALTER TABLE [dbo].[Account] ADD ServeArea BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'DistanceKm')
    ALTER TABLE [dbo].[Account] ADD DistanceKm INT NOT NULL DEFAULT 50;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'QuoteInspection')
    ALTER TABLE [dbo].[Account] ADD QuoteInspection BIT NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'PricePerKm')
    ALTER TABLE [dbo].[Account] ADD PricePerKm DECIMAL(10,2) NOT NULL DEFAULT 0;
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'FeatureUpdatedAt')
    ALTER TABLE [dbo].[Account] ADD FeatureUpdatedAt DATETIME2(7) NULL;

-- Migrate existing FeatureSetting data into Account
UPDATE a SET
    a.AnswerQuestions = ISNULL(fs.AnswerQuestions, 0),
    a.ServicesInfo = fs.ServicesInfo,
    a.FeatureServiceAreas = fs.ServiceAreas,
    a.FeatureBusinessHours = fs.BusinessHours,
    a.EmergencyAvailability = fs.EmergencyAvailability,
    a.PricingPolicy = fs.PricingPolicy,
    a.WarrantyPolicy = fs.WarrantyPolicy,
    a.FrequentlyAskedQuestions = fs.FrequentlyAskedQuestions,
    a.AdditionalBusinessInfo = fs.AdditionalBusinessInfo,
    a.ServeArea = ISNULL(fs.ServeArea, 0),
    a.DistanceKm = ISNULL(fs.DistanceKm, 50),
    a.QuoteInspection = ISNULL(fs.QuoteInspection, 0),
    a.PricePerKm = ISNULL(fs.PricePerKm, 0),
    a.FeatureUpdatedAt = fs.UpdatedAt
FROM [dbo].[Account] a
INNER JOIN [dbo].[FeatureSetting] fs ON fs.AccountId = a.Id;

-- Drop the FeatureSetting table
IF OBJECT_ID('dbo.FeatureSetting', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[FeatureSetting];
    PRINT 'Dropped FeatureSetting table';
END
ELSE
    PRINT 'FeatureSetting table already dropped';
GO

PRINT 'FeatureSetting columns merged into Account successfully';
GO
