using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.ForcePasswordReset;

public record ForcePasswordResetCommand(Guid UserId) : IRequest<Either<UserException, bool>>;
