using System.Text.Json;

namespace EvalTool.Grading;


public interface IFieldGrader
{

    string FieldPath { get; }

    double Grade(JsonElement? actual, JsonElement? expected);
}


public sealed class ExactStringGrader : IFieldGrader
{
    public string FieldPath { get; }
    public ExactStringGrader(string fieldPath) => FieldPath = fieldPath;

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var a = ReadString(actual);
        var e = ReadString(expected);
        if (a is null && e is null) return 1.0;
        if (a is null || e is null) return 0.0;
        return string.Equals(a, e, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }

    private static string? ReadString(JsonElement? el)
    {
        if (el is null) return null;
        var v = el.Value;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString()?.Trim();
    }
}


public sealed class BooleanGrader : IFieldGrader
{
    public string FieldPath { get; }
    public BooleanGrader(string fieldPath) => FieldPath = fieldPath;

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var a = ReadBool(actual);
        var e = ReadBool(expected);
        if (a is null && e is null) return 1.0;
        if (a is null || e is null) return 0.0;
        return a.Value == e.Value ? 1.0 : 0.0;
    }

    private static bool? ReadBool(JsonElement? el)
    {
        if (el is null) return null;
        var v = el.Value;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}


public sealed class IntegerToleranceGrader : IFieldGrader
{
    public string FieldPath { get; }
    public int Tolerance { get; }
    public IntegerToleranceGrader(string fieldPath, int tolerance)
    {
        FieldPath = fieldPath;
        Tolerance = tolerance;
    }

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var a = ReadInt(actual);
        var e = ReadInt(expected);
        if (a is null && e is null) return 1.0;
        if (a is null || e is null) return 0.0;
        return Math.Abs(a.Value - e.Value) <= Tolerance ? 1.0 : 0.0;
    }

    private static int? ReadInt(JsonElement? el)
    {
        if (el is null) return null;
        var v = el.Value;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.Number) return null;
        if (!v.TryGetInt32(out var i)) return null;
        return i;
    }
}


public sealed class StringArrayF1Grader : IFieldGrader
{
    public string FieldPath { get; }
    public StringArrayF1Grader(string fieldPath) => FieldPath = fieldPath;

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var actualSet = ExtractStringSet(actual);
        var expectedSet = ExtractStringSet(expected);
        return F1Score(actualSet, expectedSet);
    }

    public static double F1Score(HashSet<string> actual, HashSet<string> expected)
    {
        if (actual.Count == 0 && expected.Count == 0) return 1.0;
        if (actual.Count == 0 || expected.Count == 0) return 0.0;

        int tp = actual.Intersect(expected, StringComparer.OrdinalIgnoreCase).Count();
        int fp = actual.Except(expected, StringComparer.OrdinalIgnoreCase).Count();
        int fn = expected.Except(actual, StringComparer.OrdinalIgnoreCase).Count();

        double precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
        double recall    = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        if (precision + recall == 0) return 0;
        return 2 * precision * recall / (precision + recall);
    }

    private static HashSet<string> ExtractStringSet(JsonElement? element)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (element is null || element.Value.ValueKind != JsonValueKind.Array) return set;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var s = item.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s)) set.Add(s);
        }
        return set;
    }
}


public sealed class RoleArrayF1Grader : IFieldGrader
{
    public string FieldPath { get; }
    public RoleArrayF1Grader(string fieldPath) => FieldPath = fieldPath;

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var actualSet = ExtractAndNormalise(actual);
        var expectedSet = ExtractAndNormalise(expected);
        return StringArrayF1Grader.F1Score(actualSet, expectedSet);
    }

    private static readonly string[] SeniorityPrefixes =
    {
        "head of ", "director of ", "vp of ", "vice president of ",
        "junior ", "mid ", "middle ", "senior ", "staff ", "principal ",
        "lead ", "chief ", "associate "
    };


    private static readonly (string from, string to)[] Synonyms =
    {
        (" developer", " engineer"),
        (" dev",       " engineer"),
        (" specialist"," engineer")
    };

    private static string Normalise(string s)
    {
        var t = s.Trim().ToLowerInvariant();


        foreach (var p in SeniorityPrefixes)
        {
            if (t.StartsWith(p))
            {
                t = t.Substring(p.Length);
                break;
            }
        }


        foreach (var (from, to) in Synonyms)
        {
            if (t.EndsWith(from)) t = t.Substring(0, t.Length - from.Length) + to;
        }


        while (t.Contains("  ")) t = t.Replace("  ", " ");
        return t.Trim();
    }

    private static HashSet<string> ExtractAndNormalise(JsonElement? element)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (element is null || element.Value.ValueKind != JsonValueKind.Array) return set;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var raw = item.GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var norm = Normalise(raw);
            if (!string.IsNullOrEmpty(norm)) set.Add(norm);
        }
        return set;
    }
}


