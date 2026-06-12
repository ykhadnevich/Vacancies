#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *
#pagebreak()
= Implementation <sec:impl>

In this chapter I walk through how the design from @sec:design was turned into a working, production-deployed system. The first section documents the development methodology and the conventions that guided the code base. The next three sections walk layer by layer through the Domain, Application, and Infrastructure projects. A fifth section describes the front-end implementation. A sixth section covers deployment, operations, and operating costs. A final section presents two engineering stories — challenges I hit that show how the architecture handled change.

== Development methodology <sec:impl:methodology>

This section describes the development process behind Vakansio: how I organised the work, what conventions guided the code, what quality measures I applied, and which tools I used.

*Iterative versioned development.* I developed the project as a single author over several months. Rather than fixed-length sprints, I organised the work around explicit versioned releases of the scoring pipeline. Each version corresponded to an observable change in system behaviour: a rebalance of the scoring weights, the introduction of the global-vocabulary path in version 6.3 that replaced the earlier per-entity skill expander, and several contract-drift fixes documented in @sec:impl:narratives. The version string is also the cache-invalidation key from @sec:design:clean-arch, so each iteration is both an engineering increment and a deployable artefact with a defined effect on the persisted caches.

*Prototype-first approach.* I grew the system outward from a minimal core. The first working version contained only the deterministic seven-axis sub-scorer and a thin API. I added the Composite Judge stage next, then the batched reason generator, then the front-end tier-based evidence disclosure, then the per-stage cost-tracking subsystem. After each step I validated against real CV–vacancy pairs on the deployed instance before adding the next layer. This kept each change small, so I could roll back quickly when something broke.

*Coding standards and conventions.* The back-end is written in C\# on .NET 8 with nullable reference types enabled across every project. The code base follows the default C\# naming conventions — #raw("PascalCase") for public members, #raw("camelCase") for local variables and parameters, #raw("IPascalCase") for interfaces. Each CQRS handler is a single class with exactly one public #raw("Handle") method. Clean Architecture project references are enforced at the build level: #raw("Domain/Domain.csproj") declares zero package references, and the Application project references only Domain. On the front-end, TypeScript is built with #raw("noUnusedLocals"), #raw("noUnusedParameters"), and #raw("noFallthroughCasesInSwitch") enabled, so the build fails on dead code and missed switch cases; ESLint enforces the recommended TypeScript and React-Hooks rule sets. Cross-component contracts that would otherwise drift between files are pinned to single-source-of-truth maps such as #raw("verdictMeta.ts") and #raw("evidenceTier.ts").

*Quality assurance approach.* Three quality measures run in parallel.

- *Unit tests* cover the pure Domain rules and the sub-score calculators, where the input–output mapping is deterministic; the suite is detailed in @sec:validation.
- *Author acceptance tests* — for every observable version, I tested against the deployed instance at #link("https://dsus1dizgh006.cloudfront.net")[dsus1dizgh006.cloudfront.net] on a fixed set of CVs and search queries before considering the version shipped.
- *Offline evaluation harness* — separate from the production code path, it computes ranking-quality and reason-quality metrics on an LLM-judged gold set; the methodology and results are reported in @sec:validation.

The continuous cost-tracking subsystem (@sec:design:observability) complements the test suite by surfacing performance regressions as cost changes in the #raw("gemini_cost_log") table.

*Tooling and workflow.* JetBrains Rider was my primary IDE for the back-end. I used PostgreSQL locally during development and through Amazon RDS in production. Docker and Docker Compose containerise the back-end for both local runs and the production deployment described in @sec:impl:deployment. The deployment is a manual but reproducible script; a CI/CD workflow is the next operational extension and is named in @sec:conclusion.

== Domain layer walkthrough <sec:impl:domain>

In this section I walk through the Domain project — the innermost layer of the Clean Architecture decomposition introduced in @sec:design:clean-arch. The project lives under #raw("Domain/") and contains every artefact that describes the business of candidate–vacancy matching without reference to any external technology. #raw("Domain/Domain.csproj") declares zero package references and no project references, so the layer can be reasoned about and unit-tested in isolation from Entity Framework Core, the Gemini SDK, the AWS adapters, and every other infrastructure concern.

*Entities.* I defined seven entities under #raw("Domain/Entities/"). #raw("JobVacancy") models a vacancy as it has been extracted from a source, together with its normalised analysis as a #raw("jsonb")-stored payload (the L3 cache layer in @sec:design:cache). #raw("UserProfile") holds the user record, the pointer to the current CV version in object storage, and the cached normalisation of the CV. #raw("ApplicationTracker") models the per-user record of a job the user has decided to act on. #raw("ScoringCacheEntry") is the row backing the L2 scoring cache, keyed by the composite (CV hash, vacancy identifier, scoring version) triple. #raw("SkillVocabularyEntry") backs the global-vocabulary path introduced in version 6.3. #raw("GeminiCostLogEntry") is the per-call row in the cost ledger from @sec:design:observability. #raw("SavedUrl") records vacancies the user has manually added by URL.

