using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public sealed class CvNormalizationPromptBuilder : ICvNormalizationPromptBuilder
{


    private const double CharsPerToken = 3.7;

    private readonly ICvDomainRouter _router;
    private readonly ICvNormalizationModuleResolver _resolver;

    public CvNormalizationPromptBuilder(
        ICvDomainRouter router,
        ICvNormalizationModuleResolver resolver)
    {
        _router = router;
        _resolver = resolver;
    }


    public string CurrentExpectedModelVersionPrefix =>
        $"gemini-cv-normalization-{CvNormalizationPromptCore.Version}+";


    public CvNormalizationPromptResult Build(string cvRawText)
    {
        var detection = _router.Detect(cvRawText);
        var module = _resolver.For(detection.Domain);
        var slots = module.GetSlots();
        var prompt = CvNormalizationPromptCore.Build(cvRawText, slots);

        var compositeVersion = $"{CvNormalizationPromptCore.Version}+{module.Version}";
        var estimatedTokens = (int)Math.Ceiling(prompt.Length / CharsPerToken);

        return new CvNormalizationPromptResult(
            Prompt: prompt,
            DetectedDomain: detection.Domain,
            CompositeVersion: compositeVersion,
            EstimatedInputTokens: estimatedTokens);
    }
}
