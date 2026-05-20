using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(string Email, string Token, string NewPassword)
    : IRequest<Either<UserException, bool>>;
