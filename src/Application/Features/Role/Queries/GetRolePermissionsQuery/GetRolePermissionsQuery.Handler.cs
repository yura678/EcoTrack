using Application.Common.Interfaces.Identity;
using Application.Features.Role.Exceptions;
using LanguageExt;
using MediatR;

namespace Application.Features.Role.Queries.GetRolePermissionsQuery;

internal class GetRolePermissionsQueryHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetRolePermissionsQuery, Either<RoleException, IReadOnlyList<string>>>
{
    public async Task<Either<RoleException, IReadOnlyList<string>>> Handle(
        GetRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Mirror UpdateRoleClaimsCommandHandler tenant check: superAdmin sees any role,
            // tenant admin only roles bound to their own enterprise. Returning NotFound
            // (rather than Forbidden) avoids leaking whether the roleId exists in another tenant.
            var roleOption = await roleManagerService.GetRoleByIdAsync(request.RoleId);
            var authorized = roleOption.Match(
                Some: r => currentUserService.IsSuperAdmin()
                           || (currentUserService.GetCurrentEnterpriseId() is Guid eid
                               && r.EnterpriseId == eid),
                None: () => false);
            if (!authorized)
                return new RoleNotFoundException(Guid.Empty, request.RoleId);

            var claims = await roleManagerService.GetDynamicPermissionClaimsByRoleIdAsync(request.RoleId);
            return Either<RoleException, IReadOnlyList<string>>.Right(claims);
        }
        catch (Exception e)
        {
            return new UnhandledRoleException(request.RoleId, e);
        }
    }
}
