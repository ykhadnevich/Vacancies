using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface IJobReasoningServiceFactory
{
    IJobReasoningService Get(ReasoningProviderType providerType);
}
