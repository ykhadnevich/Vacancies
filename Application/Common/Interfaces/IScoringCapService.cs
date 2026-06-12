using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IScoringCapService
{


    double ApplyCaps(double rawScore, SubScores subScores, bool languageGapPenalty);


    double Floor { get; }


    double Ceiling { get; }
}
