using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace EvalTool.Baselines;

/// <summary>
/// Non-LLM baseline scorers for CV ↔ vacancy matching. Used by the held-out
/// evaluation pipeline as the floor the production Gemini scoring must clear
/// to justify its architectural complexity.
///
/// Two classical IR baselines are computed:
///
/// 1. <b>TF-IDF cosine</b> over character-level word-boundary n-grams of
///    length 3..5. Sub-linear term-frequency weighting (<c>1 + log(count)</c>),
///    smoothed inverse-document frequency (<c>log((N+1)/(df+1)) + 1</c>),
///    cosine similarity over the resulting sparse vectors. Robust to
///    Ukrainian/English mix because the char-ngram features absorb morphology.
///
/// 2. <b>BM25 Okapi</b> (k1=1.5, b=0.75) with per-CV min-max normalisation
///    to [0,1] so absolute BM25 scores remain comparable across queries.
///
/// No external dependency beyond <see cref="System.Text.RegularExpressions"/>.
/// </summary>
public sealed class BaselineRunner
{
    private readonly ILogger<BaselineRunner> _logger;

    private static readonly JsonSerializerOptions JsonReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private static readonly JsonSerializerOptions JsonWriteOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex TokenRegex = new(
        @"[a-zA-Zа-яА-ЯіІїЇєЄґҐ0-9+#./]+",
        RegexOptions.Compiled);

    public BaselineRunner(ILogger<BaselineRunner> logger) => _logger = logger;

