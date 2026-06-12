using Application.Common.Scoring;

namespace Application.Common.Interfaces;


public interface IExperienceCapService
{


    RoleWeightedYears? ComputeRoleWeightedYears(string cvText);


    string[] ParseTargetRoles(string cvText);


    (bool CareerSwitcher, int TechnicalSkillsCount) ParseCareerSwitcherContext(string cvText);


    (float Score, string Reason)? TryApplyCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        RoleWeightedYears roleYears,
        bool careerSwitcher = false,
        int technicalSkillsCount = 0);


    (float Score, string Reason)? TryApplyMultiCriticalCap(float score, string reason);


    (float Score, string Reason)? TryApplyDomainLockCap(
        float score,
        string reason,
        string jobDescription);


    (float Score, string Reason)? TryApplyPlatformToolCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription);


    (float Score, string Reason)? TryApplyMismatchCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string[] candidateTargetRoles);
}
