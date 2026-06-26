-- 019-domains.sql
-- Regional domains are data-driven: each Domain row maps a host to an optional
-- ISO-2 country code. A NULL CountryCode means "geo-detect" (no market pinned),
-- while a value pins the market for that domain (e.g. gigahoo.ca -> CA).
-- This is the single source of truth consumed by:
--   - the API CORS policy (allowed Gigahoo origins), and
--   - the UI middleware (host -> forced country).

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Domain]') AND type = N'U')
BEGIN
    CREATE TABLE [dbo].[Domain] (
        [Host] nvarchar(100) NOT NULL,
        [CountryCode] nvarchar(2) NULL,
        CONSTRAINT PK_Domain PRIMARY KEY ([Host])
    );
END
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Domain] WHERE [Host] = 'gigahoo.ai')
    INSERT INTO [dbo].[Domain] ([Host], [CountryCode]) VALUES ('gigahoo.ai', NULL);
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Domain] WHERE [Host] = 'gigahoo.com')
    INSERT INTO [dbo].[Domain] ([Host], [CountryCode]) VALUES ('gigahoo.com', NULL);
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Domain] WHERE [Host] = 'gigahoo.ca')
    INSERT INTO [dbo].[Domain] ([Host], [CountryCode]) VALUES ('gigahoo.ca', 'CA');
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Domain] WHERE [Host] = 'gigahoo.mx')
    INSERT INTO [dbo].[Domain] ([Host], [CountryCode]) VALUES ('gigahoo.mx', 'MX');
GO

IF NOT EXISTS (SELECT * FROM [dbo].[Domain] WHERE [Host] = 'gigahoo.com.mx')
    INSERT INTO [dbo].[Domain] ([Host], [CountryCode]) VALUES ('gigahoo.com.mx', 'MX');
GO

PRINT 'Domain table created and seeded (gigahoo.ai/.com -> geo, .ca -> CA, .mx/.com.mx -> MX)';
GO
