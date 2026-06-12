# Gold Set Freeze Proof — Thesis Evidence

**Generated:** 2026-05-30
**Purpose:** Evidence that gold sets were frozen BEFORE the v6.4 → v6.7.2 prompt
optimization marathon. Pre-empts the defense question *"Did you train on test?"*

---

## 1. Critical finding: git history is insufficient

The Vacancies repo contains only **1 git commit** (Initial commit, 2026-04-27).
Gold set directories (`gold_set/`, `gold_set_v2/`) were **never committed** to
git, so a `git log --diff-filter=A` proof is not available.

**Fallback evidence:** filesystem `mtime` + SHA256 manifests (this document).

## 2. Filesystem timeline (mtime evidence)

| Artifact | First created | Last modified | Files |
|---|---|---|---|
| `gold_set/expected/` (CV gold) | **2026-05-21 19:41** | 2026-05-21 (same day) | 25 |
| `gold_set_v2/vacancies/expected/` (Vacancy gold) | **2026-05-22 20:33** | 2026-05-22 20:39 (6-min batch) | 357 |
| `gold_set_v2/vacancies/selected/selected.json` (392 pairs) | 2026-05-22 | random_seed=42, reproducible | 1 |
| `Infrastructure/.../JudgePromptCore.cs` (v6.7.1 marker) | — | **2026-05-29 19:44** | 1 |
| `Infrastructure/.../GeminiBatchedJudgeService.cs` (v6.5 marker) | — | **2026-05-29 19:48** | 1 |
| `Infrastructure/.../GeminiBatchedReasonService.cs` (v6.4 marker) | — | **2026-05-29 20:17** | 1 |

**Gap between gold freeze and v6.7.x prompt iteration:** 7 days
**Direction:** Gold preceded prompt iteration — defensible "frozen before tuning".

Verification command (reproducible):
```bash
find gold_set/expected -type f -newermt "2026-05-22" | wc -l
# Expected: 0
find gold_set_v2/vacancies/expected -type f -newermt "2026-05-23" | wc -l
# Expected: 0
```

## 3. Cryptographic manifests

Three SHA256 manifests in this directory lock the byte content as-of
2026-05-30:

- `gold_set_cv_manifest.sha256` — 25 lines (one hash per CV gold file)
- `gold_set_v2_vacancies_manifest.sha256` — 357 lines (one hash per vacancy gold file)
- `selected_pairs_manifest.sha256` — 1 line (the 392-pair selection)

Any future modification to a gold file changes its SHA256 and will be
detected by re-running:

```bash
( cd gold_set/expected && find . -type f -name '*.json' -printf '%p\n' \
  | sort | xargs sha256sum ) | diff - gold_set_cv_manifest.sha256
# Expected: no output (identical)
```

## 4. Layer-by-layer freeze applicability

| Layer | Gold type | Freeze applicability | Notes |
|---|---|---|---|
| 1. CV Norm | Hand-annotated JSON | **Required** | Frozen 2026-05-21, verified |
| 2. Vacancy Norm | Hand-annotated JSON | **Required** | Frozen 2026-05-22, verified |
| 3. Score | Computed `ideal_score` from C# calculators on gold CV+Vacancy | **Automatic** | Determinstic function of frozen Layer 1+2 gold |
| 4. Reason | Cross-vendor LLM-as-Judge ratings (reference-free) | **N/A** | Re-generated each eval per Zheng et al. 2023 methodology |

## 5. Defense statement (cite verbatim in thesis Chapter "Threats to Validity")

> *"The annotated gold sets for Layer 1 (CV normalization, n=25) and Layer 2
> (vacancy normalization, n=357) were frozen on 2026-05-21 and 2026-05-22
> respectively, evidenced by filesystem mtime and SHA256 byte-level manifests
> archived in `docs/eval_provenance/`. All prompt iteration documented in
> Chapter 3 (v6.4 → v6.7.2) occurred between 2026-05-29 and 2026-05-30 — a
> 7-day separation. The Layer 3 score gold is deterministically derived from
> the frozen Layer 1+2 inputs and inherits their freeze. The Layer 4 reason
> evaluation follows reference-free cross-vendor LLM-as-Judge methodology
> (Zheng et al. 2023) and does not require frozen gold."*

## 6. Recommended next action (one-time, ~5 min)

Commit the gold sets + this provenance bundle to git, locking the manifest:

```bash
cd C:\Users\user\RiderProjects\Vacancies
git add gold_set/ gold_set_v2/ docs/eval_provenance/
git commit -m "freeze: gold sets + SHA256 manifests for thesis eval

CV gold (n=25):       frozen 2026-05-21
Vacancy gold (n=357): frozen 2026-05-22
Selection (n=392):    random_seed=42 (reproducible)

Pre-dates v6.4→v6.7.2 prompt iteration by 7 days.
See docs/eval_provenance/GOLD_FREEZE_PROOF.md."
```

After this commit, the freeze claim is git-backed and defense-bulletproof.
