SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 029-voices.sql
-- Make the AI-agent voices data-driven: each selectable voice is a row owned by an
-- LLM Provider (Provider.ProviderTypeId = 1). A future LLM-provider swap just seeds
-- its own voices instead of changing a hardcoded list in code.
-- Idempotent: safe to run repeatedly.

------------------------------------------------------------------------------
-- 1. Voice table (ApiName = provider voice id, Label = picker text).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Voice')
BEGIN
    CREATE TABLE [dbo].[Voice] (
        [Id]           int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Voice PRIMARY KEY,
        [ProviderId]   int           NOT NULL,
        [ApiName]      nvarchar(64)  NOT NULL,
        [Label]        nvarchar(128) NOT NULL,
        [DisplayOrder] int           NOT NULL CONSTRAINT DF_Voice_DisplayOrder DEFAULT 0,
        [IsDefault]    bit           NOT NULL CONSTRAINT DF_Voice_IsDefault    DEFAULT 0,
        [IsActive]     bit           NOT NULL CONSTRAINT DF_Voice_IsActive     DEFAULT 1,
        CONSTRAINT FK_Voice_Provider FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider]([Id]),
        CONSTRAINT UX_Voice_Provider_ApiName UNIQUE ([ProviderId],[ApiName])
    );
    -- At most one default voice per provider.
    CREATE UNIQUE INDEX UX_Voice_OneDefault ON [dbo].[Voice]([ProviderId]) WHERE [IsDefault] = 1;
END
GO

------------------------------------------------------------------------------
-- 2. Seed the 13 Qwen voices (insert only if missing, keyed on ProviderId+ApiName).
------------------------------------------------------------------------------
DECLARE @qwen int = (SELECT [Id] FROM [dbo].[Provider] WHERE [Code] = 'qwen' AND [ProviderTypeId] = 1);

INSERT INTO [dbo].[Voice] ([ProviderId],[ApiName],[Label],[DisplayOrder],[IsDefault])
SELECT @qwen, s.[ApiName], s.[Label], s.[DisplayOrder], s.[IsDefault]
FROM (VALUES
        ('Jennifer','Jennifer (American female)',0,1),
        ('Serena',  'Serena (warm female)',     1,0),
        ('Katerina','Katerina (female)',         2,0),
        ('Kiki',    'Kiki (female)',             3,0),
        ('Sunny',   'Sunny (female)',            4,0),
        ('Ethan',   'Ethan (warm male)',         5,0),
        ('Ryan',    'Ryan (energetic male)',     6,0),
        ('Aiden',   'Aiden (American male)',     7,0),
        ('Marcus',  'Marcus (male)',             8,0),
        ('Peter',   'Peter (male)',              9,0),
        ('Dylan',   'Dylan (male)',             10,0),
        ('Rocky',   'Rocky (male)',             11,0),
        ('Eric',    'Eric (male)',              12,0)
     ) AS s([ApiName],[Label],[DisplayOrder],[IsDefault])
WHERE @qwen IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM [dbo].[Voice] v
        WHERE v.[ProviderId] = @qwen AND v.[ApiName] = s.[ApiName]
);
GO

PRINT '029-voices migration applied (Voice table + 13 Qwen voices seeded)';
GO
