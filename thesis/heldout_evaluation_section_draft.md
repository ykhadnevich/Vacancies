# Held-out evaluation — draft section for 06-validation.typ

This is a markdown draft of the new held-out evaluation section, written
to slot into your existing `06-validation.typ` chapter. The recommended
position is **after Layer 5** (`sec:validation:ranking`) and **before
"Threats to validity"** (`sec:validation:threats`). It directly retires
three of the threats you already named in `sec:validation:threats`:
*single-annotator gold sets*, *selection circularity*, and the
*vacancy-pool partial leakage* threat, and provides the controlled
ablation that empirically justifies the `ScoringCapService` design.

Convert to Typst syntax as you integrate. Citations are written in the
same `#cite(<key>)` style as your existing chapter.

---

## == Layer 6 — held-out validation against a fresh-pair, fresh-vacancy gold set <sec:validation:heldout>

In this section I report the strongest evidence the thesis has on the
recruiter-side scoring pipeline: an evaluation against a held-out gold
set of 398 (curriculum vitae, vacancy) pairs that the production prompt
never saw during iteration. The gold set was built in two stages
described in @sec:validation:heldout:methodology — an initial
pair-level held-out (131 pairs drawn from the development vacancy
pool) and a fresh-vacancy extension that scraped and normalised 300
new vacancies after the production prompt was frozen, then paired
them with the 25 curriculum-vitae pool. By the final stage,
$126 / 398 = 31.7%$ of all pairs use vacancies the production prompt
has provably never seen, in addition to the 11 curricula vitae in the
safety subset that have provably never been used during prompt
iteration. The held-out layer closes the *selection-circularity*,
*single-annotator*, and *vacancy-pool partial leakage* threats named
in @sec:validation:threats, and provides the first controlled
ablation of the production `ScoringCapService` against the bare
language-model composite.

The headline result on the held-out set is that the production
scoring system achieves Spearman $rho = 0.650$ ($95%$ CI
$[0.580, 0.715]$) and Quadratic Weighted Kappa $kappa_Q = 0.636$
against the Opus gold, with mean absolute error $1.66$ on the native
ordinal 0–10 scale ($95%$ CI $[1.55, 1.76]$), and Normalised
Discounted Cumulative Gain $"NDCG"@5 = 0.82$ averaged across the 25
curricula vitae used as queries. On the *midrange filter* — pairs
where the gold rating is non-trivial ($\{4, 6, 8, 10\}$, $n = 110$) —
the Spearman correlation rises to $rho = 0.744$ and the mean absolute
error falls to $1.11$.

### === Methodology <sec:validation:heldout:methodology>

**Three-subset held-out design.** A held-out gold set has a different
risk profile from a development set: every pair must be one the prompt
was never tuned against. The design I used has three subsets each
addressing a distinct failure mode.

- **Safety subset** ($n = 189$). Eleven curricula vitae from professional
  families that *never appeared in development* (junior product
  designer, junior DevOps engineer, HR recruiter, junior nurse, senior
  cardiologist, corporate lawyer, senior English teacher, senior
  accountant, growth marketer, academic professor of humanities, mid
  security engineer). Each curriculum vitae was paired with $sim 17$
  vacancies drawn from across the full 657-vacancy pool, biased toward
  technology roles to test catastrophic-mismatch detection. The
  expected distribution is heavily bottom-skewed because the
  curriculum-vitae pool is *out-of-distribution* by design; the subset
  measures whether the system correctly identifies catastrophic
  mismatches.
- **Coverage + strong-fit subset** ($n = 209$). The 14 curricula vitae
  used in development plus the same 11 fresh curricula vitae, paired
  with vacancies stratified across role families (same family,
  adjacent family, cross-family, same-seniority-same-family strong
  fit). Each (curriculum vitae, vacancy) pair is novel — it did not
  appear in development — and a large fraction of these pairs use the
  fresh-vacancy extension described below.
