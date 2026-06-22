-- ============================================================
-- Gigahoo Database Creation Script
-- SQL Server 2022+ / Azure SQL
-- ============================================================

-- Create database (skip if running on Azure SQL where CREATE DATABASE is restricted)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'Gigahoo')
BEGIN
    CREATE DATABASE [Gigahoo];
END
GO

USE [Gigahoo];
GO

-- ============================================================
-- SCHEMA
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'app')
    EXEC('CREATE SCHEMA [app]');
GO

-- ============================================================
-- ENUM / LOOKUP TABLES
-- ============================================================

-- Plans
CREATE TABLE [app].[Plans] (
    [Id]                TINYINT         NOT NULL PRIMARY KEY,
    [Name]              NVARCHAR(50)    NOT NULL,
    [PriceMonthly]       DECIMAL(10,2)   NOT NULL DEFAULT 0,
    [IncludedMinutes]    INT             NOT NULL DEFAULT 0,
    [HasOptionalFeatures] BIT            NOT NULL DEFAULT 0,
    [IsActive]          BIT             NOT NULL DEFAULT 1
);

INSERT INTO [app].[Plans] ([Id], [Name], [PriceMonthly], [IncludedMinutes], [HasOptionalFeatures])
VALUES
    (1, N'Free',     0.00,   25,   0),
    (2, N'Starter', 49.00,  250,   0),
    (3, N'Business', 99.00, 1000,  1);
GO

-- Business Categories
CREATE TABLE [app].[BusinessCategories] (
    [Id]    TINYINT         NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]  NVARCHAR(100)   NOT NULL UNIQUE
);

INSERT INTO [app].[BusinessCategories] ([Name]) VALUES
    (N'Appliance Repair'), (N'Cleaning'), (N'Electrical'),
    (N'Garage Door Repair'), (N'HVAC'), (N'Locksmith'),
    (N'Plumbing'), (N'Roofing'), (N'Other');
GO

-- Countries
CREATE TABLE [app].[Countries] (
    [Id]        SMALLINT    NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]      NVARCHAR(100) NOT NULL,
    [Code]      CHAR(2)     NOT NULL UNIQUE,   -- ISO 3166-1 alpha-2
    [DialCode]  NVARCHAR(10) NOT NULL,
    [Flag]      NVARCHAR(10) NULL
);

INSERT INTO [app].[Countries] ([Name], [Code], [DialCode], [Flag]) VALUES
    (N'Canada',            N'CA', N'+1',  N'🇨🇦'),
    (N'Mexico',            N'MX', N'+52', N'🇲🇽'),
    (N'Australia',         N'AU', N'+61', N'🇦🇺'),
    (N'Brazil',            N'BR', N'+55', N'🇧🇷'),
    (N'France',            N'FR', N'+33', N'🇫🇷'),
    (N'Germany',           N'DE', N'+49', N'🇩🇪'),
    (N'India',             N'IN', N'+91', N'🇮🇳'),
    (N'Ireland',           N'IE', N'+353',N'🇮🇪'),
    (N'Italy',             N'IT', N'+39', N'🇮🇹'),
    (N'Japan',             N'JP', N'+81', N'🇯🇵'),
    (N'Netherlands',       N'NL', N'+31', N'🇳🇱'),
    (N'New Zealand',       N'NZ', N'+64', N'🇳🇿'),
    (N'Singapore',         N'SG', N'+65', N'🇸🇬'),
    (N'South Africa',      N'ZA', N'+27', N'🇿🇦'),
    (N'Spain',             N'ES', N'+34', N'🇪🇸'),
    (N'United Arab Emirates', N'AE', N'+971', N'🇦🇪'),
    (N'United Kingdom',    N'GB', N'+44', N'🇬🇧'),
    (N'United States',     N'US', N'+1',  N'🇺🇸'),
    (N'Other',             N'XX', N'+0',  N'');
GO

-- Supported Languages
CREATE TABLE [app].[Languages] (
    [Id]    TINYINT         NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]  NVARCHAR(50)    NOT NULL UNIQUE
);

