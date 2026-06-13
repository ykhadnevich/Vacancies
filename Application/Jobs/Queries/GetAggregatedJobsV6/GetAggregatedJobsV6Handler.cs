using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Application.Common.Configuration;
using Application.Common.Interfaces;
using Application.Common.Exceptions;
using Application.Common.Scoring;
using Application.DTOs;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Scoring;
using Application.Common.Diagnostics;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Jobs.Queries.GetAggregatedJobsV6;


public sealed class GetAggregatedJobsV6Handler
    : IRequestHandler<GetAggregatedJobsV6Query, GetAggregatedJobsV6Result>
{
    private readonly IJobVacancyRepository _jobs;
    private readonly IUserProfileRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly IScoringService _scoring;
    private readonly IJobAggregationService _aggregator;


    private readonly ISkillVocabularyService _vocab;
    private readonly IBatchedReasonService _batchedReason;
    private readonly IBatchedJudgeService  _batchedJudge;
    private readonly IScoringCapService    _caps;
    private readonly IBatchedVacancyExtractionService _batchedExtractor;
    private readonly IScoringCacheRepository _cache;
    private readonly ILogger<GetAggregatedJobsV6Handler> _logger;


    private const double RoleIntentGate = 0.6;
    private const int    PreFilterTopN  = 40;


    private const int    ReasonGenerationCap = 30;


    private readonly TimeSpan _syncNormalizeTimeout;


    private readonly IMemoryCache _responseCache;
    private static readonly TimeSpan ResponseCacheTtl = TimeSpan.FromMinutes(5);


    private readonly ICostLogService _costLog;


    /// <summary>
    /// True when <c>Scoring:Engine = "mono"</c>. Skips the Composite-Judge anchor
    /// and the batched-reason pass because Mono already produces the composite
    /// score *and* the bilingual reason in a single Gemini call.
    /// </summary>
    private readonly bool _useMonoEngine;


    private readonly IUserSearchSnapshotRepository _snapshots;

    public GetAggregatedJobsV6Handler(
        IJobVacancyRepository jobs,
        IUserProfileRepository users,
        ICurrentUserService currentUser,
        IScoringService scoring,
        IJobAggregationService aggregator,
        ISkillVocabularyService vocab,
        IBatchedReasonService batchedReason,
        IBatchedJudgeService batchedJudge,
        IScoringCapService caps,
        IBatchedVacancyExtractionService batchedExtractor,
        IScoringCacheRepository cache,
        Microsoft.Extensions.Caching.Memory.IMemoryCache responseCache,
        ICostLogService costLog,
        IUserSearchSnapshotRepository snapshots,
        IOptions<ScoringOptions> scoringOptions,
        ILogger<GetAggregatedJobsV6Handler> logger)
    {
        _jobs = jobs;
        _users = users;
        _currentUser = currentUser;
        _scoring = scoring;
        _aggregator = aggregator;
        _vocab = vocab;
        _batchedReason = batchedReason;
        _batchedJudge = batchedJudge;
        _caps = caps;
        _batchedExtractor = batchedExtractor;
        _cache = cache;
        _responseCache = responseCache;
        _costLog = costLog;
        _snapshots = snapshots;
        _syncNormalizeTimeout = TimeSpan.FromSeconds(
            scoringOptions.Value.SyncNormalizeTimeoutSeconds);
        _useMonoEngine = string.Equals(
            scoringOptions.Value.Engine, "mono", StringComparison.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<GetAggregatedJobsV6Result> Handle(
        GetAggregatedJobsV6Query request, CancellationToken ct)
    {


        using var costScope = CostBreakdown.BeginScope();
        var stopwatch = Stopwatch.StartNew();


        long elapsedAfterCvSummaryMs   = 0;
        long elapsedAfterScrapeMs      = 0;
        long elapsedAfterFilterMs      = 0;
        long elapsedAfterNormalizeMs   = 0;
        long elapsedAfterSkillVocabMs  = 0;
        long elapsedAfterExpansionMs   = 0;
        long elapsedAfterRankingMs     = 0;
        long elapsedAfterReasonsMs     = 0;
        int normalizeInputTokens = 0, normalizeOutputTokens = 0;


        if (_currentUser.UserId is not Guid userId)
            throw new UnauthorizedAccessException("v6 endpoint requires authenticated user");

        var profile = await _users.GetByIdAsync(userId, ct)
            ?? throw new UnauthorizedAccessException("User profile not found");

        if (string.IsNullOrWhiteSpace(profile.CvSummary))


            throw new CvNotReadyException();


        var responseCacheKey =
            $"v6:{userId:N}:{profile.CvVersionId:N}:" +
            $"{(request.Keywords ?? "").Trim().ToLowerInvariant()}:" +
            $"{(request.Location ?? "").Trim().ToLowerInvariant()}:{request.Limit}";

        if (_responseCache.TryGetValue<GetAggregatedJobsV6Result>(
                responseCacheKey, out var cachedResponse) && cachedResponse is not null)
        {
            _logger.LogInformation(
                "v6 query: response cache HIT for user {UserId} '{Keywords}' " +
                "(jobs={Count}, age<{Ttl}min) — skipping pipeline",
                userId, request.Keywords, cachedResponse.Jobs.Count, ResponseCacheTtl.TotalMinutes);
            return cachedResponse;
        }
        elapsedAfterCvSummaryMs = stopwatch.ElapsedMilliseconds;


        IReadOnlyList<JobVacancy> newlyInserted = Array.Empty<JobVacancy>();
        IReadOnlyList<JobVacancy> scrapedPool   = Array.Empty<JobVacancy>();
        if (!string.IsNullOrWhiteSpace(request.Keywords))
        {
            var agg = await _aggregator.ScrapeAndPersistAsync(
                request.Keywords, request.Location, request.Country, ct);
            newlyInserted = agg.NewlyInserted;


            scrapedPool = agg.Resolved.Where(j => !j.IsDuplicate).ToList();
        }
        elapsedAfterScrapeMs = stopwatch.ElapsedMilliseconds;


        var filtered = scrapedPool
            .Where(j => request.WorkFormat is null || j.WorkFormat == request.WorkFormat)
            .Where(j => request.SeniorityLevel is null || j.SeniorityLevel == request.SeniorityLevel)
            .Where(j => request.Location is null
                || (j.Location != null && j.Location.Contains(request.Location, StringComparison.OrdinalIgnoreCase)))
            .Where(j => request.Category is null || string.Equals(j.Category, request.Category, StringComparison.OrdinalIgnoreCase))
            .Where(j => request.MinSalary is null
                || (j.Salary != null && j.Salary.MinAmount.HasValue && j.Salary.MinAmount.Value >= request.MinSalary.Value))
            .ToList();
        elapsedAfterFilterMs = stopwatch.ElapsedMilliseconds;


        var unanalyzed = filtered
            .Where(j => string.IsNullOrWhiteSpace(j.VacancyAnalysisJson)
                && !string.IsNullOrWhiteSpace(j.Description))
            .ToList();
        if (unanalyzed.Count > 0)
        {
            using var normCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            normCts.CancelAfter(_syncNormalizeTimeout);

            var extractionRequests = unanalyzed
                .Select(j => new BatchedVacancyExtractionRequest(
                    VacancyId:      j.Id,
                    VacancyRawText: $"{j.Title}\n\n{j.Description}"))
                .ToList();

            IReadOnlyDictionary<Guid, VacancyExtractionResult> batchedResults =
                new Dictionary<Guid, VacancyExtractionResult>();
            try
            {
                batchedResults = await _batchedExtractor.ExtractBatchAsync(extractionRequests, normCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {

                batchedResults = new Dictionary<Guid, VacancyExtractionResult>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "v6 batched sync-normalize threw wholesale — falling back to worker pickup for {Count} unanalyzed",
                    extractionRequests.Count);
            }

            int normalizedCount = 0;
            foreach (var job in unanalyzed)
            {
                if (!batchedResults.TryGetValue(job.Id, out var res)) continue;
                if (string.IsNullOrWhiteSpace(res.Json)) continue;

                try
                {
                    await _jobs.SaveVacancyAnalysisAsync(
                        job.Id, res.Json, res.ModelVersion, ct);
                    job.SetVacancyAnalysis(res.Json, res.ModelVersion);
                    normalizedCount++;
                    normalizeInputTokens  += res.InputTokens;
                    normalizeOutputTokens += res.OutputTokens;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "v6 sync-normalize save failed for vacancy {Id} ({Title})",
                        job.Id, job.Title);
                }
            }

            _logger.LogInformation(
                "v6 query: sync-normalized {Normalized}/{Unanalyzed} candidate vacancies " +
                "(newlyInserted={New}, pre-existing backlog={Backlog}; extract-timeout={Timeout}s)",
                normalizedCount, unanalyzed.Count,
                unanalyzed.Count(j => newlyInserted.Any(n => n.Id == j.Id)),
                unanalyzed.Count - unanalyzed.Count(j => newlyInserted.Any(n => n.Id == j.Id)),
                _syncNormalizeTimeout.TotalSeconds);
        }
        elapsedAfterNormalizeMs = stopwatch.ElapsedMilliseconds;

        var totalAvailable = filtered.Count;

        var withAnalysis = filtered
            .Where(j => !string.IsNullOrWhiteSpace(j.VacancyAnalysisJson))
            .ToList();
        var skippedNoAnalysis = filtered.Count - withAnalysis.Count;

        if (skippedNoAnalysis > 0)
            _logger.LogInformation(
                "v6 query: {Skipped} scraped jobs skipped - no VacancyAnalysis yet " +
                "(sync-normalize hit timeout / 429; VacancyAnalysisWorker will fill " +
                "them and they'll appear on next request)",
                skippedNoAnalysis);

        var maxToScore = withAnalysis.Count;
        var candidatesToScore = withAnalysis
            .OrderByDescending(j => j.PublishedAt)
            .Take(maxToScore)
            .ToList();

        _logger.LogInformation(
            "v6 query: scoring {Scoring} of {Available} scraped candidates with analysis " +
            "(pool size before filters: {Pool}) — engine={Engine}",
            candidatesToScore.Count, withAnalysis.Count, scrapedPool.Count,
            _useMonoEngine ? "mono" : "linear");


        var cvSkills = CvSummaryParser.ExtractCvSkills(profile.CvSummary!);

        var vacancySkillsByJob = new Dictionary<Guid, List<string>>(candidatesToScore.Count);
        string? dominantRoleHint = null;
        var uniqueSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in cvSkills) uniqueSkills.Add(s);
        foreach (var job in candidatesToScore)
        {
            var (sk, hint) = CvSummaryParser.ExtractVacancySkillsAndRoleHint(job.VacancyAnalysisJson!);
            vacancySkillsByJob[job.Id] = sk;
            foreach (var s in sk) uniqueSkills.Add(s);
            dominantRoleHint ??= hint;
        }

        IReadOnlyDictionary<string, string> globalSyns;
        try
        {
            globalSyns = await _vocab.ResolveSynonymsAsync(uniqueSkills.ToList(), dominantRoleHint, ct);
            _logger.LogInformation(
                "v6 query: vocab resolved {Resolved}/{Total} unique skills across CV + {VacCount} vacancies",
                globalSyns.Count, uniqueSkills.Count, candidatesToScore.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "v6 query: global vocab service failed — falling back to identity expansion");
            globalSyns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var cvExpansionJson = BuildExpansionFromVocab(cvSkills, globalSyns);

        var vacancyExpansions = new Dictionary<Guid, string?>(candidatesToScore.Count);
        foreach (var job in candidatesToScore)
        {
            var sk = vacancySkillsByJob[job.Id];
            vacancyExpansions[job.Id] = BuildExpansionFromVocab(sk, globalSyns);
        }
        elapsedAfterSkillVocabMs = stopwatch.ElapsedMilliseconds;


        var cvJsonInjected = InjectExpansion(profile.CvSummary!, "_skills_expanded", cvExpansionJson);
        var jobVacJson = new Dictionary<Guid, string>(candidatesToScore.Count);
        foreach (var job in candidatesToScore)
        {
            vacancyExpansions.TryGetValue(job.Id, out var vacExp);
            jobVacJson[job.Id] = InjectExpansion(job.VacancyAnalysisJson!, "_must_haves_expanded", vacExp);
        }


        // Mono engine cache lookup. The CV+vacancy text are stable inputs, so a
        // pair already scored under the current Mono prompt version can be
        // pulled from ScoringCache.MonoResultJson and the Gemini call avoided
        // entirely. Lookup is one SELECT regardless of pool size; misses fall
        // through to the live Mono call and are written back at the end.
        string monoCvHash      = string.Empty;
        string monoScoringVersion = string.Empty;
        IReadOnlyDictionary<Guid, ScoringCacheEntry> monoCacheLookup =
            new Dictionary<Guid, ScoringCacheEntry>();
        if (_useMonoEngine)
        {
            monoCvHash         = CvHasher.ComputeHash(cvJsonInjected);
            monoScoringVersion = _scoring.Version;
            try
            {
                var ids = candidatesToScore.Select(j => j.Id).ToList();
                monoCacheLookup = await _cache.GetForCvAsync(
                    monoCvHash, ids, monoScoringVersion, ct);
                int hits = monoCacheLookup.Count(kv => kv.Value.HasMono);
                _logger.LogInformation(
                    "v6 engine=mono: cache lookup — {Hits}/{Total} hits (cv={CvHashPrefix}, ver={Ver})",
                    hits, ids.Count, monoCvHash[..Math.Min(8, monoCvHash.Length)], monoScoringVersion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "v6 engine=mono: cache lookup failed — proceeding without cache");
                monoCacheLookup = new Dictionary<Guid, ScoringCacheEntry>();
            }
        }


        using var scoringSemaphore = new SemaphoreSlim(initialCount: 8, maxCount: 8);
        var freshMonoResults = new ConcurrentDictionary<Guid, string>();

        var scoringTasks = candidatesToScore.Select(async job =>
        {
            await scoringSemaphore.WaitAsync(ct);
            try
            {
                if (ct.IsCancellationRequested) return ((JobVacancy job, ScoringResult res)?)null;


                if (_useMonoEngine
                    && monoCacheLookup.TryGetValue(job.Id, out var cached)
                    && cached.HasMono)
                {
                    try
                    {
                        var hit = JsonSerializer.Deserialize<ScoringResult>(cached.MonoResultJson!);
                        if (hit is not null) return (job, hit);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Mono cache deserialize failed for {Id} — re-scoring",
                            job.Id);
                    }
                }


                var scoring = await _scoring.ScoreAsync(
                    cvId: userId.ToString(),
                    vacancyId: job.Id,
                    cvSummaryJson: cvJsonInjected,
                    vacancyAnalysisJson: jobVacJson[job.Id],
                    ct,
                    skipReason: true,
                    skipJudge: true);


                if (_useMonoEngine)
                {
                    try
                    {
                        freshMonoResults[job.Id] = JsonSerializer.Serialize(scoring);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Mono cache serialize failed for {Id} — skip persist",
                            job.Id);
                    }
                }

                return (job, scoring);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ((JobVacancy job, ScoringResult res)?)null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "v6 linear pass failed for vacancy {Id} ({Title}) — skipping",
                    job.Id, job.Title);
                return ((JobVacancy job, ScoringResult res)?)null;
            }
            finally
            {
                scoringSemaphore.Release();
            }
        });

        var scored = await Task.WhenAll(scoringTasks);


        if (_useMonoEngine && freshMonoResults.Count > 0)
        {
            try
            {
                var upserts = freshMonoResults
                    .Select(kv => new MonoCacheUpsert(kv.Key, kv.Value))
                    .ToList();
                await _cache.UpsertMonoBatchAsync(monoCvHash, monoScoringVersion, upserts, ct);
                _logger.LogInformation(
                    "v6 engine=mono: persisted {Count} fresh cache entries (cv={CvHashPrefix}, ver={Ver})",
                    upserts.Count, monoCvHash[..Math.Min(8, monoCvHash.Length)], monoScoringVersion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "v6 engine=mono: cache persist failed for {Count} entries (non-fatal)",
                    freshMonoResults.Count);
            }
        }

        var scoredEntries = new List<(JobVacancy Job, ScoringResult Res)>();
        foreach (var entry in scored)
        {
            if (entry is null) continue;


            scoredEntries.Add((entry.Value.job, entry.Value.res));
        }


        var preFiltered = scoredEntries
            .Where(e => e.Res.SubScores.RoleIntentMatch >= RoleIntentGate
                     || e.Res.Score >= 0.50)
            .OrderByDescending(e => e.Res.Score)
            .Take(PreFilterTopN)
            .ToList();
        var preFilteredIds = preFiltered.Select(e => e.Job.Id).ToHashSet();

        _logger.LogInformation(
            "v6.5 pre-filter: {Survivors}/{Total} candidates passed family gate",
            preFiltered.Count, scoredEntries.Count);


        string cvHash = CvHasher.ComputeHash(cvJsonInjected);


        string scoringVersion =
            $"{_scoring.Version}|{_batchedJudge.Version}|{_batchedReason.Version}";

        IReadOnlyDictionary<Guid, ScoringCacheEntry> cacheLookup =
            new Dictionary<Guid, ScoringCacheEntry>();
        var judgeResultsBuilder = new Dictionary<Guid, BatchedJudgeResult>();

        if (_useMonoEngine)
        {
            _logger.LogInformation(
                "v6 engine=mono: skipping Composite-Judge pass " +
                "(Mono already produced composite scores for {Count} pre-filtered pairs)",
                preFiltered.Count);
        }
        else
        {
            if (preFiltered.Count > 0)
            {
                try
                {
                    cacheLookup = await _cache.GetForCvAsync(
                        cvHash, preFiltered.Select(e => e.Job.Id).ToList(), scoringVersion, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "v6.7 cache: lookup failed — falling through to full Judge pass (non-fatal)");
                }
            }


            foreach (var e in preFiltered)
            {
                if (cacheLookup.TryGetValue(e.Job.Id, out var cached) && cached.HasJudge)
                {
                    judgeResultsBuilder[e.Job.Id] = new BatchedJudgeResult(
                        FinalScore:    cached.JudgeScore!.Value,
                        FallbackUsed:  false,
                        FailureReason: null);
                }
            }

            var judgeMisses = preFiltered
                .Where(e => !judgeResultsBuilder.ContainsKey(e.Job.Id))
                .ToList();

            _logger.LogInformation(
                "v6.7 cache: Judge hits {Hits}/{Total} (misses → batched Judge)",
                preFiltered.Count - judgeMisses.Count, preFiltered.Count);

            if (judgeMisses.Count > 0)
            {
                var judgeRequests = judgeMisses
                    .Select(e => new BatchedJudgeRequest(
                        VacancyId:           e.Job.Id,
                        VacancyAnalysisJson: jobVacJson[e.Job.Id],
                        SubScores:           e.Res.SubScores,
                        Evidence:            e.Res.Evidence,
                        LinearScore:         e.Res.Score,
                        LinearVerdict:       e.Res.Verdict))
                    .ToList();

                try
                {
                    var fresh = await _batchedJudge.JudgeBatchAsync(cvJsonInjected, judgeRequests, ct);
                    foreach (var kv in fresh)
                        judgeResultsBuilder[kv.Key] = kv.Value;


                    var judgeUpserts = fresh
                        .Where(kv => !kv.Value.FallbackUsed)
                        .Select(kv => new JudgeCacheUpsert(
                            VacancyId:    kv.Key,
                            JudgeScore:   kv.Value.FinalScore,
                            JudgeVerdict: VerdictExtensions.FromScore(kv.Value.FinalScore)))
                        .ToList();
                    if (judgeUpserts.Count > 0)
                    {
                        try
                        {
                            await _cache.UpsertJudgeBatchAsync(cvHash, scoringVersion, judgeUpserts, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "v6.7 cache: Judge upsert failed for {Count} entries (non-fatal)",
                                judgeUpserts.Count);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex,
                        "v6.5 batched judge failed wholesale — keeping linear scores for {Count} miss pairs",
                        judgeMisses.Count);
                }
            }
        }

        IReadOnlyDictionary<Guid, BatchedJudgeResult> judgeResults = judgeResultsBuilder;


        using var cvDocForCaps = JsonDocument.Parse(cvJsonInjected);
        var cvForCaps = cvDocForCaps.RootElement;

        var refinedEntries = new List<(JobVacancy Job, ScoringResult Res)>(scoredEntries.Count);
        foreach (var e in scoredEntries)
        {
            bool hasJudgeScore = preFilteredIds.Contains(e.Job.Id)
                              && judgeResults.TryGetValue(e.Job.Id, out var jr1)
                              && !jr1.FallbackUsed;


            double basis = hasJudgeScore
                ? judgeResults[e.Job.Id].FinalScore
                : e.Res.Score;

            using var vacDoc = JsonDocument.Parse(jobVacJson[e.Job.Id]);
            bool languageGap = LanguageGapDetector.IsLanguageRequirementAbove(cvForCaps, vacDoc.RootElement);
            double capped = _caps.ApplyCaps(basis, e.Res.SubScores, languageGap);


            if (Math.Abs(capped - e.Res.Score) < 1e-9 && !hasJudgeScore)
            {
                refinedEntries.Add(e);
                continue;
            }

            var refined = e.Res with
            {
                Score   = capped,
                Verdict = VerdictExtensions.FromScore(capped)
            };
            refinedEntries.Add((e.Job, refined));
        }

        elapsedAfterExpansionMs = stopwatch.ElapsedMilliseconds;


        // Safety net for Mono's tendency to cluster scores on round numbers.
        // ShouldApply now detects BOTH a fully-collapsed cohort AND the more
        // common case where only the top window is glued (e.g. top-20 all
        // sitting at 0.880 while a long lower tail keeps overall std looking
        // healthy). When triggered, z-score rescale around the cohort mean
        // expands the variance to TargetStd. Ranks preserved; verdict redone.
        if (_useMonoEngine && refinedEntries.Count >= ScoreDispersion.MinCohortSize)
        {
            var originalScores = refinedEntries.Select(e => e.Res.Score).ToList();
            if (ScoreDispersion.ShouldApply(
                    originalScores, ScoreDispersion.TopClusterWindow, out var diagnosis))
            {
                var spreadScores = ScoreDispersion.Apply(originalScores);
                var rebuilt = new List<(JobVacancy Job, ScoringResult Res)>(refinedEntries.Count);
                for (int i = 0; i < refinedEntries.Count; i++)
                {
                    var e = refinedEntries[i];
                    var newScore = spreadScores[i];
                    rebuilt.Add((e.Job, e.Res with
                    {
                        Score   = newScore,
                        Verdict = VerdictExtensions.FromScore(newScore),
                    }));
                }
                refinedEntries = rebuilt;
                _logger.LogInformation(
                    "v6 engine=mono: dispersion applied ({Reason}) — " +
                    "ranks preserved, variance expanded to target std {TargetStd:F2}",
                    diagnosis, ScoreDispersion.TargetStd);
            }
            else
            {
                _logger.LogInformation(
                    "v6 engine=mono: dispersion skipped ({Reason})", diagnosis);
            }
        }

        var topScored = refinedEntries
            .OrderByDescending(e => e.Res.Score)
            .Take(request.Limit)
            .ToList();
        elapsedAfterRankingMs = stopwatch.ElapsedMilliseconds;


        // One-off diagnostic: dump top-10 with 3 decimals so we can tell
        // whether the visible "88% × 35 cards" is real LLM clustering or
        // just integer-percent rounding hiding 0.882 vs 0.879 differences.
        if (topScored.Count > 0)
        {
            var top10 = topScored.Take(10).ToList();
            var sample = string.Join(", ", top10.Select((e, i) =>
                $"#{i + 1}={e.Res.Score:F3}"));
            _logger.LogInformation(
                "v6 query: top-10 raw scores (3 decimals) — {Sample}", sample);
        }


        var reasonScope = topScored.Take(ReasonGenerationCap).ToList();
        var batchedReasonsBuilder = new Dictionary<Guid, BatchedReasonResult>();
        var reasonMisses = new List<(JobVacancy Job, ScoringResult Res)>(reasonScope.Count);

        if (_useMonoEngine)
        {
            _logger.LogInformation(
                "v6 engine=mono: skipping batched-reason pass " +
                "(Mono returned reason_en + reason_uk inline for {Count} pairs)",
                reasonScope.Count);
        }
        else
        {
            foreach (var e in reasonScope)
            {
                if (e.Res.Context is null) continue;
                if (cacheLookup.TryGetValue(e.Job.Id, out var cached) && cached.HasReason)
                {
                    batchedReasonsBuilder[e.Job.Id] = new BatchedReasonResult(
                        StrengthsEn:      cached.StrengthsEn!,
                        StrengthsUk:      cached.StrengthsUk!,
                        GapsEn:           cached.GapsEn!,
                        GapsUk:           cached.GapsUk!,
                        RecommendationEn: cached.RecommendationEn!,
                        RecommendationUk: cached.RecommendationUk!);
                }
                else
                {
                    reasonMisses.Add(e);
                }
            }

            _logger.LogInformation(
                "v6.7 cache: Reason hits {Hits}/{Eligible} (misses → batched Reason)",
                batchedReasonsBuilder.Count, batchedReasonsBuilder.Count + reasonMisses.Count);
        }

        if (reasonMisses.Count > 0)
        {
            var reasonRequests = reasonMisses
                .Select(e => new BatchedReasonRequest(
                    VacancyId: e.Job.Id,
                    VacancyTitle: e.Job.Title,
                    Verdict: e.Res.Verdict,
                    Score: e.Res.Score,
                    SubScores: e.Res.SubScores,
                    Evidence: e.Res.Evidence,
                    Context: e.Res.Context!))
                .ToList();

            try
            {
                var fresh = await _batchedReason.GenerateBatchAsync(reasonRequests, ct);
                _logger.LogInformation(
                    "v6 query: batched reasons generated {Got}/{Asked} pairs",
                    fresh.Count, reasonRequests.Count);
                foreach (var kv in fresh)
                    batchedReasonsBuilder[kv.Key] = kv.Value;


                var reasonUpserts = fresh
                    .Select(kv => new ReasonCacheUpsert(
                        VacancyId:        kv.Key,
                        StrengthsEn:      kv.Value.StrengthsEn,
                        StrengthsUk:      kv.Value.StrengthsUk,
                        GapsEn:           kv.Value.GapsEn,
                        GapsUk:           kv.Value.GapsUk,
                        RecommendationEn: kv.Value.RecommendationEn,
                        RecommendationUk: kv.Value.RecommendationUk))
                    .ToList();
                if (reasonUpserts.Count > 0)
                {
                    try
                    {
                        await _cache.UpsertReasonBatchAsync(cvHash, scoringVersion, reasonUpserts, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "v6.7 cache: Reason upsert failed for {Count} entries (non-fatal)",
                            reasonUpserts.Count);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "v6 query: batched reason service failed — using deterministic templates");
            }
        }


        IReadOnlyDictionary<Guid, BatchedReasonResult> batchedReasons = batchedReasonsBuilder;
        elapsedAfterReasonsMs = stopwatch.ElapsedMilliseconds;
        var topIds = topScored.Select(e => e.Job.Id).ToHashSet();
        var ranked = new List<JobVacancyV6Dto>(topScored.Count);


        var cvSkillSet = Application.Common.Helpers.EvidenceCleaner
            .BuildCvSkillSet(profile.CvSummary);
        foreach (var e in topScored)
        {
            batchedReasons.TryGetValue(e.Job.Id, out var br);
            ranked.Add(ToV6Dto(e.Job, e.Res, br, cvSkillSet));
        }

        stopwatch.Stop();
        var totalMs = stopwatch.ElapsedMilliseconds;


        var cvSummaryMs  = elapsedAfterCvSummaryMs;
        var scrapeMs     = elapsedAfterScrapeMs      - elapsedAfterCvSummaryMs;
        var filterMs     = elapsedAfterFilterMs      - elapsedAfterScrapeMs;
        var normalizeMs  = elapsedAfterNormalizeMs   - elapsedAfterFilterMs;
        var skillVocabMs = elapsedAfterSkillVocabMs  - elapsedAfterNormalizeMs;
        var expansionMs  = elapsedAfterExpansionMs   - elapsedAfterSkillVocabMs;
        var rankingMs    = elapsedAfterRankingMs     - elapsedAfterExpansionMs;
        var reasonsMs    = elapsedAfterReasonsMs     - elapsedAfterRankingMs;
        var composeMs    = totalMs                   - elapsedAfterReasonsMs;

        _logger.LogInformation(
            "v6 query completed in {Total}ms — stage breakdown:\n" +
            "  cv_summary  = {CvSummary,5}ms\n" +
            "  scrape      = {Scrape,5}ms\n" +
            "  filter      = {Filter,5}ms\n" +
            "  normalize   = {Normalize,5}ms\n" +
            "  skill_vocab = {SkillVocab,5}ms\n" +
            "  expansion   = {Expansion,5}ms\n" +
            "  ranking     = {Ranking,5}ms\n" +
            "  reasons     = {Reasons,5}ms\n" +
            "  compose     = {Compose,5}ms\n" +
            "(per-stage cost breakdown follows below)",
            totalMs, cvSummaryMs, scrapeMs, filterMs, normalizeMs,
            skillVocabMs, expansionMs, rankingMs, reasonsMs, composeMs);


        var snapshot = CostBreakdown.GetSnapshot();
        if (snapshot is { Count: > 0 })
        {
            double totalCost = 0;
            double totalStageMs = 0;
            long totalIn = 0, totalOut = 0;
            _logger.LogInformation(
                "v6 cost breakdown by stage (sums across the request, parallel calls included):");
            foreach (var s in snapshot.OrderByDescending(x => x.TotalMs))
            {
                _logger.LogInformation(
                    "  [{Stage,-18}] calls={Calls,4}  total_ms={Ms,8:F0}  avg_ms={Avg,6:F0}  " +
                    "in={In,9}  out={Out,7}  cost=${Cost:F4}",
                    s.Stage, s.Calls, s.TotalMs, s.Calls == 0 ? 0 : s.TotalMs / s.Calls,
                    s.TotalInputTokens, s.TotalOutputTokens, s.EstimatedCost);
                totalCost += s.EstimatedCost;
                totalStageMs += s.TotalMs;
                totalIn += s.TotalInputTokens;
                totalOut += s.TotalOutputTokens;
            }
            _logger.LogInformation(
                "  [TOTAL gemini    ]  stages_sum_ms={Ms:F0}  in={In}  out={Out}  cost=${Cost:F4}  " +
                "(wall_clock_ms={Wall} — lower than sum because of parallel calls)",
                totalStageMs, totalIn, totalOut, totalCost, totalMs);
        }

        var response = new GetAggregatedJobsV6Result(
            Jobs: ranked,
            TotalReturned: ranked.Count,
            TotalAvailable: totalAvailable,
            SkippedNoAnalysis: skippedNoAnalysis,
            PipelineVersion: "scoring_v6");


        var persistSnapshot = CostBreakdown.GetSnapshot();
        if (persistSnapshot is not null && persistSnapshot.Count > 0)
        {
            await _costLog.PersistAsync(
                requestId:   Guid.NewGuid(),
                requestKind: "v6_search",
                stages:      persistSnapshot,
                userId:      userId,
                keywords:    request.Keywords,
                ct:          ct);
        }


        _responseCache.Set(responseCacheKey, response, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ResponseCacheTtl,
            Size = 1,
        });

        // Persist the snapshot so the next time the user opens the app the UI
        // can show this result instantly without re-running the Mono pipeline.
        // Failures here must NOT fail the user request — cost telemetry is
        // already saved and the in-memory cache already has the response.
        try
        {
            var queryHash = V6QueryHasher.Compute(request);
            var responseJson = System.Text.Json.JsonSerializer.Serialize(response);
            var snapshotEntity = UserSearchSnapshot.Create(
                userId, queryHash, request.Keywords ?? string.Empty, responseJson);
            await _snapshots.UpsertAsync(snapshotEntity, ct);
            _logger.LogInformation(
                "v6 snapshot upserted for user {UserId} keywords='{Keywords}' (hash={HashPrefix})",
                userId, request.Keywords, queryHash[..Math.Min(8, queryHash.Length)]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "v6 snapshot upsert failed (non-fatal).");
        }

        return response;
    }


    internal static string InjectExpansion(string baseJson, string field, string? expansionJson)
    {
        if (string.IsNullOrWhiteSpace(expansionJson)) return baseJson;
        try
        {
            using var baseDoc = JsonDocument.Parse(baseJson);
            if (baseDoc.RootElement.ValueKind != JsonValueKind.Object) return baseJson;

            using var expDoc = JsonDocument.Parse(expansionJson);


            using var ms = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in baseDoc.RootElement.EnumerateObject())
                {


                    if (string.Equals(prop.Name, field, StringComparison.Ordinal)) continue;
                    prop.WriteTo(writer);
                }
                writer.WritePropertyName(field);
                expDoc.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (JsonException)
        {
            return baseJson;
        }
    }


    internal static string? BuildExpansionFromVocab(
        IReadOnlyList<string> skills,
        IReadOnlyDictionary<string, string> vocab)
    {
        if (skills.Count == 0) return null;
        var sb = new StringBuilder(skills.Count * 64);
        sb.Append('{');
        bool first = true;
        foreach (var skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill)) continue;
            if (!vocab.TryGetValue(skill, out var arrJson) || string.IsNullOrWhiteSpace(arrJson))
            {


                arrJson = "[{\"term\":" + JsonSerializer.Serialize(skill) + ",\"confidence\":1.0}]";
            }
            if (!first) sb.Append(',');
            sb.Append(JsonSerializer.Serialize(skill));
            sb.Append(':');
            sb.Append(arrJson);
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static JobVacancyV6Dto ToV6Dto(
        JobVacancy job,
        ScoringResult scoring,
        BatchedReasonResult? batchedReason = null,
        HashSet<string>? cvSkillSet = null)
    {
        var verdict = VerdictExtensions.FromScore(scoring.Score).ToShortName();
        var subScores = new Dictionary<string, double>
        {
            ["skill_match"]       = scoring.SubScores.SkillMatch,
            ["seniority_match"]   = scoring.SubScores.SeniorityMatch,
            ["experience_match"]  = scoring.SubScores.ExperienceMatch,
            ["language_match"]    = scoring.SubScores.LanguageMatch,
            ["education_match"]   = scoring.SubScores.EducationMatch,
            ["role_intent_match"] = scoring.SubScores.RoleIntentMatch,
            ["domain_alignment"]  = scoring.SubScores.DomainAlignment,
        };


        string reasonEnFinal;
        string reasonUkFinal;
        if (batchedReason is not null)
        {
            reasonEnFinal = $"Strengths: {batchedReason.StrengthsEn} Gaps: {batchedReason.GapsEn} Recommendation: {batchedReason.RecommendationEn}";
            reasonUkFinal = $"Переваги: {batchedReason.StrengthsUk} Пробіли: {batchedReason.GapsUk} Рекомендація: {batchedReason.RecommendationUk}";
        }
        else
        {
            reasonEnFinal = scoring.ReasonEn;
            reasonUkFinal = scoring.ReasonUk ?? string.Empty;
        }

        return new JobVacancyV6Dto(
            Id: job.Id,
            Title: job.Title,
            Company: job.Company,
            Location: job.Location,
            Description: job.Description,
            Source: job.Source,
            WorkFormat: job.WorkFormat,
            SeniorityLevel: job.SeniorityLevel,
            Category: job.Category,
            Urls: job.Urls,
            PublishedAt: job.PublishedAt,
            Score: scoring.Score,
            Verdict: verdict,
            ReasonEn: reasonEnFinal,
            ReasonUk: reasonUkFinal,
            SubScores: subScores,
            AntiFlagPenalty: scoring.AntiFlagPenalty,
            MatchedSkills: scoring.Evidence.MatchedSkills,


            MissingMustHaves: Application.Common.Helpers.EvidenceCleaner.FilterMissing(
                GenericGapFilter.Filter(scoring.Evidence.MissingMustHaves),
                cvSkillSet ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)),
            TriggeredAntiFlags: scoring.Evidence.TriggeredAntiFlags,
            PipelineVersion: scoring.ModelVersion,
            StrengthsEn: batchedReason?.StrengthsEn,
            StrengthsUk: batchedReason?.StrengthsUk,
            GapsEn: batchedReason?.GapsEn,
            GapsUk: batchedReason?.GapsUk,
            RecommendationEn: batchedReason?.RecommendationEn,
            RecommendationUk: batchedReason?.RecommendationUk);
    }


}
