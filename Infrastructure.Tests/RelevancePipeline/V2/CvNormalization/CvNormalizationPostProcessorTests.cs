using System.Text.Json.Nodes;
using Infrastructure.RelevancePipeline.V2.CvNormalization;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Tests.RelevancePipeline.V2.CvNormalization;

public class CvNormalizationPostProcessorTests
{
    private static CvNormalizationPostProcessor MakeProcessor() =>
        new(NullLogger<CvNormalizationPostProcessor>.Instance);

    private static string BaseJson(
        string[]? domain = null,
        string[]? technical = null,
        string[]? unverified = null) =>
        $$"""
        {
          "seniority": "junior",
          "target_roles": ["Junior Product Manager"],
          "domain_skills": {{Arr(domain)}},
          "technical_skills": {{Arr(technical)}},
          "unverified_skills": {{Arr(unverified)}},
          "experience": [],
          "education": {"degree":"bachelor","field":"CS","is_relevant":true,"status":"completed","current_year":null,"graduation_year":2024},
          "english_level": "B2",
          "languages": [{"language":"English","level":"B2"}],
          "has_real_product_experience": false,
          "career_switcher": false
        }
        """;

    private static string Arr(string[]? items) =>
        items is null
            ? "[]"
            : "[" + string.Join(", ", items.Select(s => $"\"{s}\"")) + "]";

    private static List<string> ReadList(string json, string property)
    {
        var node = JsonNode.Parse(json)!;
        return node[property]!.AsArray()
            .Select(n => n!.GetValue<string>()).ToList();
    }

    // ── Canonicalization ────────────────────────────────────────────────────

