# Reason Quality Rubric — Cross-Vendor LLM-as-Judge

**Версія:** v2 (post-validation rewrite)
**Дата:** 2026-05-24
**Покриття:** 392 (CV, vacancy) пар × 3 версії pipeline (v2 baseline, v3 anti-abstraction, v4 enriched+validator)
**Judge:** Claude (Anthropic) — cross-vendor по відношенню до rated моделі Gemini 2.5 Flash (Google)
**Methodology basis:** Zheng et al. 2023 "Judging LLM-as-a-Judge" (NeurIPS), Hu et al. 2025 "AI Hiring with LLMs" (arxiv 2504.02870)

---

## Changelog v1 → v2

Після external agent validation rubric переписано. Виправлено 3 critical issues:

1. **Calibration split** — стара dimension об'єднувала verdict word check + tone, що unrateable. Тепер `verdict_match` (binary 0/10) — лише verdict word. Tone викинуто (no value-add).
2. **Specificity / Completeness розведено** — стара Completeness 6 anchor описувала "generic vs specific" що дублювало Specificity → double-penalty. Тепер Completeness = **what** is mentioned (coverage), Specificity = **how** it's phrased (granularity).
3. **Joint rating → per-version blind** — стара версія дозволяла judge бачити v2/v3/v4 разом, що створює anchor bias (Zheng 2023 §4.2 warning). Тепер judge бачить **одну** version per call, version label = "version_X", mapping post-hoc.

Додано:
- `uk_term_preservation` — binary spot-check для bilingual integrity (Airflow → "Повітряний потік" caught)
- Restriction до anchor values only (0/2/4/6/8/10), no intermediate scores

---

## Призначення

Оцінити якість bilingual reason text (EN + UK) що супроводжує composite score у scoring pipeline. Кожен reason оцінюється на 6 dimensions (5 на 0-10 ordinal + 1 binary), формуючи per-pair overall score + per-version aggregate.

**Reference-free** — gold reason text НЕ потрібен. Judge оцінює reason проти (CV summary + vacancy analysis + evidence + computed score) — тобто проти input data що породив reason.

---

## 6 Dimensions

### 1. `factual_accuracy` (ordinal 0/2/4/6/8/10)

**Питання:** Чи всі strengths/gaps/context-claims в reason тексті **дійсно правдиві** проти CV + vacancy?

**Scale (тільки ці значення; no intermediates):**

- **10** — **все правда**: всі strengths підтверджені CV; всі gaps підтверджені vacancy must_haves І відсутні в CV; контекст factually verifiable.
- **8** — **дрібне неточно**: 1 з 3-5 згаданих items трохи не точний (e.g. "SQL" коли в CV "SQL Server" — родинне), решта правдиві.
- **6** — **значно неточно**: 30-40% items hallucinated або сильно перекручені.
- **4** — **переважно вигадано**: >50% items не підтверджені source data.
- **2** — **катастрофа**: майже все вигадано.
- **0** — повністю fabricated reason.

**Boundary clarification — 6 vs 8:**
- **8** = 1 questionable item у списку з 5 (наприклад reason каже "Strengths: SQL, Airflow, dbt" коли dbt не в vacancy must_haves, але це родинний з DataForm)
- **6** = 2 questionable items або 1 серйозна fabrication

### 2. `completeness` (ordinal 0/2/4/6/8/10) — **WHAT is mentioned**

**Питання:** Чи reason покриває **ключові факти** з input data — найвищу-impact strengths, найкритичніший gap, salient контекст (overqualified/cross-domain/role family) **якщо такий є**?

**Scale (тільки ці значення):**

- **10** — **повне покриття**: згадано 2 з top-3 matched skills + 1-2 з top-3 missing_must_haves + salient контекст-flag згаданий (overqualification/cross-domain/role-family) ЯКЩО такий присутній. Якщо anti_flags не порожні — згадано принаймні один.
- **8** — **майже повне**: 2 з top-3 strengths + 1 missing + контекст або згаданий якщо salient, або no salient context to mention.
- **6** — **середнє покриття**: 1 strength + 1 gap; salient контекст пропущено (e.g. для PMM з overqualification by 5+ years — reason мовчить).
- **4** — **слабке**: тільки strengths або тільки gaps; контекст ignored.
- **2** — **мізерне**: 1 generic item ("experience") + verdict.
- **0** — порожній reason або тільки verdict word.

