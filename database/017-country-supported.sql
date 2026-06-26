-- 017-country-supported.sql
-- Supported countries are data-driven: each Country row carries an IsSupported flag,
-- the single source of truth for where Gigahoo is available. The API exposes the
-- supported ISO-2 codes and validates signup/checkout against this flag.
-- Flip a country on/off here (and add its PlanPrice rows + Country.Currency) to change coverage.

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Country]') AND name = 'IsSupported')
    ALTER TABLE [dbo].[Country] ADD [IsSupported] bit NOT NULL CONSTRAINT DF_Country_IsSupported DEFAULT 0;
GO

UPDATE [dbo].[Country] SET [IsSupported] = 1 WHERE [Code] IN ('US', 'CA');
GO

PRINT 'Country.IsSupported added and populated (US, CA)';
GO
