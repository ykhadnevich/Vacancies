using FluentValidation;

namespace Application.Jobs.Queries.GetAggregatedJobs;

public class GetAggregatedJobsValidator : AbstractValidator<GetAggregatedJobsQuery>
{
    public GetAggregatedJobsValidator()
    {
        RuleFor(x => x.Keywords)
            .NotEmpty().WithMessage("Keywords are required")
            .MinimumLength(2).WithMessage("Keywords must be at least 2 characters")
            .MaximumLength(100).WithMessage("Keywords must not exceed 100 characters");

        RuleFor(x => x.MinSalary)
            .GreaterThan(0).When(x => x.MinSalary.HasValue)
            .WithMessage("Salary must be greater than 0");
    }
}
