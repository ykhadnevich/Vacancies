#import "@preview/hei-synd-thesis:0.1.1": *
#import "/metadata.typ": *
#pagebreak()
= #i18n("appendix-title", lang: option.lang) <sec:appendix>

== End-to-end worked example <sec:appendix:example>

In this appendix I take one CV and one vacancy, and run them through all five layers of the evaluation. The point is to show how the numbers actually work on a real pair. I picked this pair because the Composite pipeline wins on it clearly — exactly the kind of win the Composite Judge is meant to add. The aggregate result across all 14 CVs is still inconclusive (@sec:validation:ranking), but the layered methodology lets us see per-CV wins like this one instead of hiding them inside an average.

*The pair I picked.* The CV is #raw("15_qa_senior_automation") — a senior QA automation engineer with almost nine years of experience, English at B2, and a stack of Python, Selenium, Playwright, Cypress, and JMeter. The vacancy is #raw("e66e80c0") — a senior API and back-end performance/load testing role asking for exactly that Python + JMeter stack. I read the pair myself and put it at the top of the candidate set. The Sonnet judge agrees: it rates this pair 10/10 on #raw("match_quality").

*Layer 1 — what the pipeline understood about the CV.* From the gold normalisation:

- Seniority: senior
- English: B2
- Experience: about 107 months (8 years, 11 months)
- Skills: Python, Selenium, Playwright, Cypress, pytest, JMeter

The production pipeline reproduces these fields with roughly the same accuracy as the Layer 1 average reported in @sec:validation:norm.

*Layer 2 — what the pipeline understood about the vacancy.* From the gold normalisation:

- Required seniority: senior
- Required English: B2
- Must-have skills: Python, API testing, JMeter, Gatling

The production normalisation reproduces this shape with roughly the same accuracy as the Layer 2 average in @sec:validation:norm.

*Layer 3 — the score.* The Linear pipeline returns *0.642* (just the weighted sum of the seven sub-scores). The Composite pipeline returns *0.680* — the Composite Judge nudges the score up a little because the qualitative match is strong. The deterministic ideal from the gold inputs (@sec:validation:score) is in the same area. The small Linear → Composite step on this pair is exactly the kind of refinement the Judge is meant to make.

*Layer 4 — the explanation the pipeline writes.* The v6 prompt produces a bilingual reason text. It opens with *"Strong match: senior automation experience with the requested stack"*. Then three short sections:

- *Strengths:* Python, JMeter, API testing, Selenium (cross-context familiarity).
- *Gaps:* Gatling — but it sits close to JMeter, so it's a minor gap.
- *Recommendation:* apply.

The Ukrainian version keeps brand names (Python, JMeter, Gatling, Selenium) in Latin script, as the #raw("uk_term_preservation") rule asks. The Opus judge rates this text at or near the maximum on every rubric dimension.

*Layer 5 — where the pipeline ranks the pair.* I report two NDCG\@10 numbers. *NDCG\@10* is a ranking-quality metric that gives more weight to relevance at the top of the list than at the bottom (see §2.4). *$Delta$* is the difference between Composite NDCG\@10 and Linear NDCG\@10 — a positive $Delta$ means Composite ranks better on this CV.

*Like-for-like comparison.* Only the 19 candidate pairs where both pipelines produced a clean score. (Composite had 11 scoring failures on the original 30-pair set.) Composite puts the chosen vacancy at *rank 5*; Linear puts it at *rank 8*. Composite NDCG\@10 = *0.564*. Linear NDCG\@10 = *0.442*. Per-CV $Delta = +0.122$.

*Full coverage.* Use every candidate each pipeline produced. Linear ranks the gold-ten vacancy at *rank 11* — outside the top ten — so its NDCG\@10 drops to *0.230*. Composite NDCG\@10 stays at *0.561*. Per-CV $Delta = +0.331$.

The two numbers measure different things: how the two pipelines compare on the same set, and how each pipeline handles its full set. The $+0.331$ figure is the one that feeds the aggregate reported in @sec:validation:ranking.

#figure(
  table(
    columns: 4,
    stroke: none,
    align: (right, right, right, left),
    inset: 5pt,
    table.header(
      [*Composite rank*],
      [*Composite score*],
      [*Gold match_quality*],
      [*Note*],
    ),
    [1], [0.808], [4], [Composite ranks too high],
    [2], [0.780], [8], [],
    [3], [0.730], [8], [],
    [4], [0.730], [8], [],
    [5], [0.680], [10], [The pair we traced above],
    [6], [0.660], [4], [],
    [7], [0.630], [4], [],
    [8], [0.600], [4], [],
    [9], [0.588], [4], [],
    [10], [0.588], [2], [],
  ),
  caption: [Composite top-ten ranking for the CV #raw("15_qa_senior_automation") on the like-for-like subset. Our traced pair is at rank 5. The Linear pipeline puts the same pair at rank 8 on the same subset.],
) <tab:appendix:layer5-pair>

*What this example shows.* On this one pair, the Composite Judge does its job: it pushes a strongly relevant candidate higher than the deterministic calculator alone would. Layer 5 shows a clear positive $Delta$. The aggregate across all 14 CVs is still inconclusive — but an inconclusive aggregate does not mean every CV behaves the same way. The layered methodology shows us the per-CV picture instead of hiding it inside an average