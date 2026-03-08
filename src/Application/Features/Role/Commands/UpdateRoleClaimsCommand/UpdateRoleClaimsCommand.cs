using Application.Features.Role.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Role.Commands.UpdateRoleClaimsCommand;

public record UpdateRoleClaimsCommand(Guid RoleId, List<string> RoleClaimValue) : IRequest<Either<RoleException, bool>>;