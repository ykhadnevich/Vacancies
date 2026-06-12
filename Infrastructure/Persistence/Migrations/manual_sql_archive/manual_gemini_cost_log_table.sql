-- Persistent per-stage Gemini cost ledger. One row per (request, stage)
-- snapshot produced by CostBreakdown. Indexed for time-range and per-
-- request lookups. Idempotent — re-running is a no-op once the table
-- exists.

CREATE TABLE IF NOT EXISTS "GeminiCostLog" (
    "Id"           uuid          NOT NULL PRIMARY KEY,
    "Timestamp"    timestamp     NOT NULL,
    "UserId"       uuid          NULL,
    "RequestId"    uuid          NOT NULL,
    "RequestKind"  varchar(64)   NOT NULL,
    "Stage"        varchar(64)   NOT NULL,
    "Calls"        integer       NOT NULL,
    "DurationMs"   double precision NOT NULL,
    "InputTokens"  bigint        NOT NULL,
    "OutputTokens" bigint        NOT NULL,
    "CostUsd"      double precision NOT NULL,
    "Keywords"     varchar(256)  NULL
);

CREATE INDEX IF NOT EXISTS "IX_GeminiCostLog_Timestamp" ON "GeminiCostLog" ("Timestamp");
CREATE INDEX IF NOT EXISTS "IX_GeminiCostLog_RequestId" ON "GeminiCostLog" ("RequestId");
