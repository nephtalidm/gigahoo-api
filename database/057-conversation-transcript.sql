-- 057: full call transcript on Conversation.
-- Common practice for AI agents: the summary is for triage, the transcript is the receipt.
-- Nullable — SMS/other conversation types and legacy rows have none.
ALTER TABLE Conversation ADD Transcript nvarchar(max) NULL;
