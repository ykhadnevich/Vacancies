using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Application.Common.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public sealed class ScoringServiceV2 : IScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IReadOnlyDictionary<SubScoreAxis, ISubScoreCalculator> _calculators;
    private readonly ICompositeJudgeService? _judge;
    private readonly IScoringCapService? _caps;
    private readonly bool _judgeEnabled;
    private readonly ILogger<ScoringServiceV2> _logger;


    public const string Version            = "scoring_v6";


    public const string VersionWithJudge   = "scoring_v6_+_composite_judge_v4";


    string IScoringService.Version => _judgeEnabled ? VersionWithJudge : Version;


    private static readonly Dictionary<SubScoreAxis, double> Weights = new()
    {
        [SubScoreAxis.SkillMatch]       = ScoringConstants.LinearWeights.Skill,
        [SubScoreAxis.SeniorityMatch]   = ScoringConstants.LinearWeights.Seniority,
        [SubScoreAxis.ExperienceMatch]  = ScoringConstants.LinearWeights.Experience,
        [SubScoreAxis.LanguageMatch]    = ScoringConstants.LinearWeights.Language,
        [SubScoreAxis.EducationMatch]   = ScoringConstants.LinearWeights.Education,
        [SubScoreAxis.RoleIntentMatch]  = ScoringConstants.LinearWeights.RoleIntent,
        [SubScoreAxis.DomainAlignment]  = ScoringConstants.LinearWeights.Domain
    };

    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public ScoringServiceV2(
        HttpClient httpClient,
        IConfiguration configuration,
        IEnumerable<ISubScoreCalculator> calculators,
        ILogger<ScoringServiceV2> logger,
        ICompositeJudgeService? judge = null,
        IScoringCapService? caps = null)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _calculators = calculators.ToDictionary(c => c.Axis);
        _judge = judge;
        _caps  = caps;


        _judgeEnabled = judge is not null
                     && caps is not null
                     && (configuration.GetValue<bool?>("Ml:EnableCompositeJudge") ?? true);
        _logger = logger;


        foreach (SubScoreAxis axis in Enum.GetValues<SubScoreAxis>())
        {
            if (!_calculators.ContainsKey(axis))
                throw new InvalidOperationException(
                    $"Missing ISubScoreCalculator for axis '{axis}'. Check DI registration.");
        }
    }

    public async Task<ScoringResult> ScoreAsync(
        string cvId, Guid vacancyId,
        string cvSummaryJson, string vacancyAnalysisJson,
        CancellationToken ct = default,
        bool skipReason = false,
        bool skipJudge = false)
    {
        using var cvDoc = JsonDocument.Parse(cvSummaryJson);
        using var vacDoc = JsonDocument.Parse(vacancyAnalysisJson);
        var cv = cvDoc.RootElement;
        var vacancy = vacDoc.RootElement;


        var raw = new Dictionary<SubScoreAxis, double>();
        foreach (SubScoreAxis axis in Enum.GetValues<SubScoreAxis>())
            raw[axis] = Math.Clamp(_calculators[axis].Compute(cv, vacancy), 0.0, 1.0);

        var subScores = new SubScores(
            SkillMatch:      raw[SubScoreAxis.SkillMatch],
            SeniorityMatch:  raw[SubScoreAxis.SeniorityMatch],
            ExperienceMatch: raw[SubScoreAxis.ExperienceMatch],
            LanguageMatch:   raw[SubScoreAxis.LanguageMatch],
            EducationMatch:  raw[SubScoreAxis.EducationMatch],
            RoleIntentMatch: raw[SubScoreAxis.RoleIntentMatch],
            DomainAlignment: raw[SubScoreAxis.DomainAlignment]);


        var antiResult = AntiFlagEvaluator.Evaluate(cv, vacancy);


        double weightedSum = Weights.Sum(kv => raw[kv.Key] * kv.Value);
        double linearScore = Math.Clamp(weightedSum * antiResult.Penalty, 0.0, 1.0);
        var linearVerdict  = VerdictExtensions.FromScore(linearScore);


        var evidence = BuildEvidence(cv, vacancy, antiResult.Triggered);
        var sanitizedEvidence = SanitizeEvidence(evidence);
        var prioritizedEvidence = PrioritizeEvidence(sanitizedEvidence);


        double score   = linearScore;
        var    verdict = linearVerdict;
        int    judgeInputTokens = 0, judgeOutputTokens = 0;

        if (skipJudge)
        {


        }
        else if (_judgeEnabled && _judge is not null && _caps is not null)
        {


            bool languageGap = LanguageGapDetector.IsLanguageRequirementAbove(cv, vacancy);


            bool extremeBand =
                linearScore < ScoringConstants.ExtremeBand.Low
                || (linearScore >= ScoringConstants.ExtremeBand.High && antiResult.Triggered.Count == 0);

            if (extremeBand)
            {
                double capped = _caps.ApplyCaps(linearScore, subScores, languageGap);
                score   = capped;
                verdict = VerdictExtensions.FromScore(capped);
                _logger.LogDebug(
                    "Skip-the-Judge band: linear={Linear:F3} → capped={Capped:F3} (Judge bypassed)",
                    linearScore, capped);
            }
            else
            {
                var judgeResult = await _judge.JudgeAsync(
                    cv, vacancy, subScores, prioritizedEvidence,
                    linearScore, linearVerdict, ct);

                judgeInputTokens  = judgeResult.InputTokens;
                judgeOutputTokens = judgeResult.OutputTokens;

                if (!judgeResult.FallbackUsed)
                {


                    double capped = _caps.ApplyCaps(judgeResult.FinalScore, subScores, languageGap);
                    score   = capped;
                    verdict = VerdictExtensions.FromScore(capped);

                    _logger.LogDebug(
                        "Hybrid Judge applied: linear={Linear:F3} → judge={Judge:F3} → capped={Capped:F3} ({Verdict})",
                        linearScore, judgeResult.FinalScore, capped, verdict);
                }
                else
                {
                    _logger.LogInformation(
                        "Composite Judge fell back to linear ({Reason}); score={Score:F3}",
                        judgeResult.FailureReason, linearScore);


                    double capped = _caps.ApplyCaps(linearScore, subScores, languageGap);
                    score   = capped;
                    verdict = VerdictExtensions.FromScore(capped);
                }
            }
        }


        prioritizedEvidence = TrimEvidence(prioritizedEvidence,
                                           maxMatched: 12,
                                           maxMissing: 8);


        var reasonCtx = BuildReasonContext(cv, vacancy, subScores);


        int totalInputTokens  = judgeInputTokens;
        int totalOutputTokens = judgeOutputTokens;

        string reasonEn;
        string? reasonUk;

        if (skipReason)
        {


            (reasonEn, reasonUk) = DeterministicReasonFallback(verdict, prioritizedEvidence);
        }
        else
        {
            var (rEn0, rUk0, in1, out1) = await GenerateReasonsAsync(
                verdict, score, subScores, prioritizedEvidence, reasonCtx, strictMode: false, ct);
            reasonEn = rEn0; reasonUk = rUk0;
            totalInputTokens  += in1;
            totalOutputTokens += out1;


            var validation = ReasonValidator.Validate(reasonEn, reasonUk, verdict, prioritizedEvidence);
            if (validation.NeedsRegeneration)
            {
                _logger.LogInformation(
                    "Reason validation failed (hallucinated gaps EN={EnCount}, UK={UkCount}) — regenerating in strict mode",
                    validation.HallucinatedGapsEn.Count,
                    validation.HallucinatedGapsUk.Count);
                var (rEn2, rUk2, in2, out2) = await GenerateReasonsAsync(
                    verdict, score, subScores, prioritizedEvidence, reasonCtx, strictMode: true, ct);
                reasonEn = rEn2; reasonUk = rUk2;
                totalInputTokens  += in2;
                totalOutputTokens += out2;

                validation = ReasonValidator.Validate(reasonEn, reasonUk, verdict, prioritizedEvidence);
                if (validation.NeedsRegeneration)
                {
                    _logger.LogWarning(
                        "Reason still hallucinated after strict regeneration — using deterministic fallback");
                    (reasonEn, reasonUk) = DeterministicReasonFallback(verdict, prioritizedEvidence);
                }
            }


            if (validation.LengthOverflowEn || validation.LengthOverflowUk
                || validation.CalibrationDriftEn || validation.CalibrationDriftUk)
            {
                (reasonEn, reasonUk) = ReasonValidator.Fixup(reasonEn, reasonUk, validation, verdict);
            }
        }

        return new ScoringResult(
            VacancyId: vacancyId,
            CvId: cvId,


            ModelVersion: _judgeEnabled ? VersionWithJudge : Version,
            GeneratedAt: DateTime.UtcNow,
            Score: score,
            SubScores: subScores,
            AntiFlagPenalty: antiResult.Penalty,
            ReasonEn: reasonEn,
            ReasonUk: reasonUk,


            Evidence: prioritizedEvidence,
            InputTokens: totalInputTokens,
            OutputTokens: totalOutputTokens,
            Verdict: verdict,
            Context: reasonCtx);
    }


    private static string InjectExpansion(string sourceJson, string fieldName, string? expansionJson)
    {
        if (string.IsNullOrWhiteSpace(expansionJson)) return sourceJson;
        try
        {
            using var srcDoc = JsonDocument.Parse(sourceJson);
            if (srcDoc.RootElement.ValueKind != JsonValueKind.Object) return sourceJson;


            if (srcDoc.RootElement.TryGetProperty(fieldName, out _)) return sourceJson;


            using var expDoc = JsonDocument.Parse(expansionJson);
            if (expDoc.RootElement.ValueKind != JsonValueKind.Object) return sourceJson;


            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in srcDoc.RootElement.EnumerateObject())
                    prop.WriteTo(writer);
                writer.WritePropertyName(fieldName);
                expDoc.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {


            return sourceJson;
        }
    }

    private static ScoringEvidence BuildEvidence(JsonElement cv, JsonElement vacancy, IReadOnlyList<string> triggered)
    {
        var mustHave = ReadSet(vacancy, "must_have_skills");
        var niceHave = ReadSet(vacancy, "nice_to_have_skills");


        if (SkillExpansionMatcher.TryBuildCvLookup(cv, out var cvLookup))
        {
            var matchedExp = mustHave.Where(m => SkillExpansionMatcher.IsMatched(m, vacancy, cvLookup))
                                     .Concat(niceHave.Where(n => SkillExpansionMatcher.IsMatched(n, vacancy, cvLookup)))
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToList();
            var missingExp = mustHave.Where(m => !SkillExpansionMatcher.IsMatched(m, vacancy, cvLookup)).ToList();
            return new ScoringEvidence(matchedExp, missingExp, triggered);
        }


        var cvSkills = ReadSet(cv, "technical_skills");
        foreach (var s in ReadSet(cv, "domain_skills")) cvSkills.Add(s);
        var cvExpanded = SkillCanonicalizer.ExpandAll(cvSkills);

        var matched = mustHave.Where(m => SkillCanonicalizer.Matches(m, cvExpanded))
                              .Concat(niceHave.Where(n => SkillCanonicalizer.Matches(n, cvExpanded)))
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();
        var missing = mustHave.Where(m => !SkillCanonicalizer.Matches(m, cvExpanded)).ToList();

        return new ScoringEvidence(matched, missing, triggered);
    }

    private static HashSet<string> ReadSet(JsonElement obj, string field)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (obj.ValueKind != JsonValueKind.Object) return set;
        if (!obj.TryGetProperty(field, out var arr)) return set;
        if (arr.ValueKind != JsonValueKind.Array) return set;
        foreach (var it in arr.EnumerateArray())
            if (it.ValueKind == JsonValueKind.String)
            {
                var s = it.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
        return set;
    }


    private async Task<(string en, string uk, int inputTokens, int outputTokens)>
        GenerateReasonsAsync(
            Verdict verdict, double score, SubScores ss, ScoringEvidence ev,
            ReasonContext? ctx, bool strictMode, CancellationToken ct)
    {
        try
        {
            var prompt = ScoringPromptCore.BuildReasonPrompt(verdict, score, ss, ev, ctx);
            if (strictMode)
            {


                prompt += "\nREGENERATION NOTE: a previous attempt at this same input produced\n"
                       +  "reason text containing gaps that were NOT in `missing_must_haves`.\n"
                       +  "This time, list ONLY gaps from `missing_must_haves` verbatim, or\n"
                       +  "from `triggered_anti_flags`. If those lists are empty, do not list\n"
                       +  "any gaps. Failure to comply will discard your output.\n";
            }

            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {


                    temperature = 0.1,
                    topP = 0.95,


                    maxOutputTokens = 8192,
                    thinkingConfig = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema = new Dictionary<string, object>
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["reason_en"] = new Dictionary<string, object> { ["type"] = "STRING" },
                            ["reason_uk"] = new Dictionary<string, object> { ["type"] = "STRING" }
                        },
                        ["required"] = new[] { "reason_en", "reason_uk" }
                    }
                }
            };


            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);


            perCallCts.CancelAfter(TimeSpan.FromSeconds(8));

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var swCall = Stopwatch.StartNew();
            var resp = await _httpClient.PostAsJsonAsync(url, body, perCallCts.Token);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(perCallCts.Token);
            swCall.Stop();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;


            int inputTokens = 0, outputTokens = 0;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var pIn)
                    && pIn.ValueKind == JsonValueKind.Number)
                    inputTokens = pIn.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var pOut)
                    && pOut.ValueKind == JsonValueKind.Number)
                    outputTokens = pOut.GetInt32();
            }


            CostBreakdown.Track(strictMode ? "reason_strict" : "reason",
                swCall.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            {
                var (fEn, fUk) = DeterministicReasonFallback(verdict, ev);
                return (fEn, fUk, inputTokens, outputTokens);
            }
            var first = cands[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
            {
                var (fEn, fUk) = DeterministicReasonFallback(verdict, ev);
                return (fEn, fUk, inputTokens, outputTokens);
            }

            string text = "";
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? ""; break; }
            if (string.IsNullOrWhiteSpace(text))
            {
                var (fEn, fUk) = DeterministicReasonFallback(verdict, ev);
                return (fEn, fUk, inputTokens, outputTokens);
            }

            text = text.Replace("```json", "").Replace("```", "").Trim();
            using var inner = JsonDocument.Parse(text);
            var ir = inner.RootElement;
            var en = ir.TryGetProperty("reason_en", out var enEl) ? enEl.GetString() ?? "" : "";
            var uk = ir.TryGetProperty("reason_uk", out var ukEl) ? ukEl.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(en))
            {
                var (fEn, fUk) = DeterministicReasonFallback(verdict, ev);
                return (fEn, fUk, inputTokens, outputTokens);
            }

            _logger.LogDebug(
                "ScoringServiceV2 reason call ({Mode}): tokens in={In}, out={Out}",
                strictMode ? "strict" : "normal", inputTokens, outputTokens);

            return (en, uk, inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini reason generation failed; using deterministic fallback");
            var (fEn, fUk) = DeterministicReasonFallback(verdict, ev);
            return (fEn, fUk, 0, 0);
        }
    }

    private static (string en, string uk) DeterministicReasonFallback(Verdict v, ScoringEvidence ev)
    {
        var enVerd = v.ToEnglishText();
        var ukVerd = v.ToUkrainianText();
        var topMatched = string.Join(", ", ev.MatchedSkills.Take(3));
        var topMissing = string.Join(", ", ev.MissingMustHaves.Take(2));
        var en = $"{enVerd}. Strengths: {topMatched}. Gaps: {topMissing}.";
        var uk = $"{ukVerd}. Переваги: {topMatched}. Брак: {topMissing}.";
        return (en, uk);
    }


    private static ScoringEvidence SanitizeEvidence(ScoringEvidence ev)
    {
        var matchedCleanCase = DedupCaseInsensitive(ev.MatchedSkills)
            .Where(s => !IsLanguageToken(s))
            .ToList();

        var matchedLowerSet = matchedCleanCase
            .Select(s => s.Trim().ToLowerInvariant())
            .ToHashSet();

        var missingCleanCase = DedupCaseInsensitive(ev.MissingMustHaves)
            .Where(s => !IsLanguageToken(s))
            .Where(s => !matchedLowerSet.Contains(s.Trim().ToLowerInvariant()))
            .ToList();

        return new ScoringEvidence(
            MatchedSkills:       matchedCleanCase,
            MissingMustHaves:    missingCleanCase,
            TriggeredAntiFlags:  ev.TriggeredAntiFlags);
    }


    private static List<string> DedupCaseInsensitive(IReadOnlyList<string> source)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(source.Count);
        foreach (var s in source)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var key = s.Trim();
            if (seen.Add(key)) result.Add(key);
        }
        return result;
    }


    private static bool IsLanguageToken(string skill)
    {
        var s = skill.Trim().ToLowerInvariant();

        if (s == "english" || s == "ukrainian"
            || s == "англійська" || s == "українська")
            return true;


        return System.Text.RegularExpressions.Regex.IsMatch(s,
            @"^(english|ukrainian|англійська|українська)" +
            @"[\s\(\-:]+" +
            @"(a1|a2|b1|b2|c1|c2|native|fluent|advanced|intermediate|" +
            @"pre[\s\-]*intermediate|upper[\s\-]*intermediate|" +
            @"носій|вільн|середн|вище\s+середн|просун|початков)");
    }


