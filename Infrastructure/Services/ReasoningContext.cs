using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.Services;


public class ReasoningContext : IReasoningContext
{
    public ReasoningProviderType Provider { get; set; } = ReasoningProviderType.None;
    public ScoringModelType ScoringModel { get; set; } = ScoringModelType.Flash;
    public CvVersionPreference CvVersion { get; set; } = CvVersionPreference.Auto;
    public bool IncludeCompetitionSignals { get; set; } = false;
    public bool IncludeRecencyDecay { get; set; } = false;
}
