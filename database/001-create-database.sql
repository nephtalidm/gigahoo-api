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
    EXEC('CREATE SCHEMA [dbo]');
GO

-- ============================================================
-- ENUM / LOOKUP TABLES
-- ============================================================

-- Plan
CREATE TABLE [dbo].[Plan] (
    [Id]                TINYINT         NOT NULL PRIMARY KEY,
    [Name]              NVARCHAR(50)    NOT NULL,
    [PriceMonthly]       DECIMAL(10,2)   NOT NULL DEFAULT 0,
    [IncludedMinutes]    INT             NOT NULL DEFAULT 0,
    [HasOptionalFeatures] BIT            NOT NULL DEFAULT 0,
    [IsActive]          BIT             NOT NULL DEFAULT 1
);

INSERT INTO [dbo].[Plan] ([Id], [Name], [PriceMonthly], [IncludedMinutes], [HasOptionalFeatures])
VALUES
    (1, N'Free',     0.00,   25,   0),
    (2, N'Starter', 49.00,  250,   0),
    (3, N'Business', 99.00, 1000,  1);
GO

-- Business Categories
CREATE TABLE [dbo].[BusinessCategory] (
    [Id]    TINYINT         NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]  NVARCHAR(100)   NOT NULL UNIQUE
);

INSERT INTO [dbo].[BusinessCategory] ([Name]) VALUES
    (N'Appliance Repair'), (N'Cleaning'), (N'Electrical'),
    (N'Garage Door Repair'), (N'HVAC'), (N'Locksmith'),
    (N'Plumbing'), (N'Roofing'), (N'Other');
GO

-- Country
CREATE TABLE [dbo].[Country] (
    [Id]        SMALLINT    NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]      NVARCHAR(100) NOT NULL,
    [Code]      CHAR(2)     NOT NULL UNIQUE,   -- ISO 3166-1 alpha-2
    [DialCode]  NVARCHAR(10) NOT NULL,
    [Flag]      NVARCHAR(10) NULL
);

INSERT INTO [dbo].[Country] ([Name], [Code], [DialCode], [Flag]) VALUES
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

-- Supported Language
CREATE TABLE [dbo].[Language] (
    [Id]    TINYINT         NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]  NVARCHAR(50)    NOT NULL UNIQUE
);

INSERT INTO [dbo].[Language] ([Name]) VALUES
    (N'English'), (N'French'), (N'Mandarin'), (N'Cantonese'),
    (N'Spanish'), (N'Japanese'), (N'Hindi'), (N'Korean'), (N'Tagalog');
GO

-- Region (States/Provinces)
CREATE TABLE [dbo].[Region] (
    [Id]        SMALLINT    NOT NULL PRIMARY KEY IDENTITY(1,1),
    [CountryId] SMALLINT    NOT NULL REFERENCES [dbo].[Country]([Id]),
    [Name]      NVARCHAR(100) NOT NULL,
    [Code]      NVARCHAR(10) NOT NULL,
    CONSTRAINT [UQ_Regions_Country_Code] UNIQUE ([CountryId], [Code])
);

-- US States
INSERT INTO [dbo].[Region] ([CountryId], [Name], [Code])
SELECT c.[Id], v.[Name], v.[Code]
FROM [dbo].[Country] c
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
INSERT INTO [dbo].[Region] ([CountryId], [Name], [Code])
SELECT c.[Id], v.[Name], v.[Code]
FROM [dbo].[Country] c
CROSS JOIN (VALUES
    (N'Alberta',N'AB'),(N'British Columbia',N'BC'),(N'Manitoba',N'MB'),
    (N'New Brunswick',N'NB'),(N'Newfoundland and Labrador',N'NL'),
    (N'Nova Scotia',N'NS'),(N'Northwest Territories',N'NT'),(N'Nunavut',N'NU'),
    (N'Ontario',N'ON'),(N'Prince Edward Island',N'PE'),(N'Quebec',N'QC'),
    (N'Saskatchewan',N'SK'),(N'Yukon',N'YT')
) AS v([Name], [Code])
WHERE c.[Code] = N'CA';

