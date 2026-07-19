-- 060: custom greetings cap at 100 chars — a greeting is one short spoken sentence,
-- and the caller waits through every word of it before they can talk.
UPDATE Account SET GreetingMessage = LEFT(GreetingMessage, 100) WHERE LEN(GreetingMessage) > 100;
ALTER TABLE Account ALTER COLUMN GreetingMessage nvarchar(100) NULL;
