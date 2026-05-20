using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.UnlockUser;

public record UnlockUserCommand(Guid UserId) : IRequest<Either<UserException, bool>>;