-- Mexican States
INSERT INTO [dbo].[Region] ([CountryId], [Name], [Code])
SELECT c.[Id], v.[Name], v.[Code]
FROM [dbo].[Country] c
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

-- Account (single table: auth identity + business profile + feature settings)
CREATE TABLE [dbo].[Account] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),

    -- Auth identity
    [Email]             NVARCHAR(256)    NULL,
    [NormalizedEmail]   NVARCHAR(256)    NULL,
    [PhoneNumber]       NVARCHAR(50)     NULL,
    [NormalizedPhone]   NVARCHAR(50)     NULL,
    [GoogleSubjectId]   NVARCHAR(256)    NULL,
    [DisplayName]       NVARCHAR(256)    NULL,
    [IsEmailConfirmed]  BIT              NOT NULL DEFAULT 0,
    [IsPhoneConfirmed]  BIT              NOT NULL DEFAULT 0,
    [LastLoginAt]       DATETIME2(7)     NULL,

    -- Business profile
    [BusinessName]      NVARCHAR(256)    NOT NULL,
    [CategoryId]        TINYINT          NOT NULL REFERENCES [dbo].[BusinessCategory]([Id]),
    [BusinessPhone]     NVARCHAR(50)     NOT NULL,
    [PhoneCountryCode]  CHAR(2)          NOT NULL DEFAULT N'US',
    [ServiceArea]       NVARCHAR(500)    NULL,
    [WebsiteUrl]        NVARCHAR(500)    NULL,
    [BusinessHours]     NVARCHAR(500)    NULL,
    [ForwardingPhone]   NVARCHAR(50)     NULL,
    [PlanId]            TINYINT          NOT NULL DEFAULT 2 REFERENCES [dbo].[Plan]([Id]),

    -- Address
    [AddressLine1]      NVARCHAR(256)    NULL,
    [AddressLine2]      NVARCHAR(256)    NULL,
    [City]              NVARCHAR(100)    NULL,
    [RegionId]          SMALLINT         NULL REFERENCES [dbo].[Region]([Id]),
    [RegionCustom]      NVARCHAR(100)    NULL,
    [PostalCode]        NVARCHAR(20)     NULL,
    [CountryId]         SMALLINT         NOT NULL REFERENCES [dbo].[Country]([Id]),

    -- Billing
    [StripeCustomerId]  NVARCHAR(256)    NULL,
    [StripeSubscriptionId] NVARCHAR(256) NULL,
    [PhoneNumberSid]    NVARCHAR(100)    NULL,
    [TelephonyProvider] NVARCHAR(50)     NULL,
    [BillingPeriodStart] DATE            NULL,
    [BillingPeriodEnd]   DATE            NULL,
    [MinutesUsed]       INT              NOT NULL DEFAULT 0,

    -- Feature settings (Business plan only)
    [AnswerQuestions]       BIT           NOT NULL DEFAULT 0,
    [ServicesInfo]          NVARCHAR(2000) NULL,
    [FeatureServiceAreas]   NVARCHAR(500)  NULL,
    [FeatureBusinessHours]  NVARCHAR(500)  NULL,
    [EmergencyAvailability] NVARCHAR(500)  NULL,
    [PricingPolicy]         NVARCHAR(2000) NULL,
    [WarrantyPolicy]        NVARCHAR(2000) NULL,
    [FrequentlyAskedQuestions] NVARCHAR(MAX) NULL,
    [AdditionalBusinessInfo] NVARCHAR(2000) NULL,
    [ServeArea]         BIT              NOT NULL DEFAULT 0,
    [DistanceKm]        INT              NOT NULL DEFAULT 50,
    [QuoteInspection]   BIT              NOT NULL DEFAULT 0,
    [PricePerKm]        DECIMAL(10,2)    NOT NULL DEFAULT 0,
    [FeatureUpdatedAt]  DATETIME2(7)     NULL,

    -- Timestamps
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [UpdatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX [IX_Account_NormalizedEmail] ON [dbo].[Account]([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL;
CREATE UNIQUE INDEX [IX_Account_NormalizedPhone] ON [dbo].[Account]([NormalizedPhone]) WHERE [NormalizedPhone] IS NOT NULL;
CREATE UNIQUE INDEX [IX_Account_GoogleSubjectId] ON [dbo].[Account]([GoogleSubjectId]) WHERE [GoogleSubjectId] IS NOT NULL;
CREATE INDEX [IX_Account_StripeCustomerId] ON [dbo].[Account]([StripeCustomerId]) WHERE [StripeCustomerId] IS NOT NULL;
GO

-- Conversation (renamed from Call)
CREATE TABLE [dbo].[Conversation] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [AccountId]         UNIQUEIDENTIFIER NOT NULL REFERENCES [dbo].[Account]([Id]),
    [CallerName]        NVARCHAR(256)    NULL,
    [CallerPhone]       NVARCHAR(50)     NOT NULL,
    [DateTimeUtc]       DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [DurationSeconds]   INT              NOT NULL DEFAULT 0,
    [LanguageId]        TINYINT          NULL REFERENCES [dbo].[Language]([Id]),
    [Summary]           NVARCHAR(MAX)    NULL,
    [Status]            NVARCHAR(20)     NOT NULL DEFAULT N'Missed',
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_Conversations_AccountId_DateTime] ON [dbo].[Conversation]([AccountId], [DateTimeUtc] DESC);
CREATE INDEX [IX_Conversations_AccountId_Status] ON [dbo].[Conversation]([AccountId], [Status]);
GO

-- Invoice
CREATE TABLE [dbo].[Invoice] (
    [Id]                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    [AccountId]         UNIQUEIDENTIFIER NOT NULL REFERENCES [dbo].[Account]([Id]),
    [StripeInvoiceId]   NVARCHAR(256)    NULL,
    [InvoiceNumber]     NVARCHAR(50)     NOT NULL,
    [DateUtc]           DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [Amount]            DECIMAL(10,2)    NOT NULL,
    [Currency]          CHAR(3)          NOT NULL DEFAULT 'USD',
    [Status]            NVARCHAR(20)     NOT NULL DEFAULT N'Paid',
    [PdfUrl]            NVARCHAR(500)    NULL,
    [CreatedAt]         DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_Invoices_AccountId] ON [dbo].[Invoice]([AccountId], [DateUtc] DESC);
GO

-- Auth: OTP codes (magic links, SMS verification)
CREATE TABLE [dbo].[OtpCode] (
    [Id]            BIGINT           NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Identifier]    NVARCHAR(256)    NOT NULL,  -- email or phone
    [Code]          NVARCHAR(10)     NOT NULL,
    [Type]          NVARCHAR(20)     NOT NULL,  -- EmailMagicLink, SmsVerification
    [ExpiresAt]     DATETIME2(7)     NOT NULL,
    [IsUsed]        BIT              NOT NULL DEFAULT 0,
    [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
    [Attempts]      INT              NOT NULL DEFAULT 0
);

CREATE INDEX [IX_OtpCodes_Identifier_Type] ON [dbo].[OtpCode]([Identifier], [Type]) WHERE [IsUsed] = 0;
GO

-- Contact Form Submissions
CREATE TABLE [dbo].[ContactSubmission] (
    [Id]            BIGINT           NOT NULL PRIMARY KEY IDENTITY(1,1),
    [Name]          NVARCHAR(256)    NOT NULL,
    [Email]         NVARCHAR(256)    NOT NULL,
    [Subject]       NVARCHAR(500)    NOT NULL,
    [Message]       NVARCHAR(MAX)    NOT NULL,
    [IpAddress]     NVARCHAR(45)     NULL,
    [CreatedAt]     DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX [IX_ContactSubmissions_CreatedAt] ON [dbo].[ContactSubmission]([CreatedAt] DESC);
GO

-- ============================================================
-- TRIGGER: Auto-update UpdatedAt
-- ============================================================
CREATE OR ALTER TRIGGER [dbo].[TR_Account_UpdatedAt]
ON [dbo].[Account]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE a SET [UpdatedAt] = SYSUTCDATETIME()
    FROM [dbo].[Account] a
    INNER JOIN inserted i ON a.[Id] = i.[Id];
END;
GO

-- ============================================================
-- DONE
-- ============================================================
PRINT N'Gigahoo database created successfully.';
GO