*Value objects and enumerations.* Two value objects live under #raw("Domain/ValueObjects/"). #raw("RelevanceScore") wraps a floating-point score together with the pipeline stage that produced it; the constructor rejects values outside $[0, 100]$, and #raw("ToPercent()") renders the score for display. #raw("Salary") wraps a minimum amount, a maximum amount, a currency, and a raw-text fallback for cases where the source did not expose a structured value. Five enumerations live under #raw("Domain/Enums/"): #raw("ApplicationStatus") for the tracker workflow, #raw("JobSource") with seven members covering the six aggregators (Djinni, WorkUa, LinkedIn, Jooble, RobotaUa, DOU) and a #raw("Manual") fallback for user-submitted URLs, #raw("ScoringStage") for the per-stage cost annotations, #raw("SeniorityLevel"), and #raw("WorkFormat").

*Pure scoring rules.* The folder #raw("Domain/Scoring/") is the substantive core of the Domain layer. It contains the deterministic rules that operate on already-normalised inputs and produce typed outputs. #raw("CvHasher") computes the SHA-256 hash of a canonical projection of the CV-summary JSON — the sorted skill lists, the target roles, the seniority, the English level, and the total experience in months — so a non-substantive edit to the CV does not change the L2 cache key. #raw("GenericGapFilter") drops generic terms ("communication", "teamwork") from the missing-skill list before the result is shown to the user. #raw("LanguageGapDetector") recognises the case where the vacancy's required English level strictly exceeds the candidate's, using a CEFR ranking from A1 to C2. #raw("RoleFamily") and #raw("RoleFamilyDetector") provide the deterministic family routing from @sec:design:scoring. #raw("SubScores") is a #raw("record") that carries the seven per-axis scores; the current weights live in #raw("ScoringServiceV2.cs:138–147") rather than in the record itself, so a rebalance changes only one file. #raw("ScoringResult") aggregates the headline score, the sub-scores, the verdict, and the evidence chips into the single typed output of the scoring pipeline.

*Verdict labels and the bilingual reason path.* The file #raw("Domain/Scoring/Verdict.cs") ties together two cross-cutting concerns: the categorical verdict label and the bilingual output. The enumeration carries four members — #raw("Mismatch"), #raw("WeakMatch"), #raw("PartialMatch"), and #raw("StrongMatch"). The companion #raw("VerdictExtensions") class exposes #raw("FromScore(double)"), which maps a composite score to a verdict at the cuts 0.25, 0.50, and 0.75; these are user-interface display cuts and are deliberately distinct from the Composite Judge calibration anchors. The same class exposes #raw("ToEnglishText()"), #raw("ToUkrainianText()"), and #raw("ToShortName()"); the first two carry the user-visible bilingual labels (for example, "Strong match" and "Сильна відповідність"), while the third carries the short label that the front-end #raw("verdictMeta.ts") map keys on. Keeping all three vocabularies in the Domain layer means that any change to the verdict-label contract is a single-file edit. @listing:verdict shows the core of the file.

#figure(
  raw(
"public enum Verdict { Mismatch, WeakMatch, PartialMatch, StrongMatch }

public static class VerdictExtensions {
    public static Verdict FromScore(double s) => s switch {
        >= 0.75 => Verdict.StrongMatch,
        >= 0.50 => Verdict.PartialMatch,
        >= 0.25 => Verdict.WeakMatch,
        _       => Verdict.Mismatch,
    };

    public static string ToShortName(this Verdict v) => v switch {
        Verdict.StrongMatch  => \"Strong\",
        Verdict.PartialMatch => \"Partial\",
        Verdict.WeakMatch    => \"Weak\",
        Verdict.Mismatch     => \"Mismatch\",
    };
}",
    lang: "cs",
    block: true,
  ),
  caption: [#raw("Domain/Scoring/Verdict.cs") — the verdict enum and the short-name extension that the front-end keys on.],
) <listing:verdict>

*Interfaces declared in the Domain layer.* Two folders carry interface declarations. #raw("Domain/Interfaces/Repositories/") contains seven repository contracts — #raw("IJobVacancyRepository"), #raw("IUserProfileRepository"), #raw("IApplicationRepository"), #raw("ISavedUrlRepository"), #raw("IScoringCacheRepository"), #raw("ISkillVocabularyRepository"), and #raw("IGeminiCostLogRepository"). Repository contracts live in Domain rather than in Application because the aggregate they serve is itself a Domain concept. #raw("Domain/Interfaces/Services/") contains three additional contracts that describe domain operations whose implementation is technology-specific but whose intent is not: #raw("IDeduplicationService"), #raw("IJobSourceService"), and #raw("IRelevancePipeline"). Other contracts that are application-level rather than domain-level — for example #raw("ICvFileStorage"), #raw("ICostLogService"), #raw("IScoringService"), #raw("IBatchedJudgeService"), and #raw("IBatchedReasonService") — live under #raw("Application/Common/Interfaces/") and are covered in @sec:impl:application.

== Application layer walkthrough <sec:impl:application>

In this section I walk through the Application project — the use-case layer of the Clean Architecture decomposition. #raw("Application/Application.csproj") declares exactly one project reference, to Domain, and five package references: FluentValidation 12.0.0, FluentValidation.DependencyInjectionExtensions 12.0.0, MediatR 14.0.0, Microsoft.Extensions.Caching.Memory 8.0.1, and Microsoft.Extensions.DependencyInjection.Abstractions 10.0.0. There is no reference to Entity Framework Core, the Gemini SDK, or any AWS SDK — every infrastructure-bound dependency lives behind an interface declared either in Domain or in this layer.

