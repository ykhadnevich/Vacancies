using FluentValidation;

namespace Application.Jobs.Commands.AddManualJobUrl;

public class AddManualJobUrlValidator : AbstractValidator<AddManualJobUrlCommand>
{


    private static readonly HashSet<string> AllowedSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

    public AddManualJobUrlValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required")
            .Must(BeHttpOrHttpsAbsoluteUrl)
            .WithMessage("URL must be a valid absolute http or https URL");
    }

    private static bool BeHttpOrHttpsAbsoluteUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && AllowedSchemes.Contains(parsed.Scheme);
}