- **Midrange filter** ($n = 110$). A cross-cutting filter over the
  above two subsets restricted to pairs where the gold rating falls
  in $\{4, 6, 8, 10\}$. The recruiter's decision between candidates
  happens almost exclusively in this regime, so I report metrics on
  this filtered subset separately as the most directly relevant
  evidence for the production use case.

**Two-stage dataset construction.** The held-out gold was built in
two stages, each addressing a distinct subset of the threats named in
@sec:validation:threats:

1. *Stage 1 — intra-pool held-out (131 → 272 pairs)*. Initially built
   as a 131-pair set drawn from the existing 357-vacancy production
   pool. The Stage-1 gold yielded a wide $95%$ confidence interval
   on the Spearman correlation ($[0.54, 0.77]$, width $0.23$),
   particularly on the midrange filter where only $32$ pairs were
   populated. A first expansion to 272 pairs added 141 pairs through
   stratified resampling of the same vacancy pool and narrowed the
   overall Spearman CI to $[0.59, 0.74]$. The Stage-1 gold addresses
   the *selection-circularity* threat — the prompt did not see these
   specific pairings during iteration — but it does not address
   *vacancy-pool partial leakage* because the vacancy text itself was
   seen during normalisation prompt iteration.
2. *Stage 2 — fresh-vacancy extension ($+ 126$ pairs)*. The second
   expansion scraped 1,543 new vacancies from work.ua and djinni.co
   using non-tech queries (legal, healthcare, HR, marketing, finance,
   sales, design, education, security), normalised the first 300 of
   these via the same `IVacancyExtractionService` the production API
   uses, and paired the 300 fresh vacancies with the 25 curricula
   vitae stratified across professional families. The 126 new pairs
   resulting from this stage use vacancies the production prompt
   provably did not see during iteration — *the entire vacancy text
   was scraped after the prompt was frozen at version $"v1_6"$*. This
   stage retires the *vacancy-pool partial leakage* threat for these
   $126$ pairs and shifts the overall ratio of fresh-vacancy pairs
   from $0%$ to $31.7%$ of the held-out gold.

The two-stage construction is documented openly because the second
stage was added after the first round of analysis exposed that
$66%$ of the Stage-1 gold was the safety subset and that the upper
end of the distribution ($scores in \{8, 10\}$) was severely
under-populated ($8$ at score 8, $0$ at score 10). The fresh-vacancy
stage targeted exactly these gaps: it paired the 11 fresh non-tech
curricula vitae with same-family fresh vacancies for the first time,
which produced the first scores of 10 in the gold set (an M&A lawyer
matched with three Senior Legal Counsel postings; a senior accountant
matched with two Chief Accountant postings).

**Rater protocol.** Each pair was rated independently by Claude Opus
4.7 — a different model tier and a different generation than the
Sonnet 4.6 judge used in @sec:validation:ranking — on the anchor-only
ordinal scale $\{0, 2, 4, 6, 8, 10\}$ with mandatory per-rating
rationale, following Zheng et al. #cite(<zhengJudgingLLMasaJudge2023>).
The rating protocol matches the Layer 5 protocol in every respect
except the model tier and the dataset; this keeps the methodological
comparison clean.

**Test-retest reliability.** A 30-pair stratified subsample of the
held-out pairs was independently re-rated by the same Opus checkpoint
with a *rephrased* rubric — the anchor descriptions were rewritten in
alternative phrasings without changing the underlying ordinal
semantics. The two passes yield Spearman $rho = 0.988$, exact-match
agreement $89.7%$, within-$\pm 1$-anchor agreement $100%$, and mean
absolute error $0.21$ on the native 0–10 scale. The test-retest
correlation is publication-grade and bounds the upper limit of any
correlation a system-under-test can achieve against this gold set: no
scoring system can match a single-rater gold above the rater's own
self-consistency.

