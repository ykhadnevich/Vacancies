using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Jobs.Queries.GetSavedUrls;

public record GetSavedUrlsQuery : IRequest<IReadOnlyList<SavedUrlDto>>;

public record SavedUrlDto(
    Guid Id,
    string Url,
    string? Alias,
    DateTime CreatedAt,
    DateTime? LastParsedAt,
    int LastParsedCount);

public class GetSavedUrlsHandler : IRequestHandler<GetSavedUrlsQuery, IReadOnlyList<SavedUrlDto>>
{
    private readonly ISavedUrlRepository _repo;

    public GetSavedUrlsHandler(ISavedUrlRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<SavedUrlDto>> Handle(
        GetSavedUrlsQuery request, CancellationToken ct)
    {
        var urls = await _repo.GetAllAsync(ct);

        return urls.Select(u => new SavedUrlDto(
            u.Id,
            u.Url,
            u.Alias,
            u.CreatedAt,
            u.LastParsedAt,
            u.LastParsedCount)).ToList();
    }
}
