using MediatR;
using Domain.Interfaces.Repositories;

namespace Application.Tracker.Commands.DeleteApplication;

public class DeleteApplicationHandler : IRequestHandler<DeleteApplicationCommand, bool>
{
    private readonly IApplicationRepository _appRepo;

    public DeleteApplicationHandler(IApplicationRepository appRepo)
    {
        _appRepo = appRepo;
    }

    public async Task<bool> Handle(DeleteApplicationCommand command, CancellationToken ct)
    {
        var application = await _appRepo.GetByIdAsync(command.ApplicationId, ct);

        if (application is null || application.UserId != command.UserId)
            return false;

        await _appRepo.DeleteAsync(command.ApplicationId, ct);
        return true;
    }
}
