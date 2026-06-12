namespace Application.Common.Interfaces;


public interface ISkillExpansionService
{


    string Version { get; }


    Task<SkillExpansionResult> ExpandAsync(
        IReadOnlyList<string> skills,
        string skillType,
        string? roleFamilyHint,
        CancellationToken ct = default);
}


public sealed record SkillExpansionResult(
    string ExpansionJson,
    int InputTokens,
    int OutputTokens,
    bool FallbackUsed,
    string? FailureReason);