public sealed class LanguagesGrader : IFieldGrader
{
    public string FieldPath { get; }
    public LanguagesGrader(string fieldPath) => FieldPath = fieldPath;

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var actualSet = ExtractTupleSet(actual);
        var expectedSet = ExtractTupleSet(expected);
        return StringArrayF1Grader.F1Score(actualSet, expectedSet);
    }

    private static HashSet<string> ExtractTupleSet(JsonElement? element)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (element is null || element.Value.ValueKind != JsonValueKind.Array) return set;

        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var lang = item.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String
                ? l.GetString()?.Trim() : null;
            var level = item.TryGetProperty("level", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()?.Trim() : null;
            if (!string.IsNullOrEmpty(lang) && !string.IsNullOrEmpty(level))
                set.Add($"{lang}/{level}");
        }
        return set;
    }
}


public sealed class JaccardStringGrader : IFieldGrader
{
    public string FieldPath { get; }
    public JaccardStringGrader(string fieldPath) => FieldPath = fieldPath;

    private static readonly HashSet<string> SeniorityTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "junior", "jr", "middle", "mid", "senior", "sr", "lead", "principal",
        "staff", "intern", "trainee", "strong", "head", "chief",
        "молодший", "старший", "провідний", "стажер", "інтерн"
    };

    private static readonly HashSet<string> SuffixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "engineer", "інженер", "developer", "розробник",
        "spec", "specialist", "спеціаліст"
    };

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var a = Tokenize(ReadString(actual));
        var e = Tokenize(ReadString(expected));
        if (a.Count == 0 && e.Count == 0) return 1.0;
        if (a.Count == 0 || e.Count == 0) return 0.0;

        int intersect = a.Intersect(e).Count();
        int union = a.Union(e).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }

    private static HashSet<string> Tokenize(string? s)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(s)) return set;
        var lower = s.ToLowerInvariant();

        var cleaned = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
            cleaned.Append(char.IsLetterOrDigit(c) || c == '.' || c == '#' || c == '+' ? c : ' ');
        var tokens = cleaned.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            if (t.Length < 2) continue;
            if (SeniorityTokens.Contains(t)) continue;
            if (SuffixTokens.Contains(t)) continue;
            set.Add(t);
        }
        return set;
    }

    private static string? ReadString(JsonElement? el)
    {
        if (el is null) return null;
        var v = el.Value;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString();
    }
}


public sealed class CommaTokenJaccardGrader : IFieldGrader
{
    public string FieldPath { get; }
    public CommaTokenJaccardGrader(string fieldPath) => FieldPath = fieldPath;

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var a = Split(ReadString(actual));
        var e = Split(ReadString(expected));
        if (a.Count == 0 && e.Count == 0) return 1.0;
        if (a.Count == 0 || e.Count == 0) return 0.0;

        int intersect = a.Intersect(e, StringComparer.OrdinalIgnoreCase).Count();
        int union = a.Union(e, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }

    private static HashSet<string> Split(string? s)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(s)) return set;
        foreach (var part in s.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = part.Trim().ToLowerInvariant();
            if (t.Length > 0) set.Add(t);
        }
        return set;
    }

    private static string? ReadString(JsonElement? el)
    {
        if (el is null) return null;
        var v = el.Value;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString();
    }
}


public sealed class StringArrayFBetaGrader : IFieldGrader
{
    public string FieldPath { get; }
    public double Beta { get; }
    public StringArrayFBetaGrader(string fieldPath, double beta = 2.0)
    {
        FieldPath = fieldPath;
        Beta = beta;
    }

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var actualSet = Extract(actual);
        var expectedSet = Extract(expected);
        if (actualSet.Count == 0 && expectedSet.Count == 0) return 1.0;
        if (actualSet.Count == 0 || expectedSet.Count == 0) return 0.0;

        int tp = actualSet.Intersect(expectedSet, StringComparer.OrdinalIgnoreCase).Count();
        int fp = actualSet.Except(expectedSet, StringComparer.OrdinalIgnoreCase).Count();
        int fn = expectedSet.Except(actualSet, StringComparer.OrdinalIgnoreCase).Count();

