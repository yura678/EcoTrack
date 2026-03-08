using Application.Common.Interfaces.Identity;
using Application.Features.Role.Exceptions;
using Application.Models.Identity;
using LanguageExt;
using MediatR;

namespace Application.Features.Role.Commands.UpdateRoleClaimsCommand;

internal class UpdateRoleClaimsCommandHandler(IRoleManagerService roleManagerService)
    : IRequestHandler<UpdateRoleClaimsCommand, Either<RoleException, bool>>
{
    public async Task<Either<RoleException, bool>> Handle(
        UpdateRoleClaimsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
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