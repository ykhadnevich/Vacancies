-- Add Location column to ApplicationTrackers.
-- Captured from JobVacancy.Location when adding from feed, or supplied
-- manually when added via AddEntryForm. Nullable — many postings don't
-- mention a location.
--
-- Idempotent — safe to run twice.

ALTER TABLE "Applications"
    ADD COLUMN IF NOT EXISTS "Location" varchar(200) NULL;
