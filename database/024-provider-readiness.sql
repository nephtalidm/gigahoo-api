-- 024-provider-readiness.sql
-- Make billing/LLM provider-agnostic so other payment providers can be added later
-- without schema churn. Introduces a PaymentCustomer table (one customer id per
-- account x provider), generalizes PlanPrice's Stripe-specific columns into
-- provider-agnostic ones, and records the per-account LLM provider on Account.
-- Idempotent: safe to run repeatedly.

------------------------------------------------------------------------------
-- 1. PaymentCustomer: provider customer id per (account, provider).
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PaymentCustomer')
BEGIN
    CREATE TABLE [dbo].[PaymentCustomer] (
        [Id]         int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentCustomer PRIMARY KEY,
        [AccountId]  uniqueidentifier  NOT NULL,
        [Provider]   nvarchar(20)      NOT NULL CONSTRAINT DF_PaymentCustomer_Provider DEFAULT 'stripe',
        [CustomerId] nvarchar(255)     NOT NULL,
        CONSTRAINT FK_PaymentCustomer_Account FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id]),
        CONSTRAINT UX_PaymentCustomer_Account_Provider UNIQUE ([AccountId],[Provider])
    );
END
GO

-- Migrate existing Stripe customer ids from Account into PaymentCustomer.
INSERT INTO [dbo].[PaymentCustomer] ([AccountId],[Provider],[CustomerId])
SELECT a.[Id], 'stripe', a.[StripeCustomerId]
FROM [dbo].[Account] a
WHERE a.[StripeCustomerId] IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM [dbo].[PaymentCustomer] pc
        WHERE pc.[AccountId] = a.[Id] AND pc.[Provider] = 'stripe'
  );
GO

------------------------------------------------------------------------------
-- 2. PlanPrice: add Provider, rename StripePriceId -> ProviderPriceId,
--    and make the uniqueness include Provider.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'Provider')
    ALTER TABLE [dbo].[PlanPrice]
        ADD [Provider] nvarchar(20) NOT NULL CONSTRAINT DF_PlanPrice_Provider DEFAULT 'stripe';
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[PlanPrice]') AND name = 'StripePriceId')
    EXEC sp_rename 'dbo.PlanPrice.StripePriceId', 'ProviderPriceId', 'COLUMN';
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_PlanPrice_Plan_Currency' AND object_id = OBJECT_ID(N'[dbo].[PlanPrice]'))
    DROP INDEX UX_PlanPrice_Plan_Currency ON [dbo].[PlanPrice];
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_PlanPrice_Plan_Currency_Provider' AND object_id = OBJECT_ID(N'[dbo].[PlanPrice]'))
    CREATE UNIQUE INDEX UX_PlanPrice_Plan_Currency_Provider ON [dbo].[PlanPrice]([PlanId],[Currency],[Provider]);
GO

------------------------------------------------------------------------------
-- 3. Account: per-account LLM provider.
------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'LlmProvider')
    ALTER TABLE [dbo].[Account]
        ADD [LlmProvider] nvarchar(20) NOT NULL CONSTRAINT DF_Account_LlmProvider DEFAULT 'qwen';
GO

PRINT 'Provider-readiness migration applied (PaymentCustomer, PlanPrice.Provider/ProviderPriceId, Account.LlmProvider)';
GO
