SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 045-voice-names.sql
-- Give every Fish voice a friendly English first name as its picker Label (instead of the
-- generic "Male"/"Female 2" seeded by 044). Idempotent: keyed on ApiName.

UPDATE [dbo].[Voice] SET [Label] = N'Daniel' WHERE [ApiName] = 'c0f7fea11f2d4b13b89a55a000b02c1b';
UPDATE [dbo].[Voice] SET [Label] = N'Emily'  WHERE [ApiName] = '56b4cf7513704a33a074996859639cfd';
UPDATE [dbo].[Voice] SET [Label] = N'Lucas'  WHERE [ApiName] = 'c3719ef423f6494f9d0389e4274bb379';
UPDATE [dbo].[Voice] SET [Label] = N'Ethan'  WHERE [ApiName] = '4df04f6031014e93b3eb6333c4df104e';
UPDATE [dbo].[Voice] SET [Label] = N'Ryan'   WHERE [ApiName] = '098f0f275e8d41a0bc13598f70f46337';
UPDATE [dbo].[Voice] SET [Label] = N'Hannah' WHERE [ApiName] = '5161d41404314212af1254556477c17d';
UPDATE [dbo].[Voice] SET [Label] = N'Chloe'  WHERE [ApiName] = 'f662e62acfb949958043ba29058fe282';
GO

PRINT '045-voice-names applied (Fish voices labeled with English first names)';
GO
