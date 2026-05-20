using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RevokeUserSessions;

public record RevokeUserSessionsCommand(Guid UserId) : IRequest<Either<UserException, bool>>;