**ВАЖЛИВО — completeness ≠ specificity:** Completeness судить ЧИ ЗГАДАНО ключові факти (як список). Specificity (наступна dimension) судить ЯК сформульовано. Reason "Strengths: SQL, Airflow. Gaps: DataForm." має completeness=10 (всі ключові згадані) НАВІТЬ якщо specificity=6 (формулювання generic-ish).

**Boundary — 6 vs 8:**
- **8** = згадано 2 з top-3 strengths + 1 missing + (контекст or no salient context)
- **6** = згадано 1 з top-3 strengths + 1 missing (тобто 1 ключовий strength пропущений)

### 3. `specificity` (ordinal 0/2/4/6/8/10) — **HOW it's phrased**

**Питання:** Чи reason **specific to this pair** vs generic? Чи можна цей reason скопіювати в будь-яку іншу пару без зміни?

**Scale (тільки ці значення):**

- **10** — **pair-unique phrasing**: бренди/products/числа specific до цієї пари ("Snowflake/dbt expertise", "7 years overqualified for 1-yr role", "Treeum FinTech transition"). Не можна скопіювати без зміни.
- **8** — **mostly specific**: brand-like tools згадані ("SQL, Airflow, .NET"), але context generic.
- **6** — **mixed**: tools згадані specific, але абстрактні concepts замість specifics ("experience depth" замість "7 years SQL").
- **4** — **mostly generic**: фрази типу "experience depth, language requirements" що підходять до 30%+ pairs.
- **2** — **fully generic**: "skills overlap, seniority fit" — підходить до 80%+ pairs.
- **0** — без specifics зовсім.

**Boundary — 4 vs 6:**
- **6** = має specific tool names (≥2 brand tokens) АЛЕ generic context
- **4** = немає specific tool names, тільки category names ("data tools" замість "Snowflake/dbt")

### 4. `verdict_match` (binary 0/10)

**Питання:** Чи verdict word у тексті відповідає **computed score** bucket?

**Score → expected verdict:**
- ≥ 0.75 → "Strong match" / "Сильна відповідність"
- 0.50–0.74 → "Partial match" / "Часткова відповідність"
- 0.25–0.49 → "Weak match" / "Слабка відповідність"
- < 0.25 → "Mismatch" / "Невідповідність"

**Score:**
- **10** = verdict word у тексті matches expected bucket (case-insensitive, may follow context-lead phrase)
- **0** = verdict word різний bucket або відсутній

(Binary because this is deterministically rateable — немає grey area.)

### 5. `relevance_for_user` (ordinal 0/2/4/6/8/10)

**Питання:** Чи цей reason **допомагає юзеру (candidate) прийняти рішення** apply/skip? Actionable чи irrelevant?

**Scale (тільки ці значення):**

- **10** — **immediately actionable**: юзер за 5 секунд reading може вирішити apply/skip. Anti-flags явні якщо є.
- **8** — **informative**: strengths + gaps + контекст дають basis для рішення.
- **6** — **partially helpful**: треба самому подивитись CV/vacancy щоб зрозуміти.
- **4** — **confusing**: технічні терміни без контексту, юзер не розуміє salience.
- **2** — **uninformative**: generic phrasing що нічого не комунікує.
- **0** — **actively misleading**: reason суперечить score (e.g. кажa Strong коли реально Weak).

**Boundary — 6 vs 8:**
- **8** = містить хоча б одну actionable insight (anti-flag warning OR overqualification flag OR clear cross-domain note)
- **6** = тільки skills list без actionable framing

### 6. `uk_term_preservation` (binary 0/10) — spot check

**Питання:** Чи technical terms у UK reason залишені у Latin script (як вимагає prompt), чи translated wrongly?

**Score:**
- **10** = всі brand names / tech terms у UK reason у Latin script ("Airflow", ".NET", "SQL")
- **0** = принаймні один technical term wrongly transliterated/translated ("Повітряний потік" замість "Airflow", "точка-нет" замість ".NET")

(Defensive check — catches catastrophic bilingual failure mode.)

---

## Aggregate scoring

**Per dimension, across N=392 pairs (primary report):**
```
median_factual_accuracy   = median(factual_accuracy across pairs)
median_completeness       = median(completeness across pairs)
median_specificity        = median(specificity across pairs)
median_verdict_match      = mean(verdict_match across pairs)        # binary → mean = % match
median_relevance_for_user = median(relevance_for_user across pairs)
median_uk_term_preservation = mean(uk_term_preservation across pairs) # binary
```

Median for ordinal dimensions, mean for binary (mean of 0/10 binary = % passed × 10, intuitive).

**Comparison metric (primary, pre-registered):**
```
delta_per_dim = median(dim_v4) - median(dim_v2)   # per dimension
```

