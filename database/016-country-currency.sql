-- 016-country-currency.sql
-- Billing currency is data-driven: each Country row carries its ISO 4217 currency,
-- so checkout maps country -> currency from this table (no hardcoding in code).
-- Adjust any country's currency here; add a matching PlanPrice row to actually bill it.

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Country]') AND name = 'Currency')
    ALTER TABLE [dbo].[Country] ADD [Currency] nvarchar(3) NULL;
GO

UPDATE [dbo].[Country] SET [Currency] = 'MXN' WHERE [Code] = 'MX';
UPDATE [dbo].[Country] SET [Currency] = 'CAD' WHERE [Code] = 'CA';
UPDATE [dbo].[Country] SET [Currency] = 'USD' WHERE [Currency] IS NULL;
GO

PRINT 'Country.Currency added and populated';
GO
