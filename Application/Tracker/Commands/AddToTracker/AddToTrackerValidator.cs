using FluentValidation;

namespace Application.Tracker.Commands.AddToTracker;

public class AddToTrackerValidator : AbstractValidator<AddToTrackerCommand>
{
    public AddToTrackerValidator()
    {
        RuleFor(x => x)
            .Must(x => x.JobVacancyId.HasValue ||
                       (!string.IsNullOrEmpty(x.Title) &&
                        !string.IsNullOrEmpty(x.Company)))
            .WithMessage("Either JobVacancyId or Title+Company must be provided");

        When(x => !x.JobVacancyId.HasValue, () =>
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Company).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Url)
                .Must(url => string.IsNullOrEmpty(url) ||
                             Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Must be a valid URL");
        });
    }
}
