using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface IVacancyDomainRouter
{


    VacancyDomainDetectionResult Detect(string vacancyRawText);
}


public sealed record VacancyDomainDetectionResult(
    VacancyDomain Domain,
    double Confidence);
