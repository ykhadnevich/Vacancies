using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public sealed class CvNormalizationModuleResolver : ICvNormalizationModuleResolver
{
    private readonly Dictionary<CvDomain, ICvNormalizationModule> _modules;
    private readonly ICvNormalizationModule _genericFallback;

    public CvNormalizationModuleResolver(IEnumerable<ICvNormalizationModule> modules)
    {
        _modules = new Dictionary<CvDomain, ICvNormalizationModule>();

        foreach (var module in modules)
        {
            if (_modules.ContainsKey(module.Domain))
            {
                throw new InvalidOperationException(
                    $"Duplicate CV normalization module registration for domain " +
                    $"{module.Domain}. Each CvDomain may have at most one " +
                    $"ICvNormalizationModule registered in DI.");
            }

            _modules[module.Domain] = module;
        }

        if (!_modules.TryGetValue(CvDomain.Generic, out var generic))
        {
            throw new InvalidOperationException(
                "GenericCvNormalizationModule is mandatory — it is the fallback " +
                "for any CV the router cannot confidently classify into a " +
                "dedicated domain. Register it in DI alongside other modules.");
        }

        _genericFallback = generic;
    }


    public ICvNormalizationModule For(CvDomain domain) =>
        _modules.TryGetValue(domain, out var module) ? module : _genericFallback;
}
