using Application.Common.Interfaces;
using Application.Common.Scoring;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class ScoringPromptBuilder : IScoringPromptBuilder
{
    private readonly IRoleRouter _router;
    private readonly IScoringModuleResolver _resolver;
    private readonly SlotComposer _composer;

    public ScoringPromptBuilder(
        IRoleRouter router,
        IScoringModuleResolver resolver,
        SlotComposer composer)
    {
        _router   = router;
        _resolver = resolver;
        _composer = composer;
    }

    public PromptBuildResult Build(ScoringPromptContext ctx)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var detection = _router.Detect(ctx.JobTitle, ctx.JobDescription);
        var module    = _resolver.For(detection.Family);

        var prompt = _composer.Compose(ctx, module);
        var compositeVersion = $"{PromptCore.Version}+{module.Version}";


        var estimatedTokens = (int)Math.Round(prompt.Length / 3.7);

        return new PromptBuildResult(
            Prompt:               prompt,
            DetectedFamily:       detection.Family,
            CompositeVersion:     compositeVersion,
            EstimatedInputTokens: estimatedTokens);
    }
}
