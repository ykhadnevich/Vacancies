#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *
#pagebreak()
= #i18n("design-title", lang:option.lang) <sec:design>

== Architectural drivers <sec:design:drivers>

In this chapter I describe the design of the Vakansio system. Every major decision traces back to one or more requirements from @sec:analysis:requirements. The strongest drivers — the requirements that shape several design decisions at once — are listed below as a roadmap to the rest of the chapter.

The first driver is the combination of *multi-source aggregation*, *CV-driven ranking*, and *human-readable explanation*. It calls for pluggable source adapters so that each external job board can be added or replaced in isolation, a single centralised scoring pipeline through which every (CV, vacancy) pair passes, and a structured output that carries the score, the verdict, the matched skills, the missing must-haves, and the bilingual reason text. The responses appear in the technology stack section (@sec:design:stack), the Clean Architecture layering (@sec:design:clean-arch), and the scoring pipeline design (@sec:design:scoring).

The second driver is *production deployment at predictable cost*. The system has to stay reachable on the open Internet without manual intervention. This drives containerisation of the back-end, the use of managed services for the database and object storage, a reverse proxy that handles TLS automatically, and a static-hosting model for the front-end. The deployment topology is documented in @sec:design:topology.

The third driver is *measurable layer separation*. SOLID compliance has to be visible in the code, not only claimed. This requires a strict four-project decomposition (Domain, Application, Infrastructure, API), interfaces declared in the layer that uses them, and a command/query split that keeps each handler single-purpose. The decomposition is covered in @sec:design:clean-arch.

The fourth driver is *per-stage evaluability and cost transparency*. Each pipeline stage must produce a measurement and a cost record. This drives a modular pipeline whose stages can be evaluated independently, a request-scoped accumulator that gathers per-stage cost without coupling the layers, a persistent cost-ledger table for offline querying, and an offline evaluation harness separate from the production code path. The responses appear in the scoring pipeline design (@sec:design:scoring) and the observability section (@sec:design:observability).

The fifth driver is *security and data protection*. Uploaded CV files and user accounts are subject to the Ukrainian Law on the Protection of Personal Data and, where applicable, the General Data Protection Regulation. The architectural responses are stateless authentication based on JSON Web Tokens, secret storage in the AWS Systems Manager Parameter Store rather than the source tree, startup guards that refuse to start the system without an enforced SSL connection string and a CORS allow-list, and a cascade-delete pathway for full account removal. These are described in @sec:design:observability.

The sections that follow turn each driver into a concrete architectural choice.

== Technology stack selection <sec:design:stack>

This section presents the major technology choices behind Vakansio. For each choice I state what I picked, the alternatives I considered, and the reasons I picked it over them, with reference to the requirements stated in @sec:analysis:requirements.

*Back-end runtime: .NET 8.* I implemented the back-end in C\# on the .NET 8 runtime, over Node.js with TypeScript, Go, and Python with FastAPI.

- *Architecture fit:* ASP.NET Core has a mature dependency-injection container, a configuration-binding system, and a middleware pipeline that align well with the Clean Architecture and Options patterns required by @sec:analysis:requirements.
- *Type safety:* C\# offers a strong static type system with nullable reference types, which makes the interface contracts at layer boundaries enforceable at compile time.
- *Practical experience:* I already had working knowledge of the .NET ecosystem, which kept the development pace realistic for a solo project.

*Front-end framework: React and TypeScript on Vite.* I built the front-end as a single-page application in React with TypeScript, bundled by Vite, over Vue 3 and a server-side rendered framework such as Next.js.

- *Ecosystem:* React has the largest npm download share among SPA frameworks and a deep set of companion libraries — Zustand for client-side state, TanStack Query for server data fetching, React Router for routing — each of which Vakansio uses.
- *Type safety:* TypeScript gives the same compile-time contract enforcement on the front-end that C\# gives on the back-end.
- *Build speed:* Vite provides fast hot-module reloading during development and a clean production build with no #raw("webpack.config.js") to maintain.
- *Why not SSR:* The user interface is stateful and client-driven; server-side rendering would add operational cost without a search-engine-optimisation benefit on an authenticated application.

*Database: PostgreSQL via Amazon RDS.* I chose relational storage over a document store, picking PostgreSQL over MySQL and MongoDB.

