# Vacancy Normalizer v3 — Anti-Abstraction Changes

**Date:** 2026-05-24
**Driver:** reason quality investigation — concrete failure case (Treeum Data Engineer pair) traced upstream to vacancy normalizer hallucinating `["DWH", "ETL", "ELT"]` as must-have skills when raw text mentioned only `DataForm`.

---

## Problem

`docs/research/monolithic_vs_split_scoring.md` §6.1 documented +44% over-extraction by vacancy normalizer vs gold standard. Targeted scan of 116 normalized vacancies (those with raw text available) found:

- **66 of 116 vacancies (57%)** contain at least one hallucinated must-have skill (token not appearing in raw text)
- **438 individual hallucinations** across the 116 vacancies (≈32% of all extracted skills)
- **PM / PMM vacancies worst** — up to 72 of 74 must_haves invented (e.g. "JTBD", "Postman", "Swagger", "Amplitude", "LTV", "CAC" extracted from text that mentions none of them)
- Top hallucinated tokens: `ETL` (7×), `PostgreSQL` (7×), `AWS` (7×), `Python` (9×), `ELT` (5×), `Azure` (7×), `Ansible` (4×), `BigQuery` (4×), `Kubernetes` (4×)

Downstream consequence: reason text faithfully reports `"Gaps: DWH, ETL"` even when the candidate's CV explicitly lists Snowflake/BigQuery/dbt — semantically wrong but mechanically correct given the bad evidence. Same root cause inflates `skill_match` denominators, distorting `composite_score`.

---

## Changes applied

### 1. `VacancyNormalizationPromptCore.cs` — version v2 → **v3**

Added section **B.0 ANTI-ABSTRACTION RULE** before existing section B (SKILLS). Splits skill extraction into two modes:

- **(a) TOOLS / TECHNOLOGIES** must appear literally — programming languages, frameworks, SaaS products, platforms, libraries, database engines, cloud vendors, named protocols. Six worked examples (correct + wrong with explanation).
- **(b) CONCEPTS / METHODOLOGIES** may be paraphrased from semantic description — process / activity terms for PM, Marketing, Design, BA, Sales, HR roles. Three correct examples + one wrong example showing where even concept-mode goes too far.

Closes with a four-line rule of thumb: capitalized brand → literal; all-caps abbreviation → literal; process term → may paraphrase; uncertain → don't extract.

Token budget impact: ~+800 input tokens per vacancy (≈+13% from ~6000 baseline). Cost: +$0.000045 per call at Gemini 2.0 Flash pricing.

### 2. `TechVacancyNormalizationModule.cs` — no version bump (still `tech_v1`)

Updated `model_version` in worked example JSON from `vac_v1+tech_v1` → `vac_v3+tech_v1` so Gemini sees the correct composite label in its anchor example.

### 3. `GenericVacancyNormalizationModule.cs` — version generic_v1 → **generic_v2**

Replaced placeholder `FullWorkedExample = "(no domain-specific worked example for Generic — Core template provides sufficient procedure for unfamiliar role types)."` with a full PMM example demonstrating B.0 mode (b) — paraphrase-based extraction. Includes 6 "anchors to learn from this example" explanations showing exactly where to stop abstracting. This is the highest-impact module change because the diagnostic scan showed PM/PMM vacancies route to Generic and were the worst hallucination cluster.

### 4. `VacancyNormalizationPostProcessor.cs` — safety net (no version field, but functional change)

Added deterministic hallucination filter inside `CanonAndFilter()`. For every skill emitted by Gemini, after canonicalization and soft-trait filtering:

- If the token is "brand-like" (single-word, capitalized, no spaces, OR ≤5-char all-caps abbreviation) — require it to appear literally in `vacancyRawText` (either canonical form OR pre-canonicalization form). Drop if neither matches.
- If the token contains a space or lowercase characters — skip the check (treat as concept; paraphrasing allowed per B.0 (b)).

New helpers: `IsBrandLikeToken(string)` (≤20 LOC), `AppearsInText(string, string)` (≤5 LOC). Class-level XML doc updated to list the new step (4).

