using System.Text.Json;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;

namespace Infrastructure.Tests.RelevancePipeline.V2.Scoring.Monolithic;


public class MonolithicScoringServiceTests
{
    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();


    [Fact]
    public void ComputeWeightedSum_AllOnes_EqualsOne()
    {
        var subs = new SubScores(1, 1, 1, 1, 1, 1, 1);

        var sum = MonolithicScoringService.ComputeWeightedSum(subs);

        Assert.Equal(1.0, sum, precision: 6);
    }


    [Fact]
    public void ComputeWeightedSum_AllZeros_EqualsZero()
    {
        var subs = new SubScores(0, 0, 0, 0, 0, 0, 0);

        var sum = MonolithicScoringService.ComputeWeightedSum(subs);

        Assert.Equal(0.0, sum);
    }


    [Fact]
    public void ComputeWeightedSum_OnlySkill_EqualsSkillWeight()
    {

        var subs = new SubScores(
            SkillMatch:      1.0,
            SeniorityMatch:  0,
            ExperienceMatch: 0,
            LanguageMatch:   0,
            EducationMatch:  0,
            RoleIntentMatch: 0,
            DomainAlignment: 0);

        var sum = MonolithicScoringService.ComputeWeightedSum(subs);

        Assert.Equal(ScoringConstants.LinearWeights.Skill, sum, precision: 6);
    }


    [Fact]
    public void ComputeWeightedSum_Matches_ScoringServiceV2_Formula()
    {

        var subs = new SubScores(0.8, 0.6, 0.7, 0.5, 0.4, 0.9, 0.3);

        var sum = MonolithicScoringService.ComputeWeightedSum(subs);

        var expected =
            0.8 * ScoringConstants.LinearWeights.Skill +
            0.6 * ScoringConstants.LinearWeights.Seniority +
            0.7 * ScoringConstants.LinearWeights.Experience +
            0.5 * ScoringConstants.LinearWeights.Language +
            0.4 * ScoringConstants.LinearWeights.Education +
            0.9 * ScoringConstants.LinearWeights.RoleIntent +
            0.3 * ScoringConstants.LinearWeights.Domain;

        Assert.Equal(expected, sum, precision: 6);
    }


