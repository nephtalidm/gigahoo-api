-- 006-drop-paymentmethod-and-refreshtoken.sql
-- Removes the PaymentMethod table (Stripe Billing Portal manages cards)
-- and RefreshToken table (replaced by long-lived JWTs, 7-day expiry).

IF OBJECT_ID('dbo.PaymentMethod', 'U') IS NOT NULL
BEGIN
    DROP INDEX [IX_PaymentMethods_AccountId] ON [dbo].[PaymentMethod];
    DROP TABLE [dbo].[PaymentMethod];
    PRINT 'Dropped PaymentMethod table';
END
ELSE
    PRINT 'PaymentMethod table already dropped';
GO

IF OBJECT_ID('dbo.RefreshToken', 'U') IS NOT NULL
BEGIN
    DROP INDEX [IX_RefreshTokens_UserId] ON [dbo].[RefreshToken];
    DROP INDEX [IX_RefreshTokens_Token] ON [dbo].[RefreshToken];
    DROP TABLE [dbo].[RefreshToken];
    PRINT 'Dropped RefreshToken table';
END
ELSE
    PRINT 'RefreshToken table already dropped';
GO
