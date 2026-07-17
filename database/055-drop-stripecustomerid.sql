SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 055-drop-stripecustomerid.sql
-- ONE source of truth for payment-provider identities: PaymentCustomer (account x provider).
-- Account.StripeCustomerId was a legacy mirror with its own write path (split brain: the
-- webhooks read the column, the payment-methods flow read the table). Any identities that
-- exist only in the column are backfilled into PaymentCustomer, then the column is dropped.
-- Idempotent.

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('[dbo].[Account]') AND name = 'StripeCustomerId')
BEGIN
    EXEC sp_executesql N'
        INSERT INTO [dbo].[PaymentCustomer] ([AccountId], [ProviderId], [CustomerId])
        SELECT a.[AccountId], p.[ProviderId], a.[StripeCustomerId]
        FROM [dbo].[Account] a
        JOIN [dbo].[Provider] p ON p.[Code] = ''stripe'' AND p.[ProviderTypeId] = 2
        WHERE a.[StripeCustomerId] IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM [dbo].[PaymentCustomer] pc
              WHERE pc.[AccountId] = a.[AccountId] AND pc.[ProviderId] = p.[ProviderId]);';

    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'DROP INDEX [' + i.name + N'] ON [dbo].[Account];'
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID('[dbo].[Account]') AND i.is_primary_key = 0
      AND c.name = N'StripeCustomerId';
    EXEC sp_executesql @sql;

    ALTER TABLE [dbo].[Account] DROP COLUMN [StripeCustomerId];
END
GO

PRINT '055-drop-stripecustomerid applied (PaymentCustomer is the one identity store)';
GO