    [Fact]
    public void Canonicalize_ReplacesKnownVariants()
    {
        var input = BaseJson(
            domain: new[] { "Hypothesis formulation", "Mobile monetization strategies" },
            technical: new[] { "REST API understanding", "SQL for data analysis" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cvRawText: "irrelevant");

        Assert.Contains("Hypothesis validation", ReadList(output, "domain_skills"));
        Assert.Contains("Mobile monetization", ReadList(output, "domain_skills"));
        Assert.Contains("REST API", ReadList(output, "technical_skills"));
        Assert.Contains("SQL", ReadList(output, "technical_skills"));
        // Original variants are gone.
        Assert.DoesNotContain("Hypothesis formulation", ReadList(output, "domain_skills"));
        Assert.DoesNotContain("REST API understanding", ReadList(output, "technical_skills"));
    }

    // ── Role-pattern stripping ──────────────────────────────────────────────

    [Fact]
    public void Strip_RemovesRoleTitlesFromSkillLists()
    {
        var input = BaseJson(
            domain: new[] { "Mobile Product Manager", "C#" },
            technical: new[] { "Backend Developer", "Docker" },
            unverified: new[] { "Senior Software Engineer", "Analytical thinking" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cvRawText: "irrelevant");

        Assert.DoesNotContain("Mobile Product Manager", ReadList(output, "domain_skills"));
        Assert.DoesNotContain("Backend Developer", ReadList(output, "technical_skills"));
        Assert.DoesNotContain("Senior Software Engineer", ReadList(output, "unverified_skills"));
        // Legit skills preserved.
        Assert.Contains("C#", ReadList(output, "domain_skills"));
        Assert.Contains("Docker", ReadList(output, "technical_skills"));
    }

    // ── Soft skill enforcement ──────────────────────────────────────────────

    [Fact]
    public void SoftSkills_MovedToUnverifiedFromDomainAndTechnical()
    {
        var input = BaseJson(
            domain: new[] { "Analytical thinking", "C#" },
            technical: new[] { "Cross-functional collaboration", "Docker" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cvRawText: "irrelevant");

        var domain = ReadList(output, "domain_skills");
        var technical = ReadList(output, "technical_skills");
        var unverified = ReadList(output, "unverified_skills");

        Assert.DoesNotContain("Analytical thinking", domain);
        Assert.DoesNotContain("Cross-functional collaboration", technical);
        Assert.Contains("Analytical thinking", unverified);
        Assert.Contains("Cross-functional collaboration", unverified);
        // Non-soft skills stay in their bucket.
        Assert.Contains("C#", domain);
        Assert.Contains("Docker", technical);
    }

    // ── Parenthesised-stack fix ─────────────────────────────────────────────

    [Fact]
    public void ParenStack_ForcesTechnical_WhenOnlyInsideParens()
    {
        // CV mentions ".NET Core" ONLY inside parentheses of the C# stack
        // entry — no experience-bullet evidence outside parens.
        var cv =
            "Skills: C# (.NET Core, ASP.NET, EF Core), JavaScript\n" +
            "Experience: built backend using C# and React frontend";
        var input = BaseJson(
            domain: new[] { "C#", ".NET Core", "EF Core" },
            technical: Array.Empty<string>());
        var processor = MakeProcessor();

        var output = processor.Process(input, cv);

        Assert.DoesNotContain(".NET Core", ReadList(output, "domain_skills"));
        Assert.DoesNotContain("EF Core", ReadList(output, "domain_skills"));
        Assert.Contains(".NET Core", ReadList(output, "technical_skills"));
        Assert.Contains("EF Core", ReadList(output, "technical_skills"));
        // Parent C# stays — it appears in experience text outside parens.
        Assert.Contains("C#", ReadList(output, "domain_skills"));
    }

    [Fact]
    public void ParenStack_KeepsDomain_WhenAlsoMentionedOutsideParens()
    {
        // ".NET Core" appears INSIDE parens AND in an experience bullet —
        // experience evidence wins, stays in domain.
        var cv =
            "Skills: C# (.NET Core, ASP.NET, EF Core)\n" +
            "Experience: built backend using .NET Core 8 and ASP.NET Core";
        var input = BaseJson(domain: new[] { ".NET Core" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cv);

        Assert.Contains(".NET Core", ReadList(output, "domain_skills"));
        Assert.DoesNotContain(".NET Core", ReadList(output, "technical_skills"));
    }

    // ── Cross-list dedupe ──────────────────────────────────────────────────

    [Fact]
    public void CrossListDedupe_DomainWinsOverTechnical()
    {
        var input = BaseJson(
            domain: new[] { "C#" },
            technical: new[] { "C#", "Docker" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cvRawText: "irrelevant");

        Assert.Single(ReadList(output, "domain_skills").Where(
            s => string.Equals(s, "C#", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain("C#", ReadList(output, "technical_skills"));
        Assert.Contains("Docker", ReadList(output, "technical_skills"));
    }

    [Fact]
    public void CrossListDedupe_TechnicalWinsOverUnverified()
    {
        var input = BaseJson(
            technical: new[] { "Docker" },
            unverified: new[] { "Docker", "Quick learner" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cvRawText: "irrelevant");

        Assert.Contains("Docker", ReadList(output, "technical_skills"));
        Assert.DoesNotContain("Docker", ReadList(output, "unverified_skills"));
        // Real soft skill preserved in unverified.
        Assert.Contains("Quick learner", ReadList(output, "unverified_skills"));
    }

    // ── Capture-rescue ─────────────────────────────────────────────────────

    [Fact]
    public void CaptureRescue_AddsMissedItemMentionedInCv()
    {
        // CV mentions "Customer discovery" but Gemini missed it.
        var cv =
            "Experience: Genesis MVP Camp — learned MVP methodology including " +
            "customer discovery and rapid iteration";
        var input = BaseJson(
            domain: new[] { "Market research" },
            technical: Array.Empty<string>(),
            unverified: Array.Empty<string>());
        var processor = MakeProcessor();

        var output = processor.Process(input, cv);

        Assert.Contains("Customer discovery", ReadList(output, "domain_skills"));
        Assert.Contains("Rapid iteration", ReadList(output, "domain_skills"));
        // Existing items preserved.
        Assert.Contains("Market research", ReadList(output, "domain_skills"));
    }

    [Fact]
    public void CaptureRescue_SkipsWhenItemNotInCv()
    {
        // CV does NOT mention "Customer discovery" — must not invent it.
        var cv = "Experience: built backend using C# and React";
        var input = BaseJson(domain: new[] { "C#" });
        var processor = MakeProcessor();

        var output = processor.Process(input, cv);

        Assert.DoesNotContain("Customer discovery", ReadList(output, "domain_skills"));
    }

    [Fact]
    public void CaptureRescue_SkipsWhenItemAlreadyInAnyList()
    {
        var cv = "Customer discovery on FPV Drone";
        var input = BaseJson(
            domain: Array.Empty<string>(),
            technical: new[] { "Customer discovery" }); // already present
        var processor = MakeProcessor();

        var output = processor.Process(input, cv);

        // Should NOT be added to domain (already in technical).
        Assert.DoesNotContain("Customer discovery", ReadList(output, "domain_skills"));
        Assert.Contains("Customer discovery", ReadList(output, "technical_skills"));
    }

    // ── Defensive behaviour ────────────────────────────────────────────────

    [Fact]
    public void EmptyInput_ReturnedUnchanged()
    {
        var processor = MakeProcessor();
        Assert.Equal(string.Empty, processor.Process(string.Empty, "cv"));
    }

    [Fact]
    public void MalformedJson_ReturnsInputUnchanged()
    {
        var processor = MakeProcessor();
        const string broken = "this is not JSON {";

        var output = processor.Process(broken, "cv");

        Assert.Equal(broken, output);
    }

    [Fact]
    public void Determinism_SameInputPairAlwaysSameOutput()
    {
        // The post-processor is the byte-stability tier of the pipeline. Same
        // (rawJson, cvText) pair must produce byte-identical output, every time.
        var input = BaseJson(
            domain: new[] { "Hypothesis formulation", "C#", ".NET Core" },
            technical: new[] { "REST API understanding" },
            unverified: new[] { "Quick learner" });
        var cv = "Skills: C# (.NET Core, EF Core)\nBuilt REST API in .NET Core";
        var processor = MakeProcessor();

        var first = processor.Process(input, cv);
        var second = processor.Process(input, cv);
        var third = processor.Process(input, cv);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
    }
}