Composite version unaffected (post-processor isn't part of the prompt version label) — but cache invalidation still happens automatically because both `vac_v3` (Core bump) and `generic_v2` (Generic bump) change the composite key.

---

## Validation done in-line

### Unit-level (24/24 tests pass)

Hand-rolled Python parity of `IsBrandLikeToken` + `AppearsInText` tested against 24 cases covering:
- Brand-like present in text (KEEP): Snowflake, Airflow, Python, C#, .NET, Node.js
- Brand-like absent (FILTER): DWH, ETL, ELT, VBA, VB6, Nomad, Ansible, AWS
- Concepts (KEEP regardless): product positioning, customer research, market analysis, hypothesis validation, A/B testing
- Edge cases: CI/CD (slash, kept), SQL (present, kept), API (both cases tested)

All 24 expected outcomes matched.

### Real-data simulation (116 normalized vacancies)

Ran the same filter against the 116 vacancies with raw-text mapping:

| Bucket | Count | % of total skills |
|---|---|---|
| KEPT — brand-like, in text | 195 | 14% |
| KEPT — concept (multi-word/lowercase) | 733 | 53% |
| **FILTERED — brand-like, hallucinated** | **438** | **32%** |
| Total skills processed | 1366 | 100% |

**Vacancies improved: 55 of 116 (47%)** — meaning the post-processor alone (before the prompt fix even runs) would remove at least one hallucination from nearly half the data set. With the prompt fix layered on top, expected to drop substantially.

### Independent review

Conducted an independent code review with all five vacancy normalization files. Verdict:
- ✅ C# syntax clean (string concatenation, escape chars, all `+` operators present)
- ✅ Logically consistent with existing B section (B.0 is a pre-filter, no conflict)
- ⚠️ Mild concern: `must_have_skills.f1` grader is F2 (recall-heavy `beta=2.0`) — fewer extractions may dent score if gold standard itself is over-extracted. Likely net positive because gold is curated, not over-extracted, but should be measured.
- ⚠️ Edge cases the prompt doesn't fully cover: composite skills like "OAuth2 / OIDC", lowercase short tools like "dbt", implicit-by-role inferences ("DBA position" → needs SQL?). Acceptable for v3 — these are smaller second-order issues.

---

## How to run eval

```powershell
# 1. Rebuild
dotnet build EvalTool

# 2. Re-run vacancy normalization on full gold set (~5 minutes)
dotnet run --project EvalTool -- evaluate-vacancies `
    --gold-set ../gold_set_v2 `
    --output ../results/vacancy_v3_anti_abstraction `
    --version vac_v3+generic_v2

# 3. Compare overall metric vs frozen baseline (0.810)
#    Look at must_have_skills.f1 and nice_to_have_skills.f1 specifically.
#    Look at per_case_per_metric.csv for vacancy-level regression.
#    Look at HISTORY.md for the auto-appended summary row.

# 4. If overall ≥ 0.80 (within 1pp of baseline) — proceed to re-score
dotnet run --project EvalTool -- run-scoring `
    --vacancy-normalized ../results/vacancy_v3_anti_abstraction/normalized `
    --output ../results/scoring_v3_clean_evidence

# 5. Compare reason quality programmatically (hallucination rate should drop
#    from current 10.7% — see "Reason quality stats" Python check that ran
#    earlier in this analysis).
```

## Expected outcomes

| Metric | Baseline (v2) | Target (v3) | Direction |
|---|---|---|---|
| `must_have_skills.f1` (vacancy eval) | unknown numeric | within ±5% of baseline | acceptable |
| Vacancy normalizer overall | 0.810 | ≥ 0.80 | non-regression |
| Reason hallucination rate (scoring eval) | 10.7% | ≤ 5% | improvement |
| Score MAE vs ideal | 0.086 | 0.086 ± 0.005 | unchanged (math is C#) |
| Verdict bucket match | 72.6% | 75-80% | improvement (better evidence → better calibration on edge pairs) |

## What's NOT addressed yet (next iterations)

- **Family-aware SkillCanonicalizer** (Step 2 of original plan): doesn't yet treat Snowflake/BigQuery/Redshift as same DWH family. Would further reduce false "missing DWH" on candidates with one of those tools.
- **Evidence prioritization** (Step 3): reason still picks top-N missing without explicit importance ranking.
- **Reason prompt enrichment** (Step 4): reason still generic — doesn't surface overqualification, cross-domain transitions, etc.
- **Reason validation layer** (Step 5): no post-generation hallucination check on the reason text itself.

These are intentional follow-ups, not regressions.

## Files changed

```
Infrastructure/RelevancePipeline/V2/VacancyNormalization/
  VacancyNormalizationPromptCore.cs              # v2 → v3, added B.0 section
  TechVacancyNormalizationModule.cs              # model_version label updated
  GenericVacancyNormalizationModule.cs           # generic_v1 → generic_v2, full PMM example
  VacancyNormalizationPostProcessor.cs           # added hallucination filter + 2 helpers

docs/research/
  vacancy_normalizer_v3_changes.md               # this file
```

## Rollback

If post-eval shows regression > 5pp on overall vacancy normalization score:

1. Revert `VacancyNormalizationPromptCore.cs` (`Version = "v2"`, remove B.0 section, restore composite-version doc)
2. Revert `GenericVacancyNormalizationModule.cs` (`Version => "generic_v1"`, restore placeholder FullWorkedExample)
3. Revert `VacancyNormalizationPostProcessor.cs` (remove `vacancyRawText` parameter from `CanonAndFilter` and the brand-like filter block + 2 helpers)
4. Revert `TechVacancyNormalizationModule.cs` (`model_version` label back to `vac_v1+tech_v1`)
5. Re-run eval to confirm restoration

Total revert is ≤ 10 minutes via `git checkout HEAD~1 -- Infrastructure/RelevancePipeline/V2/VacancyNormalization/`.
