using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class GenericScoringModule : IScoringModule
{
    public RoleFamily Family => RoleFamily.Generic;
    public string Version => "generic_v1";

    private static readonly IReadOnlyDictionary<SlotId, SlotContent> _slots =
        new Dictionary<SlotId, SlotContent>
        {
            [SlotId.MismatchExamples] = new SlotContent(
                Text:
                    "  No specific family detected for this vacancy. Use your general knowledge\n" +
                    "  of this profession to evaluate. Identify primary daily duties from the job\n" +
                    "  description, identify which skills/tools are hard (months of training) vs\n" +
                    "  easy. If the job requires licenses/certifications (medical, legal,\n" +
                    "  financial regulatory) AND candidate has none → critical gap.",
                Policy: SlotPolicy.Append),
        };

    public IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx) => _slots;

    public IReadOnlyList<RoleBucketMapping> GetBucketMappings() => Array.Empty<RoleBucketMapping>();
    public IReadOnlyList<AdjacencyRule>     GetAdjacencyRules() => Array.Empty<AdjacencyRule>();
    public IReadOnlyList<MismatchExample>   GetMismatchList()   => Array.Empty<MismatchExample>();
    public IReadOnlyList<CareerPattern>     GetCareerPatterns() => Array.Empty<CareerPattern>();

    public IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx) =>
        new Dictionary<string, ToolWeight>();

    public IFamilyCaps GetCapsLogic() => NoOpFamilyCaps.Instance;
}