        double precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
        double recall    = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        if (precision + recall == 0) return 0;

        double beta2 = Beta * Beta;
        return (1 + beta2) * precision * recall / (beta2 * precision + recall);
    }

    private static HashSet<string> Extract(JsonElement? el)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (el is null || el.Value.ValueKind != JsonValueKind.Array) return set;
        foreach (var item in el.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var s = item.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s)) set.Add(s);
        }
        return set;
    }
}


public sealed class CEFRToleranceGrader : IFieldGrader
{
    public string FieldPath { get; }
    public int Tolerance { get; }

    public CEFRToleranceGrader(string fieldPath, int tolerance = 1)
    {
        FieldPath = fieldPath;
        Tolerance = tolerance;
    }


    private static readonly Dictionary<string, int> Ladder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A1"] = 1, ["A2"] = 2, ["B1"] = 3, ["B2"] = 4, ["C1"] = 5, ["C2"] = 6, ["native"] = 7
    };

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var a = ReadString(actual);
        var e = ReadString(expected);
        if (a is null && e is null) return 1.0;
        if (a is null || e is null) return 0.0;
        if (string.Equals(a, e, StringComparison.OrdinalIgnoreCase)) return 1.0;
        if (a.Equals("not_specified", StringComparison.OrdinalIgnoreCase) ||
            e.Equals("not_specified", StringComparison.OrdinalIgnoreCase))
            return 0.0;
        if (!Ladder.TryGetValue(a, out var ai) || !Ladder.TryGetValue(e, out var ei))
            return 0.0;
        int diff = Math.Abs(ai - ei);
        if (diff == 0) return 1.0;
        if (diff <= Tolerance) return 0.5;
        return 0.0;
    }

    private static string? ReadString(JsonElement? el)
    {
        if (el is null) return null;
        var v = el.Value;
        if (v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString()?.Trim();
    }
}


public sealed class TokenJaccardArrayFBetaGrader : IFieldGrader
{
    public string FieldPath { get; }
    public double Beta { get; }
    public double MatchThreshold { get; }

    public TokenJaccardArrayFBetaGrader(string fieldPath, double beta = 2.0, double matchThreshold = 0.5)
    {
        FieldPath = fieldPath;
        Beta = beta;
        MatchThreshold = matchThreshold;
    }

    public double Grade(JsonElement? actual, JsonElement? expected)
    {
        var actualSkills = Tokenize(actual);
        var expectedSkills = Tokenize(expected);
        if (actualSkills.Count == 0 && expectedSkills.Count == 0) return 1.0;
        if (actualSkills.Count == 0 || expectedSkills.Count == 0) return 0.0;

        int tp = 0;
        var used = new HashSet<int>();
        foreach (var exp in expectedSkills)
        {
            int bestIdx = -1;
            double bestScore = 0;
            for (int i = 0; i < actualSkills.Count; i++)
            {
                if (used.Contains(i)) continue;
                double score = Jaccard(exp, actualSkills[i]);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            if (bestScore >= MatchThreshold && bestIdx >= 0)
            {
                tp++;
                used.Add(bestIdx);
            }
        }

        int fp = actualSkills.Count - tp;
        int fn = expectedSkills.Count - tp;
        double precision = tp + fp > 0 ? (double)tp / (tp + fp) : 0;
        double recall    = tp + fn > 0 ? (double)tp / (tp + fn) : 0;
        if (precision + recall == 0) return 0;

        double beta2 = Beta * Beta;
        return (1 + beta2) * precision * recall / (beta2 * precision + recall);
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        int intersect = a.Intersect(b).Count();
        int union = a.Union(b).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }

    private static List<HashSet<string>> Tokenize(JsonElement? element)
    {
        var result = new List<HashSet<string>>();
        if (element is null || element.Value.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var raw = item.GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var tokens = SplitTokens(raw);
            if (tokens.Count > 0) result.Add(tokens);
        }
        return result;
    }

    private static HashSet<string> SplitTokens(string skill)
    {
        var lower = skill.ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
        {


            if (char.IsLetterOrDigit(c) || c == '#' || c == '+')
                sb.Append(c);
            else
                sb.Append(' ');
        }
        var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parts)
        {
            if (p.Length >= 2 || p == "c" || p == "r")
                set.Add(p);
        }
        return set;
    }
}