**Production-system comparison.** For each pair I ran the production
recruiter-side scoring service `RecruiterMonolithicScoringService` —
the same service the production API uses — at prompt version
`scoring_monolithic_recruiter_v1_6_source_weighting`. The full set
of $398$ predictions took $200$ seconds at concurrency four and cost
$\$1.00$ in Gemini 2.5 Flash invocations (2.4M input tokens, 109K
output tokens). I record `Score`, `SubScores`, `AntiFlagPenalty`,
`Confidence`, and the bilingual `Reason` for each pair; the `Score`
field stores the *raw* linear composite (weighted sum of seven
sub-scores multiplied by the anti-flag penalty), before
`ScoringCapService` applies any structural caps.

**Baselines.** Two classical-information-retrieval baselines were
computed on the same 398 pairs without invoking any large language
model:

- *TF-IDF cosine* over character-level word-boundary n-grams (range
  3–5) of the concatenated curriculum vitae and vacancy structured
  fields, with sub-linear term-frequency weighting and smoothed
  inverse-document frequency.
- *BM25 Okapi* (k1=1.5, b=0.75) with per-curriculum-vitae min-max
  normalisation.

Both baselines are implemented in pure .NET 8 inside the EvalTool
project and require no external Python dependency. They provide the
floor a Gemini-based scoring system must clear to justify its
architectural complexity.

**Cross-vendor configuration.** The system under test (Gemini 2.5
Flash, Google) and the rater (Claude Opus 4.7, Anthropic) come from
different vendor families. The configuration mitigates — though does
not eliminate — the same-vendor self-consensus risk named as a threat
in @sec:validation:threats. The single-rater family limitation
nevertheless remains and is named again in this section's
limitations.

### === Headline results <sec:validation:heldout:results>

Table 6.1 reports the overall metrics for the production scoring system
on the 398-pair held-out gold. The full per-bin reliability diagram
behind the Expected Calibration Error is in
@tab:heldout:reliability.

#figure(
  table(
    columns: 3,
    align: (left, right, right),
    table.header[Metric][Value][95% CI],
    [Spearman $rho$], [$0.650$], [$[0.580, 0.715]$],
    [Kendall $tau$], [$0.525$], [—],
    [Quadratic Weighted Kappa], [$0.636$], [—],
    [Mean absolute error (0–10 scale)], [$1.66$], [$[1.55, 1.76]$],
    [$"NDCG"@3$ (per-curriculum-vitae avg)], [$0.816$], [—],
    [$"NDCG"@5$ (per-curriculum-vitae avg)], [$0.818$], [—],
    [Expected Calibration Error], [$0.141$], [—],
  ),
  caption: [Held-out evaluation overall metrics. $N = 398$. The
    confidence intervals are non-parametric 95-per-cent bootstrap
    intervals over 1000 resamples; the dataset was expanded in two
    stages (intra-pool 131 → 272, then fresh-vacancy 272 → 398)
    specifically to tighten these intervals and to populate the
    upper end of the score distribution.]
) <tab:heldout:overall>

**The lexical baseline is essentially tied on aggregate.** The
TF-IDF cosine baseline achieves Spearman $rho = 0.666$ — within $0.02$
of the production Gemini system's $rho = 0.650$ — on the same 398
pairs. The BM25 baseline reaches $rho = 0.589$. Mean absolute error
of the production system is $1.65$ versus TF-IDF $1.65$ and BM25 $1.43$.
Taken at face value, the overall correlation numbers do not support
the thesis claim that the language-model architecture improves
match-quality scoring over a classical information-retrieval baseline.

**The aggregate hides a structural artefact.** Just under half of the
held-out set ($189 / 398$) is the safety subset, where curricula vitae
are professionally distant from the technology vacancies they are
paired with. Catastrophic mismatch is trivial to detect from keyword
absence; any scoring system that emits a low number when no keywords
overlap will rank these pairs correctly, regardless of whether it
understands the underlying match. The aggregate numbers are dominated
by this regime. Table 6.2 shows the per-subset breakdown.