INSERT INTO [app].[Languages] ([Name]) VALUES
    (N'English'), (N'French'), (N'Mandarin'), (N'Cantonese'),
    (N'Spanish'), (N'Japanese'), (N'Hindi'), (N'Korean'), (N'Tagalog');
GO

-- Regions (States/Provinces)
CREATE TABLE [app].[Regions] (
    [Id]        SMALLINT    NOT NULL PRIMARY KEY IDENTITY(1,1),
    [CountryId] SMALLINT    NOT NULL REFERENCES [app].[Countries]([Id]),
    [Name]      NVARCHAR(100) NOT NULL,
    [Code]      NVARCHAR(10) NOT NULL,
    CONSTRAINT [UQ_Regions_Country_Code] UNIQUE ([CountryId], [Code])
);

-- US States
INSERT INTO [app].[Regions] ([CountryId], [Name], [Code])
SELECT c.[Id], v.[Name], v.[Code]
FROM [app].[Countries] c
CROSS JOIN (VALUES
    (N'Alabama',N'AL'),(N'Alaska',N'AK'),(N'Arizona',N'AZ'),(N'Arkansas',N'AR'),
    (N'California',N'CA'),(N'Colorado',N'CO'),(N'Connecticut',N'CT'),(N'Delaware',N'DE'),
    (N'Florida',N'FL'),(N'Georgia',N'GA'),(N'Hawaii',N'HI'),(N'Idaho',N'ID'),
    (N'Illinois',N'IL'),(N'Indiana',N'IN'),(N'Iowa',N'IA'),(N'Kansas',N'KS'),
    (N'Kentucky',N'KY'),(N'Louisiana',N'LA'),(N'Maine',N'ME'),(N'Maryland',N'MD'),
    (N'Massachusetts',N'MA'),(N'Michigan',N'MI'),(N'Minnesota',N'MN'),(N'Mississippi',N'MS'),
    (N'Missouri',N'MO'),(N'Montana',N'MT'),(N'Nebraska',N'NE'),(N'Nevada',N'NV'),
    (N'New Hampshire',N'NH'),(N'New Jersey',N'NJ'),(N'New Mexico',N'NM'),(N'New York',N'NY'),
    (N'North Carolina',N'NC'),(N'North Dakota',N'ND'),(N'Ohio',N'OH'),(N'Oklahoma',N'OK'),
    (N'Oregon',N'OR'),(N'Pennsylvania',N'PA'),(N'Rhode Island',N'RI'),(N'South Carolina',N'SC'),
    (N'South Dakota',N'SD'),(N'Tennessee',N'TN'),(N'Texas',N'TX'),(N'Utah',N'UT'),
    (N'Vermont',N'VT'),(N'Virginia',N'VA'),(N'Washington',N'WA'),(N'West Virginia',N'WV'),
    (N'Wisconsin',N'WI'),(N'Wyoming',N'WY'),(N'District of Columbia',N'DC')
) AS v([Name], [Code])
WHERE c.[Code] = N'US';

-- Canadian Provinces
INSERT INTO [app].[Regions] ([CountryId], [Name], [Code])
SELECT c.[Id], v.[Name], v.[Code]
FROM [app].[Countries] c
CROSS JOIN (VALUES
    (N'Alberta',N'AB'),(N'British Columbia',N'BC'),(N'Manitoba',N'MB'),
    (N'New Brunswick',N'NB'),(N'Newfoundland and Labrador',N'NL'),
    (N'Nova Scotia',N'NS'),(N'Northwest Territories',N'NT'),(N'Nunavut',N'NU'),
    (N'Ontario',N'ON'),(N'Prince Edward Island',N'PE'),(N'Quebec',N'QC'),
    (N'Saskatchewan',N'SK'),(N'Yukon',N'YT')
) AS v([Name], [Code])
WHERE c.[Code] = N'CA';

