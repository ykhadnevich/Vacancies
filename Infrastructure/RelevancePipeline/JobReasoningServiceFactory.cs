using Application.Common.Enums;
using Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.RelevancePipeline;


public class JobReasoningServiceFactory : IJobReasoningServiceFactory
{
    private readonly IServiceProvider _sp;

    public JobReasoningServiceFactory(IServiceProvider sp)
    {
        _sp = sp;
    }

    public IJobReasoningService Get(ReasoningProviderType type) => type switch
    {
        ReasoningProviderType.Gemini => _sp.GetRequiredService<GeminiReasoningProvider>(),
        ReasoningProviderType.Groq   => _sp.GetRequiredService<GroqReasoningProvider>(),
        _                            => _sp.GetRequiredService<NoOpReasoningProvider>(),
    };
}
