-- 033-north-america-countries.sql
-- Reduce the Country table to the three North-American markets (US, CA, MX) and
-- give the United States CountryId = 3 (CA stays 1, MX stays 2). Region already
-- carries the states/provinces/estados for all three, so no reseed is needed.
-- Data-only (no schema/code change): everything resolves Country by Code, not id.
-- Idempotent.

SET XACT_ABORT ON;
GO

-- 1. Drop every country that isn't US / CA / MX. None of them are referenced by
--    Account / PhoneNumber / Region (those only point at CA=1, MX=2, US=18), so
--    this is a clean delete and it frees up CountryId 3 (was Australia).
DELETE FROM dbo.Country WHERE Code NOT IN ('US', 'CA', 'MX');
GO

-- 2. Move United States to CountryId = 3, repointing its live FK rows.
--    Guarded so it only runs while the US is still at its original id (18).
IF EXISTS (SELECT 1 FROM dbo.Country WHERE Code = 'US' AND CountryId = 18)
   AND NOT EXISTS (SELECT 1 FROM dbo.Country WHERE CountryId = 3)
BEGIN
    -- Free the 'US' Code briefly so the new id=3 row doesn't clash with the unique index.
    UPDATE dbo.Country SET Code = 'U0' WHERE CountryId = 18;

    SET IDENTITY_INSERT dbo.Country ON;
    INSERT INTO dbo.Country (CountryId, Name, Code, DialCode, Flag, Currency, IsSupported)
    SELECT 3, Name, 'US', DialCode, Flag, Currency, IsSupported
    FROM dbo.Country WHERE CountryId = 18;
    SET IDENTITY_INSERT dbo.Country OFF;

    UPDATE dbo.PhoneNumber SET CountryId = 3 WHERE CountryId = 18;
    UPDATE dbo.Region      SET CountryId = 3 WHERE CountryId = 18;
    UPDATE dbo.Account     SET CountryId = 3 WHERE CountryId = 18;

    DELETE FROM dbo.Country WHERE CountryId = 18;
END
GO

PRINT '033-north-america-countries.sql complete.';
GO
