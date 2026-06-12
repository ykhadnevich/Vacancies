using FluentValidation;

namespace Application.Eval.Commands.ScoreSinglePair;


public sealed class ScoreSinglePairValidator : AbstractValidator<ScoreSinglePairCommand>
{
    public ScoreSinglePairValidator()
    {
        RuleFor(c => c.CvId)
            .NotEmpty().WithMessage("cvId is required.")
            .Matches(@"^[a-z0-9_]+$")
                .WithMessage("cvId must be lowercase letters, digits, or underscores (gold-set format).");

        RuleFor(c => c.VacancyId)
            .NotEqual(Guid.Empty).WithMessage("vacancyId must be a non-empty GUID.");
    }
}