-- Mexican States
INSERT INTO [app].[Regions] ([CountryId], [Name], [Code])
SELECT c.[Id], v.[Name], v.[Code]
FROM [app].[Countries] c
CROSS JOIN (VALUES
    (N'Aguascalientes',N'AGS'),(N'Baja California',N'BC'),(N'Baja California Sur',N'BCS'),
    (N'Campeche',N'CAMP'),(N'Chiapas',N'CHIS'),(N'Chihuahua',N'CHIH'),
    (N'Coahuila',N'COAH'),(N'Colima',N'COL'),(N'Mexico City',N'CDMX'),
    (N'Durango',N'DGO'),(N'Guanajuato',N'GTO'),(N'Guerrero',N'GRO'),
    (N'Hidalgo',N'HGO'),(N'Jalisco',N'JAL'),(N'Mexico',N'MEX'),
    (N'Michoacan',N'MICH'),(N'Morelos',N'MOR'),(N'Nayarit',N'NAY'),
    (N'Nuevo Leon',N'NL'),(N'Oaxaca',N'OAX'),(N'Puebla',N'PUE'),
    (N'Queretaro',N'QRO'),(N'Quintana Roo',N'QR'),(N'San Luis Potosi',N'SLP'),
    (N'Sinaloa',N'SIN'),(N'Sonora',N'SON'),(N'Tabasco',N'TAB'),
    (N'Tamaulipas',N'TAMPS'),(N'Tlaxcala',N'TLAX'),(N'Veracruz',N'VER'),
    (N'Yucatan',N'YUC'),(N'Zacatecas',N'ZAC')
) AS v([Name], [Code])
WHERE c.[Code] = N'MX';
GO

-- ============================================================
-- CORE TABLES
-- ============================================================

-- Users (authentication identities)
CREATE TABLE [app].[Users] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [Email]             NVARCHAR(256)    NULL,
    [NormalizedEmail]   NVARCHAR(256)    NULL,
    [PhoneNumber]       NVARCHAR(50)     NULL,
    [NormalizedPhone]   NVARCHAR(50)     NULL,
    [PasswordHash]      NVARCHAR(MAX)    NULL,
    [GoogleSubjectId]   NVARCHAR(256)    NULL,
    [DisplayName]       NVARCHAR(256)    NULL,
    [IsEmailConfirmed]  BIT              NOT NULL DEFAULT 0,
    [IsPhoneConfirmed]  BIT              NOT NULL DEFAULT 0,
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [LastLoginAt]       DATETIME2(7)     NULL,
    [IsDisabled]        BIT              NOT NULL DEFAULT 0,
    CONSTRAINT [UQ_Users_Email] UNIQUE ([NormalizedEmail]),
    CONSTRAINT [UQ_Users_Phone] UNIQUE ([NormalizedPhone]),
    CONSTRAINT [UQ_Users_GoogleSubject] UNIQUE ([GoogleSubjectId])
);

CREATE INDEX [IX_Users_NormalizedEmail] ON [app].[Users]([NormalizedEmail]);
CREATE INDEX [IX_Users_NormalizedPhone] ON [app].[Users]([NormalizedPhone]);
GO

