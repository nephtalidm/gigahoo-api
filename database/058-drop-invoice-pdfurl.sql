-- 058: drop Invoice.PdfUrl — Stripe rotates invoice_pdf URL tokens, so a stored copy
-- goes stale. The dashboard fetches a fresh link at click time (billing/invoices/{id}/pdf-link)
-- and the receipt email carries the PDF as an attachment.
ALTER TABLE Invoice DROP COLUMN PdfUrl;