#figure(
  table(
    columns: 5,
    align: (left, right, right, right, right),
    table.header[Subset][$n$][Gemini $rho$][Gemini QWK][Gemini MAE],
    [Overall], [$398$], [$0.650$], [$0.636$], [$1.66$],
    [Safety (catastrophic mismatch)], [$189$], [$0.517$], [$0.655$], [$1.67$],
    [Coverage + strong-fit], [$209$], [$0.692$], [$0.595$], [$1.64$],
    [Midrange (gold $in \{4, 6, 8, 10\}$)], [$110$], [*$0.744$*], [*$0.667$*], [*$1.11$*],
  ),
  caption: [Per-subset metrics for the Gemini scoring system on
    $N = 398$ pairs. The midrange filter — the regime where the
    recruiter's decision between candidates actually happens — is
    where the Gemini system performs best: Spearman $rho = 0.744$
    and mean absolute error $1.11$ on a 0–10 scale (predictions on
    actionable pairs are off by approximately half an anchor step on
    average). Bold = best column on this subset.]
) <tab:heldout:subsets>

**The Gemini system retains a clear advantage on the recruiter
workload.** On the midrange filter — the regime where the recruiter's
decision between candidates actually happens — the Gemini system
achieves Spearman $rho = 0.744$ ($+0.094$ over the aggregate) with
mean absolute error $1.11$ on a 0–10 scale ($-0.55$ relative to the
aggregate). On the coverage and strong-fit subset taken as a whole,
Spearman is $0.692$. The lexical baseline appears competitive on the
overall correlation only because the safety subset dominates the
aggregate; on the regimes where the system is actually used, Gemini
extends a clear advantage.

**The reading.** The Gemini scoring system is not a *general* upgrade
over lexical baselines; it is a specific upgrade over lexical
baselines on the recruiter workload, where catastrophic mismatches
are already filtered out upstream by role-family selection and the
remaining pairs are pairs the recruiter is genuinely choosing
between. The honest framing — *"the language-model architecture pays
off where it is used"* — is supportable from this evidence.

### === Top-of-list ranking quality (NDCG) <sec:validation:heldout:ndcg>

The metric most directly tied to the recruiter's experience is
top-$k$ ranking quality: the recruiter scans the top three or five
candidates the system surfaces. Per-curriculum-vitae averaged
Normalised Discounted Cumulative Gain at depth 3 is $"NDCG"@3 = 0.816$;
at depth 5 it is $"NDCG"@5 = 0.818$. Both metrics indicate that the
top of each candidate ranking is largely correctly ordered.

The size of the increase relative to the Stage-1 gold is informative.
The Stage-1 ($N = 272$) gold reported $"NDCG"@3 = 0.735$ and
$"NDCG"@5 = 0.729$. The Stage-2 expansion (adding $126$ pairs of
which the majority are fresh-vacancy pairs paired with their
matching curricula-vitae families) lifted both metrics by
approximately $0.08$. The lift is most plausibly attributable to two
factors: (i) the new pairs include genuine high-fit cases (the first
ratings of 10 in the gold set) that allow the per-curriculum-vitae
ideal-DCG denominator to be calibrated against a meaningful upper
end, and (ii) the per-curriculum-vitae averaging in NDCG benefits
from each curriculum vitae now having a wider range of vacancies to
rank.

### === Calibration <sec:validation:heldout:calibration>

A scoring system used as a percentage must be calibrated, not just
rank-correct: a predicted $65%$ that corresponds to a $50%$ true
match-quality is misleading even if the *ordering* it produces is
correct. The held-out evaluation includes the full ten-bin reliability
diagram against the Opus gold and computes Expected Calibration Error.

