#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *

#let entry-list = (
  (
    key: "llm",
    short: "LLM",
    long: "Large Language Model",
    description: "A neural network trained on large text corpora to predict the next token; in this thesis used for vacancy normalisation, candidate-vitae normalisation, the Composite Judge stage of the scoring pipeline, the bilingual reason text, and the Layer 4 and Layer 5 judging in the evaluation chapter.",
    group: "Machine Learning"
  ),
  (
    key: "ndcg",
    short: "NDCG@10",
    long: "Normalised Discounted Cumulative Gain at rank 10",
    description: "A ranking-quality metric that compares a system's top-ten ordering against an ideal ordering produced by sorting the same items by ground-truth relevance, with positions discounted by a logarithm. Reported as the primary endpoint of the ranking-quality layer in the evaluation chapter.",
    group: "Evaluation"
  ),
  (
    key: "helm",
    short: "HELM",
    long: "Holistic Evaluation of Language Models",
    description: "A multi-axis decomposed evaluation framework for language-model systems that grades distinct artefacts on methodology appropriate to each artefact's data type; the framework the thesis adopts for its five-layer evaluation decomposition.",
    group: "Evaluation"
  ),
  (
    key: "judge",
    short: "LLM-as-Judge",
    long: "Large-Language-Model-as-Judge",
    description: "A reference-free evaluation methodology that uses a language model to score another model's output on a rubric, established by Zheng et al. (2023). The thesis uses an Anthropic Claude judge on the output of the Google Gemini production pipeline to avoid same-vendor bias.",
    group: "Evaluation"
  ),
  (
    key: "cqrs",
    short: "CQRS",
    long: "Command-Query Responsibility Segregation",
    description: "A pattern that separates request handlers into command handlers (which change state) and query handlers (which only read state). Vakansio uses MediatR to implement the pattern; every use case is a separate handler class with one Handle method.",
    group: "Architecture"
  ),
  (
    key: "solid",
    short: "SOLID",
    long: "Single Responsibility, Open–Closed, Liskov Substitution, Interface Segregation, Dependency Inversion",
    description: "Five class-design principles articulated by Martin (2017). The thesis maps each principle onto a concrete code artefact in the Vakansio source tree and treats the mapping as a non-negotiable evaluation criterion.",
    group: "Architecture"
  ),
  (
    key: "mae",
    short: "MAE",
    long: "Mean Absolute Error",
    description: "The mean of the absolute differences between predicted and reference values. Used in the Layer 3 score-tracking diagnostic of the evaluation chapter to compare pipeline scores against a deterministic ideal.",
    group: "Evaluation"
  ),
  (
    key: "cefr",
    short: "CEFR",
    long: "Common European Framework of Reference for Languages",
    description: "A six-level language-proficiency ladder (A1, A2, B1, B2, C1, C2) used to score the language-match axis in the Vakansio sub-score calculator.",
    group: "Domain"
  ),
  (
    key: "jwt",
    short: "JWT",
    long: "JSON Web Token",
    description: "A signed, URL-safe token format used for stateless authentication in the Vakansio API; the token is issued at login and presented on each subsequent request through the Authorization header.",
    group: "Security"
  ),
  (
    key: "aws",
    short: "AWS",
    long: "Amazon Web Services",
    description: "The cloud platform on which Vakansio is deployed: an EC2 t3.micro for the API, a managed RDS PostgreSQL for the database, an S3 bucket for curriculum-vitae files, a second S3 bucket and CloudFront for the front-end, and the Systems Manager Parameter Store for secrets.",
    group: "Infrastructure"
  ),
  (
    key: "ec2",
    short: "EC2",
    long: "Elastic Compute Cloud",
    description: "Amazon's virtual-machine service. Vakansio runs the API on a single t3.micro instance in the eu-central-1 region.",
    group: "Infrastructure"
  ),
  (
    key: "rds",
    short: "RDS",
    long: "Relational Database Service",
    description: "Amazon's managed relational-database offering. Vakansio uses RDS for PostgreSQL with TLS-required connections.",
    group: "Infrastructure"
  ),
  (
    key: "ssm",
    short: "SSM",
    long: "Systems Manager Parameter Store",
    description: "Amazon's managed key-value store used by Vakansio for production secrets (Gemini API key, JSON Web Token signing key, database connection string).",
    group: "Infrastructure"
  ),
  (
    key: "composite-judge",
    short: "Composite Judge",
    long: "Composite Large-Language-Model Judge",
    description: "The Gemini 2.5 Flash call in the Vakansio scoring pipeline that refines the deterministic linear score against a per-family calibration rubric. Distinct from the Composite Judge calibration buckets used to label verdicts.",
    group: "Pipeline"
  ),
  (
    key: "tier-disclosure",
    short: "tier-based disclosure",
    long: "Tier-based progressive evidence disclosure",
    description: "The front-end pattern that classifies evidence chips into three tiers — brand-and-tool names (Tier 1), hyphenated and multi-word skills (Tier 2), and generic concepts (Tier 3) — and renders Tier 3 behind an expansion link to keep the chip list scannable.",
    group: "User Interface"
  ),
  (
    key: "skip-band",
    short: "skip-band",
    long: "Composite Judge skip-band",
    description: "The optimisation in the Vakansio scoring pipeline that bypasses the Composite Judge for pairs whose deterministic linear score falls outside the band 0.30 to 0.85 and that carry no active anti-flag, on the grounds that the deterministic signal is already decisive. Observed to reduce judge invocations by approximately thirty to forty per cent.",
    group: "Pipeline"
  ),
  (
    key: "krippendorff",
    short: "Krippendorff α",
    long: "Krippendorff's alpha",
    description: "An inter-rater reliability coefficient that handles ordinal, interval, and ratio data and corrects for chance agreement. Used in the Layer 4 pilot of the evaluation chapter as a judge self-consistency check.",
    group: "Statistics"
  ),
  (
    key: "cohens-d",
    short: "Cohen's d",
    long: "Cohen's d",
    description: "A standardised effect-size measure equal to the mean of the paired differences divided by the standard deviation of the differences. Used in the Layer 5 paired-bootstrap analysis of the evaluation chapter alongside the headline confidence interval.",
    group: "Statistics"
  ),
  (
    key: "bootstrap",
    short: "paired bootstrap",
    long: "Paired bootstrap confidence interval",
    description: "A non-parametric confidence-interval procedure that resamples the per-pair differences with replacement and takes the empirical quantiles of the resampled means. Used throughout the Layer 4 and Layer 5 statistical inference at ten thousand resamples.",
    group: "Statistics"
  ),
  (
    key: "minicheck",
    short: "MiniCheck",
    long: "MiniCheck factuality model",
    description: "A reference-free claim-level factuality checker proposed by Tang et al. (2024). Integrated into the Vakansio Infrastructure layer as MlApiFactualityService but not evaluated in this thesis; recorded as future work.",
    group: "Evaluation"
  ),
)

#let make_glossary(
  gloss:true,
  title: i18n("gloss-title", lang: option.lang),
) = {[
  #if gloss == true {[
    #pagebreak()
    #set heading(numbering: none)
    = #title <sec:glossary>
    #print-glossary(
      entry-list,
      // show all term even if they are not referenced, default to true
      show-all: false,
      // disable the back ref at the end of the descriptions
      disable-back-references: false,
    )
  ]} else{[
    #set text(size: 0pt)
    #title <sec:glossary>
    #print-glossary(
      entry-list,
      // show all term even if they are not referenced, default to true
      show-all: false,
      // disable the back ref at the end of the descriptions
      disable-back-references: false,
    )
  ]}
]}
