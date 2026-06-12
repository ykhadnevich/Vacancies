using System.Globalization;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.DTOs.Eval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Eval;


public sealed class EvalIterationReader : IEvalIterationReader
{
    private const string ScoringDirPrefix = "scoring_";
    private const string VacancyDirPrefix = "vacancy_";
    private const string TimestampFormat = "yyyyMMdd_HHmmss";

    private readonly string _resultsRoot;
    private readonly ILogger<EvalIterationReader> _logger;

    public EvalIterationReader(IConfiguration cfg, ILogger<EvalIterationReader> logger)
    {
        _logger = logger;
        var configured = cfg["Eval:ResultsRoot"];
        _resultsRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "results")
            : configured;
        _resultsRoot = Path.GetFullPath(_resultsRoot);
        _logger.LogDebug("Eval results root: {Root}", _resultsRoot);
    }

    public async Task<IReadOnlyList<EvalIterationSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_resultsRoot)) return Array.Empty<EvalIterationSummaryDto>();

        var summaries = new List<EvalIterationSummaryDto>();
        foreach (var runDir in Directory.EnumerateDirectories(_resultsRoot, ScoringDirPrefix + "*"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var summary = await BuildSummaryAsync(runDir, ct);
                if (summary is not null) summaries.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping unreadable eval run dir: {Dir}", runDir);
            }
        }

        return summaries
            .OrderByDescending(s => s.GeneratedAt)
            .ToList();
    }

    public async Task<EvalIterationDetailsDto?> GetDetailsAsync(string runId, CancellationToken ct = default)
    {
        var runDir = ResolveRunDir(runId);
        if (runDir is null) return null;

        var summary = await BuildSummaryAsync(runDir, ct);
        if (summary is null) return null;

        var titleLookup = LoadTitleLookupForRun(runDir);
        var pairs = new List<EvalPairResultDto>();


        foreach (var cvDir in Directory.EnumerateDirectories(runDir))
        {
            ct.ThrowIfCancellationRequested();
            var cvId = Path.GetFileName(cvDir);
            var poolPairs = new List<EvalPairResultDto>();
            foreach (var pairFile in Directory.EnumerateFiles(cvDir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                var dto = await TryReadPairAsync(cvId, pairFile, titleLookup, ct);
                if (dto is not null) poolPairs.Add(dto);
            }

            poolPairs = poolPairs
                .OrderByDescending(p => p.Score)
                .Select((p, idx) => p with { Rank = idx + 1 })
                .ToList();
            pairs.AddRange(poolPairs);
        }

        return new EvalIterationDetailsDto(summary, pairs);
    }


    private async Task<EvalIterationSummaryDto?> BuildSummaryAsync(string runDir, CancellationToken ct)
    {
        var runId = Path.GetFileName(runDir);
        var ts = ParseTimestamp(runId);
        if (ts is null) return null;

        int pairCount = 0;
        int cvCount = 0;
        double scoreSum = 0;
        string? modelVersion = null;
        var verdictCounts = new Dictionary<string, int>
        {
            ["Strong"]   = 0,
            ["Partial"]  = 0,
            ["Weak"]     = 0,
            ["Mismatch"] = 0,
        };

        foreach (var cvDir in Directory.EnumerateDirectories(runDir))
        {
            cvCount++;
            foreach (var pairFile in Directory.EnumerateFiles(cvDir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                using var stream = File.OpenRead(pairFile);
                JsonDocument doc;
                try
                {
                    doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                }
                catch (JsonException)
                {
                    _logger.LogDebug("Skipping invalid JSON pair file: {File}", pairFile);
                    continue;
                }
                using (doc)
                {
                    var root = doc.RootElement;
                    pairCount++;
                    if (modelVersion is null
                        && root.TryGetProperty("model_version", out var mv)
                        && mv.ValueKind == JsonValueKind.String)
                    {
                        modelVersion = mv.GetString() ?? "?";
                    }
                    double score = root.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number
                        ? sc.GetDouble()
                        : 0;
                    scoreSum += score;
                    verdictCounts[BucketVerdict(score)] += 1;
                }
            }
        }

        if (pairCount == 0) return null;

        return new EvalIterationSummaryDto(
            RunId: runId,
            ModelVersion: modelVersion ?? "?",
            GeneratedAt: ts.Value,
            PairCount: pairCount,
            CvCount: cvCount,
            MeanScore: scoreSum / pairCount,
            VerdictCounts: verdictCounts);
    }

    private async Task<EvalPairResultDto?> TryReadPairAsync(
        string cvId, string path, IReadOnlyDictionary<string, string> titles, CancellationToken ct)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var r = doc.RootElement;

            Guid vacancyId = r.TryGetProperty("vacancy_id", out var vid)
                             && vid.ValueKind == JsonValueKind.String
                             && Guid.TryParse(vid.GetString(), out var g)
                ? g
                : Guid.Empty;

            double score = r.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number
                ? sc.GetDouble() : 0;
            double antiPen = r.TryGetProperty("anti_flag_penalty", out var ap)
                             && ap.ValueKind == JsonValueKind.Number
                ? ap.GetDouble() : 1.0;

            var ss = r.TryGetProperty("sub_scores", out var ssEl) && ssEl.ValueKind == JsonValueKind.Object
                ? ssEl
                : default;

            var ev = r.TryGetProperty("evidence", out var evEl) && evEl.ValueKind == JsonValueKind.Object
                ? evEl
                : default;

            return new EvalPairResultDto(
                CvId: cvId,
                VacancyId: vacancyId,
                VacancyTitle: titles.TryGetValue(vacancyId.ToString(), out var t) ? t : "?",
                Rank: 0,
                Score: score,
                Verdict: BucketVerdict(score),
                SkillMatch:       ReadSubScore(ss, "skill_match"),
                SeniorityMatch:   ReadSubScore(ss, "seniority_match"),
                ExperienceMatch:  ReadSubScore(ss, "experience_match"),
                LanguageMatch:    ReadSubScore(ss, "language_match"),
                EducationMatch:   ReadSubScore(ss, "education_match"),
                RoleIntentMatch:  ReadSubScore(ss, "role_intent_match"),
                DomainAlignment:  ReadSubScore(ss, "domain_alignment"),
                AntiFlagPenalty:  antiPen,
                ReasonEn:         r.TryGetProperty("reason_en", out var en) ? en.GetString() ?? "" : "",
                ReasonUk:         r.TryGetProperty("reason_uk", out var uk) ? uk.GetString() : null,
                MatchedSkills:    ReadStringArray(ev, "matched_skills"),
                MissingMustHaves: ReadStringArray(ev, "missing_must_haves"),
                TriggeredAntiFlags: ReadStringArray(ev, "triggered_anti_flags"));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Skipping unreadable pair file: {Path}", path);
            return null;
        }
    }

    private string? ResolveRunDir(string runId)
    {

        string candidate = runId.StartsWith(ScoringDirPrefix, StringComparison.OrdinalIgnoreCase)
            ? runId
            : ScoringDirPrefix + runId;
        var path = Path.Combine(_resultsRoot, candidate);
        return Directory.Exists(path) ? path : null;
    }

    private Dictionary<string, string> LoadTitleLookupForRun(string runDir)
    {


        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_resultsRoot)) return dict;

        var vacancyDir = Directory.EnumerateDirectories(_resultsRoot, VacancyDirPrefix + "*")
            .OrderByDescending(d => d)
            .FirstOrDefault();
        if (vacancyDir is null) return dict;

        var normalizedDir = Path.Combine(vacancyDir, "normalized");
        if (!Directory.Exists(normalizedDir)) return dict;

        foreach (var file in Directory.EnumerateFiles(normalizedDir, "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("role_title", out var rt)
                    && rt.ValueKind == JsonValueKind.Object
                    && rt.TryGetProperty("en", out var enEl)
                    && enEl.ValueKind == JsonValueKind.String)
                {
                    var key = Path.GetFileNameWithoutExtension(file);
                    dict[key] = enEl.GetString() ?? "?";
                }
            }
            catch {  }
        }
        return dict;
    }

    private static DateTime? ParseTimestamp(string runId)
    {
        var tsPart = runId.StartsWith(ScoringDirPrefix, StringComparison.OrdinalIgnoreCase)
            ? runId[ScoringDirPrefix.Length..]
            : runId;
        return DateTime.TryParseExact(
                tsPart, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dt)
            ? dt
            : null;
    }

    private static string BucketVerdict(double score) =>
        score >= 0.75 ? "Strong"   :
        score >= 0.50 ? "Partial"  :
        score >= 0.25 ? "Weak"     : "Mismatch";

    private static double ReadSubScore(JsonElement ss, string field)
    {
        if (ss.ValueKind != JsonValueKind.Object) return 0;
        if (!ss.TryGetProperty(field, out var v)) return 0;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string field)
    {
        if (obj.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        if (!obj.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new List<string>(arr.GetArrayLength());
        foreach (var e in arr.EnumerateArray())
            if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                list.Add(e.GetString()!);
        return list;
    }
}
