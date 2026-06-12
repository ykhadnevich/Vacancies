# Held-out Evaluation Runbook  *(.NET-only)*

End-to-end execution order for the thesis held-out evaluation pipeline.
All steps run from the repository root. **Everything is pure .NET 8 +
MathNet.Numerics — zero Python dependency.** TF-IDF and BM25 baselines are
ported to C# inside `EvalTool/Baselines/`.

## Prerequisites *(one-time)*

```powershell
# Service Key (lsv2_sk_*), NOT a Personal Access Token (lsv2_pt_*)
# Only needed for the LangSmith upload commands; score-heldout /
# compute-metrics / ablation-caps / baselines don't touch LangSmith.
$env:LangSmith__ApiKey = "lsv2_sk_..."
```

`GeminiApiKey` is already wired through `appsettings.Local.json` / env vars
from your existing production setup — no extra step. `score-heldout` reuses
the same configuration the API uses.

If you prefer to pin the LangSmith config in `appsettings.Local.json`
(gitignored) instead of env vars:

```json
{
  "LangSmith": {
    "ApiKey":   "lsv2_sk_...",
    "Endpoint": "https://api.smith.langchain.com",
    "Project":  "vakansio"
  }
}
```

## 1.  Upload held-out gold to LangSmith *(idempotent)*

```powershell
dotnet build
dotnet run --project EvalTool -- upload-langsmith-dataset
```

- Creates the LangSmith Dataset `vakansio_match_quality_heldout`
- Uploads 131 Examples (CV summary + normalized vacancy + gold rating + rationale)
- Idempotent: existing examples (matched by `metadata.pair_key`) are skipped
- Writes `gold_set_v2/match_quality_heldout/_aggregated/langsmith_example_map.json`

## 2.  Score held-out via production Gemini

```powershell
dotnet run --project EvalTool -- score-heldout `
    --output results/heldout_v1_6.json
# optional: --concurrency 4 --limit 5 (smoke test)
```

- Runs the **production** `RecruiterMonolithicScoringService` (same code path
  the API uses) on all 131 pairs
- Emits per-pair predicted composite + 7 sub-scores + reason + tokens + cost +
  latency to `results/heldout_v1_6.json`
- Cost: ~$1.50 for a full run; wall-clock ~5-10 min at concurrency=4

## 3.  Compute non-LLM baselines *(TF-IDF + BM25)*

```powershell
dotnet run --project EvalTool -- baselines
```

- Writes `gold_set_v2/match_quality_heldout/_aggregated/baseline_predictions.json`
- Required for the side-by-side baseline table in Step 5
- Pure C# implementation in `EvalTool/Baselines/BaselineRunner.cs` —
  TF-IDF char_wb 3..5 n-grams + cosine; BM25 Okapi (k1=1.5, b=0.75)
  with per-CV min-max normalisation. No external packages.

## 4.  Upload predictions to LangSmith as Experiment

```powershell
dotnet run --project EvalTool -- upload-langsmith-experiment `
    --predictions results/heldout_v1_6.json `
    --experiment-name vakansio_v1_6_source_weighting
```

- Creates a LangSmith **Session** with `reference_dataset_id` set
- Posts 131 Runs, each linked to its Example via `reference_example_id` +
  `session_id`
- Posts `abs_error` feedback per run for server-side aggregation in the UI
- Result: open `https://smith.langchain.com → Datasets →
  vakansio_match_quality_heldout → Experiments`. Screenshot for the thesis
  appendix.

## 5.  Compute thesis metrics + markdown report

```powershell
dotnet run --project EvalTool -- compute-metrics `
    --predictions results/heldout_v1_6.json
```

Outputs to `results/metrics_<timestamp>/`:

| File | Contents |
|---|---|
| `report.json` | Full metric payload (machine-readable) |
| `report.md` | Markdown tables ready to paste into thesis |

Metrics computed (with bootstrap 95% CIs where applicable):

- **Spearman ρ + Kendall τ** — rank correlation against gold
- **Quadratic Weighted Kappa (QWK)** — ordinal-rater agreement (standard)
- **MAE** on native 0-10 scale
- **NDCG@3 + NDCG@5** — per-CV averaged recruiter ranking quality
- **Reliability diagram (10 bins) + Expected Calibration Error (ECE)**
- **Subset breakdown** — safety / coverage_strong_fit / midrange-only
- **Baseline comparison** — Gemini vs TF-IDF vs BM25 side-by-side

## 6.  *(Optional)* Regression: compare two prompt versions

To compare `v1_5` vs `v1_6` for the thesis ablation table:

1. Add a sibling `RecruiterMonolithicScoringPromptV1_5.cs` containing the older
   prompt text (keep the existing v1_6 file untouched).
2. Add `RecruiterMonolithicScoringServiceV1_5.cs` (copy of the v1_6 service,
   point its `Build()` call at the v1_5 prompt and set its `Version` accordingly).
3. Register both in `ServiceConfiguration.cs` with keyed DI:
   ```csharp
   services.AddKeyedScoped<IRecruiterScoringService, RecruiterMonolithicScoringService>("v1_6");
   services.AddKeyedScoped<IRecruiterScoringService, RecruiterMonolithicScoringServiceV1_5>("v1_5");
   ```
4. Add `--version v1_5|v1_6` flag to `score-heldout`, resolve the keyed service.
5. Run both → two predictions files → `compute-metrics` on each → paste both
   `report.md` tables in the thesis side-by-side.

## File layout

```
Vacancies/
├── EvalTool/                                  ← Pure .NET 8 — zero Python
│   ├── EvalTool.csproj                        ← + MathNet.Numerics
│   ├── Program.cs                             ← + 5 new commands
│   ├── ServiceConfiguration.cs                ← + new DI registrations
│   ├── Pipeline/HeldoutScorer.cs              ← C# score-heldout
│   ├── Baselines/BaselineRunner.cs            ← TF-IDF + BM25 (Step 1)
│   ├── LangSmith/
│   │   ├── LangSmithDatasetUploader.cs        ← Step 2
│   │   └── LangSmithExperimentUploader.cs     ← Step 4
│   └── Metrics/
│       ├── MetricsCalculator.cs               ← Pure stats helpers
│       ├── HeldoutMetricsRunner.cs            ← Step 5 orchestrator + MD writer
│       └── CapsAblationRunner.cs              ← Caps on/off ablation
├── Infrastructure/
│   └── Observability/
│       ├── LangSmithTracer.cs                 ← (existing) fire-and-forget hot-path tracer
│       └── LangSmithDatasetClient.cs          ← sync management-API client
└── gold_set_v2/match_quality_heldout/
    └── _aggregated/
        ├── per_pair_resolved.json             ← Gold (272 pairs, ρ_retest=0.988)
        ├── baseline_predictions.json          ← Step 3 output
        └── langsmith_example_map.json         ← Step 1 output
```