- *JSON fit:* First-class #raw("jsonb") columns let normalised vacancy and CV representations live alongside relational data without a separate document database.
- *SQL features:* Window functions, full-text search, and advisory locks cover the analytical queries that the cost-ledger and evaluation paths require.
- *Managed offering:* Amazon RDS removes the operational overhead of running and backing up the database, which matters for a solo project.
- *Why not MySQL:* It lacks native #raw("jsonb") support, which the normalisation cache relies on.
- *Why not MongoDB:* The data model does not require schema-less storage, and PostgreSQL's strong-consistency guarantees simplify the cache-invalidation reasoning described in @sec:design:cache.

*Large language model: Gemini 2.5 Flash.* I chose Gemini 2.5 Flash for the CV normalisation, vacancy normalisation, Composite Judge, and reason-generation stages, over OpenAI's GPT-4-class models, Anthropic's Claude, the Groq inference service, and a locally hosted open-weight model.

- *Cost per token:* Materially lower than GPT-4 and Claude at the scale of the pipeline.
- *Structured output:* Native JSON output simplifies parsing in the scoring pipeline.
- *Latency:* Acceptable at the volumes seen by Vakansio.
- *Rubric following:* The Composite Judge benefits from a model that can follow a long calibration rubric reliably and return scores within a strict numerical range.
- *Why not local:* The t3.micro deployment target does not have enough memory or compute to serve a model with comparable instruction-following quality.

*Cloud platform: Amazon Web Services.* I hosted the system on AWS, over Google Cloud Platform, Microsoft Azure, and DigitalOcean.

- *Familiarity:* My prior working experience with the AWS console and command-line tooling.
- *Free tier:* The AWS twelve-month free-tier programme covers one t3.micro Elastic Compute Cloud instance, twenty gigabytes of Relational Database Service storage, and modest Simple Storage Service traffic — together this covers the full production cost for a solo capstone project.
- *Edge network:* CloudFront ensures low front-end latency for users across Ukraine without operating a separate content-delivery network.

*Supporting libraries.* Three further choices are worth noting. I chose *Entity Framework Core* over Dapper for its first-class migration tooling and its tight integration with the .NET DI container. I chose *MediatR* over a hand-rolled dispatcher for the ecosystem of pipeline behaviours (validation, logging, retry) available through its abstraction. I chose *BCrypt.Net-Next* for password hashing because it implements the standard work-factor parameter and has a long track record of independent audits.

== Clean Architecture layering and SOLID mapping <sec:design:clean-arch>

This section addresses the third architectural driver from @sec:design:drivers. The requirements in @sec:analysis:requirements explicitly demand that SOLID compliance be visible in the code and that the layer separation be measurable rather than merely claimed. Clean Architecture #cite(<martinCleanArchitecture2017>) provides the structural answer, and SOLID provides the class-level answer; in this section I map both onto the Vakansio code base.

*The four layers.* I organised the back-end as four .NET projects, named for the role they play.

- *Domain* contains entities, value objects, enumerations, and pure scoring rules that describe the business of candidate–vacancy matching. It depends on no other project and on no third-party library — #raw("Domain/Domain.csproj") declares zero package references.
- *Application* contains use cases (expressed as MediatR handlers), options classes that configure them, and abstractions through which the use cases call out to the rest of the system. It depends only on Domain.
- *Infrastructure* provides the concrete implementations of those abstractions — the Entity Framework Core data context, the Gemini service clients, the AWS S3 adapter, the SSM Parameter Store reader, and the seven job-source adapters — and depends on Application (with Domain pulled in transitively).
- *API* is the thin HTTP entry layer: ASP.NET Core controllers, JWT middleware, the CORS configuration, options binding, and the startup file #raw("Program.cs"). It depends on Application and Infrastructure.

*The Dependency Rule.* The decisive property of Clean Architecture is that source-code dependencies point inward only. I enforce this through the composition of project references: Domain is reference-free, Application references only Domain, Infrastructure references Application (and Domain transitively), and API references Application and Infrastructure. Nothing can make the Domain layer depend on, say, the Gemini SDK or Entity Framework Core. The compiler enforces the rule at build time. The same property makes the Domain layer trivially unit-testable, because there is no infrastructure to mock out.

