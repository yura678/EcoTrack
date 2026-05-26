using Application.Features.Users.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Users.Commands.RevokeUserSessions;

// EnterpriseId is optional: tenant admins leave it null and the handler derives it from
// the JWT's CompanyId claim. SuperAdmin must pass it explicitly to target a tenant.
public record RevokeUserSessionsCommand(Guid UserId, Guid? EnterpriseId = null)
    : IRequest<Either<UserException, bool>>;