-- Business Accounts (one per user initially)
CREATE TABLE [app].[Accounts] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [UserId]            UNIQUEIDENTIFIER NOT NULL REFERENCES [app].[Users]([Id]),
    [BusinessName]      NVARCHAR(256)    NOT NULL,
    [CategoryId]        TINYINT          NOT NULL REFERENCES [app].[BusinessCategories]([Id]),
    [BusinessPhone]     NVARCHAR(50)     NOT NULL,
    [PhoneCountryCode]  CHAR(2)          NOT NULL DEFAULT N'US',
    [Email]             NVARCHAR(256)    NOT NULL,
    [ServiceArea]       NVARCHAR(500)    NULL,
    [WebsiteUrl]        NVARCHAR(500)    NULL,
    [BusinessHours]     NVARCHAR(500)    NULL,
    [ForwardingPhone]   NVARCHAR(50)     NULL,
    [PlanId]            TINYINT          NOT NULL DEFAULT 2 REFERENCES [app].[Plans]([Id]),
    -- Address
    [AddressLine1]      NVARCHAR(256)    NULL,
    [AddressLine2]      NVARCHAR(256)    NULL,
    [City]              NVARCHAR(100)    NULL,
    [RegionId]          SMALLINT         NULL REFERENCES [app].[Regions]([Id]),
    [RegionCustom]      NVARCHAR(100)    NULL,
    [PostalCode]        NVARCHAR(20)     NULL,
    [CountryId]         SMALLINT         NOT NULL REFERENCES [app].[Countries]([Id]),
    -- Billing
    [StripeCustomerId]  NVARCHAR(256)    NULL,
    [StripeSubscriptionId] NVARCHAR(256) NULL,
    [BillingPeriodStart] DATE            NULL,
    [BillingPeriodEnd]   DATE            NULL,
    [MinutesUsed]       INT              NOT NULL DEFAULT 0,
    -- Timestamps
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_Accounts_UserId] ON [app].[Accounts]([UserId]);
CREATE INDEX [IX_Accounts_StripeCustomerId] ON [app].[Accounts]([StripeCustomerId]) WHERE [StripeCustomerId] IS NOT NULL;
GO

-- Optional Feature Settings (Business plan only)
CREATE TABLE [app].[FeatureSettings] (
    [AccountId]         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY REFERENCES [app].[Accounts]([Id]),
    [AnswerQuestions]   BIT              NOT NULL DEFAULT 0,
    [ServicesInfo]      NVARCHAR(MAX)    NULL,
    [ServeArea]         BIT              NOT NULL DEFAULT 0,
    [DistanceKm]        INT              NOT NULL DEFAULT 50,
    [QuoteInspection]   BIT              NOT NULL DEFAULT 0,
    [PricePerKm]        DECIMAL(10,2)    NOT NULL DEFAULT 0,
    [UpdatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

-- Calls
CREATE TABLE [app].[Calls] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [AccountId]         UNIQUEIDENTIFIER NOT NULL REFERENCES [app].[Accounts]([Id]),
    [CallerName]        NVARCHAR(256)    NULL,
    [CallerPhone]       NVARCHAR(50)     NOT NULL,
    [DateTimeUtc]       DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [DurationSeconds]   INT              NOT NULL DEFAULT 0,
    [LanguageId]        TINYINT          NULL REFERENCES [app].[Languages]([Id]),
    [Summary]           NVARCHAR(MAX)    NULL,
    [Status]            NVARCHAR(20)     NOT NULL DEFAULT N'Missed', -- Answered, Completed, Missed, Failed
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_Calls_AccountId_DateTime] ON [app].[Calls]([AccountId], [DateTimeUtc] DESC);
CREATE INDEX [IX_Calls_AccountId_Status] ON [app].[Calls]([AccountId], [Status]);
GO

-- Collected Info from calls (key-value pairs)
CREATE TABLE [app].[CallCollectedInfo] (
    [Id]        BIGINT           NOT NULL PRIMARY KEY IDENTITY(1,1),
    [CallId]    UNIQUEIDENTIFIER NOT NULL REFERENCES [app].[Calls]([Id]) ON DELETE CASCADE,
    [Label]     NVARCHAR(100)    NOT NULL,
    [Value]     NVARCHAR(500)    NOT NULL
);

CREATE INDEX [IX_CallCollectedInfo_CallId] ON [app].[CallCollectedInfo]([CallId]);
GO

-- Invoices
CREATE TABLE [app].[Invoices] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [AccountId]         UNIQUEIDENTIFIER NOT NULL REFERENCES [app].[Accounts]([Id]),
    [StripeInvoiceId]   NVARCHAR(256)    NULL,
    [InvoiceNumber]     NVARCHAR(50)     NOT NULL,
    [DateUtc]           DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [Amount]            DECIMAL(10,2)    NOT NULL,
    [Currency]          CHAR(3)          NOT NULL DEFAULT 'USD',
    [Status]            NVARCHAR(20)     NOT NULL DEFAULT N'Paid',
    [PdfUrl]            NVARCHAR(500)    NULL,
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_Invoices_AccountId] ON [app].[Invoices]([AccountId], [DateUtc] DESC);
GO