*Feature-folder CQRS organisation.* The use cases are organised under four feature folders — #raw("Jobs/"), #raw("Tracker/"), #raw("User/"), and #raw("Eval/") — each split into #raw("Commands/") for state-changing operations and #raw("Queries/") for read-only operations. Each use case lives in its own subfolder named after the operation, composed of three files: a #raw("*Command.cs") or #raw("*Query.cs") that declares the request DTO and implements #raw("IRequest<TResponse>"); a #raw("*Handler.cs") that implements #raw("IRequestHandler<TRequest, TResponse>") with one #raw("Handle") method; and, where input validation is required, a #raw("*Validator.cs") that derives from #raw("AbstractValidator<TRequest>"). Representative examples include #raw("Jobs/Commands/AddManualJobUrl/"), #raw("Jobs/Queries/GetAggregatedJobsV6/"), #raw("Tracker/Commands/AddToTracker/"), and #raw("User/Commands/DeleteCurrentUser/"). The folder layout makes the CQRS split visible at a glance — a reader can answer "what does the system do?" by scanning the folder names alone.

*The orchestrator handler.* The largest handler in the project is #raw("Jobs/Queries/GetAggregatedJobsV6/GetAggregatedJobsV6Handler.cs"), at roughly one thousand lines of code. It is the entry point for the search-and-score pipeline whose stages are documented in @sec:design:scoring. Despite its size, the handler contains no direct Gemini call, no Entity Framework Core context, and no scraping logic. Every collaborator — the aggregator service, the scoring service, the Composite Judge, the batched reason generator, the skill-vocabulary service, the cost logger — is injected through its interface. The handler's job is to compose those collaborators in the right order, manage the per-request cost-tracking scope, and assemble the response DTO; the work of calling out to the Gemini API or the database lives in Infrastructure.

*Options pattern for tuning knobs.* I bind pipeline tuning knobs through the #raw("IOptions<T>") pattern. The first POCO is #raw("Application/Common/Configuration/ScoringOptions.cs"), which exposes #raw("SyncNormalizeTimeoutSeconds") (default 300). The value is read from the #raw("\"Scoring\"") section of #raw("appsettings.json") at startup, so retuning the pipeline does not require a rebuild. The same pattern is used for further tuning knobs under the Application layer.

*Common interfaces and DTOs.* #raw("Application/Common/Interfaces/") declares the abstractions through which the use cases reach out to the rest of the system: #raw("ICvFileStorage"), #raw("ICostLogService"), #raw("IScoringService"), #raw("ICompositeJudgeService"), #raw("IBatchedJudgeService"), #raw("IBatchedReasonService"), #raw("ISkillVocabularyService"), #raw("ICvDomainRouter"), #raw("IVacancyDomainRouter"), and #raw("ICurrentUserService"). #raw("Application/DTOs/") holds the carrier objects that cross the layer boundary into the API project, including #raw("JobVacancyV6Dto"), #raw("RankedJobDto"), #raw("ApplicationTrackerDto"), and #raw("UserProfileDto"). DTOs are independent of the Domain entities they shadow, so a change to a stored entity does not automatically reshape the public API surface.

*Cross-cutting MediatR pipeline behaviour.* Input validation runs as a MediatR pipeline behaviour rather than as a call inside each handler. #raw("Application/Common/Behaviors/ValidationBehavior.cs") implements #raw("IPipelineBehavior<TRequest, TResponse>") — on every dispatched request, it collects every registered #raw("IValidator<TRequest>") from the DI container, runs them, and throws a #raw("ValidationException") with the accumulated failures if any rule fails. Only if every validator passes does control reach the actual handler. The wiring lives in #raw("Application/DependencyInjection.cs"), whose #raw("AddApplication") extension method registers all MediatR handlers from the current assembly, registers all #raw("AbstractValidator") subclasses, and registers the pipeline behaviour itself as a transient open-generic service.

*Per-stage cost accumulator.* I implemented the per-stage cost-tracking subsystem from @sec:design:observability in #raw("Application/Common/Diagnostics/CostBreakdown.cs") as a static class backed by an #raw("AsyncLocal<Accumulator?>") field, shown in @listing:cost.

#figure(
  raw(
"public static class CostBreakdown {
    private const double InputPricePerMillion  = 0.30;
    private const double OutputPricePerMillion = 2.50;

    private static readonly AsyncLocal<Accumulator?> _current = new();

    public static IDisposable BeginScope() {
        _current.Value = new Accumulator();
        return new Scope(_current);
    }

    public static void Track(string stage, long ms, int inT, int outT) {
        _current.Value?.Add(stage, ms, inT, outT,
            (inT * InputPricePerMillion + outT * OutputPricePerMillion) / 1_000_000);
    }

    public static IReadOnlyList<StageStats> GetSnapshot()
        => _current.Value?.Snapshot() ?? Array.Empty<StageStats>();
}",
    lang: "cs",
    block: true,
  ),
  caption: [#raw("Application/Common/Diagnostics/CostBreakdown.cs") — request-scoped, parallel-safe per-stage cost accumulator. Pricing constants are private to this file, so a Gemini-tier price change is a one-line edit.],
) <listing:cost>

