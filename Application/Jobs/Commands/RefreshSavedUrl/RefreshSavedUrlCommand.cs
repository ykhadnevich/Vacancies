using MediatR;

namespace Application.Jobs.Commands.RefreshSavedUrl;

public record RefreshSavedUrlCommand(Guid SavedUrlId) : IRequest<RefreshSavedUrlResult>;

public class RefreshSavedUrlResult
{
    public bool Success { get; init; }
    public int ParsedCount { get; init; }
    public int AddedCount { get; init; }
    public string? ErrorMessage { get; init; }
}