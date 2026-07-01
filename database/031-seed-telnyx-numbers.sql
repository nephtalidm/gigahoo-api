-- 031-seed-telnyx-numbers.sql
-- Add the 4 Telnyx numbers (bought directly in the Telnyx portal) to the pool so
-- they're assignable like any provisioned number. Uses the ProviderId/CountryId
-- FKs from 030. Idempotent: keyed on Sid.
--   US -> Seattle (206), CA -> Vancouver (236).

DECLARE @telnyxPhone INT     = (SELECT [ProviderId] FROM [dbo].[Provider] WHERE [Code] = 'telnyx' AND [ProviderTypeId] = 3);
DECLARE @us          SMALLINT = (SELECT [CountryId]  FROM [dbo].[Country]  WHERE [Code] = 'US');
DECLARE @ca          SMALLINT = (SELECT [CountryId]  FROM [dbo].[Country]  WHERE [Code] = 'CA');

IF @telnyxPhone IS NULL OR @us IS NULL OR @ca IS NULL
BEGIN
    RAISERROR('Missing Telnyx Phone provider or US/CA country rows — run 030 first.', 16, 1);
    RETURN;
END

INSERT INTO [dbo].[PhoneNumber] ([Sid], [Number], [CountryId], [ProviderId], [Status])
SELECT v.[Sid], v.[Number], v.[CountryId], @telnyxPhone, 'Available'
FROM (VALUES
        ('2993967294956176798', '+12065960776', @us),  -- Seattle
        ('2993967294931010973', '+12065804005', @us),  -- Seattle
        ('2993966817468220798', '+12364767183', @ca),  -- Vancouver
        ('2993966271411782998', '+12364769180', @ca)   -- Vancouver
     ) AS v([Sid], [Number], [CountryId])
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PhoneNumber] p WHERE p.[Sid] = v.[Sid]);

PRINT '031-seed-telnyx-numbers.sql complete.';
GO