-- Payment Methods
CREATE TABLE [app].[PaymentMethods] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [AccountId]             UNIQUEIDENTIFIER NOT NULL REFERENCES [app].[Accounts]([Id]),
    [StripePaymentMethodId] NVARCHAR(256)    NOT NULL,
    [Brand]                 NVARCHAR(50)     NOT NULL,   -- Visa, Mastercard, etc.
    [Last4]                 CHAR(4)          NOT NULL,
    [ExpMonth]              TINYINT          NOT NULL,
    [ExpYear]               SMALLINT         NOT NULL,
    [IsDefault]             BIT              NOT NULL DEFAULT 0,
    [CreatedAt]             DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_PaymentMethods_AccountId] ON [app].[PaymentMethods]([AccountId]);
GO

-- Auth: Refresh Tokens
CREATE TABLE [app].[RefreshTokens] (
    [Id]            BIGINT           NOT NULL PRIMARY KEY IDENTITY(1,1),
    [UserId]        UNIQUEIDENTIFIER NOT NULL REFERENCES [app].[Users]([Id]) ON DELETE CASCADE,
    [Token]         NVARCHAR(256)    NOT NULL UNIQUE,
    [ExpiresAt]     DATETIME2(7)     NOT NULL,
    [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [RevokedAt]     DATETIME2(7)     NULL,
    [ReplacedByToken] NVARCHAR(256)  NULL,
    [IsRevoked] AS (CASE WHEN [RevokedAt] IS NOT NULL THEN 1 ELSE 0 END)
);

CREATE INDEX [IX_RefreshTokens_Token] ON [app].[RefreshTokens]([Token]);
CREATE INDEX [IX_RefreshTokens_UserId] ON [app].[RefreshTokens]([UserId]);
GO

-- Auth: OTP codes (magic links, SMS verification)
CREATE TABLE [app].[OtpCodes] (
    [Id]            BIGINT           NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Identifier]    NVARCHAR(256)    NOT NULL,  -- email or phone
    [Code]          NVARCHAR(10)     NOT NULL,
    [Type]          NVARCHAR(20)     NOT NULL,  -- EmailMagicLink, SmsVerification
    [ExpiresAt]     DATETIME2(7)     NOT NULL,
    [IsUsed]        BIT              NOT NULL DEFAULT 0,
    [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [Attempts]      INT              NOT NULL DEFAULT 0
);

CREATE INDEX [IX_OtpCodes_Identifier_Type] ON [app].[OtpCodes]([Identifier], [Type]) WHERE [IsUsed] = 0;
GO

-- Contact Form Submissions
CREATE TABLE [app].[ContactSubmissions] (
    [Id]            BIGINT           NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]          NVARCHAR(256)    NOT NULL,
    [Email]         NVARCHAR(256)    NOT NULL,
    [Subject]       NVARCHAR(500)    NOT NULL,
    [Message]       NVARCHAR(MAX)    NOT NULL,
    [IpAddress]     NVARCHAR(45)     NULL,
    [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_ContactSubmissions_CreatedAt] ON [app].[ContactSubmissions]([CreatedAt] DESC);
GO

-- ============================================================
-- TRIGGER: Auto-update UpdatedAt
-- ============================================================
CREATE OR ALTER TRIGGER [app].[TR_Users_UpdatedAt]
ON [app].[Users]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE u SET [UpdatedAt] = SYSUTCDATETIME()
    FROM [app].[Users] u
    INNER JOIN inserted i ON u.[Id] = i.[Id];
END;
GO

CREATE OR ALTER TRIGGER [app].[TR_Accounts_UpdatedAt]
ON [app].[Accounts]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE a SET [UpdatedAt] = SYSUTCDATETIME()
    FROM [app].[Accounts] a
    INNER JOIN inserted i ON a.[Id] = i.[Id];
END;
GO

-- ============================================================
-- DONE
-- ============================================================
PRINT N'Gigahoo database created successfully.';
GO
