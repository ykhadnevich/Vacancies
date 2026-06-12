using System.Collections.Concurrent;
using System.Text.Json;
using Application.Common.Diagnostics;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Common.SkillExpansion;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Domain.Scoring;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Recruiter.Commands.AnalyzeListAgainstVacancy;

/// <summary>
/// Mirrors the v6 candidate-side pipeline for the recruiter cabinet, with the
/// asymmetries documented inline:
/// <list type="number">
///   <item>Skill expansion (vocab synonyms) injected into BOTH CV and vacancy
///         JSON before scoring — matches v6 InjectExpansion step.</item>
///   <item>Recruiter-framed Mono scoring (third-person reason text).</item>
///   <item>Structural caps applied (seniority / language / role-intent) via
///         <c>IScoringCapService</c>.</item>
///   <item>NO <c>ScoreDispersion</c> (small cohorts; would be a no-op anyway).</item>
///   <item>NO shared <c>ScoringCache</c> — results stored on <c>CandidateScore</c>
///         keyed by (VacancyId, RecruiterCandidateId).</item>
///   <item>In-memory lock per vacancyId protects against double clicks.</item>
///   <item>Only-new semantics: candidates already scored against this vacancy
///         are skipped on re-analyse.</item>
///   <item>Cost telemetry persisted via <c>ICostLogService</c> at end of request.</item>
/// </list>
/// </summary>
public sealed class AnalyzeListAgainstVacancyHandler
    : IRequestHandler<AnalyzeListAgainstVacancyCommand, AnalyzeListAgainstVacancyResult>
{
    private readonly IJobVacancyRepository _vacancies;
    private readonly IRecruiterCandidateRepository _candidates;
    private readonly ICandidateScoreRepository _scores;
    private readonly IRecruiterScoringService _scoring;
    private readonly ISkillVocabularyService _vocab;
    private readonly IScoringCapService _caps;
    private readonly ICostLogService _costLog;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AnalyzeListAgainstVacancyHandler> _logger;

    // Per-vacancy lock. Stays in-process — fine for the single-instance prod deployment
    // documented in HANDOFF_DAY5_TO_DAY6.md. Idempotency on existing CandidateScore rows
    // is the backstop if this ever moves to multi-instance.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Locks = new();

    // Cap Gemini concurrency per analyse run. Same shape v6 handler uses (8); kept at 6
    // here as a safer default for the free Gemini tier.
    private const int ScoringConcurrency = 6;

    public AnalyzeListAgainstVacancyHandler(
        IJobVacancyRepository vacancies,
        IRecruiterCandidateRepository candidates,
        ICandidateScoreRepository scores,
        IRecruiterScoringService scoring,
        ISkillVocabularyService vocab,
        IScoringCapService caps,
        ICostLogService costLog,
        ICurrentUserService currentUser,
        ILogger<AnalyzeListAgainstVacancyHandler> logger)
    {
        _vacancies = vacancies;
        _candidates = candidates;
        _scores = scores;
        _scoring = scoring;
        _vocab = vocab;
        _caps = caps;
        _costLog = costLog;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AnalyzeListAgainstVacancyResult> Handle(
        AnalyzeListAgainstVacancyCommand cmd, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var gate = Locks.GetOrAdd(cmd.VacancyId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.Zero, ct))
        {
            _logger.LogInformation(
                "Analyse rejected — already running for vacancy {VacancyId} (recruiter {UserId}).",
                cmd.VacancyId, userId);
            return new AnalyzeListAgainstVacancyResult(
                AnalyzeStatus.AlreadyRunning, 0, 0, 0, 0, _scoring.Version);
        }

        // Per-request cost telemetry scope. CostBreakdown.Track inside the Mono
        // service writes here automatically; we persist the snapshot at the end.
        using var costScope = CostBreakdown.BeginScope();

        try
        {
            return await RunAsync(cmd, userId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AnalyzeListAgainstVacancyResult> RunAsync(
        AnalyzeListAgainstVacancyCommand cmd,
        Guid userId,
        CancellationToken ct)
    {
        var version = _scoring.Version;

        var vacancy = await _vacancies.GetByIdAsync(cmd.VacancyId, ct);
        if (vacancy is null || string.IsNullOrWhiteSpace(vacancy.VacancyAnalysisJson))
        {
            _logger.LogWarning(
                "Analyse blocked: vacancy {VacancyId} is missing its normalised analysis.",
                cmd.VacancyId);
            return new AnalyzeListAgainstVacancyResult(
                AnalyzeStatus.VacancyNotNormalized, 0, 0, 0, 0, version);
        }

        var inList = await _candidates.ListByListAsync(cmd.CandidateListId, ct);
        var ready = inList
            .Where(c => c.Status == CandidateNormalizationStatus.Normalized
                     && !string.IsNullOrWhiteSpace(c.CvNormalizedJson))
            .ToList();

        var skipped = inList.Count - ready.Count;

        if (ready.Count == 0)
        {
            return new AnalyzeListAgainstVacancyResult(
                AnalyzeStatus.NothingToScore, 0, 0, skipped, 0, version);
        }

        var alreadyScoredIds = await _scores.GetScoredCandidateIdsAsync(
            cmd.VacancyId, ready.Select(c => c.Id).ToList(), ct);
        var alreadyScored = alreadyScoredIds.Count;

        var toScore = ready.Where(c => !alreadyScoredIds.Contains(c.Id)).ToList();
        if (toScore.Count == 0)
        {
            _logger.LogInformation(
                "Analyse for vacancy {VacancyId}: all {Count} ready candidates already scored.",
                cmd.VacancyId, ready.Count);
            return new AnalyzeListAgainstVacancyResult(
                AnalyzeStatus.Completed, 0, alreadyScored, skipped, 0, version);
        }

        // ─── Step 1: Build the per-pair injected JSON (mirrors v6 L271-317) ────
        var (cvJsonInjectedByCandidate, vacancyJsonInjected) =
            await BuildExpandedInputsAsync(toScore, vacancy.VacancyAnalysisJson!, ct);

        _logger.LogInformation(
            "Analysing vacancy {VacancyId} × list {ListId}: scoring {New} new candidates " +
            "({Already} already scored, {Skipped} skipped) under {Version}.",
            cmd.VacancyId, cmd.CandidateListId, toScore.Count, alreadyScored, skipped, version);

        // ─── Step 2: Parallel scoring + per-task isolation (mirrors v6 L352-422) ──
        using var gate = new SemaphoreSlim(ScoringConcurrency, ScoringConcurrency);
        var freshScores = new ConcurrentBag<CandidateScore>();
        int failedCount = 0;

        // CV/vacancy JsonDocuments for the cap loop. Parse once.
        using var vacJsonDoc = JsonDocument.Parse(vacancyJsonInjected);
        var vacancyElement = vacJsonDoc.RootElement.Clone();

        var tasks = toScore.Select(async candidate =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var cvInjected = cvJsonInjectedByCandidate[candidate.Id];

                var result = await _scoring.ScoreAsync(
                    cvId:                candidate.Id.ToString(),
                    vacancyId:           cmd.VacancyId,
                    cvSummaryJson:       cvInjected,
                    vacancyAnalysisJson: vacancyJsonInjected,
                    ct:                  ct);

                if (result.ModelVersion.Contains("_fallback"))
                {
                    Interlocked.Increment(ref failedCount);
                    _logger.LogWarning(
                        "Recruiter scoring fallback for candidate {CandidateId}: {ModelVersion}.",
                        candidate.Id, result.ModelVersion);
                    return;
                }

                // ─── Step 3: Structural cap (seniority/language/role-intent) ──
                using var cvDoc = JsonDocument.Parse(cvInjected);
                bool languageGap = LanguageGapDetector.IsLanguageRequirementAbove(
                    cvDoc.RootElement, vacancyElement);
                double capped = _caps.ApplyCaps(result.Score, result.SubScores, languageGap);

                var finalResult = Math.Abs(capped - result.Score) < 1e-9
                    ? result
                    : result with
                    {
                        Score   = capped,
                        Verdict = VerdictExtensions.FromScore(capped),
                    };

                var json = JsonSerializer.Serialize(finalResult);
                freshScores.Add(CandidateScore.Create(
                    cmd.VacancyId, candidate.Id, finalResult.Score, version, json));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failedCount);
                _logger.LogWarning(ex,
                    "Recruiter scoring failed for candidate {CandidateId} against vacancy {VacancyId}.",
                    candidate.Id, cmd.VacancyId);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        var batch = freshScores.ToList();
        if (batch.Count > 0)
        {
            await _scores.UpsertBatchAsync(batch, ct);
        }

        // ─── Step 4: Cost telemetry — mirrors v6 L841-847 ──────────────────────
        var snapshot = CostBreakdown.GetSnapshot();
        if (snapshot is { Count: > 0 })
        {
            try
            {
                await _costLog.PersistAsync(
                    requestId:   Guid.NewGuid(),
                    requestKind: "recruiter_analyse",
                    stages:      snapshot,
                    userId:      userId,
                    keywords:    null,
                    ct:          ct);
            }
            catch (Exception ex)
            {
                // Cost log failure must NOT fail the analysis.
                _logger.LogWarning(ex, "Cost log persist failed for recruiter analyse (non-fatal).");
            }
        }

        // NOTE: ScoreDispersion intentionally NOT applied. It short-circuits at
        // N<5 (see Application.Common.Scoring.ScoreDispersion.MinCohortSize) and
        // was designed to fix LLM clustering across the candidate-side aggregation
        // cohort of 30+ vacancies. Recruiter ranking is over <=20 candidates per
        // vacancy where the structural caps + raw Mono scores read honestly.

        return new AnalyzeListAgainstVacancyResult(
            AnalyzeStatus.Completed,
            NewlyScored:    batch.Count,
            AlreadyScored:  alreadyScored,
            Skipped:        skipped,
            Failed:         failedCount,
            ScoringVersion: version);
    }

    /// <summary>
    /// Mirrors the v6 InjectExpansion step: resolve synonyms once for the union
    /// of all skills (vacancy + every candidate) so the Mono prompt sees the
    /// `_skills_expanded` / `_must_haves_expanded` hints. One batched vocab call
    /// per analyse run regardless of candidate count.
    /// </summary>
    private async Task<(IReadOnlyDictionary<Guid, string> CvByCandidate, string VacancyJson)>
        BuildExpandedInputsAsync(
            IReadOnlyList<RecruiterCandidate> candidates,
            string vacancyAnalysisJson,
            CancellationToken ct)
    {
        // Collect skills + a dominant role hint from the vacancy.
        var (vacancySkills, roleHint) = SkillExpansionHelper.ExtractVacancySkillsAndRoleHint(vacancyAnalysisJson);

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in vacancySkills) unique.Add(s);

        var cvSkillsByCandidate = new Dictionary<Guid, List<string>>(candidates.Count);
        foreach (var c in candidates)
        {
            var cvSkills = SkillExpansionHelper.ExtractCvSkills(c.CvNormalizedJson!);
            cvSkillsByCandidate[c.Id] = cvSkills;
            foreach (var s in cvSkills) unique.Add(s);
        }

        // Single batched vocab call — graceful fallback to identity expansion on failure
        // (matches v6 handler behaviour on vocab service errors).
        IReadOnlyDictionary<string, string> vocab;
        try
        {
            vocab = await _vocab.ResolveSynonymsAsync(unique.ToList(), roleHint, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Vocab service failed during recruiter analyse — falling back to identity expansion.");
            vocab = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var vacancyExpansion = SkillExpansionHelper.BuildExpansionFromVocab(vacancySkills, vocab);
        var vacancyInjected = SkillExpansionHelper.InjectExpansion(
            vacancyAnalysisJson, "_must_haves_expanded", vacancyExpansion);

        var byCandidate = new Dictionary<Guid, string>(candidates.Count);
        foreach (var c in candidates)
        {
            var cvExpansion = SkillExpansionHelper.BuildExpansionFromVocab(
                cvSkillsByCandidate[c.Id], vocab);
            byCandidate[c.Id] = SkillExpansionHelper.InjectExpansion(
                c.CvNormalizedJson!, "_skills_expanded", cvExpansion);
        }

        return (byCandidate, vacancyInjected);
    }
}