*Dependency Inversion in practice.* The Application layer expresses what it needs through interfaces declared in its own folder (#raw("Application/Common/Interfaces/")): #raw("ICvFileStorage"), #raw("ICostLogService"), #raw("IScoringService"), #raw("IBatchedJudgeService"), #raw("IBatchedReasonService"), and others. Repository contracts owned by the entities themselves (for example #raw("IJobVacancyRepository")) live in Domain instead. The Infrastructure layer provides the implementations. The composition root in #raw("Program.cs") wires implementations to interfaces through the built-in .NET dependency-injection container. Handler classes in the Application layer never see a concrete class directly — every collaborator is injected through an interface. This makes the dependency direction explicit at every call site and allows entire infrastructure components to be substituted in tests or replaced in production (for example, swapping Gemini for OpenAI would touch only the Infrastructure layer).

*CQRS via MediatR.* Use cases in the Application layer follow the Command–Query Responsibility Segregation pattern, with separate handler classes for read-only operations (under #raw("Application/*/Queries/")) and state-changing operations (under #raw("Application/*/Commands/")). Each handler implements #raw("IRequestHandler<TRequest, TResponse>") and exposes exactly one #raw("Handle") method. MediatR dispatches incoming requests to the right handler and supports cross-cutting concerns through its pipeline-behaviour abstraction. The pattern keeps individual handlers single-purpose: the largest handler, #raw("GetAggregatedJobsV6Handler"), orchestrates the full search-and-score pipeline but contains no Gemini calls, no Entity Framework Core context, and no scraping logic; it composes those concerns through the interfaces injected into its constructor.

*Versioning as cache-invalidation.* A small but architecturally significant idiom is the use of version strings to drive cache invalidation. The Composite Judge prompt body carries a #raw("BodyVersion") constant; the scoring pipeline exposes a composite #raw("VersionWithJudge") string that concatenates the scoring version, the judge body version, and the model identifier; the persisted #raw("ScoringCache") row stores the composite version it was generated against. When any underlying constant changes (because a prompt was edited or a weight was rebalanced), the resulting cache key changes, and every previously cached row becomes unreachable by readers without any explicit invalidation pass. Cache correctness therefore does not depend on a separate operational procedure.

*SOLID mapping.* I map the five SOLID principles onto concrete artefacts in the Vakansio code base as shown in @tab:design:solid.

#figure(
  table(
    columns: 2,
    stroke: none,
    align: (left, left),
    inset: 6pt,
    table.header([*Principle*], [*Concrete artefact in Vakansio*]),
    [Single Responsibility], [#raw("Domain/Scoring/Verdict.cs") and #raw("Domain/Scoring/GenericGapFilter.cs") each contain a single rule and no orchestration; #raw("GetAggregatedJobsV6Handler") delegates all stages to injected services rather than executing them inline.],
    [Open/Closed], [#raw("Application/Common/Configuration/ScoringOptions.cs") together with the #raw("IOptions<T>") pattern makes tuning knobs such as #raw("SyncNormalizeTimeoutSeconds") configurable through #raw("appsettings.json") without recompilation.],
    [Liskov Substitution], [The scoring services depend on the interfaces #raw("IScoringService"), #raw("IBatchedJudgeService"), and #raw("IBatchedReasonService"); alternative implementations against another LLM provider would satisfy the same contracts with no consumer changes.],
    [Interface Segregation], [The three scoring interfaces are kept as separate single-purpose contracts rather than fused into one large interface; a class that implements #raw("IBatchedReasonService") is not forced to also provide judging or scoring methods.],
    [Dependency Inversion], [Application handlers take only abstractions in their constructors; the composition root in #raw("Program.cs") wires concrete implementations to those abstractions at startup, so the Application layer never imports an Infrastructure type directly.],
  ),
  caption: [SOLID principles mapped onto concrete artefacts in the Vakansio code base.],
) <tab:design:solid>

Each row points to a file path that can be opened and read.

== Scoring pipeline design <sec:design:scoring>

In this section I present the design of the scoring pipeline — the part of Vakansio that converts a (CV, vacancy) pair into a numerical score, a verdict label, and a bilingual explanation. The pipeline is the system's core technical contribution and the artefact that the validation in @sec:validation evaluates.

*Pipeline overview.* Once the CV and the vacancies returned for the candidate's query have been normalised, every (CV, vacancy) pair flows through the same sequence of stages: deterministic sub-scoring, an anti-flag penalty, an optional Composite Judge refinement, a safety cap, and finally a batched reason-generation step that produces the bilingual explanation. The shape of the pipeline is fixed; the stages are wired together by the scoring service implemented in #raw("ScoringServiceV2.cs").

*The seven sub-score axes.* The deterministic part of the score is a weighted sum of seven axis-specific sub-scores, each computed by a pure C\# calculator under #raw("Infrastructure/RelevancePipeline/V2/Scoring/SubScoreCalculators/"). The axes and their weights, taken from #raw("ScoringServiceV2.cs") (lines 138–147), are shown in @tab:design:weights. The weights are not the result of an automated search — they are explicit design choices I made. The most recent rebalance (Skill 0.30 → 0.40, Language 0.10 → 0.05, Education 0.05 → 0.02) came from one observation: Language and Education scores are uniformly high across the candidate pool, so they add little to discrimination. The Skill score carries most of the signal that separates a good match from a poor one.

#figure(
  table(
    columns: 3,
    stroke: none,
    align: (left, right, left),
    inset: 6pt,
    table.header([*Axis*], [*Weight*], [*Note*]),
    [Skill match], [0.40], [Primary discriminator (rebalanced from 0.30)],
    [Role-intent match], [0.15], [],
    [Seniority match], [0.15], [],
    [Experience match], [0.15], [],
    [Domain alignment], [0.08], [Modest reduction from 0.10],
    [Language match], [0.05], [Was 0.10 — near-uniform high],
    [Education match], [0.02], [Was 0.05 — near-uniform high],
    table.hline(),
    [*Total*], [*1.00*], [],
  ),
  caption: [Composite weights of the seven scoring axes, as configured in #raw("ScoringServiceV2.cs:138–147").],
) <tab:design:weights>

The deterministic linear score is the weighted sum

$ S_"linear" = sum_(i=1)^7 w_i dot s_i, quad "where" sum_(i=1)^7 w_i = 1.00. $

*Anti-flag penalty.* The deterministic linear score is then multiplied by an anti-flag penalty between zero and one. The penalty is produced by #raw("AntiFlagEvaluator.cs") and triggers on conditions that suggest a candidate is structurally unsuited to the vacancy (for example, the vacancy requires on-site work in a city the candidate is unwilling to relocate to). I made the penalty multiplicative rather than additive so that a single strong anti-flag can pull an otherwise strong score down decisively.

*Role-family routing.* Not every vacancy belongs to the same job family, and the relative weight of "what counts as a strong skill match" differs across families. Before the Composite Judge step, each vacancy is routed to one of a fixed set of role families — Product, Engineering, Data, Design, or a Generic fallback — by #raw("KeywordRoleRouter.cs"). The router scores each candidate family by counting weighted matches of family-specific keywords in the vacancy title (with weight five) and description (with weight one); the family with the highest score above a confidence threshold of 0.30 wins, otherwise the routing falls through to Generic. The selected family in turn selects the Composite Judge calibration examples used in the next step.

*Composite Judge with skip-band.* The Composite Judge is a Gemini 2.5 Flash call that takes the (CV, vacancy, deterministic score, selected sub-scores) tuple and returns a refined score together with a brief justification. The judge prompt, defined in #raw("JudgePromptCore.cs"), includes a calibration rubric that anchors specific score bands to qualitative descriptions (0.85–0.95 EXCELLENT, down to 0.20–0.25 MISMATCH) and a set of family-specific worked examples. The Composite Judge is gated by configuration (#raw("Ml:EnableCompositeJudge")) and includes a skip-band optimisation. Pairs whose deterministic linear score falls outside the range $[0.30, 0.85]$ and have no active anti-flags skip the Judge entirely: the deterministic signal is already extreme, so a Judge call would only confirm it and spend a model invocation. I observed that the skip-band reduces the number of Judge calls per query by approximately thirty to forty per cent.

*Sub-score caps as a safety layer.* After the Composite Judge step, the candidate score passes through #raw("IScoringCapService"), which applies a floor of 0.20, a ceiling of 0.88, and a set of asymmetric caps that limit how high a score can go when a candidate has a clear seniority gap, a clear language gap, or a clear role-family mismatch. The caps act as a belt-and-braces safety layer: even if the language-model judge returns a misleadingly high score for an unsuitable match, the cap pulls it back into a range that reflects the structural mismatch.

*Two-pass ranking.* The full pipeline above is expensive to run on every candidate vacancy because the Composite Judge and the reason generation are model calls. To keep the per-request cost manageable, I run a cheap pre-filter first. The deterministic linear score is computed for all returned vacancies, and only the top forty (configurable through #raw("PreFilterTopN")) are passed on to the Composite Judge stage. Of those, the top thirty (configurable through #raw("ReasonGenerationCap")) are passed to the batched reason-generation step. The two-pass shape is a deliberate cost–quality trade-off: the cheap pre-filter handles the easy decisions where the deterministic score is decisive, and the model calls are reserved for the cases where refinement is most likely to change the ranking.

*Reason generation.* For the top thirty pairs, a batched reason-generation step produces a structured JSON response that contains six text fields per pair — strengths, gaps, and recommendation, each in English and Ukrainian. The work is split into chunks of ten pairs per Gemini call, with up to three chunks dispatched in parallel. Each call uses #raw("temperature=0.1") for predictability, validates the response against a strict schema, retries once if the schema check fails, and finally falls back to a deterministic template if both attempts fail. The bilingual JSON schema lets the front-end render the appropriate language without a second round trip.

*Skill expansion and canonicalisation.* Before any sub-score is computed, the raw skill tokens from both the CV and the vacancy are expanded and canonicalised against a global vocabulary. #raw("SkillCanonicalizer.cs") maps synonyms (for example "React.js", "ReactJS", and "react") onto a single canonical form, and the global-vocabulary path in #raw("SkillVocabularyService") handles rare or compound terms that the static vocabulary does not cover, through a single batched Gemini call per request. I introduced the global-vocabulary path in version 6.3 to replace an earlier per-entity expander that produced many false-negative skill matches due to surface-form variation.

*Evidence pipeline.* The matched and missing skill chips that the front-end shows for each vacancy are not the raw output of the sub-score calculators. They pass through a four-step evidence pipeline — Build, Sanitise (deduplicate, strip CEFR tokens, drop self-contradictions), Prioritise (Tier 1 brand acronyms first, Tier 3 generic concepts last), and Trim (at most twelve matched skills and eight missing must-haves). This pipeline is what makes the Tier-based progressive disclosure on the front-end (@sec:design:frontend) feasible.

== Cache hierarchy <sec:design:cache>

The scoring pipeline is expensive: each cold (CV, vacancy) pair requires several model calls. Caching is therefore not an optimisation but a structural requirement. I organised Vakansio around a four-layer back-end cache hierarchy, supplemented by a client-side cache on the front-end. Each layer answers a different question about cost and freshness.

*L1 — Response cache.* In-process #raw("IMemoryCache") with a five-minute TTL, keyed by user identifier, CV version, and search parameters. Repeated identical searches by the same user are served from memory without rerunning the pipeline.

*L2 — Scoring cache.* PostgreSQL #raw("ScoringCache") table indexed by the composite key (#raw("CvHash"), #raw("VacancyId"), #raw("ScoringVersion")). The CV hash is the SHA-256 of a canonical CV projection, so a non-substantive edit (whitespace or field-order changes) does not change the key. There is no time-based expiry — invalidation is by version-string bump in the composite key (@sec:design:clean-arch).

*L3 — Vacancy normalisation.* The structured vacancy representation produced by the vacancy-normalisation Gemini call is stored as a #raw("jsonb") column (#raw("VacancyAnalysisJson")) on the #raw("JobVacancy") row. This is the most expensive single normalisation step in the pipeline. Once computed, it remains valid until the vacancy is re-extracted.

*L4 — Scrape cache.* In-process #raw("IMemoryCache") with a thirty-minute TTL, keyed by lowercased keywords and location. This is the layer that lets a second identical search reuse the previous scrape rather than pull every source from the network again.

*Front-end cache.* TanStack Query persisted to IndexedDB. The persisted cache has a seventy-two-hour maximum age and a ten-minute stale time; writes are throttled to one per second; a cache-buster version string ensures that incompatible deployments do not serve stale data; and a multi-tab synchronisation channel keeps several open browser tabs consistent with one another.

The four back-end layers use different keying strategies but share a single invalidation philosophy. Where time-based expiry is appropriate (L1 response and L4 scrape) it is short, because the underlying data is volatile. Where time-based expiry is inappropriate (L2 scoring and L3 normalisation) the key itself encodes the version of the procedure that produced the cached value, so a procedure change drops the entire affected slice without any explicit operation. This pattern is what makes the scoring pipeline safe to evolve without coordinated cache flushes.

== Frontend architecture and UX design <sec:design:frontend>

The front-end is a single-page React application written in TypeScript and bundled by Vite. It serves five distinct pages — Login, Register, Profile, Job Feed, and Tracker — and acts as the visible surface of the system. In this section I describe the organisational and user-experience design decisions on the front-end side.

*Component organisation.* I separated three concerns in the source tree.

- #raw("components/ui/") holds design-system primitives — Badge, Button, Card, Input, Modal, Icon — that carry no business-domain knowledge.
- #raw("components/jobs/") holds feature-level components specific to vacancy display — VerdictBadge, EvidenceChips, VacancyDetailDrawer, and the JobCard variants.
- #raw("pages/") holds the route-level components that React Router maps to URLs.

Cross-component contracts that would otherwise drift between files are pinned to single-source-of-truth maps: for example, #raw("verdictMeta.ts") defines the verdict label and colour once, and every place that renders a verdict reads from this map rather than maintaining its own table.

*State management.* Two separate state systems coexist on the front-end, each with a clearly defined role.

- *Zustand* stores cross-cutting UI and session state (the search term, the active filter selection, the authentication token).
- *TanStack Query* stores server data (the result of an API call together with its loading and error state).
- *React component-level state* stores strictly local data — for example, the identifier of the currently open detail drawer — because it has no readers outside its owning component.

The split keeps the React component tree free of imperative state-management code and avoids the common anti-pattern of caching server data in component-level local state.

*Job-feed and detail-drawer pattern.* The core screen of the application is the Job Feed. Vacancies are displayed as cards in a scrollable list; clicking a card opens a side drawer containing the full vacancy description and the structured explanation. I made the drawer a sibling component of the list rather than a separate route deliberately: a route change would invalidate the in-page list state (scroll position, filter selections, expanded chip groups) and force a re-render of the result list. The drawer pattern keeps the underlying list mounted and reuses the already-fetched data for the detail view.

*Tier-based progressive disclosure of evidence.* Each vacancy carries up to twelve matched-skill chips and eight missing-must-have chips. To prevent the user from being overwhelmed by a wall of chips, and to make scanning useful when the chip count is high, I classify the evidence chips by #raw("evidenceTier.ts") into three tiers based on structural shape.

- *Tier 1* — brand names and tool acronyms (Figma, Mixpanel, GA4), shown by default.
- *Tier 2* — hyphenated or multi-word skills, shown with a slight visual hierarchy.
- *Tier 3* — generic concepts ("communication", "user-centric approach"), hidden behind a single "+N similar" expansion link.

The classifier carries exception lists for known false positives — #raw("LOWERCASE_BRAND_TOOLS") rescues lowercase brand names such as "react" or "spring" from being demoted, and #raw("IMPLICIT_PM_METRICS") rescues product-manager metric phrases — so the classifier behaves correctly on shapes that the heuristic alone would misclassify. The tier policy mirrors the back-end evidence-pipeline priorities described in @sec:design:scoring.

*Verdict badges and bilingual UX.* Each vacancy is labelled with a coloured verdict badge — *Strong* (green), *Partial* (blue), *Weak* (amber), or *Mismatch* (red) — read from #raw("verdictMeta.ts"). The reason text is bilingual: the back-end returns both English and Ukrainian strings, and the front-end renders whichever language the user has selected through the #raw("LanguageContext"). The internationalisation layer uses paired locale tables (#raw("en.ts") and #raw("uk.ts")), a #raw("useT") hook for translated strings, and a #raw("usePlural") hook for plural-form handling, so no user-visible string is hard-coded.

*Persistence subsystem.* A dedicated module under #raw("frontend/src/persistence/") owns the lifecycle of the front-end cache. It provides a migration step that upgrades the schema between application versions, a multi-tab synchronisation channel that propagates writes across browser tabs, a quota check that prevents the IndexedDB cache from exceeding browser-imposed limits, and an eviction policy that drops the persisted cache blob when it exceeds a fifteen-megabyte hard cap. The subsystem is what makes the TanStack Query persisted cache (described in @sec:design:cache) safe to leave running across multiple sessions.

== Deployment topology <sec:design:topology>

I designed the deployment topology of Vakansio around three goals: keep the public surface small and standard, lean on managed services wherever the operational gain outweighs the configuration cost, and stay within a single-author project's operational budget. The result is a single-instance back-end, three managed AWS components, and an edge cache for the front-end. The overall shape is shown in @fig:design:topology.

#figure(
  block(
    inset: 6pt,
    stroke: 0.5pt,
    radius: 3pt,
    align(left,
      raw("Browser
  │
  ├──► CloudFront ──► S3 bucket  (front-end bundle, static)
  │
  └──► Caddy (EC2 t3.micro, ports 80 / 443 / 443-UDP)
         │  reverse proxy, auto-TLS, security headers
         ▼
       .NET 8 API container  (Docker, private bridge network)
         │
         ├──► RDS PostgreSQL  (TLS-required connection)
         ├──► S3 bucket  (CV files, server-side AES-256)
         ├──► SSM Parameter Store  (/vacancies/prod/*)
         └──► Gemini 2.5 Flash  (HTTPS, external)")
    )
  ),
  caption: [Vakansio production topology on Amazon Web Services.],
) <fig:design:topology>

*Compute host.* The application back-end runs on a single Amazon Elastic Compute Cloud t3.micro instance in the eu-central-1 region. The instance hosts two Docker containers managed by a single Compose file: Caddy at the public edge and the .NET 8 API container behind it on a private bridge network. There is no separate orchestrator — no Kubernetes, no Elastic Container Service. For a single-author project with one production environment, the operational complexity of an orchestrator outweighs the benefit.

*Reverse proxy and TLS.* Caddy serves as the public entry point on ports 80, 443, and 443 UDP (the last for HTTP/3). It terminates TLS using automatically issued and renewed Let's Encrypt certificates, proxies application traffic to the API container, and applies a layer of security headers (HSTS, X-Content-Type-Options, X-Frame-Options DENY, Referrer-Policy). I set a 600-second back-end timeout in the Caddy configuration to accommodate cold Gemini calls in the scoring pipeline, which can exceed the default proxy budget on the first request after a cache miss.

*Database.* PostgreSQL runs on the Amazon Relational Database Service in the same region as the application. The connection is enforced over TLS — the API refuses to start in production unless the connection string carries #raw("SslMode=Require"), #raw("VerifyCA"), or #raw("VerifyFull"). The database is reachable only through a security-group rule that admits the application instance.

*Object storage.* Uploaded CV files are stored in an Amazon Simple Storage Service bucket with server-side encryption (AES-256), under a per-user key prefix of the form #raw("users/{id}/cv/{timestamp}-{filename}"). The API exposes CV files to the front-end through pre-signed URLs valid for five minutes, rather than streaming the bytes through the API itself.

*Secret management.* Production configuration secrets — the database connection string, the Gemini API key, the JWT signing key, and others — live in the AWS Systems Manager Parameter Store under the prefix #raw("/vacancies/prod/*"). The API loads them at startup through the AWS SDK's #raw("AddSystemsManager") extension method and refreshes them in the background every five minutes, so secret rotation does not require a container restart.

*Front-end delivery.* The compiled front-end bundle is served from a separate Simple Storage Service bucket behind CloudFront. The CloudFront distribution provides geographic edge caching for users across Ukraine, which keeps the time-to-first-byte small even though the application back-end is hosted in Frankfurt.

*Identity and access control.* The Elastic Compute Cloud instance carries an Identity and Access Management instance profile that grants the application read access to its parameter-store namespace and read-write access to the CV bucket. There are no long-lived AWS access keys in the source tree or in the container's environment.

*Single-instance trade-offs.* I deliberately picked one back-end instance despite the obvious limits — a t3.micro is the smallest viable size, and there is no horizontal redundancy. The choice keeps the operational cost within the AWS twelve-month free tier, eliminates a class of distributed-systems concerns (session affinity, distributed cache coherence) that would consume engineering time without producing thesis-relevant evidence, and is consistent with the scope of a capstone project. The migration path to a multi-instance topology would replace the Compose file with an Elastic Container Service task definition and place an Application Load Balancer in front of the instances; the layer separation described in @sec:design:clean-arch makes this migration largely mechanical.

== Observability and security design <sec:design:observability>

The fourth and fifth architectural drivers from @sec:design:drivers — per-stage evaluability with cost transparency, and security with data protection — share a common pattern. Both rely on cross-cutting infrastructure that has to be present at every stage of the pipeline without coupling the layers. In this section I describe my design responses for each.

*Per-stage cost tracking.* Every Gemini call in the pipeline goes through an #raw("AsyncLocal")-backed accumulator declared in #raw("Application/Common/Diagnostics/CostBreakdown.cs"). The accumulator is request-scoped, so parallel scoring tasks within the same incoming HTTP request share a single record while concurrent requests do not interfere with one another. For each stage it tracks the number of calls, the elapsed wall-clock time, the input and output token counts, and the dollar cost computed from the current Gemini 2.5 Flash unit prices. I deliberately hosted the accumulator in the Application layer rather than in Infrastructure: this allows both the orchestrating Application handler and the concrete Infrastructure services to write into the same accumulator without the Application layer reaching downward into Infrastructure, and it preserves the Clean Architecture dependency direction.

*Persistent cost ledger.* At the end of each request the accumulator is flushed to a PostgreSQL table named #raw("gemini_cost_log"), modelled by the #raw("GeminiCostLogEntry") entity. The accumulator emits one entry per stage that was touched during the request, and each entry is persisted as a row with columns for the user identifier, the request identifier, the request kind (for example #raw("v6_search"), #raw("cv_normalize"), or #raw("worker_backfill")), the stage name (#raw("judge_batched"), #raw("reason_batched"), #raw("skill_expansion"), and so on), the number of calls, the wall-clock duration, the input and output token counts, the dollar cost, and the keyword string when applicable. An administrator can query the table directly through #raw("/api/admin/cost"), which returns the data as comma-separated values grouped by an optional dimension. The numbers themselves are reported in @sec:validation.

*Best-effort writes.* A failure in the cost-logging path must never break a user response. The implementation in #raw("Infrastructure/Services/CostLogService.cs") catches every database exception and continues, on the principle that a missing log entry is preferable to a failed search. Structured logging through #raw("ILogger") at every stage means the loss is visible in the application logs and can be diagnosed after the fact.

*Health endpoints.* The API exposes two health endpoints. The lightweight #raw("/health") endpoint returns a fixed liveness response without touching any dependency; it is consumed by Caddy and by the Docker container's healthcheck on each interval. The heavier #raw("/health/ready") endpoint performs a database round-trip through #raw("IDatabaseHealthService") and is used during deployment to confirm that the new container is ready to serve traffic before old containers are stopped.

*Authentication and rate limiting.* The API uses stateless JSON Web Tokens for authentication, configured through the #raw("AddJwtBearer") extension in #raw("Program.cs"). Passwords are hashed with the BCrypt.Net-Next library, which exposes the standard work-factor parameter and is regularly audited. Request rate limiting is implemented as ASP.NET Core middleware inside the API project rather than in Caddy — I kept the Caddy layer deliberately narrow (TLS termination, security headers, request forwarding) so the application has a single source of truth for the policy and can apply per-route limits informed by application context.

*Startup guards.* I added two production-only startup guards that turn configuration mistakes into immediate, loud failures. The first refuses to start the application unless the database connection string carries one of #raw("SslMode=Require"), #raw("SslMode=VerifyCA"), or #raw("SslMode=VerifyFull"). The second refuses to start unless the #raw("Cors:AllowedOrigins") list is non-empty, which prevents a configuration mistake from leaving the application open to cross-origin calls from any origin. Both guards throw at startup and emit a clear log line; a misconfigured deployment cannot drift into a quietly insecure state.

*GDPR cascade delete.* User-account deletion is implemented as a transactional cascade in #raw("Application/User/Commands/DeleteCurrentUser/DeleteCurrentUserHandler.cs"). The handler reads the CV file key from the user record, runs a transactional cascade through the database (the user's tracker applications, the relevance-explanation rows keyed on the user's current CV version identifier, and the user-profile row itself), and finally issues a best-effort S3 deletion of the CV file. The database is treated as the authoritative artefact for the right-to-be-forgotten obligation; an orphan blob in the S3 bucket is recoverable via the bucket's lifecycle policy and does not violate the deletion semantics.

*Soft-determinism on model calls.* Every Gemini call carries a short per-call timeout — eight seconds in the inner scoring-loop reason call, fifteen to twenty-five seconds in the dedicated Composite Judge, batched Judge, and batched reason services — and falls back to a deterministic template if the timeout fires or the response fails schema validation. A misbehaving language-model call therefore cannot block the request loop or produce a non-renderable output.

== Chapter summary <sec:design:summary>

In this chapter I turned the requirements from Chapter 2 into a concrete design. The system is a .NET 8 back-end and a React/TypeScript front-end, hosted on AWS behind Caddy, organised as four Clean Architecture projects whose dependency graph the compiler enforces. The scoring pipeline routes each (CV, vacancy) pair through deterministic sub-scores, an anti-flag penalty, a Gemini-based Composite Judge with a skip-band optimisation, asymmetric safety caps, and a batched bilingual reason-generation step. A four-layer cache hierarchy with version-string invalidation keeps the warm-path cost low and removes the need for coordinated cache flushes when the pipeline evolves. The front-end pairs Zustand and TanStack Query with a Tier-based progressive disclosure of evidence chips and a bilingual user experience. Cross-cutting cost tracking, GDPR cascade deletion, and production startup guards are designed once at the Application layer rather than scattered across the code base, which keeps the layer boundaries intact. Chapter 4 walks through how this design is realised in code.
