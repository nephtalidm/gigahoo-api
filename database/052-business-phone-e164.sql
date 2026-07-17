SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 052-business-phone-e164.sql
-- ONE phone string: BusinessPhoneNumber becomes full E.164 ("+17783923021") and the
-- separate PhoneCountryCode column is dropped. Existing local-format values are converted
-- using each account's current PhoneCountryCode joined to Country.DialCode. Display
-- formatting ("+1 (778) 392-3021") is a presentation concern owned by code. Idempotent.

------------------------------------------------------------------------------
-- 1. Convert non-E.164 values: '+' + dial digits + local digits.
------------------------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'PhoneCountryCode')
BEGIN
    EXEC sp_executesql N'
        UPDATE a
        SET a.[BusinessPhoneNumber] = ''+'' + d.[Digits] + p.[Digits]
        FROM [dbo].[Account] a
        JOIN [dbo].[Country] c ON c.[Code] = a.[PhoneCountryCode]
        CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(c.[DialCode],''+'',''''),'' '',''''),''-'',''''),''('',''''),'')'','''') AS [Digits]) d
        CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(a.[BusinessPhoneNumber],''+'',''''),'' '',''''),''-'',''''),''('',''''),'')'',''''),''.'','''') AS [Digits]) p
        WHERE a.[BusinessPhoneNumber] IS NOT NULL
          AND a.[BusinessPhoneNumber] NOT LIKE ''+%'';';
END
GO

------------------------------------------------------------------------------
-- 2. Drop PhoneCountryCode (defaults first).
------------------------------------------------------------------------------
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'ALTER TABLE [dbo].[Account] DROP CONSTRAINT [' + dc.name + N'];'
FROM sys.default_constraints dc
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID('[dbo].[Account]') AND c.name = N'PhoneCountryCode';
EXEC sp_executesql @sql;
GO
ALTER TABLE [dbo].[Account] DROP COLUMN IF EXISTS [PhoneCountryCode];
GO

PRINT '052-business-phone-e164 applied (BusinessPhoneNumber is E.164; PhoneCountryCode dropped)';
GO