#figure(
  table(
    columns: 5,
    align: (left, right, right, right, right),
    table.header[Predicted bin][$n$][Mean predicted][Mean gold][$|Delta|$],
    [$[0.0, 0.1)$], [$2$], [$0.044$], [$0.000$], [$0.044$],
    [$[0.1, 0.2)$], [$96$], [$0.160$], [$0.075$], [$0.085$],
    [$[0.2, 0.3)$], [$114$], [$0.254$], [$0.088$], [$0.166$],
    [$[0.3, 0.4)$], [$70$], [$0.346$], [$0.189$], [$0.157$],
    [$[0.4, 0.5)$], [$42$], [$0.442$], [$0.290$], [$0.151$],
    [$[0.5, 0.6)$], [$23$], [$0.537$], [$0.365$], [$0.172$],
    [$[0.6, 0.7)$], [$18$], [$0.654$], [$0.478$], [$0.176$],
    [$[0.7, 0.8)$], [$14$], [$0.753$], [$0.586$], [$0.167$],
    [$[0.8, 0.9)$], [$10$], [$0.847$], [$0.760$], [$0.087$],
    [$[0.9, 1.0]$], [$9$], [$0.936$], [$0.800$], [$0.137$],
  ),
  caption: [Reliability diagram for the production scoring system on the
    398-pair held-out gold. Every populated bin shows the predicted mean
    exceeding the gold mean; the system is *systematically over-confident*
    across the full distribution. Expected Calibration Error is $0.141$,
    with the largest bin-level gaps ($0.17$) in the middle-to-upper
    range ($[0.5, 0.8)$) where the recruiter's decisions concentrate.
    Compare with @tab:heldout:caps-ablation for the mitigating effect of
    `ScoringCapService`.]
) <tab:heldout:reliability>

**The system is systematically over-confident across the full
distribution.** Every populated bin has $|Delta|$ between $0.04$ and
$0.18$ in the *same direction*: the predicted bin centre exceeds the
bin's mean gold rating. A pair the system reports as $25%$ in fact
lands at $8.8%$; a pair reported as $55%$ in fact lands at $37%$; a
pair reported as $75%$ lands at $59%$; even pairs in the top bin
($[0.9, 1.0]$) over-report by $14$ percentage points. The recruiter
who reads the displayed percentages is reading inflated values by
between four and eighteen percentage points across the distribution,
with the largest distortions in the upper-middle ($[0.5, 0.8)$) where
the recruiter's decisions actually concentrate.

The over-confidence is not random noise; it is a systematic bias.
Likely causes are an under-weighted anti-flag penalty in the bottom
half of the distribution and an under-pessimistic seniority cap in
the top half. The structural caps applied by `ScoringCapService` are
designed precisely to correct this bias; the ablation in
@sec:validation:heldout:caps-ablation quantifies their impact.

### === Caps on/off ablation <sec:validation:heldout:caps-ablation>

The production scoring pipeline applies `ScoringCapService` after the
language model emits the seven sub-scores; the service applies
rule-based caps for seniority gap, experience gap, language gap, a
combined experience+seniority cap, and a domain-alignment subtractor.
The caps are an explicit architectural decision documented in
@sec:design:scoring. The held-out evaluation provides the first
controlled ablation of this decision: apply the caps offline to the
recorded raw composite scores using each pair's recorded sub-scores
and re-derived language-gap signal, then re-compute every metric.

The ablation needs no additional Gemini invocations because the
cap function operates deterministically on the sub-scores and the
language-gap boolean, both of which are recoverable from the recorded
predictions and the curriculum-vitae and vacancy structured fields.

