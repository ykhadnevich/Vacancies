using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;


public class KeywordRoleRouterTests
{
    private readonly KeywordRoleRouter _router = new();

    public static IEnumerable<object[]> ProductCases() => new[]
    {
        new object[] { "Junior Product Manager", "looking for a junior PM to own roadmap" },
        new object[] { "Senior Product Owner", "B2B SaaS, agile, hypothesis-driven product team" },
        new object[] { "Product Marketing Manager", "lead positioning, GTM for new feature launches" },
        new object[] { "Growth Manager", "growth experiments, activation funnel, AARRR" },
        new object[] { "Business Analyst (Fintech)", "requirements gathering, stakeholder workshops" },
        new object[] { "Project Manager", "manage cross-functional engineering project portfolio" },
        new object[] { "Head of Product", "leading product strategy for fintech platform" },
        new object[] { "Associate Product Manager", "rotational APM program, mentorship" },
        new object[] { "Продакт-менеджер", "відповідальність за продукт від ідеї до запуску" },
        new object[] { "Менеджер з продукту", "власник дорожньої карти, агіл, гіпотези" },
    };

    public static IEnumerable<object[]> EngineeringCases() => new[]
    {
        new object[] { "Senior .NET Developer", "ASP.NET Core, EF Core, PostgreSQL, Docker" },
        new object[] { "Backend Engineer (Go)", "build microservices, observability, Kubernetes" },
        new object[] { "Frontend Developer", "React, TypeScript, Redux, performance optimization" },
        new object[] { "Fullstack Developer", "Node.js + Vue, REST APIs, GraphQL" },
        new object[] { "DevOps Engineer", "AWS, Terraform, CI/CD, Datadog" },
        new object[] { "QA Automation Engineer", "Cypress, Playwright, Java" },
        new object[] { "ML Engineer", "training pipelines, model deployment, PyTorch" },
        new object[] { "iOS Developer", "Swift, SwiftUI, CoreData, push notifications" },
        new object[] { "Android Developer", "Kotlin, Jetpack Compose, MVVM" },
        new object[] { "Розробник Backend", "C#, .NET 8, PostgreSQL, мікросервіси" },
    };

    public static IEnumerable<object[]> DataCases() => new[]
    {
        new object[] { "Data Analyst", "SQL, Tableau, business KPIs, dashboards" },
        new object[] { "Senior Data Scientist", "Python, statistical modelling, ML experiments" },
        new object[] { "BI Analyst", "Power BI, dimensional modeling, ETL with Airflow" },
        new object[] { "Аналітик даних", "SQL, Tableau, дашборди, KPIs" },
    };

    public static IEnumerable<object[]> DesignCases() => new[]
    {
        new object[] { "UX/UI Designer", "design Figma flows, user research, prototypes" },
        new object[] { "Product Designer", "end-to-end product design for SaaS" },
        new object[] { "Senior UI Designer", "design systems, brand consistency, Figma" },
        new object[] { "Motion Designer", "After Effects, Lottie animations, brand motion" },
    };


    public static IEnumerable<object[]> GenericCases() => new[]
    {
        new object[] { "Office Manager", "manage office logistics, vendor relationships, supplies" },
        new object[] { "Accountant", "bookkeeping, payroll, tax filings, IFRS reporting" },
        new object[] { "HR Recruiter", "source candidates via LinkedIn, schedule interviews" },
        new object[] { "Customer Support Agent", "respond to user tickets, troubleshoot common issues" },
        new object[] { "Sales Representative", "B2B outbound, qualification, CRM updates" },
        new object[] { "Content Writer", "blog posts, social media copy, SEO basics" },
    };

    [Theory]
    [MemberData(nameof(ProductCases))]
    public void Detects_Product(string title, string description)
    {
        var result = _router.Detect(title, description);
        Assert.Equal(RoleFamily.Product, result.Family);
    }

    [Theory]
    [MemberData(nameof(EngineeringCases))]
    public void Detects_Engineering(string title, string description)
    {
        var result = _router.Detect(title, description);
        Assert.Equal(RoleFamily.Engineering, result.Family);
    }

    [Theory]
    [MemberData(nameof(DataCases))]
    public void Detects_Data(string title, string description)
    {
        var result = _router.Detect(title, description);
        Assert.Equal(RoleFamily.Data, result.Family);
    }

    [Theory]
    [MemberData(nameof(DesignCases))]
    public void Detects_Design(string title, string description)
    {
        var result = _router.Detect(title, description);
        Assert.Equal(RoleFamily.Design, result.Family);
    }

    [Theory]
    [MemberData(nameof(GenericCases))]
    public void Falls_Back_To_Generic(string title, string description)
    {
        var result = _router.Detect(title, description);
        Assert.Equal(RoleFamily.Generic, result.Family);
    }

    [Fact]
    public void Empty_Title_And_Description_Returns_Generic()
    {
        var result = _router.Detect(string.Empty, string.Empty);
        Assert.Equal(RoleFamily.Generic, result.Family);
        Assert.Equal(0.0, result.Confidence);
    }

    [Fact]
    public void Aggregated_Accuracy_Meets_Phase1_Target_85Percent()
    {
        var cases = new List<(string Title, string Description, RoleFamily Expected)>();
        foreach (var c in ProductCases())     cases.Add(((string)c[0], (string)c[1], RoleFamily.Product));
        foreach (var c in EngineeringCases()) cases.Add(((string)c[0], (string)c[1], RoleFamily.Engineering));
        foreach (var c in DataCases())        cases.Add(((string)c[0], (string)c[1], RoleFamily.Data));
        foreach (var c in DesignCases())      cases.Add(((string)c[0], (string)c[1], RoleFamily.Design));
        foreach (var c in GenericCases())     cases.Add(((string)c[0], (string)c[1], RoleFamily.Generic));

        var correct = cases.Count(t =>
            _router.Detect(t.Title, t.Description).Family == t.Expected);
        var accuracy = (double)correct / cases.Count;

        Assert.True(accuracy >= 0.85,
            $"Routing accuracy {accuracy:P0} ({correct}/{cases.Count}) below 85% target.");
    }
}
