namespace Domain.Scoring;

public enum Verdict
{
    Mismatch,
    WeakMatch,
    PartialMatch,
    StrongMatch
}

public static class VerdictExtensions
{
    public static Verdict FromScore(double score) => score switch
    {
        >= 0.75 => Verdict.StrongMatch,
        >= 0.50 => Verdict.PartialMatch,
        >= 0.25 => Verdict.WeakMatch,
        _       => Verdict.Mismatch
    };

    public static string ToEnglishText(this Verdict v) => v switch
    {
        Verdict.StrongMatch  => "Strong match",
        Verdict.PartialMatch => "Partial match",
        Verdict.WeakMatch    => "Weak match",
        _                    => "Mismatch"
    };

    public static string ToUkrainianText(this Verdict v) => v switch
    {
        Verdict.StrongMatch  => "Сильна відповідність",
        Verdict.PartialMatch => "Часткова відповідність",
        Verdict.WeakMatch    => "Слабка відповідність",
        _                    => "Невідповідність"
    };


    public static string ToShortName(this Verdict v) => v switch
    {
        Verdict.StrongMatch  => "Strong",
        Verdict.PartialMatch => "Partial",
        Verdict.WeakMatch    => "Weak",
        _                    => "Mismatch"
    };
}
