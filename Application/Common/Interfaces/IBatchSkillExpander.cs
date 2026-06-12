namespace Application.Common.Interfaces;


public interface IBatchSkillExpander
{


    Task<IReadOnlyDictionary<string, string>> ExpandBatchAsync(
        IReadOnlyList<string> skills,
        string? roleFamilyHint,
        CancellationToken ct = default);


    string Version { get; }
}
