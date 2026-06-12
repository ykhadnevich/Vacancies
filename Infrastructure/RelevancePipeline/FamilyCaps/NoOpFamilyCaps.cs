using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.FamilyCaps;


public sealed class NoOpFamilyCaps : IFamilyCaps
{
    public static readonly NoOpFamilyCaps Instance = new();

    public (float Score, string Reason)? TryApplyDomainLockCap(
        float score, string reason, string jobTitle, string jobDescription, string cvText) => null;

    public (float Score, string Reason)? TryApplyPlatformToolCap(
        float score, string reason, string jobTitle, string jobDescription, string cvText) => null;

    public (float Score, string Reason)? TryApplyMismatchCap(
        float score, string reason, string jobTitle, string jobDescription,
        string[] candidateTargetRoles, string cvText) => null;
}
