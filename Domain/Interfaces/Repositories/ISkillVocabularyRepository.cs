using Domain.Entities;

namespace Domain.Interfaces.Repositories;


public interface ISkillVocabularyRepository
{


    Task<IReadOnlyDictionary<string, SkillVocabularyEntry>> GetByCanonicalLowerAsync(
        IReadOnlyCollection<string> canonicalLowers,
        CancellationToken ct = default);


    Task UpsertBatchAsync(
        IReadOnlyCollection<SkillVocabularyEntry> entries,
        CancellationToken ct = default);
}
