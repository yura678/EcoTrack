using MediatR;

namespace Application.Features.Auth.Commands.RequestPasswordReset;

public record RequestPasswordResetCommand(string Email) : IRequest<Unit>;
