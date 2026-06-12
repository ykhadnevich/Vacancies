namespace Application.Common.Interfaces;


public interface ISkillVocabularyService
{


    Task<IReadOnlyDictionary<string, string>> ResolveSynonymsAsync(
        IReadOnlyCollection<string> skills,
        string? roleFamilyHint,
        CancellationToken ct = default);


    string Version { get; }
}
