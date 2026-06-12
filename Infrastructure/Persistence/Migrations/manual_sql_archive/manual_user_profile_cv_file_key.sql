-- CR1 — UserProfile.CvFileKey
-- ----------------------------------------------------------------------------
-- Adds the S3 object key column for the original CV PDF uploaded to the
-- vacancies-cv-files bucket. Existing rows get NULL, which the application
-- treats as "no S3 file yet" — the next CV upload populates it.
--
-- Combine with the hard-reset described in
-- aws-setup/DEPLOY_CRITICAL_DECISIONS.md §3 on the first deploy that
-- introduces the S3 path: existing users re-upload their CVs through
-- the new endpoint.
--
-- Idempotent: ADD COLUMN IF NOT EXISTS is safe to re-run.
-- ----------------------------------------------------------------------------

ALTER TABLE "UserProfiles"
    ADD COLUMN IF NOT EXISTS "CvFileKey" VARCHAR(512) NULL;
