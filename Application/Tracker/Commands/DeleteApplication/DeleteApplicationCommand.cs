using MediatR;

namespace Application.Tracker.Commands.DeleteApplication;

public record DeleteApplicationCommand(Guid ApplicationId, Guid UserId) : IRequest<bool>;
