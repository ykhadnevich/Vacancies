using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Recruiter.Commands.DeleteRecruiterCandidate;

public sealed class DeleteRecruiterCandidateHandler
    : IRequestHandler<DeleteRecruiterCandidateCommand, Unit>
{
    private readonly IRecruiterCandidateRepository _candidates;
    private readonly ICurrentUserService _currentUser;

    public DeleteRecruiterCandidateHandler(
        IRecruiterCandidateRepository candidates,
        ICurrentUserService currentUser)
    {
        _candidates = candidates;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteRecruiterCandidateCommand cmd, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        // Candidates live at the recruiter level (not list-level), so we
        // verify ownership here instead of with a behavior — the marker
        // would need a CandidateId getter on the request anyway.
        var candidate = await _candidates.GetByIdAsync(cmd.CandidateId, ct);
        if (candidate is null || candidate.RecruiterUserId != userId)
            throw new ForbiddenAccessException(
                $"You do not have access to candidate {cmd.CandidateId}.");

        // Repository cascade drops all CandidateListMemberships + CandidateScores.
        await _candidates.DeleteAsync(cmd.CandidateId, ct);
        return Unit.Value;
    }
}
