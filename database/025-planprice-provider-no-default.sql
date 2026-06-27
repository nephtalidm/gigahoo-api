-- 025-planprice-provider-no-default.sql
-- Provider is set explicitly per price row (which provider that ProviderPriceId
-- belongs to) — don't bake 'stripe' into the schema as a default.
IF EXISTS (SELECT * FROM sys.default_constraints WHERE name = 'DF_PlanPrice_Provider')
    ALTER TABLE [dbo].[PlanPrice] DROP CONSTRAINT DF_PlanPrice_Provider;
GO
PRINT 'Dropped DF_PlanPrice_Provider (provider no longer defaults to stripe)';
GO
