using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class VacancyNormalizationPromptBuilder : IVacancyNormalizationPromptBuilder
{
    private readonly IVacancyDomainRouter _router;
    private readonly IVacancyNormalizationModuleResolver _resolver;

    public VacancyNormalizationPromptBuilder(
        IVacancyDomainRouter router,
        IVacancyNormalizationModuleResolver resolver)
    {
        _router = router;
        _resolver = resolver;
    }


    public string CurrentExpectedModelVersionPrefix =>
        $"gemini-vac-normalization-{VacancyNormalizationPromptCore.Version}+";

    public VacancyNormalizationPromptResult Build(string vacancyRawText)
    {
        var routing = _router.Detect(vacancyRawText);
        var module = _resolver.For(routing.Domain);
        var slots = module.GetSlots();
        var prompt = VacancyNormalizationPromptCore.Build(vacancyRawText, slots);

        var estimatedTokens = prompt.Length / 4;
        var compositeVersion =
            $"{VacancyNormalizationPromptCore.Version}+{module.Version}";

        return new VacancyNormalizationPromptResult(
            Prompt: prompt,
            DetectedDomain: routing.Domain,
            CompositeVersion: compositeVersion,
            EstimatedInputTokens: estimatedTokens);
    }
}
