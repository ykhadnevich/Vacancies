using MediatR;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;

namespace Application.Tracker.Commands.UpdateApplicationStatus;

public class UpdateApplicationStatusHandler
    : IRequestHandler<UpdateApplicationStatusCommand, bool>
{
    private readonly IApplicationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public UpdateApplicationStatusHandler(
        IApplicationRepository repo,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(
        UpdateApplicationStatusCommand command,
        CancellationToken ct)
    {
        var application = await _repo.GetByIdAsync(command.ApplicationId, ct);

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        var userId = _currentUser.UserId;

        if (application is null || application.UserId != userId)
            return false;

        if (command.NewStatus.HasValue)
            application.UpdateStatus(command.NewStatus.Value);

        if (command.PipelineStep is not null && command.PipelineStepValue.HasValue)
            application.UpdatePipelineStep(command.PipelineStep, command.PipelineStepValue.Value);

        if (command.UpdateNotes)
            application.UpdateNotes(command.Notes);

        await _repo.UpdateAsync(application, ct);
        return true;
    }
}