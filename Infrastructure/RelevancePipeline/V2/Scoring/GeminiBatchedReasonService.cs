using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Common.Diagnostics;
using Application.Common.Interfaces;
using Domain.Scoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public sealed class GeminiBatchedReasonService : IBatchedReasonService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiBatchedReasonService> _logger;
    private readonly ILlmTracer _tracer;

    private const string Model   = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";


    private const int ChunkSize = 10;


    private const int ParallelChunks = 3;


    private const int MinWordsPerSection = 3;


    private const int MaxWordsPerSection = 60;


    private const string BatchedReasonVersionLiteral = "batched_reason_v7_1_banned_vocab+gemini-2.5-flash";
    public string Version => BatchedReasonVersionLiteral;

    public GeminiBatchedReasonService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiBatchedReasonService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<IReadOnlyDictionary<Guid, BatchedReasonResult>> GenerateBatchAsync(
        IReadOnlyList<BatchedReasonRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
            return new Dictionary<Guid, BatchedReasonResult>();


        var chunks = new List<IReadOnlyList<BatchedReasonRequest>>();
        for (int i = 0; i < requests.Count; i += ChunkSize)
        {
            int take = Math.Min(ChunkSize, requests.Count - i);
            var slice = new BatchedReasonRequest[take];
            for (int j = 0; j < take; j++) slice[j] = requests[i + j];
            chunks.Add(slice);
        }

        _logger.LogInformation(
            "Batched reason: {Total} pairs → {Chunks} chunks of ≤{ChunkSize} (parallelism={Par})",
            requests.Count, chunks.Count, ChunkSize, ParallelChunks);

        using var sem = new SemaphoreSlim(ParallelChunks, ParallelChunks);
        var tasks = chunks.Select(async chunk =>
        {
            await sem.WaitAsync(ct);
            try { return await ExpandSingleChunkAsync(chunk, ct); }
            finally { sem.Release(); }
        });
        var partials = await Task.WhenAll(tasks);


        var result = new Dictionary<Guid, BatchedReasonResult>();
        foreach (var part in partials)
            foreach (var kv in part)
                result[kv.Key] = kv.Value;
        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, BatchedReasonResult>> ExpandSingleChunkAsync(
        IReadOnlyList<BatchedReasonRequest> chunk,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(chunk);
        using var span = _tracer.StartSpan(
            name: "batched_reason",
            runType: LlmRunType.LLM,
            inputs: new { prompt, model = Model, version = Version, chunk_size = chunk.Count });
        try
        {
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature      = 0.2,
                    topP             = 0.95,
                    maxOutputTokens  = 8192,
                    thinkingConfig   = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema   = BuildResponseSchema()
                }
            };


            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(25));

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var swCall = Stopwatch.StartNew();
            var resp = await _httpClient.PostAsJsonAsync(url, body, perCallCts.Token);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsStringAsync(perCallCts.Token);
            swCall.Stop();

            using var doc = JsonDocument.Parse(raw);
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
            CostBreakdown.Track("reason_batched", swCall.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            {
                _logger.LogWarning(
                    "Batched reason chunk: no candidates returned ({Count} pairs → empty)",
                    chunk.Count);
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, parsed_pairs = 0, reason = "no_candidates" });
                return new Dictionary<Guid, BatchedReasonResult>();
            }
            var first = cands[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
            {
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, parsed_pairs = 0, reason = "no_parts" });
                return new Dictionary<Guid, BatchedReasonResult>();
            }

            string text = string.Empty;
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? string.Empty; break; }
            if (string.IsNullOrWhiteSpace(text))
            {
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, parsed_pairs = 0, reason = "empty_text" });
                return new Dictionary<Guid, BatchedReasonResult>();
            }

            text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
            var parsed = ParseBatchOutput(text, chunk);
            // Raw Gemini reason output JSON for LangSmith UI debugging.
            // Best-effort: a malformed payload should never throw here.
            JsonElement? extractedJson = null;
            try
            {
                using var extractedDoc = JsonDocument.Parse(text);
                extractedJson = extractedDoc.RootElement.Clone();
            }
            catch (JsonException) { /* tracing best-effort */ }

            span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, parsed_pairs = parsed.Count, extracted = extractedJson });
            return parsed;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {


            _logger.LogError("Batched reason chunk timed out — {Count} pairs dropped to template", chunk.Count);
            span.EndError(new TimeoutException("batched_reason timeout"));
            return new Dictionary<Guid, BatchedReasonResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batched reason chunk failed — {Count} pairs dropped to template", chunk.Count);
            span.EndError(ex);
            return new Dictionary<Guid, BatchedReasonResult>();
        }
    }


    private IReadOnlyDictionary<Guid, BatchedReasonResult> ParseBatchOutput(
        string json, IReadOnlyList<BatchedReasonRequest> chunk)
    {
        var result = new Dictionary<Guid, BatchedReasonResult>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;


            JsonElement pairsEl = default;
            bool shapeOk = root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("pairs", out pairsEl)
                        && pairsEl.ValueKind == JsonValueKind.Array;
            if (!shapeOk)
            {
                _logger.LogWarning(
                    "Batched reason: expected {{pairs:[...]}} but got root={RootKind}, " +
                    "pairs={PairsKind} — dropping chunk of {Count}",
                    root.ValueKind,
                    pairsEl.ValueKind == JsonValueKind.Undefined ? "missing" : pairsEl.ValueKind.ToString(),
                    chunk.Count);
                return result;
            }


            int rejectedValidation = 0;
            foreach (var item in pairsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("pair_idx", out var idxEl)
                    || idxEl.ValueKind != JsonValueKind.Number) continue;
                int idx = idxEl.GetInt32();
                if (idx < 0 || idx >= chunk.Count) continue;

                string sEn = ReadString(item, "strengths_en");
                string sUk = ReadString(item, "strengths_uk");
                string gEn = ReadString(item, "gaps_en");
                string gUk = ReadString(item, "gaps_uk");
                string rEn = ReadString(item, "recommendation_en");
                string rUk = ReadString(item, "recommendation_uk");

                if (!ValidateAndFixup(ref sEn, ref sUk, ref gEn, ref gUk, ref rEn, ref rUk))
                {
                    rejectedValidation++;
                    continue;
                }

                result[chunk[idx].VacancyId] = new BatchedReasonResult(
                    StrengthsEn: sEn,
                    StrengthsUk: sUk,
                    GapsEn: gEn,
                    GapsUk: gUk,
                    RecommendationEn: rEn,
                    RecommendationUk: rUk);
            }


            if (result.Count < chunk.Count)
            {
                var dropped = new List<string>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (!result.ContainsKey(chunk[i].VacancyId))
                        dropped.Add($"idx={i}/vacancy={chunk[i].VacancyId}");
                }
                _logger.LogWarning(
                    "Batched reason: {Returned}/{Expected} pairs parsed " +
                    "({ValidationRejects} rejected by validator). Missing: {Missing}",
                    result.Count, chunk.Count, rejectedValidation, string.Join("; ", dropped));
            }
        }
        catch (JsonException jx)
        {
            _logger.LogWarning(jx,
                "Batched reason JSON parse failed for chunk of {Count} (first 200 chars: {Snippet})",
                chunk.Count, json.Length > 200 ? json[..200] : json);
        }
        return result;
    }


    private static bool ValidateAndFixup(
        ref string sEn, ref string sUk,
        ref string gEn, ref string gUk,
        ref string rEn, ref string rUk)
    {
        return TryFix(ref sEn) && TryFix(ref sUk)
            && TryFix(ref gEn) && TryFix(ref gUk)
            && TryFix(ref rEn) && TryFix(ref rUk);

        static bool TryFix(ref string field)
        {
            if (string.IsNullOrWhiteSpace(field)) return false;
            var words = field.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < MinWordsPerSection) return false;
            if (words.Length > MaxWordsPerSection)
            {
                var truncated = string.Join(" ", words.Take(MaxWordsPerSection));
                if (!truncated.EndsWith('.')) truncated += ".";
                field = truncated;
            }
            return true;
        }
    }


    private static object BuildResponseSchema() => new
    {
        type = "OBJECT",
        properties = new
        {
            pairs = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        pair_idx          = new { type = "INTEGER" },
                        strengths_en      = new { type = "STRING" },
                        strengths_uk      = new { type = "STRING" },
                        gaps_en           = new { type = "STRING" },
                        gaps_uk           = new { type = "STRING" },
                        recommendation_en = new { type = "STRING" },
                        recommendation_uk = new { type = "STRING" }
                    },
                    required = new[]
                    {
                        "pair_idx",
                        "strengths_en", "strengths_uk",
                        "gaps_en", "gaps_uk",
                        "recommendation_en", "recommendation_uk"
                    }
                }
            }
        },
        required = new[] { "pairs" }
    };

    private static string ReadString(JsonElement obj, string field)
    {
        if (obj.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? string.Empty;
        return string.Empty;
    }


    public bool UseLegacyV6Prompt { get; set; } = false;

    private string BuildPrompt(IReadOnlyList<BatchedReasonRequest> chunk)
    {
        bool useLegacy = UseLegacyV6Prompt;
        var sb = new StringBuilder(4096);
        sb.AppendLine("You are an expert recruiter explaining job-fit assessments.");
        sb.AppendLine("For each candidate-vacancy pair below, produce a structured explanation");
        sb.AppendLine("in BOTH English and Ukrainian.");
        sb.AppendLine();
        sb.AppendLine("=== OUTPUT FORMAT ===");
        sb.AppendLine();
        sb.AppendLine("Return a COMPACT single-line JSON object of shape {\"pairs\":[...]}.");
        sb.AppendLine("The pairs array contains ONE object per input pair, in INPUT order.");
        sb.AppendLine("Each pair object has these fields (REQUIRED):");
        sb.AppendLine();
        sb.AppendLine("  pair_idx           — integer, 0-based index from INPUT");
        sb.AppendLine("  strengths_en       — 1-2 sentences: WHY the candidate is a fit");
        sb.AppendLine("                       (cite specific matched skills + how they map to the role)");
        sb.AppendLine("  strengths_uk       — same content in Ukrainian");
        sb.AppendLine("  gaps_en            — 1-2 sentences: WHICH must-haves are missing and");
        sb.AppendLine("                       how blocking they actually are (e.g. \"quick to learn\"");
        sb.AppendLine("                       vs \"hard requirement\")");
        sb.AppendLine("  gaps_uk            — same in Ukrainian");
        sb.AppendLine("  recommendation_en  — 1 sentence: should the candidate apply, and what to");
        sb.AppendLine("                       emphasise in their cover letter");
        sb.AppendLine("  recommendation_uk  — same in Ukrainian");
        sb.AppendLine();
        sb.AppendLine("=== RULES ===");
        sb.AppendLine();
        sb.AppendLine("1. FACTUAL ONLY. Cite skills from matched_skills / missing_must_haves verbatim.");
        sb.AppendLine("   Do not invent skills the data doesn't list.");
        sb.AppendLine();
        sb.AppendLine("2. SHOW REASONING. Don't just list — explain WHY a skill matters or doesn't.");
        sb.AppendLine("   E.g. \"6 years FinTech experience directly maps to the billing / API integration");
        sb.AppendLine("   surface\" — not just \"Strengths: FinTech, API\".");
        sb.AppendLine();
        sb.AppendLine("3. ACKNOWLEDGE OVERQUALIFICATION POSITIVELY. Senior on Mid is not a downside.");
        sb.AppendLine();
        sb.AppendLine("4. NO GENERIC PHRASES. Avoid \"Strong match. Strengths: X.\" template phrasing.");
        sb.AppendLine();
        sb.AppendLine("5. CONCISE. 1-2 sentences per field; never more than 3.");
        sb.AppendLine();
        sb.AppendLine("5b. BANNED VOCABULARY — these expose the internal scoring system to the");
        sb.AppendLine("    candidate and MUST NOT appear in any output field:");
        sb.AppendLine("      English: \"anti-flag\", \"anti flag\", \"triggered\", \"anti_flag_penalty\",");
        sb.AppendLine("               \"sub-score\", \"sub_score\", \"penalty triggered\", \"flag fired\"");
        sb.AppendLine("      Ukrainian: \"анти-флаг\", \"анти-прапор\", \"анти флаг\", \"спрацював\",");
        sb.AppendLine("                 \"тригер\", \"sub_score\", \"penalty\", \"штраф спрацював\"");
        sb.AppendLine("    Forbidden template fragments (mechanical-sounding):");
        sb.AppendLine("      \"Strengths: X. Gaps: Y.\" / \"Сильні сторони: X. Прогалини: Y.\"");
        sb.AppendLine("    When a hard blocker fires, NAME the actual thing in plain language.");
        sb.AppendLine("      WRONG: \"Anti-flag for military service triggered.\"");
        sb.AppendLine("      RIGHT: \"This is a uniformed military contract, not a civilian role.\"");
        sb.AppendLine("      WRONG: \"Спрацював анти-флаг громадянства США.\"");
        sb.AppendLine("      RIGHT: \"Позиція тільки для громадян США — формальної перевірки не пройти.\"");
        sb.AppendLine();
        if (!useLegacy)
        {
        sb.AppendLine("─── v6.7.2 — quality guardrails ───");
        sb.AppendLine();
        sb.AppendLine("6. GAP FILTERING — implicit-by-seniority skills NEVER appear in gaps_*.");
        sb.AppendLine("   When candidate_seniority is senior or lead, NEVER list any of these as");
        sb.AppendLine("   missing, even if missing_must_haves contains them:");
        sb.AppendLine("     - \"cross-functional teams\", \"crossfunctional collaboration\"");
        sb.AppendLine("     - \"communication skills\", \"stakeholder management\"");
        sb.AppendLine("     - \"leadership\", \"mentorship\", \"team management\"");
        sb.AppendLine("     - \"problem-solving\", \"decision-making\", \"critical thinking\"");
        sb.AppendLine("     - \"analytical skills\", \"strategic thinking\"");
        sb.AppendLine("     - generic process words: \"prioritization\", \"backlog management\",");
        sb.AppendLine("       \"product lifecycle\", \"delivery\", \"product fundamentals\"");
        sb.AppendLine("     - PM craft elements implicit at senior level:");
        sb.AppendLine("       \"user stories\", \"acceptance criteria\", \"product specs\",");
        sb.AppendLine("       \"product requirements\", \"requirements gathering\",");
        sb.AppendLine("       \"product strategy\", \"product vision\", \"product roadmap\",");
        sb.AppendLine("       \"product development\", \"product management\",");
        sb.AppendLine("       \"market research\", \"competitive analysis\", \"competitor analysis\",");
        sb.AppendLine("       \"customer discovery\", \"user research\", \"unit economics\",");
        sb.AppendLine("       \"user-centric approach\", \"user-centric\", \"data-driven approach\"");
        sb.AppendLine("   These are implicit in the seniority and listing them looks foolish.");
        sb.AppendLine("   For candidate_seniority junior/mid these MAY appear if explicitly listed.");
        sb.AppendLine("   IMPORTANT: also drop tokens that the CV CONTAINS verbatim — if the");
        sb.AppendLine("   matched_skills list shows the candidate has the skill, and a paraphrase");
        sb.AppendLine("   of the same skill appears in missing_must_haves, NEVER mention the");
        sb.AppendLine("   missing variant (Layer-2 matcher granularity gap). E.g. if matched");
        sb.AppendLine("   has \"Competitor analysis\" and missing has \"competitive analysis\", skip");
        sb.AppendLine("   the missing variant entirely.");
        sb.AppendLine();
        sb.AppendLine("7. THEME CLUSTERING — verdict-scaled cap. Do NOT comma-dump 8+ missing items.");
        sb.AppendLine("   Group the most blocking gaps into thematic clusters and name the cluster");
        sb.AppendLine("   (e.g. \"domain-specific stack (BESS, PV, VPP)\" or");
        sb.AppendLine("   \"enterprise auth/security (RBAC, OAuth, audit logging)\").");
        sb.AppendLine("   Max theme count depends on verdict (see Rule 12):");
        sb.AppendLine("     StrongMatch  (score ≥ 0.75)       → AT MOST 2 themes (frame as nice-to-haves)");
        sb.AppendLine("     PartialMatch (0.50 ≤ score < 0.75) → AT MOST 3 themes");
        sb.AppendLine("     WeakMatch / Mismatch (score < 0.50) → AT MOST 3 themes (blunt tone OK)");
        sb.AppendLine("   After the cap stop — even if more gaps remain.");
        sb.AppendLine();
        sb.AppendLine("8. LANGUAGE-GAP HANDLING (vacancy CEFR > CV CEFR). When vacancy_english_required");
        sb.AppendLine("   is HIGHER than candidate_english_level, mention this AS A SEPARATE");
        sb.AppendLine("   CLAUSE (\"the role calls for C1 vs the candidate's B2 — a real language");
        sb.AppendLine("   stretch\"), NEVER as a missing SKILL (\"the candidate lacks English\" is");
        sb.AppendLine("   absurd and reads as machine output). NEVER write \"English\" as a missing");
        sb.AppendLine("   must-have even if it appears in missing_must_haves.");
        sb.AppendLine("   IF candidate_english_level >= vacancy_english_required (CEFR order");
        sb.AppendLine("   A1 < A2 < B1 < B2 < C1 < C2 < native), do NOT mention English at all —");
        sb.AppendLine("   levels match, there is no language story to tell.");
        sb.AppendLine();
        sb.AppendLine("9. NO CYRILLIC IN ENGLISH FIELDS, NO TRANSLITERATED LATIN IN UKRAINIAN.");
        sb.AppendLine("   If a missing_must_have is in Cyrillic (e.g. \"UX-тестування\") and you");
        sb.AppendLine("   reference it in gaps_en, TRANSLATE it (\"UX testing\"). Do NOT leave");
        sb.AppendLine("   Cyrillic in the English text.");
        sb.AppendLine("   Mirror rule for Ukrainian text — keep tool/brand names in LATIN, never");
        sb.AppendLine("   transliterate them to Cyrillic. Examples (FORBIDDEN → CORRECT):");
        sb.AppendLine("     \"Джира\"        → \"JIRA\"");
        sb.AppendLine("     \"Постгрес\"     → \"PostgreSQL\"");
        sb.AppendLine("     \"Реакт\"        → \"React\"");
        sb.AppendLine("     \"Сноуфлейк\"    → \"Snowflake\"");
        sb.AppendLine("     \".НЕТ\"         → \".NET\"");
        sb.AppendLine();
        sb.AppendLine("10. UK = NATIVE PARAPHRASE, NEVER MACHINE TRANSLATION. Write Ukrainian as");
        sb.AppendLine("    an experienced UA IT recruiter would phrase it — not word-for-word from EN.");
        sb.AppendLine("    Specific forbidden calques (REPLACEMENTS shown):");
        sb.AppendLine("      \"доставка\"               (delivery) → \"поставка\" / \"реліз\" / \"впровадження\"");
        sb.AppendLine("      \"інтенсивні дані\"        → \"високонавантажені дані\"");
        sb.AppendLine("      \"інтеграційно-насичений\" → \"зі складними інтеграціями\"");
        sb.AppendLine("      \"Їхній\"/\"їхньої\" як they → use \"кандидат(ка)\" or rewrite without pronoun");
        sb.AppendLine("    Use \"кандидат(ка)\" for gender-neutral reference, NEVER \"вони\" / \"їхній\".");
        sb.AppendLine();
        sb.AppendLine("11. VARIED RECOMMENDATION PHRASING. Do NOT close every recommendation with");
        sb.AppendLine("    \"the candidate should apply, highlighting X, demonstrating willingness");
        sb.AppendLine("    to learn Y\". Vary between: direct apply advice, what to lead the cover");
        sb.AppendLine("    letter with, specific interview prep angle, or candid \"this is a stretch");
        sb.AppendLine("    — apply only if domain X excites you\". Pattern-template = automatic fail.");
        sb.AppendLine();
        sb.AppendLine("12. VERDICT-COHERENT GAP TONE. The `score` and `verdict` line is TRUTH —");
        sb.AppendLine("    gap framing MUST match. Re-read the per-pair `score (Verdict)` line before");
        sb.AppendLine("    writing gaps_*. Verdict labels emitted by the system: StrongMatch /");
        sb.AppendLine("    PartialMatch / WeakMatch / Mismatch.");
        sb.AppendLine();
        sb.AppendLine("    StrongMatch (score ≥ 0.75):");
        sb.AppendLine("      - gaps_* MAX 2 items / 1 cluster, framed as \"worth brushing up on …\"");
        sb.AppendLine("        or \"nice-to-have polish\", NEVER as blockers.");
        sb.AppendLine("      - In gaps_* the following words/phrases are FORBIDDEN:");
        sb.AppendLine("        \"fundamental\", \"critical\", \"essential\", \"missing must-have\",");
        sb.AppendLine("        \"hard requirement\", \"основні відсутні\", \"критично відсутні\",");
        sb.AppendLine("        \"фундаментальн-\". (They contradict the StrongMatch verdict.)");
        sb.AppendLine("      - ACRONYM COLLAPSE — domain-neutral pattern:");
        sb.AppendLine("        If the remaining gaps are all short tool-acronyms / capitalized tool");
        sb.AppendLine("        names (≤5 chars ALL-CAPS) AND matched_skills already cover the SAME");
        sb.AppendLine("        functional area, COLLAPSE into ONE descriptive cluster phrase. Examples");
        sb.AppendLine("        across roles (NOT a hardcoded list — the pattern generalizes):");
        sb.AppendLine("          PMM / Growth:  MRR / SEO / PPC / SDR / LTV  + analytics/growth matched");
        sb.AppendLine("                         → \"specific marketing-ops tooling\"");
        sb.AppendLine("                           / \"особлива маркетинг-ops інструменталка\"");
        sb.AppendLine("          Backend / Infra: JWT / gRPC / REST / S3 / RDS + APIs/cloud matched");
        sb.AppendLine("                         → \"specific API/cloud surface tooling\"");
        sb.AppendLine("                           / \"особливі API/cloud інструменти\"");
        sb.AppendLine("          DevOps:        K8s / IaC / ECS / RBAC + CI-CD/container matched");
        sb.AppendLine("                         → \"specific orchestration / IaC tooling\"");
        sb.AppendLine("                           / \"особлива оркестрація / IaC інструменталка\"");
        sb.AppendLine("          Data / ML:     ETL / ELT / DWH / NLP / LLM + pipelines/modeling matched");
        sb.AppendLine("                         → \"specific data-stack / modeling tooling\"");
        sb.AppendLine("                           / \"особлива data-stack інструменталка\"");
        sb.AppendLine("          QA:            BDD / TDD / SUT / UAT + automation/testing matched");
        sb.AppendLine("                         → \"specific test-process tooling\"");
        sb.AppendLine("                           / \"особливі test-process інструменти\"");
        sb.AppendLine("        Collapse summarizes existing items — it does NOT violate Rule 1");
        sb.AppendLine("        (which forbids inventing skills, not naming clusters).");
        sb.AppendLine("      - If after collapse no meaningful gap remains, write a SHORT honest");
        sb.AppendLine("        gaps_* like \"no material gaps — strong match\" / \"суттєвих прогалин");
        sb.AppendLine("        немає — сильна відповідність\". Empty string is also acceptable.");
        sb.AppendLine();
        sb.AppendLine("    PartialMatch (0.50 ≤ score < 0.75):");
        sb.AppendLine("      - Up to 3 named gaps. Honest \"hard requirement\" framing OK if true.");
        sb.AppendLine();
        sb.AppendLine("    WeakMatch / Mismatch (score < 0.50):");
        sb.AppendLine("      - Full enumeration allowed; blunt tone OK.");
        sb.AppendLine();
        sb.AppendLine("13. VERDICT-COHERENT RECOMMENDATION TONE. recommendation_* MUST mirror the");
        sb.AppendLine("    verdict label (StrongMatch / PartialMatch / WeakMatch / Mismatch) shown");
        sb.AppendLine("    in the per-pair header. No hedge that contradicts it:");
        sb.AppendLine("      StrongMatch  → unambiguous APPLY. NEVER \"apply if willing to learn the");
        sb.AppendLine("                     fundamentals\" / \"необхідно вивчити базові …\" — that");
        sb.AppendLine("                     contradicts StrongMatch and reads as if the model didn't");
        sb.AppendLine("                     trust the score. Focus on cover-letter angle, interview");
        sb.AppendLine("                     prep, or which strength to lead with.");
        sb.AppendLine("      PartialMatch → conditional \"apply if X / be ready to discuss Y\".");
        sb.AppendLine("      WeakMatch    → candid \"this is a stretch — apply only if domain X excites\".");
        sb.AppendLine("      Mismatch     → suggest passing or pivoting; do NOT pretend it's salvageable.");
        sb.AppendLine();
        }
        sb.AppendLine("=== COMPACT OUTPUT (single line, no whitespace) ===");
        sb.AppendLine();
        sb.AppendLine("Example shape (multi-line here for readability, but RETURN COMPACT):");
        sb.AppendLine("{\"pairs\":[");
        sb.AppendLine("  {\"pair_idx\":0,\"strengths_en\":\"...\",\"strengths_uk\":\"...\",\"gaps_en\":\"...\",");
        sb.AppendLine("  \"gaps_uk\":\"...\",\"recommendation_en\":\"...\",\"recommendation_uk\":\"...\"}");
        sb.AppendLine("]}");
        sb.AppendLine();
        sb.AppendLine("=== INPUT — pairs to explain ===");
        sb.AppendLine();

        for (int i = 0; i < chunk.Count; i++)
        {
            var r = chunk[i];
            sb.AppendLine($"--- pair_idx {i} ---");
            sb.AppendLine($"vacancy_title:  {r.VacancyTitle}");
            sb.AppendLine($"score:          {r.Score:F3}  ({r.Verdict})");
            sb.AppendLine($"matched_skills: {(r.Evidence.MatchedSkills.Count == 0 ? "(none)" : string.Join(", ", r.Evidence.MatchedSkills.Take(10)))}");
            sb.AppendLine($"missing_must_haves: {(r.Evidence.MissingMustHaves.Count == 0 ? "(none)" : string.Join(", ", r.Evidence.MissingMustHaves.Take(8)))}");
            if (r.Evidence.TriggeredAntiFlags.Count > 0)
                sb.AppendLine($"anti_flags: {string.Join(", ", r.Evidence.TriggeredAntiFlags)}");
            if (r.Context.CandidateYearsOfExperience.HasValue)
                sb.AppendLine($"candidate_years: {r.Context.CandidateYearsOfExperience}");
            if (r.Context.VacancyRequiredYears.HasValue)
                sb.AppendLine($"vacancy_min_years: {r.Context.VacancyRequiredYears}");
            if (r.Context.OverqualifiedByYears.HasValue)
                sb.AppendLine($"overqualified_by_years: {r.Context.OverqualifiedByYears} (positive framing — no penalty)");
            if (r.Context.UnderqualifiedByYears.HasValue)
                sb.AppendLine($"underqualified_by_years: {r.Context.UnderqualifiedByYears}");
            if (!string.IsNullOrWhiteSpace(r.Context.CandidateSeniority))
                sb.AppendLine($"candidate_seniority: {r.Context.CandidateSeniority}");
            if (!string.IsNullOrWhiteSpace(r.Context.VacancySeniority))
                sb.AppendLine($"vacancy_seniority: {r.Context.VacancySeniority}");
            if (!string.IsNullOrWhiteSpace(r.Context.VacancyRoleEn))
                sb.AppendLine($"vacancy_role: {r.Context.VacancyRoleEn}");
            if (r.Context.CandidateTargetRoles.Count > 0)
                sb.AppendLine($"candidate_target_roles: {string.Join(", ", r.Context.CandidateTargetRoles.Take(3))}");
            if (r.Context.CrossDomainTransition)
                sb.AppendLine("cross_domain_transition: true (acknowledge transfer risk)");


            if (!string.IsNullOrWhiteSpace(r.Context.CandidateEnglishLevel))
                sb.AppendLine($"candidate_english_level: {r.Context.CandidateEnglishLevel}");
            if (!string.IsNullOrWhiteSpace(r.Context.VacancyEnglishRequired))
                sb.AppendLine($"vacancy_english_required: {r.Context.VacancyEnglishRequired}");
            sb.AppendLine();
        }

        sb.AppendLine("Return the JSON object now. ONLY the JSON, no prose, no markdown fences.");
        return sb.ToString();
    }
}
