using Application.Common.Interfaces.Identity;
using Application.Features.Role.Exceptions;
using Application.Models.Identity;
using LanguageExt;
using MediatR;

namespace Application.Features.Role.Commands.UpdateRoleClaimsCommand;

internal class UpdateRoleClaimsCommandHandler(
    IRoleManagerService roleManagerService,
    ICurrentUserService currentUserService)
    : IRequestHandler<UpdateRoleClaimsCommand, Either<RoleException, bool>>
{
    public async Task<Either<RoleException, bool>> Handle(
        UpdateRoleClaimsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var roleOption = await roleManagerService.GetRoleByIdAsync(request.RoleId);
            var caller = currentUserService;
            var authorized = roleOption.Match(
                Some: r => caller.IsSuperAdmin()
                           || (caller.GetCurrentEnterpriseId() is Guid eid && r.EnterpriseId == eid),
                None: () => false);
            if (!authorized)
                return new RoleNotFoundException(Guid.Empty, request.RoleId);

            var updateRoleResult = await roleManagerService.ChangeRolePermissionsAsync(
                new EditRolePermissionsDto()
                {
                    RoleId = request.RoleId,
                    Permissions = request.RoleClaimValue
                });

            return updateRoleResult
                ? true
                : new RoleClaimsUpdateException(request.RoleId);
        }
        catch (Exception e)
        {
            return new UnhandledRoleException(Guid.Empty, e);
        }
    }
}