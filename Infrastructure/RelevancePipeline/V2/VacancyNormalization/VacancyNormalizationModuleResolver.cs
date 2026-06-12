using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class VacancyNormalizationModuleResolver : IVacancyNormalizationModuleResolver
{
    private readonly IReadOnlyDictionary<VacancyDomain, IVacancyNormalizationModule> _byDomain;
    private readonly IVacancyNormalizationModule _generic;

    public VacancyNormalizationModuleResolver(IEnumerable<IVacancyNormalizationModule> modules)
    {
        var byDomain = new Dictionary<VacancyDomain, IVacancyNormalizationModule>();
        foreach (var module in modules)
        {
            if (byDomain.ContainsKey(module.Domain))
                throw new InvalidOperationException(
                    $"Duplicate IVacancyNormalizationModule for domain '{module.Domain}'. " +
                    $"Existing: {byDomain[module.Domain].GetType().Name}, " +
                    $"new: {module.GetType().Name}");
            byDomain[module.Domain] = module;
        }

        if (!byDomain.TryGetValue(VacancyDomain.Generic, out var generic))
            throw new InvalidOperationException(
                "No IVacancyNormalizationModule registered for VacancyDomain.Generic — " +
                "GenericVacancyNormalizationModule is mandatory as the fallback.");

        _byDomain = byDomain;
        _generic = generic;
    }

    public IVacancyNormalizationModule For(VacancyDomain domain) =>
        _byDomain.TryGetValue(domain, out var module) ? module : _generic;
}
