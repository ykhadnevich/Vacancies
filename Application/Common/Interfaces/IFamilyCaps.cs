namespace Application.Common.Interfaces;


public interface IFamilyCaps
{


    (float Score, string Reason)? TryApplyDomainLockCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string cvText);


    (float Score, string Reason)? TryApplyPlatformToolCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string cvText);


    (float Score, string Reason)? TryApplyMismatchCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string[] candidateTargetRoles,
        string cvText);
}
