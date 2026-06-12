using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Recruiter.Commands.CreateCandidateList;

public sealed class CreateCandidateListHandler
    : IRequestHandler<CreateCandidateListCommand, CreateCandidateListResult>
{
    private readonly ICandidateListRepository _lists;
    private readonly ICurrentUserService _currentUser;

    public CreateCandidateListHandler(
        ICandidateListRepository lists,
        ICurrentUserService currentUser)
    {
        _lists = lists;
        _currentUser = currentUser;
    }

    public async Task<CreateCandidateListResult> Handle(
        CreateCandidateListCommand cmd, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var list = CandidateList.Create(userId, cmd.Name, cmd.Description);
        await _lists.AddAsync(list, ct);
        return new CreateCandidateListResult(list.Id);
    }
}
