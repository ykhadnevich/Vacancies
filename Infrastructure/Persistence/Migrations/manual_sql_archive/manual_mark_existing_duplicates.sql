-- One-shot cleanup for the cross-scrape duplicates that accumulated before
-- JobAggregationService gained its cross-DB (Company, Title) semantic dedup.
--
-- Strategy: partition every row by (Company, Title), keep the oldest as
-- canonical, mark the rest as IsDuplicate=true with CanonicalJobId pointing
-- at the canonical's Id. Idempotent — re-running flips nothing already
-- correctly marked.
--
-- After applying this, v6 query's Resolved-pool filter (j.IsDuplicate=false)
-- silently drops the marked rows from the user-facing search.

BEGIN;

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
)
UPDATE "JobVacancies" jv
SET "IsDuplicate"    = true,
    "CanonicalJobId" = ranked.canonical_id
FROM ranked
WHERE jv."Id" = ranked."Id"
  AND ranked.rn > 1
  AND jv."IsDuplicate" = false;

-- Reset any rows that were canonicals but got marked by an earlier flawed
-- run (defensive — currently a no-op because canonicals always have rn = 1).
UPDATE "JobVacancies"
SET "IsDuplicate" = false,
    "CanonicalJobId" = NULL
WHERE "Id" IN (
    SELECT DISTINCT "CanonicalJobId"
    FROM "JobVacancies"
    WHERE "CanonicalJobId" IS NOT NULL
) AND "IsDuplicate" = true;

COMMIT;

-- Verify the result
SELECT
    "IsDuplicate",
    COUNT(*) AS count
FROM "JobVacancies"
GROUP BY "IsDuplicate";

SELECT
    'duplicates by company/title pair' AS metric,
    COUNT(*) AS pairs
FROM (
    SELECT 1
    FROM "JobVacancies"
    WHERE "IsDuplicate" = false
    GROUP BY LOWER(TRIM("Company")), LOWER(TRIM("Title"))
    HAVING COUNT(*) > 1
) x;
