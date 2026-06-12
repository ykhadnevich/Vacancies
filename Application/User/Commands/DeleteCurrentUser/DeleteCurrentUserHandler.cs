using MediatR;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;

namespace Application.User.Commands.DeleteCurrentUser;


public class DeleteCurrentUserHandler : IRequestHandler<DeleteCurrentUserCommand, bool>
{
    private readonly IUserProfileRepository _userRepo;
    private readonly ICvFileStorage _cvFileStorage;

    public DeleteCurrentUserHandler(
        IUserProfileRepository userRepo,
        ICvFileStorage cvFileStorage)
    {
        _userRepo = userRepo;
        _cvFileStorage = cvFileStorage;
    }

    public async Task<bool> Handle(DeleteCurrentUserCommand command, CancellationToken ct)
    {


        var profile = await _userRepo.GetByIdAsync(command.UserId, ct);
        if (profile is null) return false;
        var fileKey = profile.CvFileKey;

        var deleted = await _userRepo.DeleteUserCascadeAsync(command.UserId, ct);
        if (!deleted) return false;

        if (!string.IsNullOrWhiteSpace(fileKey))
        {
            try
            {
                await _cvFileStorage.DeleteAsync(fileKey, ct);
            }
            catch
            {


            }
        }

        return true;
    }
}
