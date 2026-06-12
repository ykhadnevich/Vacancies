using System.Text.Json;

namespace Domain.Entities;


public sealed class SkillVocabularyEntry
{


    public string CanonicalLower { get; private set; } = string.Empty;


    public string Canonical { get; private set; } = string.Empty;


    public string SynonymsJson { get; private set; } = "[]";


    public string Domain { get; private set; } = "general";


    public decimal Confidence { get; private set; } = 1.0m;


    public string Source { get; private set; } = "llm_batch";


    public string? ModelVersion { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SkillVocabularyEntry() { }


    public static SkillVocabularyEntry Create(
        string canonical,
        string synonymsJson,
        string domain,
        string source,
        string? modelVersion,
        decimal confidence = 1.0m)
    {
        if (string.IsNullOrWhiteSpace(canonical))
            throw new ArgumentException("Canonical skill cannot be empty", nameof(canonical));
        if (string.IsNullOrWhiteSpace(synonymsJson))
            throw new ArgumentException("SynonymsJson cannot be empty", nameof(synonymsJson));

        var trimmed = canonical.Trim();
        var now = DateTime.UtcNow;
        return new SkillVocabularyEntry
        {
            CanonicalLower = trimmed.ToLowerInvariant(),
            Canonical      = trimmed,
            SynonymsJson   = synonymsJson,
            Domain         = string.IsNullOrWhiteSpace(domain) ? "general" : domain,
            Confidence     = confidence,
            Source         = string.IsNullOrWhiteSpace(source) ? "llm_batch" : source,
            ModelVersion   = modelVersion,
            CreatedAt      = now,
            UpdatedAt      = now,
        };
    }


    public void UpdateSynonyms(string synonymsJson, string source, string? modelVersion)
    {
        if (string.IsNullOrWhiteSpace(synonymsJson))
            throw new ArgumentException("SynonymsJson cannot be empty", nameof(synonymsJson));
        SynonymsJson = synonymsJson;
        Source       = string.IsNullOrWhiteSpace(source) ? Source : source;
        ModelVersion = modelVersion ?? ModelVersion;
        UpdatedAt    = DateTime.UtcNow;
    }
}
