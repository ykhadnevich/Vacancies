using FluentValidation;

namespace Application.Jobs.Commands.AddManualJobUrl;

public class AddManualJobUrlValidator : AbstractValidator<AddManualJobUrlCommand>
{
    public AddManualJobUrlValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("URL must be a valid absolute URL");
    }
}