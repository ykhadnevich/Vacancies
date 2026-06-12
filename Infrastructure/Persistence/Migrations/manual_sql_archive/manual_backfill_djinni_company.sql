-- Djinni rows had Company='' because the scraper only recognised the Ukrainian
-- " в " separator in the page <title>, but Djinni now serves English locale
-- with " at " for many sessions. The split missed and the whole string landed
-- in Title. This script lifts the company out of the Title for every existing
-- Djinni row whose Company is empty.
--
-- Idempotent — the WHERE clause makes a re-run a no-op once the rows are
-- patched. Run AFTER deploying the scraper patch so newly-scraped rows are
-- already correct.

BEGIN;

-- 1. Move " at <Company>" suffix into the Company column.
UPDATE "JobVacancies"
SET
    "Company" = TRIM(SPLIT_PART("Title", ' at ', 2)),
    "Title"   = TRIM(SPLIT_PART("Title", ' at ', 1))
WHERE "Source" = 'Djinni'
  AND ("Company" = '' OR "Company" IS NULL)
  AND POSITION(' at ' IN "Title") > 0;

-- 2. Some Djinni titles may use " в " (Ukrainian) instead — same fix.
UPDATE "JobVacancies"
SET
    "Company" = TRIM(SPLIT_PART("Title", ' в ', 2)),
    "Title"   = TRIM(SPLIT_PART("Title", ' в ', 1))
WHERE "Source" = 'Djinni'
  AND ("Company" = '' OR "Company" IS NULL)
  AND POSITION(' в ' IN "Title") > 0;

-- 3. Re-run the cross-row (Company, Title) dedup pass over the freshly
--    populated Company column so the previously-hidden Djinni duplicates
--    now collapse onto their cross-source canonicals.
WITH ranked AS (
    SELECT
        "Id",
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(TRIM("Company")), LOWER(TRIM("Title"))
            ORDER BY "AggregatedAt" ASC, "Id" ASC
        ) AS rn,
        FIRST_VALUE("Id") OVER (
            PARTITION BY LOWER(TRIM("Company")), LOWER(TRIM("Title"))
            ORDER BY "AggregatedAt" ASC, "Id" ASC
        ) AS canonical_id
    FROM "JobVacancies"
    WHERE "Company" <> ''
      AND "Company" IS NOT NULL
)
UPDATE "JobVacancies" jv
SET "IsDuplicate"    = true,
    "CanonicalJobId" = ranked.canonical_id
FROM ranked
WHERE jv."Id" = ranked."Id"
  AND ranked.rn > 1
  AND jv."IsDuplicate" = false;

COMMIT;

-- Verification
SELECT "Source",
       COUNT(*) FILTER (WHERE "Company" = '' OR "Company" IS NULL) AS empty_company,
       COUNT(*) FILTER (WHERE "IsDuplicate" = true)                AS duplicates,
       COUNT(*)                                                    AS total
FROM "JobVacancies"
GROUP BY "Source"
ORDER BY total DESC;

SELECT 'unique canonicals (IsDuplicate=false)' AS metric,
       COUNT(*) AS count
FROM "JobVacancies"
WHERE "IsDuplicate" = false;
