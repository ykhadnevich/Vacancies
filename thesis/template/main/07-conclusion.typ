#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *
#pagebreak()
= #i18n("conclusion-title", lang:option.lang) <sec:conclusion>

== Project summary <sec:conclusion:summary>

In this thesis I presented Vakansio, a deployed personalised job-matching service for the Ukrainian IT market, together with the evaluation methodology and empirical results behind it. The system pulls vacancies from seven sources, turns them and the candidate's CV into structured data, and scores every (CV, vacancy) pair. The score comes from a deterministic seven-axis sub-score calculator, refined by a Composite LLM Judge, and the result is presented with bilingual English and Ukrainian explanations. The deployed instance runs on a single Amazon EC2 t3.micro host behind Caddy, with managed PostgreSQL, S3 object storage, and CloudFront delivery. It is reachable at #link("https://dsus1dizgh006.cloudfront.net")[dsus1dizgh006.cloudfront.net]. The implementation follows the Clean Architecture decomposition of Robert C. Martin #cite(<martinCleanArchitecture2017>) with the SOLID principles mapped to concrete code artefacts.

My evaluation breaks the pipeline into five layers — CV normalisation, vacancy normalisation, score, reason text, and ranking — with a method matched to each layer's data type. The decomposition follows the multi-axis principle of the Holistic Evaluation of Language Models framework #cite(<liangHolisticEvaluation2023>). The two normalisation layers are frozen at per-field scores of 0.896 (25 CVs) and 0.810 (355 of 357 vacancies). The score layer is reported as a calibration diagnostic after two audits identified structural artefacts in its harness. The reason-text layer reports a significant improvement of the v6 prompt over the v4 baseline on all four ordinal rubric dimensions (overall $Delta = +0.30$, 95 per cent CI $[+0.19, +0.40]$, $N approx 391$ pairs), judged by Claude Opus 4.7. The ranking layer is the primary endpoint of the central thesis claim, and is reported as directionally non-significant on the available gold set of 14 CVs ($Delta "NDCG"@10 = -0.047$, 95 per cent CI $[-0.168, +0.065]$), judged by Claude Sonnet 4.6. A post-hoc sensitivity check on the proposed calibration clamp shifts the headline to $Delta = +0.018$ but does not change the inconclusive verdict.

== Comparison with the initial objectives <sec:conclusion:objectives>

I revisit the four objectives stated in @sec:intro:objectives below.

*Objective one — a working public service.* The system runs on the open internet, supports user registration and login, accepts a CV upload as a PDF, fetches vacancies from seven external sources, and returns a personalised list with verdicts and bilingual explanations. The deployment is documented in @sec:design:topology and @sec:impl:deployment. *Met.*

*Objective two — a disciplined software architecture.* The code base is organised as four .NET projects whose project-reference graph enforces the Clean Architecture dependency rule at build time. #raw("Domain/Domain.csproj") declares zero package references; Application references only Domain; Infrastructure references Application (and Domain transitively); API references Application and Infrastructure. The SOLID principles are mapped to concrete artefacts in @sec:design:clean-arch and elaborated in @sec:impl:domain, @sec:impl:application, and @sec:impl:infrastructure. *Met.*

*Objective three — measurable match quality.* The five-layer methodology in @sec:validation:architecture is the architectural answer. The empirical answer is mixed, and honestly so. On the reason-text layer the v6 prompt is a clear win over the v4 baseline across all four ordinal dimensions. On the ranking layer the central claim — that the Composite Judge improves ranking quality over the Linear baseline — is neither supported nor refuted at the standard significance level on 14 CVs; both pipelines reach strong full-ordering correlation against the cross-vendor relevance ratings (Spearman $rho approx 0.69$ each). The conditions under which the ranking result would resolve definitively are listed in @sec:validation:threats and @sec:conclusion:future. *Methodology met; empirical answer partial and openly characterised.*

*Objective four — transparency of every LLM call.* Every Gemini call writes a row to the #raw("gemini_cost_log") table through the per-stage cost accumulator in #raw("Application/Common/Diagnostics/CostBreakdown.cs"). The accumulator records the stage, the call count, the input and output token counts, the wall-clock duration, and the dollar cost computed from the current Gemini 2.5 Flash short-context tier prices. An admin endpoint exposes the data for offline querying. *Met.*

== Encountered difficulties <sec:conclusion:difficulties>

In this section I record the four difficulties whose resolution shaped the chapters above. Each is documented in detail in the chapter cited.

