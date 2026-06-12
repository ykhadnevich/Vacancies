using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface IReasoningContext
{
    ReasoningProviderType Provider { get; set; }


    ScoringModelType ScoringModel { get; set; }


    CvVersionPreference CvVersion { get; set; }


    bool IncludeCompetitionSignals { get; set; }


    bool IncludeRecencyDecay { get; set; }
}
