using MediatR;

namespace Application.Jobs.Commands.AddManualJobUrl;

public record AddManualJobUrlCommand(string Url, string? Alias = null) : IRequest<AddManualJobUrlResult>;

public class AddManualJobUrlResult
{
    public bool Success { get; init; }
    public Guid? SavedUrlId { get; init; }
    public int JobsFound { get; init; }
    public string? ErrorMessage { get; init; }
}
