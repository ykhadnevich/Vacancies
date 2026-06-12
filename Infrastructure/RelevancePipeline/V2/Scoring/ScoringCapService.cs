using Application.Common.Interfaces;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public sealed class ScoringCapService : IScoringCapService
{
    public double Floor   => 0.20;
    public double Ceiling => 0.88;

    public double ApplyCaps(double rawScore, SubScores subScores, bool languageGapPenalty)
    {
        double score = rawScore;
        double cap = double.MaxValue;


        if (subScores.SeniorityMatch <= 0.15) cap = System.Math.Min(cap, 0.25);
        else if (subScores.SeniorityMatch <= 0.30) cap = System.Math.Min(cap, 0.35);


        if (languageGapPenalty)
        {
            if (subScores.LanguageMatch <= 0.40) cap = System.Math.Min(cap, 0.40);
            else if (subScores.LanguageMatch <= 0.70) cap = System.Math.Min(cap, 0.55);
        }


        if (subScores.RoleIntentMatch <= 0.30) cap = System.Math.Min(cap, 0.50);


        // Experience cap — AGGRESSIVE. The vacancy requires N years and the
        // candidate has roughly 0 — the recruiter must see "no realistic
        // chance", not "borderline match". Without this guard the composite
        // sits ~55% just from theory knowledge of methodologies.
        if (subScores.ExperienceMatch <= 0.15) cap = System.Math.Min(cap, 0.30);
        else if (subScores.ExperienceMatch <= 0.30) cap = System.Math.Min(cap, 0.40);
        else if (subScores.ExperienceMatch <= 0.50) cap = System.Math.Min(cap, 0.60);

        // Combined cap — catastrophic mismatch. When BOTH "no production
        // experience" AND "seniority gap" fire, the candidate is unambiguously
        // not for this role.
        //
        // Threshold 0.70 (NOT 0.50) on seniority_match — Gemini empirically
        // softens junior→senior to 0.70 (the "±1 level" rule) instead of the
        // strict 0.30 (the "±2 level" rule). Yan's CV is target=junior PM on a
        // Senior PM posting and gets seniority_match=0.70 — the looser
        // threshold catches this honestly.
        //
        // SAFETY: overqualified cases (Senior CV on Junior posting) have
        // experience_match ≈ 1.0 (5+ years vs implied 1y junior is plenty),
        // so the first condition (≤ 0.30) is false and this guard stays silent.
        // The existing role-intent cap still handles overqualification.
        if (subScores.ExperienceMatch <= 0.30 && subScores.SeniorityMatch <= 0.70)
            cap = System.Math.Min(cap, 0.25);

        score = System.Math.Min(score, cap);


        if (subScores.DomainAlignment <= 0.50) score -= 0.05;

        return System.Math.Clamp(score, Floor, Ceiling);
    }
}
