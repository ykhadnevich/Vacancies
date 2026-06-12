using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Recruiter.Commands.DeleteCandidateList;

public sealed class DeleteCandidateListHandler : IRequestHandler<DeleteCandidateListCommand, Unit>
{
    private readonly ICandidateListRepository _lists;

    public DeleteCandidateListHandler(ICandidateListRepository lists)
    {
        _lists = lists;
    }

    public async Task<Unit> Handle(DeleteCandidateListCommand cmd, CancellationToken ct)
    {
        // Ownership is enforced by RequireCandidateListOwnershipBehavior.
        // The repository internally drops membership rows in a transaction
        // and leaves candidates intact (they may live in other lists).
        await _lists.DeleteAsync(cmd.CandidateListId, ct);
        return Unit.Value;
    }
}
