using Application.Common.CvNormalization;
using Infrastructure.RelevancePipeline.V2.CvNormalization;

namespace Infrastructure.Tests.RelevancePipeline.V2.CvNormalization;

public class CvNormalizationPromptCoreTests
{
    private static CvNormalizationSlots SampleSlots() => new(
        SeniorityBands: "SAMPLE_SENIORITY_BANDS",
        EducationRelevanceGuide: "SAMPLE_EDU_GUIDE",
        TargetRolesGuidance: "SAMPLE_TARGET_ROLES",
        ExperienceTypeNotes: "SAMPLE_EXP_NOTES",
        CanonicalizationExamples: "SAMPLE_CANON_EX",
        FullWorkedExample: "SAMPLE_WORKED_EXAMPLE");

    [Fact]
    public void Build_InjectsCvText()
    {
        var prompt = CvNormalizationPromptCore.Build(
            "UNIQUE_CV_TEXT_MARKER", SampleSlots());

        Assert.Contains("UNIQUE_CV_TEXT_MARKER", prompt);
    }

    [Fact]
    public void Build_InjectsAllSixSlots()
    {
        var prompt = CvNormalizationPromptCore.Build("cv body", SampleSlots());

        Assert.Contains("SAMPLE_SENIORITY_BANDS", prompt);
        Assert.Contains("SAMPLE_EDU_GUIDE", prompt);
        Assert.Contains("SAMPLE_TARGET_ROLES", prompt);
        Assert.Contains("SAMPLE_EXP_NOTES", prompt);
        Assert.Contains("SAMPLE_CANON_EX", prompt);
        Assert.Contains("SAMPLE_WORKED_EXAMPLE", prompt);
    }

    [Fact]
    public void Build_SkipsExampleBlock_WhenSlotIsEmpty()
    {
        var slots = SampleSlots() with { FullWorkedExample = "" };
        var prompt = CvNormalizationPromptCore.Build("cv body", slots);


        Assert.Contains("SAMPLE_SENIORITY_BANDS", prompt);

        Assert.DoesNotContain("FULL WORKED EXAMPLE", prompt);
    }

    [Fact]
    public void Build_ContainsUniversalProcedureSections()
    {
        var prompt = CvNormalizationPromptCore.Build("cv body", SampleSlots());


        Assert.Contains("A. EXPERIENCE", prompt);
        Assert.Contains("B. SKILLS", prompt);
        Assert.Contains("C. OTHER FIELDS", prompt);
        Assert.Contains("B1. EXTRACT", prompt);
        Assert.Contains("B2. CANONICALIZE", prompt);
        Assert.Contains("B3. CLASSIFY", prompt);
        Assert.Contains("Q1.", prompt);
        Assert.Contains("Q2.", prompt);
        Assert.Contains("Q3.", prompt);
        Assert.Contains("Q4.", prompt);
    }

    [Fact]
    public void Build_ContainsExperienceTypeTaxonomy()
    {
        var prompt = CvNormalizationPromptCore.Build("cv body", SampleSlots());

        Assert.Contains("PRODUCTION", prompt);
        Assert.Contains("FREELANCE", prompt);
        Assert.Contains("INTERNSHIP", prompt);
        Assert.Contains("PET_PROJECT", prompt);
        Assert.Contains("COURSE", prompt);
    }

    [Fact]
    public void Build_ContainsGraduationYearInferenceRule()
    {
        var prompt = CvNormalizationPromptCore.Build("cv body", SampleSlots());


        Assert.Contains("graduation_year", prompt);
        Assert.Contains("infer", prompt);
    }

    [Fact]
    public void Build_DeterministicOnSameInput()
    {
        var slots = SampleSlots();

        var first = CvNormalizationPromptCore.Build("cv body", slots);
        var second = CvNormalizationPromptCore.Build("cv body", slots);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Build_HandlesEmptyOptionalSlotGracefully()
    {


        var slotsWithEmpty = SampleSlots() with { ExperienceTypeNotes = "" };

        var prompt = CvNormalizationPromptCore.Build("cv body", slotsWithEmpty);


        Assert.Contains("PRODUCTION", prompt);

        Assert.DoesNotContain("\n  \n", prompt);
    }

    [Fact]
    public void Version_IsStableContractedConstant()
    {


        Assert.Equal("v5_1_confidence", CvNormalizationPromptCore.Version);
    }
}
