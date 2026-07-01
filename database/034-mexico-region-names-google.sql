-- 034-mexico-region-names-google.sql
-- Align Mexico's Region.Name values to Google's administrative_area_level_1
-- long_name (accents + canonical labels) so Google Places results resolve to a
-- Region row. The 25 unaccented states already match; only these 7 differ.
-- Accents are written as NCHAR() code points so this file stays pure-ASCII and
-- stores correctly no matter how sqlcmd reads it. Idempotent (keyed on Code).

DECLARE @mx smallint = (SELECT CountryId FROM dbo.Country WHERE Code = 'MX');

UPDATE dbo.Region SET Name = N'Ciudad de M' + NCHAR(233) + N'xico' WHERE CountryId = @mx AND Code = 'CDMX'; -- México
UPDATE dbo.Region SET Name = N'M' + NCHAR(233) + N'xico'           WHERE CountryId = @mx AND Code = 'MEX';  -- México (state)
UPDATE dbo.Region SET Name = N'Michoac' + NCHAR(225) + N'n'        WHERE CountryId = @mx AND Code = 'MICH'; -- á
UPDATE dbo.Region SET Name = N'Nuevo Le' + NCHAR(243) + N'n'       WHERE CountryId = @mx AND Code = 'NL';   -- ó
UPDATE dbo.Region SET Name = N'Quer' + NCHAR(233) + N'taro'        WHERE CountryId = @mx AND Code = 'QRO';  -- é
UPDATE dbo.Region SET Name = N'San Luis Potos' + NCHAR(237)        WHERE CountryId = @mx AND Code = 'SLP';  -- í
UPDATE dbo.Region SET Name = N'Yucat' + NCHAR(225) + N'n'          WHERE CountryId = @mx AND Code = 'YUC';  -- á

PRINT '034-mexico-region-names-google.sql complete.';
GO
