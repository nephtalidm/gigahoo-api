-- 015-plan-prices.sql
-- Per-currency pricing for plans. One row per (plan, currency) holding the Stripe
-- recurring price id used at checkout plus a display amount. Adding a currency or
-- adjusting a price is now an INSERT/UPDATE here -- no schema change.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PlanPrice')
BEGIN
    CREATE TABLE [dbo].[PlanPrice] (
        [Id]            int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PlanPrice PRIMARY KEY,
        [PlanId]        tinyint        NOT NULL,
        [Currency]      nvarchar(3)    NOT NULL,
        [StripePriceId] nvarchar(255)  NULL,
        [Amount]        decimal(10,2)  NOT NULL CONSTRAINT DF_PlanPrice_Amount   DEFAULT 0,
        [IsActive]      bit            NOT NULL CONSTRAINT DF_PlanPrice_IsActive DEFAULT 1,
        CONSTRAINT FK_PlanPrice_Plan FOREIGN KEY ([PlanId]) REFERENCES [dbo].[Plan]([Id])
    );
    CREATE UNIQUE INDEX UX_PlanPrice_Plan_Currency ON [dbo].[PlanPrice]([PlanId],[Currency]);
END
GO

-- Pre-seed a row per paid plan x currency (USD/MXN/CAD) so the prices can be filled in
-- later (set StripePriceId + Amount). Idempotent.
INSERT INTO [dbo].[PlanPrice] ([PlanId],[Currency],[Amount])
SELECT p.[Id], x.[cur], 0
FROM [dbo].[Plan] p
CROSS JOIN (VALUES ('USD'),('MXN'),('CAD')) AS x([cur])
WHERE p.[PriceMonthly] > 0
  AND NOT EXISTS (SELECT 1 FROM [dbo].[PlanPrice] pp WHERE pp.[PlanId] = p.[Id] AND pp.[Currency] = x.[cur]);
GO

PRINT 'PlanPrice table created/seeded';
GO
