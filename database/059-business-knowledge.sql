-- 059: owner-provided business knowledge the phone agent answers caller questions from.
-- Capped at 2000 chars: the text rides every reply's prompt, and prompt bloat costs
-- instruction-following quality long before it costs money.
ALTER TABLE Account ADD BusinessKnowledge nvarchar(2000) NULL;