#if NEVER_COMPILED
    private static bool _legacyIsLanguageTokenUnused(string skill)
    {
        var s = skill.Trim().ToLowerInvariant();


        s = System.Text.RegularExpressions.Regex.Replace(
            s, @"[\s–— \-–—()].*$", "").Trim();
        return s == "english" || s == "ukrainian"
            || s == "англійська" || s == "українська"


            || System.Text.RegularExpressions.Regex.IsMatch(skill.Trim().ToLowerInvariant(),
                @"^(english|ukrainian|англійська|українська)[\s\(\-:]+(a1|a2|b1|b2|c1|c2|native|fluent|advanced|intermediate|pre[\s\-]*intermediate|upper[\s\-]*intermediate|носій|вільн|середн|вище\s+середн|просун|початков)");
    }
#endif

    private static ScoringEvidence PrioritizeEvidence(ScoringEvidence ev)
    {
        static int Rank(string skill)
        {
            if (string.IsNullOrWhiteSpace(skill)) return 99;
            bool hasSpace = skill.Contains(' ');

            if (!hasSpace)
            {

                if (skill.Length <= 5 && skill.All(c => char.IsUpper(c) || char.IsDigit(c)))
                    return 1;

                if (System.Text.RegularExpressions.Regex.IsMatch(
                        skill, @"^[A-Z.][a-zA-Z0-9.+#\-]*$"))
                    return 1;
            }

            if (hasSpace && skill.Split(' ').Any(t =>
                t.Length > 0 && (char.IsUpper(t[0]) || t.All(char.IsUpper))))
                return 2;

            return 3;
        }


        var matched = ev.MatchedSkills
            .Select((s, i) => (s, i, r: Rank(s)))
            .OrderBy(t => t.r).ThenBy(t => t.i)
            .Select(t => t.s).ToList();
        var missing = ev.MissingMustHaves
            .Select((s, i) => (s, i, r: Rank(s)))
            .OrderBy(t => t.r).ThenBy(t => t.i)
            .Select(t => t.s).ToList();

        return new ScoringEvidence(matched, missing, ev.TriggeredAntiFlags);
    }


    private static ScoringEvidence TrimEvidence(
        ScoringEvidence ev, int maxMatched, int maxMissing)
    {
        if (ev.MatchedSkills.Count    <= maxMatched
         && ev.MissingMustHaves.Count <= maxMissing)
            return ev;

        var matched = ev.MatchedSkills.Count > maxMatched
            ? ev.MatchedSkills.Take(maxMatched).ToList()
            : ev.MatchedSkills;
        var missing = ev.MissingMustHaves.Count > maxMissing
            ? ev.MissingMustHaves.Take(maxMissing).ToList()
            : ev.MissingMustHaves;

        return new ScoringEvidence(matched, missing, ev.TriggeredAntiFlags);
    }


    private static ReasonContext BuildReasonContext(
        JsonElement cv, JsonElement vacancy, SubScores ss)
    {


        int? candidateYears = null;
        if (cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("experience", out var exp)
            && exp.ValueKind == JsonValueKind.Array)
        {
            double monthsTotal = 0;
            foreach (var e in exp.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var type = e.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (string.Equals(type, "COURSE", StringComparison.OrdinalIgnoreCase)) continue;
                if (e.TryGetProperty("duration_months", out var dm)
                    && dm.ValueKind == JsonValueKind.Number)
                    monthsTotal += dm.GetDouble();
            }
            if (monthsTotal > 0) candidateYears = (int)Math.Round(monthsTotal / 12.0);
        }

        int? requiredYears = null;
        if (vacancy.ValueKind == JsonValueKind.Object
            && vacancy.TryGetProperty("min_years_experience", out var my)
            && my.ValueKind == JsonValueKind.Number)
        {


            requiredYears = (int)Math.Round(my.GetDouble());
        }

        int? overBy = null, underBy = null;
        if (candidateYears.HasValue && requiredYears.HasValue)
        {
            var diff = candidateYears.Value - requiredYears.Value;
            if (diff >= 3) overBy = diff;
            else if (diff <= -1) underBy = -diff;
        }

        string? candidateSeniority = cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("seniority", out var cs) ? cs.GetString() : null;
        string? vacancySeniority = vacancy.ValueKind == JsonValueKind.Object
            && vacancy.TryGetProperty("seniority_required", out var vs) ? vs.GetString() : null;

        var targetRoles = new List<string>();
        if (cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("target_roles", out var tr)
            && tr.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in tr.EnumerateArray())
                if (r.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(r.GetString()))
                    targetRoles.Add(r.GetString()!);
        }

        string? vacancyRoleEn = null;
        if (vacancy.ValueKind == JsonValueKind.Object
            && vacancy.TryGetProperty("role_title", out var rt)
            && rt.ValueKind == JsonValueKind.Object
            && rt.TryGetProperty("en", out var rten))
            vacancyRoleEn = rten.GetString();


        bool aligned = false;
        if (targetRoles.Count > 0 && !string.IsNullOrEmpty(vacancyRoleEn))
        {
            string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(
                s, @"\b(senior|junior|middle|mid|lead|principal|staff|head\s+of|sr\.?|jr\.?)\b",
                "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim().ToLowerInvariant();
            var vacBase = Strip(vacancyRoleEn);
            aligned = targetRoles.Any(t =>
            {
                var b = Strip(t);
                return !string.IsNullOrEmpty(b) && !string.IsNullOrEmpty(vacBase)
                    && (vacBase.Contains(b) || b.Contains(vacBase));
            });
        }

        bool crossDomain = ss.DomainAlignment < 0.60;


        string? candidateDomainsSummary = null;
        if (cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("domain_skills", out var ds)
            && ds.ValueKind == JsonValueKind.Array)
        {
            var domainsList = new List<string>();
            foreach (var d in ds.EnumerateArray())
                if (d.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(d.GetString()))
                    domainsList.Add(d.GetString()!);
            if (domainsList.Count > 0)
                candidateDomainsSummary = string.Join(", ", domainsList.Take(3));
        }

        string? vacancyDomain = null;
        if (vacancy.ValueKind == JsonValueKind.Object
            && vacancy.TryGetProperty("domain_context", out var dc)
            && dc.ValueKind == JsonValueKind.Object
            && dc.TryGetProperty("en", out var dcen))
            vacancyDomain = dcen.GetString();


        string? candidateEnglishLevel = null;
        if (cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("english_level", out var cel)
            && cel.ValueKind == JsonValueKind.String)
            candidateEnglishLevel = cel.GetString();

        string? vacancyEnglishRequired = null;
        if (vacancy.ValueKind == JsonValueKind.Object
            && vacancy.TryGetProperty("english_required", out var ver)
            && ver.ValueKind == JsonValueKind.String)
            vacancyEnglishRequired = ver.GetString();

        return new ReasonContext(
            CandidateYearsOfExperience: candidateYears,
            VacancyRequiredYears:       requiredYears,
            OverqualifiedByYears:       overBy,
            UnderqualifiedByYears:      underBy,
            CandidateSeniority:         candidateSeniority,
            VacancySeniority:           vacancySeniority,
            CandidateTargetRoles:       targetRoles,
            VacancyRoleEn:              vacancyRoleEn,
            TargetRoleAligned:          aligned,
            CrossDomainTransition:      crossDomain,
            CandidateDomainsSummary:    candidateDomainsSummary,
            VacancyDomain:              vacancyDomain,
            CandidateEnglishLevel:      candidateEnglishLevel,
            VacancyEnglishRequired:     vacancyEnglishRequired);
    }
}