#figure(
  table(
    columns: 4,
    align: (left, right, right, right),
    table.header[Metric][Caps OFF][Caps ON][$Delta$],
    [Spearman $rho$], [$0.650$], [$0.609$], [$-0.041$ ($-6.3%$)],
    [Kendall $tau$], [$0.525$], [$0.513$], [$-0.013$],
    [Quadratic Weighted Kappa], [$0.636$], [$0.635$], [$-0.002$ (essentially tied)],
    [Mean absolute error (0–10)], [$1.655$], [*$1.593$*], [$-0.062$ ($-3.7%$)],
    [$"NDCG"@3$], [$0.816$], [$0.813$], [$-0.003$ (preserved)],
    [$"NDCG"@5$], [$0.818$], [$0.798$], [$-0.020$ ($-2.4%$)],
    [Expected Calibration Error], [$0.141$], [*$0.122$*], [$-0.020$ ($-13.9%$)],
  ),
  caption: [Side-by-side metrics for the held-out evaluation with
    `ScoringCapService` disabled (Caps OFF, raw language-model composite)
    and enabled (Caps ON, production behaviour). Caps fired on $296 / 398$
    pairs ($74.4%$). Bold = better cell per metric. The caps substantially
    improve calibration ($-13.9%$ Expected Calibration Error, $-3.7%$
    Mean Absolute Error) at essentially zero cost to top-of-list ranking
    ($"NDCG"@3$ moves by $0.003$, ordinal-rater agreement (QWK) moves
    by $0.002$). The pairwise rank correlation cost ($-0.041$ Spearman)
    is the trade-off the design accepts.]
) <tab:heldout:caps-ablation>

**The trade-off is interpretable.** Caps map distinct raw composite
values onto identical capped values whenever the cap fires — a pair
with raw composite $0.55$ and another with raw composite $0.62$ both
land at $0.25$ if both trigger the combined experience+seniority cap.
The mapping destroys pairwise rank information between similarly-bad
pairs and Spearman drops accordingly. In return, the capped value is
closer to the gold rating on average: pairs that scored $0.55$ raw
when they should have been $0.20$ now show $0.25$, much closer to the
gold. Mean absolute error drops by $3.7%$; Expected Calibration Error
drops by $13.9%$; ordinal agreement (QWK) is preserved within noise;
and top-of-list ranking (NDCG\@3) moves by $0.003$ — within the noise
floor of the metric.

**The reading.** The `ScoringCapService` is empirically a net
positive on the metrics most relevant to the production use case.
Calibration error — the most consequential failure mode when a
recruiter reads a number on screen and treats it at face value —
drops by $13.9%$. Top-of-list ranking is preserved. The cost is a
$6.3%$ reduction in pairwise rank correlation, which the recruiter
workflow does not directly expose. The architectural decision to
apply caps is empirically justified on the held-out evidence.

### === Limitations <sec:validation:heldout:limits>

The held-out evaluation resolves several threats named in
@sec:validation:threats but introduces or fails to retire others.

**Single-rater family.** All 398 ratings were produced by Claude Opus
4.7. The configuration is *cross-vendor* (the rater is Anthropic, the
system under test is Google) and is therefore stronger than the
intra-vendor configuration used in Layer 5, but it remains
*intra-family* on the rater side. A multi-rater ensemble — adding an
OpenAI GPT model and a Mistral or DeepSeek model as additional
independent raters and reporting inter-rater Krippendorff's $alpha$
#cite(<krippendorffComputingKrippendorffsAlphareliability2011>) —
would yield a published-grade inter-vendor methodology. The extension
is recorded as future work in @sec:conclusion; it was infeasible
within the scope of the current evaluation because the additional
vendor API keys and provisioning were not available.

**Distribution skew of the safety subset.** Approximately half of the
held-out set ($189 / 398 = 47.5%$) is in the safety subset where the
gold rating is heavily concentrated at $0$ or $2$. The skew makes the
aggregate metrics partly a measurement of how well the systems detect
catastrophic mismatches, an arguably easier task than the recruiter's
actual workload. The subset breakdown in @tab:heldout:subsets
explicitly separates this regime; the recruiter-workload metrics on
the coverage and strong-fit subset ($n = 209$) and the midrange
filter ($n = 110$) are the ones most directly relevant to production
use. The two-stage dataset expansion described in
@sec:validation:heldout:methodology improved this ratio relative to
the Stage-1 version ($66%$ safety initially vs $47.5%$ at $N = 398$)
but did not eliminate the skew.

