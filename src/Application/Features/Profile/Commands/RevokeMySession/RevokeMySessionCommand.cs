using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Commands.RevokeMySession;

public record RevokeMySessionCommand(Guid SessionId) : IRequest<Either<UserException, bool>>;