The orchestrator handler opens a scope at the start of the request through #raw("CostBreakdown.BeginScope()"); each Gemini-calling service in Infrastructure reports its stage statistics through #raw("CostBreakdown.Track(...)"); and at the end of the request the handler reads the snapshot through #raw("GetSnapshot()") and persists the rows. #raw("AsyncLocal") storage makes the accumulator both request-scoped (two concurrent HTTP requests do not interfere) and parallel-safe within a single request (the up-to-three parallel reason-generation chunks all write into the same accumulator). I put the accumulator in Application on purpose: both the handler and the Infrastructure services need to read and write it without breaking the Clean Architecture dependency direction.

== Infrastructure layer walkthrough <sec:impl:infrastructure>

In this section I walk through the Infrastructure project — the layer that translates the abstractions declared in Domain and Application into concrete calls against the database, the Gemini API, the AWS services, and the seven external job sources. #raw("Infrastructure/Infrastructure.csproj") declares one project reference, to Application, and a focused set of package references: Entity Framework Core 8.0.10 with the Npgsql and Pgvector providers, AWSSDK.S3 4.0.15, BCrypt.Net-Next 4.0.3, HtmlAgilityPack 1.12.0, System.ServiceModel.Syndication 8.0.0 (RSS), PdfPig 0.1.14 (PDF), and the Microsoft.Extensions.Hosting and Http packages for the background workers and typed HTTP clients.

*Persistence with Entity Framework Core.* #raw("Infrastructure/Persistence/") holds the database integration. #raw("AppDbContext") is the single #raw("DbContext"), with one #raw("DbSet") per persisted entity. The companion #raw("AppDbContextFactory") provides the design-time factory used by the EF Core CLI during migrations. #raw("Persistence/Configurations/") contains six #raw("IEntityTypeConfiguration<T>") classes, one per Domain entity that needs explicit column shaping. #raw("Persistence/Migrations/") contains ten sequential migrations from #raw("20260314_InitialCreate") through #raw("20260528_AddSkillExpansionColumns"). #raw("Persistence/Repositories/") contains seven repository implementations, one per Domain repository contract. #raw("Persistence/Entities/RelevanceExplanation.cs") is the only entity declared in Infrastructure rather than Domain; it carries no invariants and exists purely as a denormalised projection of LLM-generated explanations. The cascade-delete behaviour from @sec:design:observability is implemented through #raw("OnDelete(DeleteBehavior.Cascade)") annotations in #raw("UserProfileConfiguration") and reinforced by #raw("DeleteCurrentUserHandler") in Application.

*Job-source adapters and aggregation.* #raw("Infrastructure/JobSources/") contains the seven adapters that pull vacancies from the external sources, split by access pattern. Two are REST API clients (#raw("JoobleApiService"), #raw("RobotaUaApiService")) under #raw("JobSources/Api/"). One is an RSS feed parser (#raw("DouRssFeedService")) under #raw("JobSources/Rss/"). Four are HTML scrapers (#raw("DjinniScraperService"), #raw("LinkedInGuestService"), #raw("WorkUaScraperService"), #raw("ManualUrlScraperService")) under #raw("JobSources/Scraping/"). Every adapter implements the same #raw("IJobSourceService") contract declared in Domain. #raw("JobAggregation/JobAggregationService") orchestrates the request — it resolves every registered #raw("IJobSourceService"), fans the call out in parallel, and catches per-source exceptions so that a single failing source does not break the response (@listing:agg).

#figure(
  raw(
"var tasks = _sources.Select(async src => {
    try {
        return await src.SearchAsync(keywords, location, ct);
    } catch (Exception ex) {
        _logger.LogWarning(ex, \"Source {Source} failed; continuing\", src.SourceName);
        return Enumerable.Empty<ScrapedVacancyDto>();
    }
});
var perSource = await Task.WhenAll(tasks);
var all = perSource.SelectMany(x => x).ToList();

_cache.Set($\"scrape:{keywords.ToLower()}:{location.ToLower()}\", all,
    new MemoryCacheEntryOptions {
        AbsoluteExpirationRelativeToNow = ScrapeCacheTtl, // 30 minutes
        Size = 1,
    });",
    lang: "cs",
    block: true,
  ),
  caption: [#raw("JobAggregationService") — parallel fan-out with per-source isolation and the L4 scrape cache write (excerpt).],
) <listing:agg>

The combined result then passes through #raw("Deduplication/DeduplicationService") and is cached in #raw("IMemoryCache") under the key #raw("scrape:{lowercased keywords}:{lowercased location}"). The thirty-minute TTL is a static readonly field at #raw("JobAggregationService.cs:37").

*Scoring pipeline and normalisation modules.* #raw("Infrastructure/RelevancePipeline/V2/Scoring/") implements the pipeline whose stages I designed in @sec:design:scoring. #raw("ScoringServiceV2") is the implementation of #raw("IScoringService"); the weight table at lines 138–147 is the canonical source of the seven sub-score weights. #raw("SubScoreCalculators/") contains the seven per-axis calculators, all stateless and registered as singletons. The deterministic safety layer is #raw("AntiFlagEvaluator") and #raw("ScoringCapService"); the latter applies the 0.20 floor, 0.88 ceiling, and the asymmetric caps from @sec:design:scoring. The LLM-bound services are #raw("GeminiCompositeJudgeService"), #raw("GeminiBatchedJudgeService"), and #raw("GeminiBatchedReasonService"); #raw("JudgePromptCore") holds the prompt body and the version string that participates in the cache key. The CV-normalisation and vacancy-normalisation folders under #raw("RelevancePipeline/V2/") follow the same five-component shape — keyword router, module resolver, per-domain modules, prompt builder, post-processor. The CV side ships two modules (Tech, Generic); the vacancy side ships six (Tech, HR, Healthcare, Marketing, Sales, Generic). Adding a new domain is a four-step procedure: extend the domain enum, implement the module interface, register the module in #raw("DependencyInjection.cs"), and extend the keyword router.

