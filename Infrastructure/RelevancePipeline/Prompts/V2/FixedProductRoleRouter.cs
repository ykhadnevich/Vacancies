using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class FixedProductRoleRouter : IRoleRouter
{
    public RoleDetectionResult Detect(string jobTitle, string jobDescription) =>
        new(RoleFamily.Product, Confidence: 1.0);
}
