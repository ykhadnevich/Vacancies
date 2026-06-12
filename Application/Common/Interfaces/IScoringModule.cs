using Application.Common.Scoring;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IScoringModule
{

    RoleFamily Family { get; }


    string Version { get; }


    IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx);


    IReadOnlyList<RoleBucketMapping> GetBucketMappings();


    IReadOnlyList<AdjacencyRule> GetAdjacencyRules();


    IReadOnlyList<MismatchExample> GetMismatchList();


    IReadOnlyList<CareerPattern> GetCareerPatterns();


    IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx);


    IFamilyCaps GetCapsLogic();
}