Per-version "headline number" is **median of factual_accuracy** (most direct proxy for "reason quality") — NOT a synthetic combined index. Combined index would mix scales (ordinal + binary) and is hard to defend; reporting per-dimension medians side-by-side is cleaner.

**Statistical defense:**
- **Paired bootstrap CI 95%** (10,000 resamples) on each `delta_per_dim` — for v4 vs v2 difference
- **Pre-registered primary finding:** `delta_factual_accuracy` (semantic correctness is primary success metric)
- **Exploratory:** other dimension deltas (multiple-comparison risk if treated as primary)
- **Median (not mean)** for ordinal dimensions — ratings are bounded ordinal, not interval

**Statistical defense:**
- **Paired bootstrap CI 95%** (10,000 resamples) on delta_overall — for v4 vs v2 difference
- **Pre-registered:** main finding = mean_overall diff. Per-dimension diffs are **exploratory** (multiple comparisons risk if treated as primary).
- **Median не mean** for individual metrics — ratings ordinal, not interval; outliers should not bias.

---

## Rating procedure — для judge (rater)

### Per-version blind rating

**Judge call signature:** rate ОДНУ version per call. Judge не знає чи це v2/v3/v4. Version label у prompt = "version_X" з randomized X per call. Real mapping resolved post-hoc by orchestrator.

**Input для judge (per call):**
1. CV summary JSON (gold normalization)
2. Vacancy analysis JSON (gold normalization)
3. Evidence (matched_skills, missing_must_haves, triggered_anti_flags)
4. Computed score (composite)
5. **ОДИН** reason text (en + uk) labeled "version_X"

**Output:**
```json
{
  "cv_id": "...",
  "vacancy_id": "...",
  "version_label": "version_X",
  "ratings": {
    "factual_accuracy": 8,
    "completeness": 6,
    "specificity": 4,
    "verdict_match": 10,
    "relevance_for_user": 6,
    "uk_term_preservation": 10
  },
  "rationale": "Strengths SQL/Airflow correct (factual=8 because dbt is borderline). Generic experience phrasing (spec=4). Missed overqualification flag (completeness=6). All UK tech terms preserved."
}
```

---

## Pilot phase (before scaling)

**Перед запуском 14 raters на 392 пар:**

1. **Inter-rater reliability (IRR) check** — run 2 raters independently rate the SAME 10 pairs (stratified)
2. **Compute Krippendorff's α** (ordinal data) per dimension
3. **Acceptance threshold:** α ≥ 0.6 for ordinal dimensions, ≥ 0.7 for binary
4. If pass — scale to full 392
5. If fail — refine rubric anchors, repeat pilot

---

## Bias mitigation

1. **Per-version blind** — judge не бачить інші versions під час rating цієї version (eliminates joint anchoring).
2. **Version label obscured** — judge не знає чи це baseline чи нова version.
3. **Order randomization** — у batch agent rates pairs у random order, не grouped by version.
4. **Same prompt** для всіх pairs — no per-pair tuning.
5. **Rationale required** — judge має пояснити кожний score, що дає traceability.
6. **Cross-vendor** — Claude судить Gemini output (different family).
7. **Stronger judge** — Claude 4.5+ vs Gemini 2.5 Flash (asymmetric).
8. **Anchor-only scores** — 0/2/4/6/8/10 only, no intermediates (forces clearer boundaries).
9. **Median over mean** — bounded ordinal ratings have non-normal distribution.

---

## Defense-ready methodology paragraph

> "Reason quality was evaluated through cross-vendor LLM-as-judge methodology following Zheng et al. (2023). Claude (Anthropic) served as judge for Gemini 2.5 Flash (Google) output, providing asymmetric stronger-judge configuration that mitigates same-family bias. All 392 (CV, vacancy) pairs were rated on a 6-dimension rubric (factual accuracy, completeness, specificity, verdict match, user relevance, UK term preservation) following the checklist-evaluation pattern of Hu et al. (2025). Per-version blind rating eliminated joint-anchor bias (Zheng 2023 §4.2). Inter-rater reliability validated via pilot subsample (Krippendorff's α ≥ 0.6 ordinal, ≥ 0.7 binary). The full rubric, all 1,176 ratings (392 pairs × 3 versions), judge prompts, and per-pair rationales are published in `gold_set_v2/reasons/` for reproducibility. Aggregate per-version statistics use median (not mean) given ordinal nature of ratings, with paired bootstrap 95% confidence intervals (10,000 resamples) on the primary v4-v2 difference. Per-dimension differences are reported as exploratory."