    public async Task RunAsync(
        string goldPath,
        string cvDir,
        string vacancyDir,
        string outputPath,
        CancellationToken ct = default)
    {
        var gold = JsonSerializer.Deserialize<GoldFile>(
            await File.ReadAllTextAsync(goldPath, ct), JsonReadOpts)
            ?? throw new InvalidOperationException("Gold file empty");

        var pairs = gold.Ratings.Select(r => (CvId: r.CvId, VacancyId: r.VacancyId, Gold: r.MatchQuality)).ToList();
        _logger.LogInformation("Loaded {N} pairs from gold", pairs.Count);

        var uniqueCvs = pairs.Select(p => p.CvId).Distinct().OrderBy(x => x).ToList();
        var uniqueVacs = pairs.Select(p => p.VacancyId).Distinct().OrderBy(x => x).ToList();

        var cvText = new Dictionary<string, string>();
        var vacText = new Dictionary<string, string>();
        foreach (var c in uniqueCvs)
            cvText[c] = BuildCvText(await File.ReadAllTextAsync(Path.Combine(cvDir, $"{c}.json"), ct));
        foreach (var v in uniqueVacs)
            vacText[v] = BuildVacancyText(await File.ReadAllTextAsync(Path.Combine(vacancyDir, $"{v}.json"), ct));

        _logger.LogInformation("Unique CVs: {C}, Unique vacancies: {V}", uniqueCvs.Count, uniqueVacs.Count);

        // ── Baseline 1: TF-IDF cosine (char_wb 3..5) ──────────────────────
        _logger.LogInformation("[TF-IDF cosine]");
        var allDocs = uniqueCvs.Select(c => cvText[c])
                               .Concat(uniqueVacs.Select(v => vacText[v]))
                               .ToList();
        var tfidfVectors = ComputeTfIdf(allDocs);
        var cvVecs = uniqueCvs.Select((c, i) => (c, tfidfVectors[i])).ToDictionary(t => t.c, t => t.Item2);
        var vacVecs = uniqueVacs.Select((v, i) => (v, tfidfVectors[uniqueCvs.Count + i]))
                                .ToDictionary(t => t.v, t => t.Item2);

        var tfidfScores = new Dictionary<(string, string), double>();
        foreach (var p in pairs)
            tfidfScores[(p.CvId, p.VacancyId)] = Cosine(cvVecs[p.CvId], vacVecs[p.VacancyId]);
        _logger.LogInformation("  TF-IDF range: [{Lo:F3}, {Hi:F3}]",
            tfidfScores.Values.Min(), tfidfScores.Values.Max());

        // ── Baseline 2: BM25 Okapi ────────────────────────────────────────
        _logger.LogInformation("[BM25 Okapi]");
        var vacTokens = uniqueVacs.Select(v => Tokenize(vacText[v])).ToList();
        var bm25 = new Bm25Okapi(vacTokens);
        var bm25Raw = new Dictionary<(string, string), double>();
        foreach (var c in uniqueCvs)
        {
            var query = Tokenize(cvText[c]);
            var scores = bm25.GetScores(query);
            for (int i = 0; i < uniqueVacs.Count; i++)
                bm25Raw[(c, uniqueVacs[i])] = scores[i];
        }
        // Per-CV min-max normalise to [0,1]
        var bm25Norm = new Dictionary<(string, string), double>();
        foreach (var c in uniqueCvs)
        {
            var perCv = pairs.Where(p => p.CvId == c)
                             .Select(p => bm25Raw[(c, p.VacancyId)])
                             .ToList();
            var allForCv = uniqueVacs.Select(v => bm25Raw[(c, v)]).ToList();
            double lo = allForCv.Min(), hi = allForCv.Max();
            double range = hi - lo;
            foreach (var p in pairs.Where(p => p.CvId == c))
                bm25Norm[(p.CvId, p.VacancyId)] = range > 0
                    ? (bm25Raw[(p.CvId, p.VacancyId)] - lo) / range
                    : 0.0;
        }
        _logger.LogInformation("  BM25 normalised range: [{Lo:F3}, {Hi:F3}]",
            bm25Norm.Values.Min(), bm25Norm.Values.Max());

        // ── Quick sanity Spearman vs gold (full evaluation is in compute-metrics) ──
        var gnArr = pairs.Select(p => (double)p.Gold).ToArray();
        var tfArr = pairs.Select(p => tfidfScores[(p.CvId, p.VacancyId)]).ToArray();
        var bmArr = pairs.Select(p => bm25Norm[(p.CvId, p.VacancyId)]).ToArray();
        var spTf = EvalTool.Metrics.MetricsCalculator.Spearman(tfArr, gnArr);
        var spBm = EvalTool.Metrics.MetricsCalculator.Spearman(bmArr, gnArr);
        _logger.LogInformation("Quick Spearman vs gold (final values in compute-metrics):");
        _logger.LogInformation("  TF-IDF cosine: rho = {Rho:F4}", spTf);
        _logger.LogInformation("  BM25 (norm)  : rho = {Rho:F4}", spBm);

        // ── Save ──────────────────────────────────────────────────────────
        var output = new
        {
            schema_version = "baseline_predictions_v1",
            generated_at = DateTime.UtcNow.ToString("O"),
            n_pairs = pairs.Count,
            baselines = new
            {
                tfidf_cosine = new
                {
                    description = "TF-IDF char_wb 3..5 n-grams, smoothed IDF, sublinear TF, cosine similarity. Multilingual-safe (UK/EN).",
                    score_range = "[0, 1]",
                    spearman_vs_gold_quick = Math.Round(spTf, 4)
                },
                bm25_norm = new
                {
                    description = "BM25 Okapi (k1=1.5, b=0.75) over vacancy corpus, per-CV min-max normalised to [0,1].",
                    score_range = "[0, 1]",
                    spearman_vs_gold_quick = Math.Round(spBm, 4)
                }
            },
            predictions = pairs.Select(p => new
            {
                cv_id = p.CvId,
                vacancy_id = p.VacancyId,
                gold = p.Gold,
                tfidf_cosine = Math.Round(tfidfScores[(p.CvId, p.VacancyId)], 6),
                bm25_norm = Math.Round(bm25Norm[(p.CvId, p.VacancyId)], 6)
            }).ToList()
        };
        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(output, JsonWriteOpts), ct);
        _logger.LogInformation("Saved baseline predictions: {Path}", outputPath);
    }

    // ── Text construction (mirrors what Gemini sees) ────────────────────

    private static string BuildCvText(string cvJson)
    {
        using var doc = JsonDocument.Parse(cvJson);
        var r = doc.RootElement;
        var parts = new List<string>();
        if (r.TryGetProperty("target_roles", out var tr))
            parts.Add(string.Join(' ', tr.EnumerateArray().Select(x => x.GetString() ?? "")));
        foreach (var key in new[] { "domain_skills", "domain_skills_sample", "technical_skills", "unverified_skills" })
            if (r.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                parts.Add(string.Join(' ', arr.EnumerateArray().Select(x => x.GetString() ?? "")));
        if (r.TryGetProperty("seniority", out var sen) && sen.ValueKind == JsonValueKind.String)
            parts.Add($"seniority: {sen.GetString()}");
        if (r.TryGetProperty("education_field", out var ed) && ed.ValueKind == JsonValueKind.String)
            parts.Add($"education: {ed.GetString()}");
        return string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
    }

    private static string BuildVacancyText(string vacancyJson)
    {
        using var doc = JsonDocument.Parse(vacancyJson);
        var r = doc.RootElement;
        var parts = new List<string>();
        parts.Add(GetEnOrUk(r, "role_title"));
        foreach (var key in new[] { "must_have_skills", "nice_to_have_skills" })
            if (r.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                parts.Add(string.Join(' ', arr.EnumerateArray().Select(x => x.GetString() ?? "")));
        if (r.TryGetProperty("seniority_required", out var sen) && sen.ValueKind == JsonValueKind.String)
            parts.Add($"seniority required: {sen.GetString()}");
        parts.Add($"domain: {GetEnOrUk(r, "domain_context")}");
        if (r.TryGetProperty("english_required", out var en) && en.ValueKind == JsonValueKind.String)
            parts.Add($"english: {en.GetString()}");
        return string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
    }

    private static string GetEnOrUk(JsonElement el, string field)
    {
        if (!el.TryGetProperty(field, out var v)) return "";
        if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? "";
        if (v.ValueKind == JsonValueKind.Object)
        {
            if (v.TryGetProperty("en", out var en) && en.ValueKind == JsonValueKind.String) return en.GetString() ?? "";
            if (v.TryGetProperty("uk", out var uk) && uk.ValueKind == JsonValueKind.String) return uk.GetString() ?? "";
        }
        return "";
    }

    private static List<string> Tokenize(string text) =>
        TokenRegex.Matches(text)
                  .Select(m => m.Value.ToLowerInvariant())
                  .Where(t => t.Length >= 2)
                  .ToList();

    // ── TF-IDF (char_wb n-grams) ────────────────────────────────────────

    private static List<Dictionary<string, double>> ComputeTfIdf(List<string> docs)
    {
        // 1) Extract char_wb 3..5 n-grams per doc, compute raw TF
        var docTf = new List<Dictionary<string, double>>(docs.Count);
        var docFreq = new Dictionary<string, int>();
        foreach (var doc in docs)
        {
            var tf = new Dictionary<string, double>();
            foreach (var ng in CharWbNgrams(doc, 3, 5))
            {
                tf[ng] = tf.TryGetValue(ng, out var c) ? c + 1 : 1;
            }
            foreach (var k in tf.Keys.ToList())
                tf[k] = 1 + Math.Log(tf[k]);  // sublinear_tf
            docTf.Add(tf);
            foreach (var term in tf.Keys)
                docFreq[term] = docFreq.TryGetValue(term, out var d) ? d + 1 : 1;
        }

        // 2) IDF (smoothed) and weighted vectors
        int N = docs.Count;
        var idf = new Dictionary<string, double>(docFreq.Count);
        foreach (var (t, df) in docFreq)
            idf[t] = Math.Log((N + 1.0) / (df + 1.0)) + 1.0;

        var weighted = new List<Dictionary<string, double>>(docs.Count);
        foreach (var tf in docTf)
        {
            var vec = new Dictionary<string, double>(tf.Count);
            foreach (var (term, tfVal) in tf)
                vec[term] = tfVal * idf[term];
            weighted.Add(vec);
        }
        return weighted;
    }

    private static IEnumerable<string> CharWbNgrams(string text, int min, int max)
    {
        foreach (var word in text.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            var padded = " " + word + " ";
            for (int n = min; n <= max; n++)
                for (int i = 0; i <= padded.Length - n; i++)
                    yield return padded.Substring(i, n);
        }
    }

    private static double Cosine(Dictionary<string, double> a, Dictionary<string, double> b)
    {
        double dot = 0, na = 0, nb = 0;
        foreach (var v in a.Values) na += v * v;
        foreach (var v in b.Values) nb += v * v;
        // iterate the smaller dict
        var (small, large) = a.Count <= b.Count ? (a, b) : (b, a);
        foreach (var (k, v) in small)
            if (large.TryGetValue(k, out var w)) dot += v * w;
        return na > 0 && nb > 0 ? dot / (Math.Sqrt(na) * Math.Sqrt(nb)) : 0;
    }

    // ── BM25 Okapi ──────────────────────────────────────────────────────

    private sealed class Bm25Okapi
    {
        private const double K1 = 1.5;
        private const double B = 0.75;

        private readonly List<List<string>> _docs;
        private readonly List<Dictionary<string, int>> _docTfs;
        private readonly Dictionary<string, int> _docFreq;
        private readonly double _avgDl;
        private readonly int _n;

        public Bm25Okapi(List<List<string>> docs)
        {
            _docs = docs;
            _n = docs.Count;
            _avgDl = docs.Count > 0 ? docs.Average(d => (double)d.Count) : 0.0;
            _docTfs = new List<Dictionary<string, int>>(_n);
            _docFreq = new Dictionary<string, int>();
            foreach (var doc in docs)
            {
                var tf = new Dictionary<string, int>();
                foreach (var t in doc)
                    tf[t] = tf.TryGetValue(t, out var c) ? c + 1 : 1;
                _docTfs.Add(tf);
                foreach (var t in tf.Keys)
                    _docFreq[t] = _docFreq.TryGetValue(t, out var d) ? d + 1 : 1;
            }
        }

        public double[] GetScores(List<string> query)
        {
            var qDistinct = query.Distinct().ToList();
            var scores = new double[_n];
            for (int i = 0; i < _n; i++)
            {
                double dl = _docs[i].Count;
                double s = 0;
                foreach (var t in qDistinct)
                {
                    if (!_docFreq.TryGetValue(t, out var df)) continue;
                    var idf = Math.Log((_n - df + 0.5) / (df + 0.5) + 1.0);
                    double tf = _docTfs[i].TryGetValue(t, out var c) ? c : 0;
                    s += idf * tf * (K1 + 1) / (tf + K1 * (1 - B + B * dl / _avgDl));
                }
                scores[i] = s;
            }
            return scores;
        }
    }

    // ── DTOs ────────────────────────────────────────────────────────────

    private sealed record GoldFile(
        string SchemaVersion,
        string Rater,
        List<GoldRating> Ratings);

    private sealed record GoldRating(
        string CvId,
        string VacancyId,
        int MatchQuality);
}