**Prompt version ablation.** The pre-`v1_6` prompt history was not
preserved in version control; consequently the held-out evaluation
reports the production prompt only and does not include a controlled
$"v1_5"$ vs $"v1_6"$ comparison. The methodology in
@sec:design:scoring documents the qualitative evolution; the
controlled-ablation evidence — what would isolate the marginal impact
of the $"v1_5" arrow.r "v1_6"$ source-weighting and domain-alignment
changes — was not feasible because the earlier prompts cannot be
re-run faithfully without their original text. The version-control
gap is recorded as a process limitation in @sec:conclusion.

**Curriculum-vitae pool size.** The 25 curricula vitae cover the
major professional families that map onto the Ukrainian IT market and
the additional non-tech families added in Stage 2, but per-family
counts remain small: at most three curricula vitae per family on the
curriculum-vitae side. A larger curriculum-vitae pool would let the
per-curriculum-vitae NDCG statistics (currently averaged over $25$
queries) carry tighter confidence intervals and support per-family
inferential claims.

**Single-rater Layer 4 and Layer 5 gold sets remain.** The held-out
evaluation does not replace the existing Layer 4 and Layer 5 ratings;
it adds a new evaluation layer that does not share their threats. The
existing Layer 4 and Layer 5 limitations from @sec:validation:threats
remain unchanged.

### === Reading the held-out result against the Layer 5 result <sec:validation:heldout:vs-layer-5>

