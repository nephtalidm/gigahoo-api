SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 046-drop-dead-account-columns.sql
-- Remove the dead Account columns: LlmProviderId (never referenced by any code) and the
-- entire Business-plan "feature settings" block (CRUD-only — the voice agent never reads
-- any of it, so the stored values have no effect on the product). Idempotent.

-- 1. Default constraints must go before their columns can.
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('[dbo].[Account]')
  AND c.name IN (
    N'LlmProviderId', N'AnswerQuestions', N'ServicesInfo', N'FeatureServiceAreas',
    N'FeatureBusinessHours', N'EmergencyAvailability', N'PricingPolicy', N'WarrantyPolicy',
    N'FrequentlyAskedQuestions', N'AdditionalBusinessInfo', N'ServeArea', N'DistanceKm',
    N'QuoteInspection', N'PricePerKm', N'FeatureUpdatedAt');
EXEC sp_executesql @sql;
GO

-- 2. The LlmProvider FK, if present.
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Account_LlmProvider')
    ALTER TABLE [dbo].[Account] DROP CONSTRAINT [FK_Account_LlmProvider];
GO

-- 3. The columns themselves.
ALTER TABLE [dbo].[Account] DROP COLUMN IF EXISTS
    [LlmProviderId],
    [AnswerQuestions], [ServicesInfo], [FeatureServiceAreas], [FeatureBusinessHours],
    [EmergencyAvailability], [PricingPolicy], [WarrantyPolicy], [FrequentlyAskedQuestions],
    [AdditionalBusinessInfo], [ServeArea], [DistanceKm], [QuoteInspection], [PricePerKm],
    [FeatureUpdatedAt];
GO

-- 4. Rename CategoryId -> BusinessCategoryId (matches the BusinessCategory table + its PK).
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'CategoryId')
    EXEC sp_rename '[dbo].[Account].[CategoryId]', 'BusinessCategoryId', 'COLUMN';
GO

PRINT '046-drop-dead-account-columns applied (LlmProviderId + 14 feature columns dropped; CategoryId renamed to BusinessCategoryId)';
GO