*Adapters, ML API, workers, and the composition root.* #raw("Infrastructure/Services/") holds three small adapters:

- *#raw("S3CvFileStorage")* — AES-256 server-side encryption, pre-signed URLs with five-minute lifetimes, key prefix #raw("users/{userId:N}/cv/{timestamp}-{filename}"). It is registered conditionally on the presence of #raw("S3:CvBucket"); #raw("NoOpCvFileStorage") is the boot fallback for development.
- *#raw("CostLogService")* — best-effort writes to #raw("gemini_cost_log"). Every database exception is caught and logged.
- *#raw("DatabaseHealthService")* — the database round-trip used by the #raw("/health/ready") endpoint.

#raw("Infrastructure/MlApi/") wraps an optionally deployed Python ML service: #raw("MlApiScoringService") (bi-encoder), #raw("MlApiEmbeddingService") (multilingual-e5-base), #raw("MlApiReasoningService") (Qwen2.5-3B-Instruct), and #raw("MlApiFactualityService") (MiniCheck, used by the reason-quality evaluation in @sec:validation). #raw("Infrastructure/Workers/") contains four #raw("BackgroundService") implementations: #raw("JobEmbeddingWorker"), #raw("CvSummaryWorker"), #raw("VacancyAnalysisWorker"), and #raw("ReasoningWorker"). All four are flag-gated and default off in production. #raw("ReasoningWorker") is intentionally not registered at all because the inline batched reason generator superseded its role. The hot path replaces the disabled background normalisation with the synchronous fan-out documented above, bounded by #raw("SyncNormalizeTimeoutSeconds"). The composition root #raw("Infrastructure/DependencyInjection.cs") wires every contract above to its implementation through an explicit registration block — no MEF-style assembly scan — and configures #raw("AddMemoryCache(opts => opts.SizeLimit = 1000)") as the shared in-memory cache backing both the thirty-minute scrape cache and the five-minute response cache from @sec:design:cache.

== Frontend implementation details <sec:impl:frontend>

In this section I turn the front-end design from @sec:design:frontend into a concrete file map and document the implementation choices that the design section did not cover. The front-end stack is React 19.2.5 with TypeScript 6.0.2 and the Vite 8.0.10 bundler. Routing across the five pages — Login, Register, Profile, Job Feed, and Tracker — is handled by react-router-dom 7.14.2. The production build runs as #raw("tsc -b && vite build"), so a TypeScript compilation error fails the build before Vite ever produces a bundle. Linting runs as #raw("eslint .") under the configuration described in @sec:impl:methodology.

*Source-tree organisation.* #raw("src/components/ui/") holds six design-system primitives, and #raw("src/components/jobs/") holds twelve feature-level components, including the single-source-of-truth maps #raw("verdictMeta.ts") and #raw("evidenceTier.ts") introduced in @sec:design:frontend. #raw("src/pages/") contains one subfolder per route. #raw("src/api/") contains six files: the shared axios client and five per-feature wrappers (#raw("authApi.ts"), #raw("jobsApi.ts"), #raw("profileApi.ts"), #raw("trackerApi.ts"), #raw("userApi.ts")). #raw("src/store/") holds the two Zustand stores. #raw("src/i18n/") holds the internationalisation layer. #raw("src/persistence/") holds the five-file persisted-cache subsystem. The remaining folders are #raw("src/hooks/"), #raw("src/styles/"), #raw("src/types/"), and #raw("src/assets/").

*API client and JWT handling.* #raw("src/api/client.ts") declares a single #raw("axios.create()") instance shared by every per-feature wrapper, with request and response interceptors handling JWT injection and the 401 redirect (@listing:axios).

#figure(
  raw(
"export const client = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL ?? \"http://localhost:5180/api\",
    timeout: 300_000, // 5 min: cold v6 search can take up to 2 min
});

