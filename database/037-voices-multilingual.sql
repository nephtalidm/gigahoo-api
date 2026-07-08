SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- 037-voices-multilingual.sql
-- Expand the Qwen voice picker to the 47 MULTILINGUAL qwen3.5-omni voices (each speaks every
-- supported language, verified working on qwen3.5-omni-flash-realtime 2026-07-02). Deactivate
-- the Chinese-dialect voices seeded in 029 (Kiki/Sunny/Marcus/Peter/Dylan/Rocky) + Eric — they
-- only speak their dialect, wrong for a multilingual receptionist. Idempotent.

DECLARE @qwen int = (SELECT [ProviderId] FROM [dbo].[Provider] WHERE [Code] = 'qwen');

-- The multilingual set, in picker order (name doubles as the label for newly-added voices;
-- the six that 029 already seeded keep their nicer existing labels).
DECLARE @voices TABLE ([ApiName] nvarchar(64), [DisplayOrder] int);
INSERT INTO @voices ([ApiName],[DisplayOrder]) VALUES
  (N'Jennifer',0), (N'Serena',1), (N'Ethan',2), (N'Aiden',3), (N'Ryan',4), (N'Katerina',5),
  (N'Tina',6), (N'Cindy',7), (N'Liora Mira',8), (N'Sunnybobi',9), (N'Raymond',10), (N'Theo Calm',11),
  (N'Harvey',12), (N'Maia',13), (N'Evan',14), (N'Qiao',15), (N'Momo',16), (N'Wil',17), (N'Angel',18),
  (N'Li Cassian',19), (N'Mia',20), (N'Joyner',21), (N'Gold',22), (N'Mione',23), (N'Sohee',24),
  (N'Lenn',25), (N'Ono Anna',26), (N'Sonrisa',27), (N'Bodega',28), (N'Emilien',29), (N'Andre',30),
  (N'Radio Gol',31), (N'Alek',32), (N'Rizky',33), (N'Roya',34), (N'Arda',35), (N'Hana',36), (N'Dolce',37),
  (N'Jakub',38), (N'Griet',39), (N'Eliška',40), (N'Marina',41), (N'Siiri',42), (N'Ingrid',43), (N'Sigga',44),
  (N'Bea',45), (N'Chloe',46);

-- 1. Insert any that aren't in the table yet (label = name).
INSERT INTO [dbo].[Voice] ([ProviderId],[ApiName],[Label],[DisplayOrder],[IsDefault],[IsActive])
SELECT @qwen, v.[ApiName], v.[ApiName], v.[DisplayOrder], 0, 1
FROM @voices v
WHERE @qwen IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [dbo].[Voice] x WHERE x.[ProviderId] = @qwen AND x.[ApiName] = v.[ApiName]);

-- 2. Reactivate + reorder the whole multilingual set (existing rows keep their labels).
UPDATE tgt SET tgt.[DisplayOrder] = v.[DisplayOrder], tgt.[IsActive] = 1
FROM [dbo].[Voice] tgt
JOIN @voices v ON v.[ApiName] = tgt.[ApiName]
WHERE tgt.[ProviderId] = @qwen;

-- 3. Deactivate anything NOT multilingual (the dialect voices + Eric from 029).
UPDATE [dbo].[Voice] SET [IsActive] = 0
WHERE [ProviderId] = @qwen AND [ApiName] NOT IN (SELECT [ApiName] FROM @voices);

-- 4. Jennifer is the (single) default, and active.
UPDATE [dbo].[Voice] SET [IsDefault] = 0 WHERE [ProviderId] = @qwen;
UPDATE [dbo].[Voice] SET [IsDefault] = 1, [IsActive] = 1 WHERE [ProviderId] = @qwen AND [ApiName] = N'Jennifer';
GO

PRINT '037-voices-multilingual applied (47 multilingual voices active; dialect voices deactivated)';
GO
