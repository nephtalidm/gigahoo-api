-- ============================================================
-- Migration 003: Rename Call to Conversation, merge CallCollectedInfo
-- Converts the 1:many Call -> CallCollectedInfo relationship into
-- a single Conversation table where Summary holds the customer need.
-- ============================================================

USE [Gigahoo];
GO

-- Step 1: Rename the Call table to Conversation
EXEC sp_rename 'dbo.Call', 'Conversation';
GO

-- Step 2: Rename FK and index constraints to match new table name
EXEC sp_rename 'FK_Call_Account_AccountId', 'FK_Conversation_Account_AccountId';
GO

EXEC sp_rename 'FK_Call_Language_LanguageId', 'FK_Conversation_Language_LanguageId', 'OBJECT';
GO

EXEC sp_rename 'IX_Calls_AccountId_DateTime', 'IX_Conversations_AccountId_DateTime', 'INDEX';
GO

EXEC sp_rename 'IX_Calls_AccountId_Status', 'IX_Conversations_AccountId_Status', 'INDEX';
GO

-- Step 3: Drop the CallCollectedInfo table (data merged into Conversation.Summary)
DROP TABLE IF EXISTS [dbo].[CallCollectedInfo];
GO

PRINT N'Migration 003 complete: Call renamed to Conversation, CallCollectedInfo dropped.';
GO