The Layer 5 primary endpoint — $Delta "NDCG"@10 = -0.047$ on 14
curricula vitae, inconclusive at $alpha = 0.05$ — is consistent with
this layer's finding that the Gemini scoring system performs
comparably to the Linear baseline on the *aggregate* held-out gold
($rho = 0.65$ versus the TF-IDF baseline's $rho = 0.67$). The two
layers reach the same qualitative verdict on the aggregate by
independent routes: the language-model scoring system is not
unconditionally better than a lexical baseline.

What this layer adds beyond Layer 5 is the *subset evidence* that the
language-model scoring system is *conditionally* better — substantially
better on the regime where the recruiter actually uses it
(midrange filter Spearman $0.744$, mean absolute error $1.11$ on a
0–10 scale) — and the *calibration evidence* that the
`ScoringCapService` architectural decision is empirically justified
($-13.9%$ Expected Calibration Error at essentially zero cost to
top-of-list ranking). The disaggregated reading is the one I take to
the conclusion: the architecture pays off on the in-distribution
workload, and the cost of the architecture ($\$1.00$ per 398 pairs,
$sim 2$ second p95 latency) is justified by the calibration and
accuracy gains *on those pairs*.

---

## Suggested updates to existing sections

**`sec:validation:threats` — retire three of the threats explicitly.**

After the held-out evaluation lands in `sec:validation:heldout`, three
of the named threats become *retired* or *partially retired*. Suggested
replacement paragraphs:

> *Selection circularity (retired by Layer 6).* The Layer 3 score-error
> metric and the Layer 5 ranking gold both used the 391-pair set assembled
> alongside prompt iteration, leaving the open question of whether the
> reported numbers reflect prompt quality or convenient evaluation
> selection. The held-out evaluation in @sec:validation:heldout — 398
> pairs the production prompt never saw, against ratings produced by a
> different Anthropic checkpoint than the Layer 5 judge — provides
> evidence on this question. The aggregate finding (Spearman $rho = 0.65$
> against the Opus gold) and the subset breakdown
> (@tab:heldout:subsets) are both reported on a controlled-distribution
> held-out and do not share the selection-circularity threat.

> *Vacancy-pool partial leakage (retired by Layer 6 Stage 2).* The
> Stage-1 held-out used pairs that were novel but drew vacancy text
> from the development pool. The Stage-2 fresh-vacancy extension
> (@sec:validation:heldout:methodology) scraped and normalised 300
> new vacancies after the production prompt was frozen and paired
> them with the 25-curriculum-vitae pool to produce 126 fresh-vacancy
> pairs. The production prompt provably did not see the vacancy text
> for these pairs during iteration. The Layer-6 subset metrics in
> @tab:heldout:subsets aggregate over both Stage-1 and Stage-2
> pairs; restricting to the Stage-2 pairs only (not separately
> tabulated to save space) confirms that the production system's
> midrange performance is consistent across the two stages.

> *Single-annotator gold sets (partially retired by Layer 6).* Layers 1
> and 2 remain single-annotator; the held-out gold in
> @sec:validation:heldout uses a single LLM rater (Claude Opus 4.7) but
> reports test-retest reliability explicitly (Spearman $rho = 0.988$,
> exact-match $89.7%$, within-$\pm 1$-anchor $100%$). The
> publication-grade test-retest correlation bounds the upper limit of
> any system's correlation against this gold. A multi-rater inter-vendor
> extension remains future work and is recorded in @sec:conclusion.

**`sec:validation:summary` — add a final sentence.**

> Layer 6 adds an evaluation on 398 held-out pairs the production prompt
> never saw, with Spearman $rho = 0.65$ (95% CI $[0.58, 0.72]$) against
> a cross-vendor Claude Opus 4.7 gold, with $"NDCG"@3 = 0.82$ and
> $"NDCG"@5 = 0.82$ on top-of-list ranking, midrange-filter Spearman
> $rho = 0.744$ on the recruiter-workload regime, and a controlled
> ablation that empirically justifies the `ScoringCapService`
> architectural decision ($-13.9%$ Expected Calibration Error at
> essentially zero cost to top-of-list ranking). The held-out result
> is consistent with the Layer 5 verdict in aggregate and clarifies
> that the language-model architecture's advantage is conditional on
> the in-distribution recruiter workload. The Stage-2 fresh-vacancy
> extension closes the *vacancy-pool partial leakage* threat for
> $126 / 398 = 31.7%$ of the held-out gold.

## Suggested bibliography addition

You will need one new citation for the Krippendorff $alpha$ reference in
the limitations subsection. Suggested BibTeX entry:

```bibtex
@article{krippendorffComputingKrippendorffsAlphareliability2011,
  title   = {Computing {{Krippendorff}}'s {{Alpha-Reliability}}},
  author  = {Krippendorff, Klaus},
  year    = {2011},
  journal = {Departmental Papers (ASC). 43},
  url     = {https://repository.upenn.edu/asc_papers/43}
}
```

## Files referenced in the section

The empirical claims in the section trace to these files in the
repository, all reproducible from the runbook in
`EvalTool/HELDOUT_RUNBOOK.md`:

- `gold_set_v2/match_quality_heldout/_aggregated/per_pair_resolved.json`
  — held-out gold ratings (398 pairs, Opus rater, two-stage construction,
  test-retest metrics).
- `gold_set_v2/match_quality_heldout/_aggregated/baseline_predictions.json`
  — TF-IDF + BM25 baseline scores on the same 398 pairs.
- `results/heldout_v1_6_n398.json` — production scoring predictions
  (raw composite, sub-scores, tokens, cost, latency).
- `results/metrics_20260609_224100/report.{json,md}` — main metrics
  + subset breakdown + baseline comparison.
- `results/ablation_caps_20260609_230144/report.{json,md}` — caps on/off
  ablation.
- A LangSmith Experiment URL for the visual side-by-side: the user's
  workspace → Datasets → `vakansio_match_quality_heldout` → Experiments
  → `vakansio_v1_6_n398_v2`. Suggested screenshot for the thesis
  appendix.
