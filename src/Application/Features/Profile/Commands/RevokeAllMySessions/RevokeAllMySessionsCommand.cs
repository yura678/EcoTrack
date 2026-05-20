using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Profile.Commands.RevokeAllMySessions;

public record RevokeAllMySessionsCommand : IRequest<Either<UserException, bool>>;
