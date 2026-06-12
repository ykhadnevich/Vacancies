using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface ICvDomainRouter
{


    CvDomainDetectionResult Detect(string cvRawText);
}


public sealed record CvDomainDetectionResult(
    CvDomain Domain,
    double Confidence);