*Mid-thesis methodology pivot.* My original primary endpoint was a single end-to-end score-MAE metric. Two audits showed it was structurally biased against any pipeline that deliberately departs from the deterministic calculator output (@sec:validation:score). I pivoted to top-ten NDCG against independent-vendor relevance ratings and kept the score-MAE as a diagnostic. The pivot is reported as a methodological contribution, not as a hidden failure.

*Front-end and back-end contract drift.* A back-end serialisation change in v6.7.8 (the addition of #raw("JsonStringEnumConverter") at #raw("API/Program.cs:66")) broke the front-end source filter and the verdict-label vocabulary at the same time. The silent empty-result failure and the two-file fix are documented in @sec:impl:narratives. The episode is the strongest argument for the single-source-of-truth maps on the front-end (@sec:design:frontend).

*Composite Judge calibration defect identified but not deployed.* The rule-precedence override pattern that allows the Composite Judge to depart from its bounded-adjustment band is documented in @sec:validation:score, and the proposed fix is a single-file change at the boundary of #raw("GeminiCompositeJudgeService"). I did not deploy the fix in time for the submitted evaluation. The post-hoc sensitivity check in @sec:validation:ranking quantifies the defect's contribution and confirms that the methodology's verdict on the ranking layer holds whether the fix ships or not.

*Single-vendor judge at submission time.* The full multi-vendor judge ensemble — Claude plus an OpenAI judge plus a third-vendor judge — was constrained by API access at submission time. I adopted an intra-Anthropic configuration (Claude Opus 4.7 for reason-text, Claude Sonnet 4.6 for ranking) as a deliberate scope choice. The inter-vendor extension is recorded in @sec:validation:threats and below.

== Future perspectives <sec:conclusion:future>

In this section I name the extensions that would most directly tighten the empirical findings above. Each is a discrete piece of work with a clearly defined cost and a known location in the code or in the gold set.

*Production deployment of the calibration clamp.* The hard $abs(s_"judge" - s_"linear") lt.eq 0.10$ clamp at the boundary of #raw("GeminiCompositeJudgeService") is a single-file change. Deploying it requires re-running the ranking pipeline on the existing 14 CVs to produce a freshly clamped result, rather than the post-hoc sensitivity check that the chapter currently reports.

*Multi-vendor judge ensemble.* The single-vendor judge family is the principal limitation. Adding judges from one OpenAI model at its strongest tier and one model from a third vendor (for example Mistral's Large tier) would let me measure inter-judge Cohen's $kappa$ directly across vendor families. It would also show that the reason-quality and ranking-quality results are not artefacts of the Anthropic family.

*Gold-set extension for per-family inference.* The Layer 5 confidence interval of width roughly 0.23 reflects the $n = 14$ sample. Extending the CV gold set to at least 30 per role family (Engineering, Data, Product, Design) would support per-family inference and would tighten the aggregate interval enough to support a directional verdict.

*Independent inter-annotator agreement.* The match-quality gold ratings are produced by a single judge family and audited by me, the interested author. An inter-annotator coefficient on a stratified subset, computed by an annotator with no stake in the outcome, would establish the gold set's external validity and would complement the judge self-consistency check in @sec:validation:reason.

*CI/CD pipeline.* My current deployment is a manual but reproducible script (@sec:impl:deployment). A GitHub Actions workflow that builds the image, transfers it to the host, and runs the Compose roll-forward is straightforward to add and is the most concrete operational gap recorded in @sec:impl:deployment.

*A/B production validation of the v6 prompt.* The reason-quality result on v6 was obtained under the protocol of Zheng et al. and predicts user satisfaction by literature precedent; it does not measure user satisfaction directly. A v4-versus-v6 A/B comparison under real-user click-through and apply rates would close that gap.

*Reference-free factuality evaluation as a Layer 4 complement.* The MiniCheck factuality model of Tang et al. #cite(<tangMiniCheck2024>) is already integrated in the Infrastructure layer (@sec:impl:infrastructure) as #raw("MlApiFactualityService") but is not evaluated in this thesis. Activating it as a third reason-quality method alongside rubric-based judging and programmatic checks would complement the multi-vendor ensemble with a reference-free claim-level verifier.

== Final reflection <sec:conclusion:reflection>

The thesis delivers a deployed system, a defensible layered evaluation methodology, a statistically significant positive empirical result on reason-text quality, and a directionally non-significant empirical result on ranking quality whose conditions for resolution I stated openly. The methodology stands regardless of the empirical verdict; the empirical question on ranking quality remains open, with each of the extensions above proposed as the smallest piece of work that would meaningfully tighten the answer. Beyond the deliverables themselves, my main takeaway is the discipline of treating evaluation as a first-class engineering concern. The methodology pivot and the calibration audits in Chapter 5 were the hardest parts of the work, and they shaped the system more than any single architectural decision.