    [Fact]
    public void ParseAndCompose_HappyPath_ProducesScoreEqualToWeightedSum()
    {
        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 0.9,
                "seniority_match": 1.0,
                "experience_match": 0.8,
                "language_match": 1.0,
                "education_match": 1.0,
                "role_intent_match": 0.9,
                "domain_alignment": 0.7
              },
              "anti_flag_penalty": 1.0,
              "matched_skills": ["C#", ".NET"],
              "missing_must_haves": [],
              "triggered_anti_flags": [],
              "reason_en": "Strong .NET fit.",
              "reason_uk": "Сильний .NET збіг."
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, inputTokens: 100, outputTokens: 50);

        Assert.Equal(1.0, result.AntiFlagPenalty);
        Assert.Equal(0.9, result.SubScores.SkillMatch);
        Assert.Equal(0.7, result.SubScores.DomainAlignment);
        Assert.Equal(2, result.Evidence.MatchedSkills.Count);
        Assert.Empty(result.Evidence.MissingMustHaves);
        Assert.Equal("Strong .NET fit.", result.ReasonEn);
        Assert.Equal("Сильний .NET збіг.", result.ReasonUk);


        var expectedSum = MonolithicScoringService.ComputeWeightedSum(result.SubScores);
        Assert.Equal(expectedSum, result.Score, precision: 6);
    }


    [Fact]
    public void ParseAndCompose_AntiFlagPenalty_AppliedMultiplicatively()
    {
        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 1.0,
                "seniority_match": 1.0,
                "experience_match": 1.0,
                "language_match": 1.0,
                "education_match": 1.0,
                "role_intent_match": 1.0,
                "domain_alignment": 1.0
              },
              "anti_flag_penalty": 0.5,
              "matched_skills": [],
              "missing_must_haves": [],
              "triggered_anti_flags": ["onsite only Berlin"],
              "reason_en": "Anti-flag onsite.",
              "reason_uk": "Анти-флаг локація."
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);


        Assert.Equal(0.5, result.Score, precision: 6);
        Assert.Equal(0.5, result.AntiFlagPenalty);
        Assert.Single(result.Evidence.TriggeredAntiFlags);
    }


    [Fact]
    public void ParseAndCompose_MissingSubScoresField_FallsBack()
    {
        var root = Parse("""
            {
              "anti_flag_penalty": 1.0,
              "matched_skills": [],
              "missing_must_haves": [],
              "triggered_anti_flags": [],
              "reason_en": "",
              "reason_uk": ""
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);


        Assert.Equal(0.5, result.Score);
        Assert.Contains("fallback", result.ModelVersion);
        Assert.Contains("missing_sub_scores", result.ModelVersion);
    }


    [Fact]
    public void ParseAndCompose_SubScore_OutOfRange_IsClamped()
    {

        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 1.7,
                "seniority_match": -0.5,
                "experience_match": 0.5,
                "language_match": 0.5,
                "education_match": 0.5,
                "role_intent_match": 0.5,
                "domain_alignment": 0.5
              },
              "anti_flag_penalty": 1.0,
              "matched_skills": [],
              "missing_must_haves": [],
              "triggered_anti_flags": [],
              "reason_en": "",
              "reason_uk": ""
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.Equal(1.0, result.SubScores.SkillMatch);
        Assert.Equal(0.0, result.SubScores.SeniorityMatch);
        Assert.InRange(result.Score, 0.0, 1.0);
    }


    [Fact]
    public void ParseAndCompose_AntiPenalty_OutOfRange_Clamped()
    {
        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 0.5,
                "seniority_match": 0.5,
                "experience_match": 0.5,
                "language_match": 0.5,
                "education_match": 0.5,
                "role_intent_match": 0.5,
                "domain_alignment": 0.5
              },
              "anti_flag_penalty": 2.5,
              "matched_skills": [],
              "missing_must_haves": [],
              "triggered_anti_flags": [],
              "reason_en": "",
              "reason_uk": ""
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.Equal(1.0, result.AntiFlagPenalty);
        Assert.Equal(0.5, result.Score, precision: 6);
    }


    [Fact]
    public void ParseAndCompose_VersionString_TracksFallback()
    {
        var root = Parse("""{"unrelated": true}""");

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.StartsWith(MonolithicScoringService.Version + "_fallback", result.ModelVersion);
    }


    [Fact]
    public void ParseAndCompose_Confidence_Present_IsParsed()
    {
        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 0.8, "seniority_match": 0.8, "experience_match": 0.8,
                "language_match": 0.8, "education_match": 0.8, "role_intent_match": 0.8,
                "domain_alignment": 0.8
              },
              "anti_flag_penalty": 1.0,
              "confidence": 0.65,
              "matched_skills": [], "missing_must_haves": [], "triggered_anti_flags": [],
              "reason_en": "", "reason_uk": ""
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.Equal(0.65, result.Confidence, precision: 4);
    }


    [Fact]
    public void ParseAndCompose_Confidence_Missing_DefaultsToOne()
    {

        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 0.5, "seniority_match": 0.5, "experience_match": 0.5,
                "language_match": 0.5, "education_match": 0.5, "role_intent_match": 0.5,
                "domain_alignment": 0.5
              },
              "anti_flag_penalty": 1.0
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.Equal(1.0, result.Confidence);
    }


    [Fact]
    public void ParseAndCompose_Confidence_OutOfRange_IsClamped()
    {
        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 0.5, "seniority_match": 0.5, "experience_match": 0.5,
                "language_match": 0.5, "education_match": 0.5, "role_intent_match": 0.5,
                "domain_alignment": 0.5
              },
              "anti_flag_penalty": 1.0,
              "confidence": 1.7
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.Equal(1.0, result.Confidence);
    }


    [Fact]
    public void ParseAndCompose_MissingOptionalArrays_DefaultsToEmpty()
    {
        var root = Parse("""
            {
              "sub_scores": {
                "skill_match": 0.5,
                "seniority_match": 0.5,
                "experience_match": 0.5,
                "language_match": 0.5,
                "education_match": 0.5,
                "role_intent_match": 0.5,
                "domain_alignment": 0.5
              },
              "anti_flag_penalty": 1.0
            }
            """);

        var result = MonolithicScoringService.ParseAndCompose(
            "cv-1", Guid.NewGuid(), root, 0, 0);

        Assert.Empty(result.Evidence.MatchedSkills);
        Assert.Empty(result.Evidence.MissingMustHaves);
        Assert.Empty(result.Evidence.TriggeredAntiFlags);
        Assert.Equal(string.Empty, result.ReasonEn);
        Assert.Null(result.ReasonUk);
    }
}
