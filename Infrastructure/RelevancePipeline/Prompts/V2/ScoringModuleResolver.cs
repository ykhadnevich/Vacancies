using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class ScoringModuleResolver : IScoringModuleResolver
{
    private readonly Dictionary<RoleFamily, IScoringModule> _byFamily;
    private readonly IScoringModule? _fallback;

    public ScoringModuleResolver(IEnumerable<IScoringModule> modules, bool requireGeneric = false)
    {
        _byFamily = new Dictionary<RoleFamily, IScoringModule>();
        foreach (var module in modules)
        {
            if (_byFamily.ContainsKey(module.Family))
            {
                throw new InvalidOperationException(
                    $"Duplicate IScoringModule registration for family {module.Family}. " +
                    $"Existing: {_byFamily[module.Family].GetType().Name}, " +
                    $"new: {module.GetType().Name}.");
            }
            _byFamily[module.Family] = module;
        }

        if (!_byFamily.TryGetValue(RoleFamily.Generic, out _fallback) && requireGeneric)
        {
            throw new InvalidOperationException(
                "Mandatory IScoringModule for RoleFamily.Generic is missing. " +
                "Register a GenericScoringModule in DI before resolving any vacancy.");
        }
    }

    public IScoringModule For(RoleFamily family)
    {
        var mapped = MapToRegisteredFamily(family);

        if (_byFamily.TryGetValue(mapped, out var module))
            return module;

        if (_fallback is not null) return _fallback;


        if (_byFamily.Count > 0)
            return _byFamily.Values.First();

        throw new InvalidOperationException(
            $"No IScoringModule available for family {family} and no fallback registered.");
    }


    /// <summary>
    /// Map fine-grained Domain families (Marketing, DevOps, Other) onto the coarser
    /// set of registered scoring modules. This preserves the granularity used by
    /// <see cref="DomainAlignmentCalculator"/> and <see cref="JudgePromptCore"/>
    /// while keeping a single source of truth for module selection.
    ///
    /// Currently invoked when callers use <c>RoleFamilyDetector</c> (CV side) — the
    /// vacancy-side <c>KeywordRoleRouter</c> doesn't produce DevOps/Marketing today.
    /// </summary>
    private static RoleFamily MapToRegisteredFamily(RoleFamily family) => family switch
    {

        RoleFamily.DevOps    => RoleFamily.Engineering,


        RoleFamily.Marketing => RoleFamily.Generic,


        RoleFamily.Other     => RoleFamily.Generic,


        _ => family,
    };
}
