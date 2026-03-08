using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RequestLogout;

public record RequestLogoutCommand(Guid UserId) : IRequest<Either<UserException, bool>>;