client.interceptors.request.use(cfg => {
    const token = localStorage.getItem(\"token\");
    if (token) cfg.headers.Authorization = `Bearer ${token}`;
    return cfg;
});

client.interceptors.response.use(r => r, (err) => {
    if (err.response?.status === 401 && location.pathname !== \"/login\") {
        [\"token\", \"userId\", \"email\"].forEach(k => localStorage.removeItem(k));
        location.replace(\"/login\");
    }
    return Promise.reject(err);
});",
    lang: "typescript",
    block: true,
  ),
  caption: [#raw("src/api/client.ts") — shared axios instance with JWT injection and unauthenticated-redirect handling.],
) <listing:axios>

*Vite dev-mode proxy.* #raw("vite.config.ts") configures #raw("server.proxy") to forward every request under #raw("/api") to #raw("https://api.vakansio.online") with #raw("changeOrigin: true"). The browser only ever issues same-origin requests to #raw("localhost:5173/api/*"); Vite forwards them server-side to the production API. Two practical consequences follow. First, the production #raw("Cors:AllowedOrigins") allow-list (@sec:design:observability) does not need to include the developer's local host, because no cross-origin request ever leaves the browser. Second, the development iteration loop runs against real production data and the real production scoring caches.

*State management implementation.* The split designed in @sec:design:frontend maps onto two Zustand stores and the TanStack Query layer. #raw("src/store/authStore.ts") holds authentication state — the JWT, the user identity, and the helpers that read or write the persisted #raw("localStorage") keys. #raw("src/store/jobStore.ts") holds the cross-cutting search state: the current #raw("searchParams") object (#raw("keywords"), #raw("location"), and the #raw("runRelevancePipeline") flag), together with the cached server payload (#raw("jobs"), #raw("totalCount"), #raw("duplicatesRemoved")) and the loading and error flags. Server data also lives in TanStack Query (#raw("@tanstack/react-query 5.100.3")) under its own keyed cache, and the front-end never copies that cache into the Zustand stores. Strictly local state, such as the identifier of the currently open vacancy drawer (#raw("drawerJob")), remains in component-level #raw("useState") in #raw("JobFeedPage.tsx") because no other component reads it.

*Persistence subsystem implementation.* #raw("src/persistence/") contains five files, one per concern of the design from @sec:design:frontend. #raw("queryPersister.ts") builds an async-storage persister from #raw("@tanstack/query-async-storage-persister") over an IndexedDB store backed by #raw("idb-keyval 6.2.1"); #raw("src/main.tsx") then wraps the application in #raw("PersistQueryClientProvider") and passes the persister together with the cache-buster string (#raw("v6.7.2") at the time of writing). Advancing the buster string causes the persist-client library to drop the persisted blob on the next mount. #raw("migration.ts") performs a one-shot migration from the legacy #raw("localStorage")-backed cache blob to the new IndexedDB key on startup and exposes a #raw("clearAllQueryCache()") helper used during logout. #raw("multiTabSync.ts") opens a #raw("BroadcastChannel") so that a write in one tab invalidates the corresponding query in every other open tab. #raw("storageQuota.ts") exposes three diagnostic helpers — #raw("isStorageAvailable") (IndexedDB-availability probe), #raw("getStorageEstimate") (wrapper over #raw("navigator.storage.estimate()")), and #raw("isQuotaExceededError") (error classifier) — that the rest of the subsystem uses for diagnostics and graceful degradation; it does not gate writes. #raw("cacheEviction.ts") implements the fifteen-megabyte hard cap by dropping the persisted blob in its entirety (#raw("HARD_LIMIT_BYTES = 15 * 1024 * 1024")) once the threshold is crossed, rather than evicting individual entries.

*Internationalisation implementation.* #raw("src/i18n/") implements the bilingual user experience. #raw("LanguageContext.tsx") provides the React context that propagates the active language down the tree. #raw("useT.ts") exposes a hook that resolves a translation key against the active locale table. #raw("usePlural.ts") handles the plural-form rules for English and Ukrainian, which differ on the cardinality boundaries. #raw("translations.ts") declares the type-safe key catalogue, and the two locale tables (#raw("locales/en.ts") and #raw("locales/uk.ts")) carry the actual strings. No user-visible string is hard-coded in a component file.

== Deployment, operations, and operating costs <sec:impl:deployment>

In this section I document how the topology designed in @sec:design:topology is built, shipped, and run, and what it costs to keep the system in production. The build artefact is a single Docker image produced from the repository root; the production environment is a single Amazon EC2 host that runs two containers through Docker Compose; the deployment is a manual but reproducible procedure.

*Container build.* #raw("Dockerfile") is a three-stage build that produces a slim, multi-architecture runtime image (#raw("linux/amd64") and #raw("linux/arm64")). The first stage, #raw("sdk-restore"), copies only the four #raw(".csproj") files into the build context and runs #raw("dotnet restore"); the layer cache therefore only invalidates when the package graph changes. The second stage, #raw("sdk-build"), copies the rest of the source and runs #raw("dotnet publish -c Release --no-restore /p:UseAppHost=false") into #raw("/app/publish"). The third stage starts from the official #raw("mcr.microsoft.com/dotnet/aspnet:8.0") image, installs #raw("curl") for the container health probe, creates a non-root #raw("app") user, copies the published output, exposes port 8080, and declares a container-level #raw("HEALTHCHECK") that polls #raw("/health") every thirty seconds.

*Compose topology.* #raw("docker-compose.production.yml") defines two services on a private bridge network #raw("vacancies-net"). The #raw("caddy") service runs the Caddy 2.8-alpine image with ports 80, 443, and 443 UDP exposed to the host, and two persistent volumes carry the Let's Encrypt account material and configuration. The #raw("api") service runs the #raw("vacancies-api:latest") image with port 8080 exposed only inside the network. A memory cap of seven hundred megabytes protects the t3.micro host (which has roughly nine hundred megabytes of usable memory) from a runaway .NET allocation. The environment variable #raw("BackgroundWorkers__EnableVacancyAnalysis=false") explicitly disables the vacancy-analysis worker; the compose file's inline comment records the rationale that an always-on background worker would burn three to six dollars per day in Gemini calls even with no user traffic. Caddy's #raw("depends_on: api { condition: service_healthy }") ensures the reverse proxy does not start serving traffic before the API container reports healthy.

*Caddy reverse proxy.* The #raw("Caddyfile") binds the public hostname from #raw("API_HOSTNAME") (the deployed instance uses #raw("api.vakansio.online")) and obtains a Let's Encrypt certificate automatically (@listing:caddy).

#figure(
  raw(
"{$API_HOSTNAME} {
    header {
        Strict-Transport-Security \"max-age=31536000; includeSubDomains\"
        X-Content-Type-Options    \"nosniff\"
        X-Frame-Options           \"DENY\"
        Referrer-Policy           \"strict-origin-when-cross-origin\"
        -Server
    }

    handle /health        { reverse_proxy api:8080 }
    handle /health/ready  { reverse_proxy api:8080 }

    handle {
        reverse_proxy api:8080 {
            header_up X-Real-IP {remote_host}
            header_up X-Forwarded-For   {remote_host}
            header_up X-Forwarded-Proto {scheme}
            header_up X-Forwarded-Host  {host}

            transport http {
                read_timeout      600s
                write_timeout     600s
                response_header_timeout 600s
            }
        }
    }

    encode zstd gzip
}",
    lang: "",
    block: true,
  ),
  caption: [#raw("Caddyfile") — TLS termination, hardening headers, and the long timeouts that accommodate cold Gemini calls.],
) <listing:caddy>

Rate limiting and the CORS policy are intentionally not configured in Caddy; both live in the API project, so the policy has a single source of truth.

*Deployment procedure.* Deployment is a manual but reproducible script. On the developer workstation I build the image (#raw("docker build -t vacancies-api:<sha> -f Dockerfile .")), save and compress it (#raw("docker save vacancies-api:<sha> | gzip > image.tar.gz")), and copy it to the production host with #raw("scp"). On the host I load the image (#raw("docker load < image.tar.gz")), retag it to #raw("latest"), and roll with #raw("docker compose -f docker-compose.production.yml up -d"). No container registry is involved. The front-end deployment compiles the bundle with #raw("npm run build"), uploads it to the static-hosting bucket with #raw("aws s3 sync dist/ s3://..."), and issues a CloudFront invalidation for the affected paths. The repository has no #raw(".github/workflows/") directory; a CI/CD pipeline is recorded as future work in @sec:conclusion.

*Health probes and rollout safety.* The two-tier health endpoint design from @sec:design:observability is realised here. The Docker container's own #raw("HEALTHCHECK") polls #raw("/health") (liveness only, no database touch) with #raw("curl") every thirty seconds, with a five-second timeout, three retries, and a thirty-second start period. Caddy refuses to start until that probe reports healthy through #raw("depends_on: service_healthy"). The deeper #raw("/health/ready") endpoint performs the database round-trip and is the probe to use when manually confirming that a freshly rolled API container is ready to serve traffic.

*Operating-cost analysis.* The operating cost decomposes into a fixed infrastructure component and a variable language-model component. At publicly listed June 2026 prices in the #raw("eu-central-1") region, a single t3.micro EC2 instance under on-demand Linux billing costs approximately \$0.0114 per hour (~\$8.30 per month). A db.t3.micro RDS instance running PostgreSQL in a single Availability Zone costs approximately \$0.020 per hour (~\$14.60 per month), with an additional \$2.30 per month for twenty gigabytes of general-purpose SSD storage. The S3 bucket for CV files draws a per-gigabyte-month charge of about \$0.0245; for the prototype scale, the monthly bill rounds to a few cents. The CloudFront-fronted front-end bucket falls entirely within the always-free tier of one terabyte of transfer and ten million requests per month. SSM Parameter Store charges nothing for Standard-tier parameters at the volumes used by Vakansio. The total fixed monthly infrastructure cost is therefore approximately \$25 to \$28 per month once any Free-Tier allowance is exhausted; while the account remains within the Free-Tier window, the realised cost is near zero.

The variable component is the Gemini 2.5 Flash usage, priced at \$0.30 per million input tokens and \$2.50 per million output tokens at the short-context tier. These two rates appear verbatim as the private constants #raw("InputPricePerMillion") and #raw("OutputPricePerMillion") in #raw("CostBreakdown.cs:20-21") (@listing:cost), so the application's own cost-estimation arithmetic agrees with the published price list. Per-request cost depends on the pipeline path taken (cold versus warm, judge skip-band hit rate, number of pairs that enter the batched reason generator), and is measured continuously through the #raw("gemini_cost_log") table. Per-stage measured numbers and aggregate per-request statistics are reported in @sec:validation. Three architectural decisions have the most direct effect on the variable cost: the Composite Judge skip-band (avoids ~30–40% of judge invocations), the global-vocabulary path (replaces per-entity skill expansion with a single batched call), and the disabled background workers (keep idle Gemini spend at zero).

*Operational characteristics.* Three properties of the production deployment are worth recording as deliberate choices. The system runs on a single t3.micro host because that fits the cost envelope of a single-author capstone; the migration path to an ECS task definition behind an Application Load Balancer is mechanical from this code base if scale demands it (@sec:design:topology). The deployment is a manual but reproducible Docker Compose script; a CI/CD workflow is the next obvious operational extension. The four background workers default off in production because their always-on Gemini cost is not justified at current usage; the synchronous fan-out replaces them on the request path, which keeps cost predictable.

== Selected engineering narratives <sec:impl:narratives>

In this section I report two engineering challenges that I encountered during the implementation of Vakansio and resolved without weakening the layer separation. Each narrative illustrates a different way the architecture absorbed unexpected change.

*Frontend–back-end contract drift fix (v6.7.8).* The most consequential bug I encountered during the second half of the project was a silent empty-result failure on the front-end search page after a routine back-end serialisation change. The change itself was a single line at #raw("API/Program.cs:66"): the addition of #raw("new JsonStringEnumConverter()") to the JSON serialiser's converter collection, so that the API would emit enum members by name (#raw("\"Djinni\""), #raw("\"StrongMatch\"")) instead of by their underlying ordinal values. The motivation was Swagger readability and the front-end's preference for typed string unions over ordinal magic numbers.

The change broke the front-end in two related but independent places, both fixed under v6.7.8. The first regression was at #raw("frontend/src/pages/JobFeed/JobFeedPage.tsx:153–157"): the source-filter #raw("<select>") used numeric option values (#raw("\"0\""), #raw("\"1\"")) and read #raw("Number(sourceFilter)") to compare against the response payload. After the back-end switch the payload carried strings; #raw("Number(\"Djinni\")") returned #raw("NaN"); and every #raw("job.source !== sourceFilter") comparison was vacuously truthy. The filter rejected every job, and the user saw an empty list with no error. The fix changed option values to mirror the back-end string names and removed the #raw("Number()") coercion. The second regression involved the verdict-label vocabulary. The Domain enum #raw("Verdict") (@sec:impl:domain) defined four members whose CLR names are #raw("StrongMatch"), #raw("PartialMatch"), #raw("WeakMatch"), and #raw("Mismatch"); the string-enum converter therefore emitted these long names on the wire, but #raw("verdictMeta.ts") keyed display metadata on the short forms (#raw("Strong"), #raw("Partial"), #raw("Weak"), #raw("Mismatch")). I resolved this in Domain rather than at the boundary by adding #raw("VerdictExtensions.ToShortName()") at #raw("Verdict.cs:69–75") (@listing:verdict); the DTO uses #raw("ToShortName()") whenever it serialises a verdict, while the reasoning prompt continues to use #raw("ToString()") because it expects the long names. Two callers, two vocabularies, both routed through a single file. The lesson: contract-changing edits need a cross-stack audit, not a unit test on one side. Single-source-of-truth maps such as #raw("verdictMeta.ts") and #raw("evidenceTier.ts") (@sec:design:frontend) and exhaustive extension methods such as #raw("VerdictExtensions") are what made the second fix a one-file edit rather than a scattered touch-up.

*Graceful degradation as an architectural pattern.* The third narrative is not a single bug but a recurring pattern. I built the v6 pipeline on the assumption that any single external call can fail or run out of time, and that the appropriate response is rarely to fail the entire user-facing request. The clearest example is the #raw("SkippedNoAnalysis") field on #raw("GetAggregatedJobsV6Result"). A cold v6 search can return up to roughly one hundred and fifty freshly scraped vacancies, each of which needs a Gemini vacancy-normalisation call before scoring can run. The synchronous fan-out is bounded by #raw("ScoringOptions.SyncNormalizeTimeoutSeconds") (currently three hundred seconds). When the budget blows, the remaining un-normalised vacancies are not scored; they are counted into #raw("SkippedNoAnalysis") and returned alongside the partial result set. On a subsequent request from the same user, the still-unanalysed vacancies in the database are picked up and normalised inside the next budget window. The XML documentation on #raw("ScoringOptions") records this explicitly as a "self-healing" property of the pipeline.

The same pattern reappears in three other places. #raw("JobAggregationService") (@listing:agg) wraps every individual #raw("IJobSourceService") call in a try-catch so that a single failing source — most often LinkedIn returning a rate-limit response — does not break the entire aggregated search. Every Gemini call carries a short per-call timeout — eight seconds in the inner scoring-loop reason call, fifteen seconds in the Composite Judge, eighteen seconds in the batched Judge, and twenty-five seconds in the batched reason service — and falls back to a deterministic template if the timeout fires or the response fails schema validation. #raw("CostLogService") catches every database exception on the principle that a missing log entry is preferable to a failed user response. The lesson: at design time, ask not whether each external call will fail, but how the system will behave when it does. Recording the chosen answer in a typed surface such as #raw("SkippedNoAnalysis") makes the degradation observable rather than silent.

== Chapter summary <sec:impl:summary>

In this chapter I turned the design from Chapter 3 into a deployed, working system. The back-end is four .NET projects whose dependency graph the compiler enforces, with the Domain layer free of any external package. The Application layer hosts a request-scoped, parallel-safe cost accumulator that both the orchestrating handler and the Infrastructure-bound LLM services write into without breaking the Clean Architecture dependency direction. The Infrastructure layer wires seven job-source adapters, the four-stage Gemini scoring pipeline, S3 and SSM Parameter Store adapters, and four flag-gated background workers (all default-off in production). The front-end pairs Zustand for cross-cutting UI state with TanStack Query for server data, persists the query cache to IndexedDB with a fifteen-megabyte hard cap and a cache-buster version, and renders verdicts and explanations bilingually through a typed translation layer. The deployment is a manual but reproducible Docker Compose roll-out behind Caddy on a single EC2 t3.micro host, with a fixed monthly infrastructure cost of approximately \$25–\$28 once the Free-Tier window closes. Two engineering narratives — a v6.7.8 contract drift fix and a graceful-degradation pattern — illustrate how the architecture absorbed unexpected change without scattering the response across the layers. Chapter 5 reports the measurements that close the loop on these im