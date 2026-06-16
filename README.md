# Vakansio

> CV ↔ vacancy matching for the Ukrainian job market — built on .NET 8 + React 19 + PostgreSQL + Gemini 2.5 Flash, with a calibrated scoring pipeline whose every prompt change is regression-tested against a 398-pair held-out gold set.

[![CI](https://github.com/ykhadnevich/Vacancies/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/ykhadnevich/Vacancies/actions/workflows/ci.yml)
[![Deploy API](https://github.com/ykhadnevich/Vacancies/actions/workflows/deploy-api.yml/badge.svg?branch=main)](https://github.com/ykhadnevich/Vacancies/actions/workflows/deploy-api.yml)
[![Deploy Frontend](https://github.com/ykhadnevich/Vacancies/actions/workflows/deploy-frontend.yml/badge.svg?branch=main)](https://github.com/ykhadnevich/Vacancies/actions/workflows/deploy-frontend.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react)](https://react.dev/)
[![Production](https://img.shields.io/badge/prod-live-success)](https://dsus1dizgh006.cloudfront.net)

## What it does

Two production flows over the same scoring pipeline:

**Candidate flow.** Upload a CV → search vacancies → get a ranked list with per-pair explanations covering skills, seniority, experience, role intent, domain, language and education.

**Recruiter flow.** Create a vacancy and a candidate list → the system scores every candidate against that vacancy, returning sub-score breakdowns, anti-flag evidence, calibrated match-quality percentages and bilingual reason text.

Every Gemini call is traced to [LangSmith](https://smith.langchain.com) for cost and quality observability. Every prompt change is regression-tested against a 398-pair held-out gold set before it can ship.

## Architecture

```
                ┌──────────────────────────────────────────────┐
                │  Frontend (React 19 + Vite + Tailwind)       │
                │  S3 + CloudFront                             │
                └──────────────────┬───────────────────────────┘
                                   │  HTTPS
                ┌──────────────────▼───────────────────────────┐
                │  API (.NET 8 Clean Architecture)             │
                │  EC2 + Caddy + docker compose                │
                │                                              │
                │  ┌──── Domain ────────────┐                  │
                │  │  Entities, ValueObjects │                 │
                │  │  ScoringResult, SubScores                │
                │  └────────────────────────┘                  │
                │  ┌──── Application ──────┐                   │
                │  │  MediatR Commands     │                   │
                │  │  Pipeline Behaviours  │                   │
                │  │  IScoreCalibrator etc │                   │
                │  └───────────────────────┘                   │
                │  ┌──── Infrastructure ───┐                   │
                │  │  EF Core / Postgres   │                   │
                │  │  Gemini services      │                   │
                │  │  LangSmithTracer      │                   │
                │  │  IsotonicCalibrator   │                   │
                │  └───────────────────────┘                   │
                └─────┬───────────────────┬────────────────────┘
                      │                   │
            ┌─────────▼─────┐    ┌────────▼────────┐
            │   PostgreSQL  │    │  Gemini 2.5     │
            │   RDS         │    │  Flash          │
            └───────────────┘    └─────────────────┘
                      │                   │
                      └─────────┬─────────┘
                                │
                       ┌────────▼─────────┐
                       │  LangSmith       │
                       │  (observability) │
                       └──────────────────┘
```

Backend is **Clean Architecture + SOLID**: Domain ↔ Application ↔ Infrastructure ↔ API. The scoring service depends on the `IScoreCalibrator` abstraction in `Application/Common/Interfaces/`; the production isotonic-regression implementation lives in `Infrastructure/Calibration/` and is wired through DI. Swapping calibration strategies — to Platt scaling, to a future neural calibrator — needs zero domain or application changes.

## Scoring quality (held-out evidence)

Reported against a 398-pair held-out gold set rated by Claude Opus 4.7 (test-retest Spearman ρ = 0.988). The full thesis chapter is in [`thesis/template/main/06-validation.typ`](thesis/template/main/06-validation.typ).

| Metric | Value | 95% CI |
|---|---|---|
| Spearman ρ | 0.65 | [0.58, 0.72] |
| Quadratic Weighted Kappa | 0.64 | — |
| NDCG@5 | 0.82 | — |
| Mean absolute error (0–10) | 1.65 | [1.55, 1.76] |
| Midrange Spearman ρ (n=110, gold ∈ {4,6,8,10}) | **0.74** | — |
| Expected Calibration Error (after calibration) | **0.027** | (5-fold CV) |

The ECE reduction from 0.140 to 0.027 (~80.6% relative) is delivered by a post-hoc isotonic-regression calibrator fitted on the held-out and loaded by `CalibratorLoader` at production startup.

## Repository layout

```
.
├── Domain/                                 # Entities, value objects, scoring sub-scores
├── Application/                            # MediatR, behaviours, interfaces, DTOs
├── Infrastructure/                         # EF Core, Gemini services, LangSmith, Calibration
│   └── RelevancePipeline/V2/Scoring/       # The Mono scoring engine
├── API/                                    # Controllers, JWT, DI bootstrap
│   └── Calibrators/recruiter_latest.json   # Active calibrator artefact
├── EvalTool/                               # Offline evaluation CLI (.NET)
│   ├── Pipeline/HeldoutScorer.cs           # Score 398 held-out pairs
│   ├── Baselines/BaselineRunner.cs         # TF-IDF + BM25
│   ├── Metrics/                            # Spearman, QWK, NDCG, ECE, bootstrap CIs
│   ├── Calibration/                        # Isotonic + Platt fitters
│   ├── Evaluation/VersionEvaluator.cs      # One-shot regression test
│   └── LangSmith/                          # Dataset + Experiment uploaders
├── frontend/                               # React 19 + Vite + Tailwind
├── gold_set_v2/match_quality_heldout/      # 398-pair held-out gold + baseline preds
├── results/                                # Per-version metrics + calibration artefacts
├── thesis/                                 # Typst-based thesis manuscript
└── .github/workflows/                      # CI + deploy pipelines
```

## Development

```bash
# Backend
dotnet restore Vacancies.sln
dotnet build Vacancies.sln
dotnet test Vacancies.sln
dotnet run --project API

# Frontend
cd frontend
npm ci
npm run dev
```

## Evaluation workflow (after any prompt change)

```powershell
# One command — replaces the manual six-step pipeline
dotnet run --project EvalTool -- evaluate-version --version v1_7 --compare-to v1_6

# Outputs (under results/):
#   heldout_v1_7.json              # 398 predicted scores from production scoring service
#   metrics_v1_7/report.md         # Spearman/QWK/NDCG/ECE + subset breakdown
#   ablation_caps_v1_7/report.md   # Caps on/off trade-off
#   calibration_v1_7/report.md     # Calibrator with before/after ECE
#   comparison_v1_7_vs_v1_6.md     # Δ-table + per-pair regressions + verdict
```

The verdict line is one of:

- ✅ **ship** — Spearman improved without significant regression
- ⚠️ **investigate** — more regressed pairs than improved
- ➖ **indistinguishable** — within noise

See [`EvalTool/HELDOUT_RUNBOOK.md`](EvalTool/HELDOUT_RUNBOOK.md) for the full runbook.

## Deployment

CI/CD is wired via GitHub Actions in [`.github/workflows/`](.github/workflows/):

- `ci.yml` — restore, build, test on every PR + push (backend and frontend in parallel)
- `deploy-api.yml` — Docker build + SCP to EC2 + `docker compose up -d` on `main`, with `/health` verification
- `deploy-frontend.yml` — Vite build + S3 sync + CloudFront invalidation on `main`

See [`.github/workflows/README.md`](.github/workflows/README.md) for the required GitHub secrets.

## Production

Live at <https://dsus1dizgh006.cloudfront.net>.

API runs on a single EC2 host behind Caddy (auto-Let's Encrypt). Postgres is on RDS; secrets are read from SSM Parameter Store via the EC2 instance profile (no static AWS credentials in the container).

## Thesis

The methodology, evaluation, and architectural decisions are documented in the bachelor's thesis under [`thesis/`](thesis/). Built with Typst.

## License

MIT — see [LICENSE](LICENSE).
