-- Soft (semantic) deduplication via Gemini text-embedding-004 + pgvector
-- cosine distance.
--
-- Detects (Company, Title) pairs that:
--   * share the exact same canonical Company,
--   * have a cosine distance < 0.15 (i.e. cosine similarity > 0.85),
--   * are not already marked IsDuplicate,
--   * are not the canonical (oldest by AggregatedAt) themselves.
--
-- The "same Company" gate is what stops the algorithm from collapsing
-- generic-sounding titles (e.g. "Senior Backend Engineer") across unrelated
-- employers — that case wasn't a duplicate in the first place.
--
-- Threshold 0.15 was chosen against the live data set: catches
-- "Senior Full Stack .NET" ≈ "Senior .NET Full-Stack" (distance ~0.07) and
-- "Lead .NET Developer (...Embedded)" ≈ "Senior/Lead .NET Developer
-- (...Embedded)" (distance ~0.09), while leaving genuinely-different roles
-- at the same company (e.g. Storyby vs Howly at SKELAR — distance > 0.4)
-- alone.
--
-- Run AFTER embed_titles.sh has populated the Embedding column on every
-- canonical row. Idempotent — re-runs only mark new soft duplicates.

BEGIN;

WITH pairs AS (
    SELECT
        a."Id"        AS dup_id,
        b."Id"        AS canonical_id,
        (a."Embedding" <=> b."Embedding") AS distance
    FROM "JobVacancies" a
    JOIN "JobVacancies" b
        ON LOWER(TRIM(a."Company")) = LOWER(TRIM(b."Company"))
       AND a."Id" <> b."Id"
       AND a."AggregatedAt" >= b."AggregatedAt"
    WHERE a."IsDuplicate" = false
      AND b."IsDuplicate" = false
      AND a."Embedding" IS NOT NULL
      AND b."Embedding" IS NOT NULL
      AND a."Company" <> ''
      AND a."Company" IS NOT NULL
      AND (a."Embedding" <=> b."Embedding") < 0.15
),
-- Keep the closest canonical when multiple match (defensive).
best AS (
    SELECT DISTINCT ON (dup_id)
        dup_id,
        canonical_id,
        distance
    FROM pairs
    ORDER BY dup_id, distance ASC, canonical_id ASC
)
UPDATE "JobVacancies" jv
SET "IsDuplicate"    = true,
    "CanonicalJobId" = best.canonical_id
FROM best
WHERE jv."Id" = best.dup_id;

COMMIT;

-- Verification
SELECT
    'soft duplicates marked'                 AS metric,
    COUNT(*)                                  AS count
FROM "JobVacancies"
WHERE "IsDuplicate" = true;

SELECT
    'unique canonicals (IsDuplicate=false)'  AS metric,
    COUNT(*)                                  AS count
FROM "JobVacancies"
WHERE "IsDuplicate" = false;

-- Inspect a few examples (canonical + duplicate side-by-side, smallest
-- distances first).
SELECT
    can."Company",
    can."Title" AS canonical_title,
    dup."Title" AS duplicate_title,
    ROUND((dup."Embedding" <=> can."Embedding")::numeric, 4) AS cosine_distance
FROM "JobVacancies" dup
JOIN "JobVacancies" can ON dup."CanonicalJobId" = can."Id"
WHERE dup."IsDuplicate" = true
  AND dup."Embedding" IS NOT NULL
  AND can."Embedding" IS NOT NULL
ORDER BY dup."Embedding" <=> can."Embedding" ASC
LIMIT 15;
