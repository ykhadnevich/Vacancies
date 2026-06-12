using Application.Common.Scoring;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IRoleRouter
{


    RoleDetectionResult Detect(string jobTitle, string jobDescription);
}


public sealed record RoleDetectionResult(
    RoleFamily Family,
    double Confidence);
